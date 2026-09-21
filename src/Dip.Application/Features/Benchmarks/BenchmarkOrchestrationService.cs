// Purpose: Orchestrates benchmark definitions, execution, evidence, incidents, and Project Truth refresh without hiding persistence failures.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Incidents;
using Dip.Application.Features.Jobs;
using Dip.Application.Features.Performance;
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Benchmarks;

public sealed partial class BenchmarkOrchestrationService
{
    private readonly IBenchmarkRepository _repo;
    private readonly IBenchmarkExecutor _executor;
    private readonly DurableJobService _jobs;
    private readonly ArtifactService _artifacts;
    private readonly IEvidenceRepository _evidence;
    private readonly IncidentAggregationService _incidents;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly ProjectTruthService _truth;
    private readonly PerformanceBudgetService _budgets;

    public BenchmarkOrchestrationService(
        IBenchmarkRepository repo,
        IBenchmarkExecutor executor,
        DurableJobService jobs,
        ArtifactService artifacts,
        IEvidenceRepository evidence,
        IncidentAggregationService incidents,
        IUnitOfWork uow,
        ISystemClock clock,
        ProjectTruthService truth,
        PerformanceBudgetService budgets)
    {
        _repo = repo;
        _executor = executor;
        _jobs = jobs;
        _artifacts = artifacts;
        _evidence = evidence;
        _incidents = incidents;
        _uow = uow;
        _clock = clock;
        _truth = truth;
        _budgets = budgets;
    }

    public async Task<BenchmarkDefinitionDto> CreateDefinitionAsync(
        CreateBenchmarkDefinitionCommand command,
        CancellationToken ct)
    {
        if (await _repo.GetDefinitionByKeyAsync(command.Key, ct) is not null)
            throw new InvalidOperationException($"Benchmark definition '{command.Key}' already exists.");
        Validate(command.Configuration);
        var entity = new BenchmarkDefinition(
            Guid.NewGuid(),
            command.Key,
            command.Name,
            command.Kind,
            JsonSerializer.Serialize(command.Configuration),
            command.Enabled,
            _clock.UtcNow);
        await _repo.AddDefinitionAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<IReadOnlyList<BenchmarkDefinitionDto>> ListDefinitionsAsync(CancellationToken ct) =>
        (await _repo.ListDefinitionsAsync(ct)).Select(Map).ToArray();

    public Task<EnqueuedJobDto> EnqueueAsync(BenchmarkJobRequest request, CancellationToken ct) =>
        _jobs.EnqueueAsync(request.ProjectId, DurableJobType.Benchmark, request.SubjectKey, request, 3, ct);

    public async Task<BenchmarkRunResultDto> ExecuteJobAsync(
        DurableJob job,
        BenchmarkJobRequest request,
        CancellationToken ct)
    {
        var definition = await _repo.GetDefinitionAsync(request.DefinitionId, ct)
            ?? throw new KeyNotFoundException($"Benchmark definition {request.DefinitionId} was not found.");
        if (!definition.Enabled)
            throw new InvalidOperationException("Benchmark definition is disabled.");

        var spec = JsonSerializer.Deserialize<BenchmarkExecutionSpec>(definition.ConfigurationJson)
            ?? throw new InvalidDataException("Benchmark configuration is invalid.");
        Validate(spec);
        var configHash = Sha256(definition.ConfigurationJson);
        var startedAt = _clock.UtcNow;
        var execution = await _executor.ExecuteAsync(spec, ct);
        var environment = string.IsNullOrWhiteSpace(request.EnvironmentFingerprint)
            ? execution.EnvironmentFingerprint
            : request.EnvironmentFingerprint.Trim();

        var run = new BenchmarkRun(
            Guid.NewGuid(), request.ProjectId, definition.Id, job.Id, request.SubjectKey,
            request.CommitSha, environment, configHash, startedAt);
        await _repo.AddRunAsync(run, ct);
        run.Start(startedAt);
        await _uow.SaveChangesAsync(ct);

        try
        {
            var metrics = BuildMetrics(run.Id, execution.Samples);
            await _repo.AddMetricsAsync(metrics, ct);
            var baseline = await _repo.FindBaselineAsync(
                request.ProjectId, definition.Id, environment, configHash, request.CommitSha, ct);
            var comparisons = baseline is null
                ? []
                : await CompareAsync(run.Id, baseline.Id, metrics, spec, ct);
            if (comparisons.Count > 0)
                await _repo.AddComparisonsAsync(comparisons, ct);

            PerformanceBudgetEvaluationDto? budget = null;
            if (request.PerformanceBudgetProfileId.HasValue)
            {
                budget = await _budgets.EvaluateAsync(
                    request.ProjectId,
                    request.PerformanceBudgetProfileId.Value,
                    metrics.Select(x => new PerformanceMetricInput(x.Name, x.Aggregation, x.Value, x.Unit)).ToArray(),
                    null,
                    run.Id,
                    ct);
            }

            var state = Analyze(execution, comparisons, budget);
            var summary = BuildSummary(definition, metrics, baseline, state, budget);
            var artifacts = await StoreArtifactsAsync(job, request, definition, run, execution, metrics, comparisons, baseline, budget, summary, ct);
            await RecordEvidenceAndIncidentsAsync(job, request, definition, run, metrics, comparisons, budget, state, artifacts, summary, ct);

            await _uow.SaveChangesAsync(ct);
            await _truth.RecalculateAsync(request.ProjectId, ct);
            run.Complete(state.RunStatus, summary, _clock.UtcNow);
            await _uow.SaveChangesAsync(ct);
            return new(
                run.Id,
                run.Status.ToString(),
                metrics.Select(Map).ToArray(),
                comparisons.Select(Map).ToArray(),
                artifacts.Summary.Id,
                summary);
        }
        catch (OperationCanceledException)
        {
            await PersistTerminalStateAsync(run, BenchmarkRunStatus.Cancelled, "Benchmark execution was cancelled.", null);
            throw;
        }
        catch (Exception ex)
        {
            await PersistTerminalStateAsync(run, BenchmarkRunStatus.InfrastructureFailure, ex.Message, ex);
            throw;
        }
    }

    public async Task<BenchmarkRunResultDto?> GetRunAsync(Guid runId, CancellationToken ct)
    {
        var run = await _repo.GetRunAsync(runId, ct);
        if (run is null)
            return null;
        var metrics = await _repo.ListMetricsAsync(runId, ct);
        var comparisons = await _repo.ListComparisonsAsync(runId, ct);
        return new(
            run.Id,
            run.Status.ToString(),
            metrics.Select(Map).ToArray(),
            comparisons.Select(Map).ToArray(),
            null,
            run.Summary ?? string.Empty);
    }
}
