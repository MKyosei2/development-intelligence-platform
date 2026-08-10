using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Intelligence;

public sealed class P2OrchestrationService
{
    private readonly IIntelligenceRunStore _runs;
    private readonly IEvidenceRepository _evidence;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly ProjectTruthService _truth;
    private readonly IMinimumHardwareRunner _minimum;
    private readonly IRegressionRunner _regression;
    private readonly IReliabilityRunner _reliability;
    private readonly IMediaRegressionRunner _media;
    private readonly IFuzzRunner _fuzz;
    private readonly IMutationRunner _mutation;

    public P2OrchestrationService(
        IIntelligenceRunStore runs,
        IEvidenceRepository evidence,
        IUnitOfWork uow,
        ISystemClock clock,
        ProjectTruthService truth,
        IMinimumHardwareRunner minimum,
        IRegressionRunner regression,
        IReliabilityRunner reliability,
        IMediaRegressionRunner media,
        IFuzzRunner fuzz,
        IMutationRunner mutation)
    {
        _runs = runs; _evidence = evidence; _uow = uow; _clock = clock; _truth = truth;
        _minimum = minimum; _regression = regression; _reliability = reliability; _media = media; _fuzz = fuzz; _mutation = mutation;
    }

    private async Task<(IntelligenceRun Run, string Directory)> BeginAsync(Guid projectId, string subject, IntelligenceRunType type, object input, CancellationToken ct)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        var id = Guid.NewGuid();
        var dir = Path.Combine(Path.GetFullPath("artifacts/p2"), projectId.ToString("N"), id.ToString("N"));
        Directory.CreateDirectory(dir);
        var run = new IntelligenceRun(id, projectId, type, subject, dir, JsonSerializer.Serialize(input), _clock.UtcNow);
        await _runs.AddAsync(run, ct);
        run.Start(_clock.UtcNow);
        await _uow.SaveChangesAsync(ct);
        return (run, dir);
    }

    private async Task FinishAsync(IntelligenceRun run, IntelligenceRunStatus status, object result, EvidenceRecord evidence, CancellationToken ct)
    {
        run.Complete(status, Trim(JsonSerializer.Serialize(result), 32000), _clock.UtcNow);
        await _evidence.AddAsync(evidence, ct);
        await _uow.SaveChangesAsync(ct);
        await _truth.RecalculateAsync(run.ProjectId, ct);
    }

    private async Task FailInfrastructureAsync(IntelligenceRun run, Exception ex, CancellationToken ct)
    {
        run.Complete(IntelligenceRunStatus.InfrastructureFailure, Trim(JsonSerializer.Serialize(new { error = ex.Message, type = ex.GetType().FullName }), 32000), _clock.UtcNow);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<MinimumHardwareResult> MinimumHardwareAsync(MinimumHardwareRequest request, CancellationToken ct)
    {
        var (run, dir) = await BeginAsync(request.ProjectId, request.SubjectKey, IntelligenceRunType.MinimumHardware, request, ct);
        try
        {
            var result = await _minimum.FindAsync(request, dir, ct);
            var evidence = new EvidenceRecord(Guid.NewGuid(), request.ProjectId, null, request.SubjectKey, EvidenceSourceType.Performance,
                "hardware.minimum-estimate", EvidenceOutcome.Informational, "Estimated minimum hardware boundary",
                $"Estimated safe CPU {result.SafeCpuPercent:0.#}% / GPU {result.SafeGpuPercent:0.#}% of the calibrated machine. This is simulation-derived evidence, not a real-device measurement.",
                EvidenceConfidence.Medium, null, Trim(result.EvidenceJson, 16000), _clock.UtcNow, _clock.UtcNow);
            await FinishAsync(run, IntelligenceRunStatus.Succeeded, result, evidence, ct);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, ex, ct); throw;
        }
    }

    public async Task<RegressionResult> RegressionAsync(RegressionRequest request, CancellationToken ct)
    {
        var (run, dir) = await BeginAsync(request.ProjectId, request.SubjectKey, IntelligenceRunType.GitRegression, request, ct);
        try
        {
            var result = await _regression.FindOriginAsync(request, dir, ct);
            var resolved = result.FirstBadCommit is not null;
            var evidence = new EvidenceRecord(Guid.NewGuid(), request.ProjectId, null, request.SubjectKey, EvidenceSourceType.GitHub,
                "regression.origin", resolved ? EvidenceOutcome.Failed : EvidenceOutcome.Unknown,
                resolved ? "Regression origin isolated" : "Regression origin not resolved",
                resolved ? $"First bad commit: {result.FirstBadCommit}" : "Automated commit search could not isolate a first bad commit.",
                resolved ? EvidenceConfidence.High : EvidenceConfidence.Medium, result.FirstBadCommit, Trim(result.EvidenceJson, 16000), _clock.UtcNow, _clock.UtcNow);
            await FinishAsync(run, resolved ? IntelligenceRunStatus.Succeeded : IntelligenceRunStatus.Warning, result, evidence, ct);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, ex, ct); throw;
        }
    }

    public async Task<ReliabilityResult> ReliabilityAsync(ReliabilityRequest request, CancellationToken ct)
    {
        var type = request.SoakMinutes > 0 ? IntelligenceRunType.Soak : IntelligenceRunType.Flaky;
        var (run, dir) = await BeginAsync(request.ProjectId, request.SubjectKey, type, request, ct);
        try
        {
            var result = await _reliability.RunAsync(request, dir, ct);
            var risky = result.FailureRate > 5 || result.PerformanceDriftPercent > 15 || result.MemoryGrowthMb > 256;
            var evidence = new EvidenceRecord(Guid.NewGuid(), request.ProjectId, null, request.SubjectKey, EvidenceSourceType.Runtime,
                request.SoakMinutes > 0 ? "runtime.soak" : "runtime.flaky", risky ? EvidenceOutcome.Failed : EvidenceOutcome.Succeeded,
                risky ? "Reliability risk detected" : "Reliability run passed",
                $"Failure rate {result.FailureRate:0.##}%, memory growth {result.MemoryGrowthMb:0.#} MB, performance drift {result.PerformanceDriftPercent:0.#}%.",
                EvidenceConfidence.High, null, Trim(result.EvidenceJson, 16000), _clock.UtcNow, _clock.UtcNow);
            await FinishAsync(run, risky ? IntelligenceRunStatus.Warning : IntelligenceRunStatus.Succeeded, result, evidence, ct);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, ex, ct); throw;
        }
    }

    public async Task<MediaRegressionResult> MediaAsync(VisualRegressionRequest request, string kind, CancellationToken ct)
    {
        var normalized = kind.ToLowerInvariant();
        var type = normalized switch { "video" => IntelligenceRunType.VideoRegression, "audio" => IntelligenceRunType.AudioRegression, _ => IntelligenceRunType.VisualRegression };
        var (run, dir) = await BeginAsync(request.ProjectId, request.SubjectKey, type, request, ct);
        try
        {
            var result = await _media.CompareAsync(request, dir, normalized, ct);
            var evidence = new EvidenceRecord(Guid.NewGuid(), request.ProjectId, null, request.SubjectKey, EvidenceSourceType.Runtime,
                $"regression.{normalized}", result.Passed ? EvidenceOutcome.Succeeded : EvidenceOutcome.Failed,
                $"{normalized} regression {(result.Passed ? "passed" : "detected")}",
                $"Difference {result.DifferencePercent:0.###}% against the supplied baseline.", EvidenceConfidence.High,
                result.DiffArtifact, Trim(result.EvidenceJson, 16000), _clock.UtcNow, _clock.UtcNow);
            await FinishAsync(run, result.Passed ? IntelligenceRunStatus.Succeeded : IntelligenceRunStatus.Warning, result, evidence, ct);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, ex, ct); throw;
        }
    }

    public async Task<FuzzResult> FuzzAsync(FuzzRequest request, CancellationToken ct)
    {
        var (run, dir) = await BeginAsync(request.ProjectId, request.SubjectKey, IntelligenceRunType.Fuzz, request, ct);
        try
        {
            var result = await _fuzz.RunAsync(request, dir, ct);
            var risky = result.Crashes > 0 || result.Timeouts > 0;
            var evidence = new EvidenceRecord(Guid.NewGuid(), request.ProjectId, null, request.SubjectKey, EvidenceSourceType.Quality,
                "qa.fuzz", risky ? EvidenceOutcome.Failed : EvidenceOutcome.Succeeded,
                risky ? "Fuzzing found unstable input behavior" : "Fuzz campaign completed",
                $"{result.Cases} cases, {result.Crashes} crash-like exit(s), {result.Timeouts} timeout(s), {result.NonZeroExits} non-zero exit(s).",
                EvidenceConfidence.High, result.ArtifactPath, Trim(result.EvidenceJson, 16000), _clock.UtcNow, _clock.UtcNow);
            await FinishAsync(run, risky ? IntelligenceRunStatus.Warning : IntelligenceRunStatus.Succeeded, result, evidence, ct);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, ex, ct); throw;
        }
    }

    public async Task<MutationResult> MutationAsync(MutationRequest request, CancellationToken ct)
    {
        var (run, dir) = await BeginAsync(request.ProjectId, request.SubjectKey, IntelligenceRunType.Mutation, request, ct);
        try
        {
            var result = await _mutation.RunAsync(request, dir, ct);
            var weak = result.MutationScore < 70;
            var evidence = new EvidenceRecord(Guid.NewGuid(), request.ProjectId, null, request.SubjectKey, EvidenceSourceType.Test,
                "qa.mutation", weak ? EvidenceOutcome.Warning : EvidenceOutcome.Succeeded,
                weak ? "Test strength appears weak" : "Mutation test strength passed",
                $"Mutation score {result.MutationScore:0.##}% ({result.Killed}/{result.Mutants} mutants killed).",
                EvidenceConfidence.High, null, Trim(result.EvidenceJson, 16000), _clock.UtcNow, _clock.UtcNow);
            await FinishAsync(run, weak ? IntelligenceRunStatus.Warning : IntelligenceRunStatus.Succeeded, result, evidence, ct);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await FailInfrastructureAsync(run, ex, ct); throw;
        }
    }

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];
}
