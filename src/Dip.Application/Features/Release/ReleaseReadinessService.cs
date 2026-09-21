// Purpose: Orchestrates exact-revision release readiness by composing focused evaluators and persisting the resulting decision.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Release;

public sealed class ReleaseReadinessService
{
    private readonly ReleaseReadinessIncidentEvaluator _incidentEvaluator;
    private readonly ReleaseReadinessPerformanceEvaluator _performanceEvaluator;
    private readonly ReleaseReadinessEvidenceEvaluator _evidenceEvaluator;
    private readonly ReleaseReadinessQaEvaluator _qaEvaluator;
    private readonly ReleaseReadinessProjectTruthEvaluator _projectTruthEvaluator;
    private readonly ReleaseReadinessDevelopmentGraphEvaluator _developmentGraphEvaluator;
    private readonly IReleaseReadinessRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public ReleaseReadinessService(
        IIncidentRepository incidents,
        IDurableJobRepository jobs,
        IPerformanceBudgetRepository budgets,
        IProjectTruthRepository truth,
        IEvidenceRepository evidence,
        IChangeRiskRepository risk,
        IReleaseEvidenceRepository releaseEvidence,
        IReleaseReadinessRepository repo,
        IDevelopmentGraphRepository graph,
        IQaGovernanceRepository qaGovernance,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _incidentEvaluator = new(incidents, jobs, risk);
        _performanceEvaluator = new(budgets);
        _evidenceEvaluator = new(evidence, releaseEvidence, clock);
        _qaEvaluator = new(qaGovernance, clock);
        _projectTruthEvaluator = new(truth);
        _developmentGraphEvaluator = new(graph, clock);
        _repo = repo;
        _uow = uow;
        _clock = clock;
    }

    public async Task<ReleaseReadinessDto> EvaluateAsync(
        EvaluateReleaseReadinessRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.CommitSha))
            throw new ArgumentException("CommitSha is required.", nameof(request));

        var context = new ReleaseReadinessEvaluationContext(request);
        await _incidentEvaluator.EvaluateAsync(context, ct);
        await _performanceEvaluator.EvaluateAsync(context, ct);
        await _evidenceEvaluator.EvaluateAsync(context, ct);
        await _qaEvaluator.EvaluateAsync(context, ct);
        await _projectTruthEvaluator.EvaluateAsync(context, ct);
        await _developmentGraphEvaluator.EvaluateAsync(context, ct);

        var decision = context.Blockers.Count > 0
            ? ReleaseDecision.NoGo
            : context.Warnings.Count > 0
                ? ReleaseDecision.ConditionalGo
                : ReleaseDecision.Go;

        var confidence = ReleaseReadinessPolicy.CalculateConfidence(
            context.FreshEvidenceCount,
            context.HasProjectTruth,
            context.PerformanceEvaluationCount > 0);

        var summary = decision switch
        {
            ReleaseDecision.Go => "GO: no evidence-backed release blocker is currently open.",
            ReleaseDecision.ConditionalGo => "CONDITIONAL GO: no hard blocker is open, but warnings require explicit review.",
            _ => "NO-GO: one or more evidence-backed release blockers must be resolved."
        };

        var entity = new ReleaseReadinessSnapshot(
            Guid.NewGuid(),
            request.ProjectId,
            request.CommitSha,
            decision,
            confidence,
            JsonSerializer.Serialize(context.Blockers),
            JsonSerializer.Serialize(context.Warnings),
            JsonSerializer.Serialize(context.Actions.Distinct()),
            summary,
            _clock.UtcNow);

        await _repo.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(entity);
    }

    public async Task<ReleaseReadinessDto?> GetLatestAsync(Guid projectId, CancellationToken ct)
    {
        var snapshot = await _repo.GetLatestAsync(projectId, ct);
        return snapshot is null ? null : Map(snapshot);
    }

    private static ReleaseReadinessDto Map(ReleaseReadinessSnapshot snapshot) =>
        new(
            snapshot.Id,
            snapshot.CommitSha,
            snapshot.Decision.ToString(),
            snapshot.Confidence,
            JsonSerializer.Deserialize<string[]>(snapshot.BlockersJson) ?? [],
            JsonSerializer.Deserialize<string[]>(snapshot.WarningsJson) ?? [],
            JsonSerializer.Deserialize<string[]>(snapshot.RequiredActionsJson) ?? [],
            snapshot.Summary,
            snapshot.EvaluatedAtUtc);
}

internal sealed class ReleaseReadinessEvaluationContext(EvaluateReleaseReadinessRequest request)
{
    public EvaluateReleaseReadinessRequest Request { get; } = request;
    public List<string> Blockers { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Actions { get; } = [];
    public int FreshEvidenceCount { get; set; }
    public int PerformanceEvaluationCount { get; set; }
    public bool HasProjectTruth { get; set; }
}
