// Purpose: Implements Intelligence Dashboard Service application behavior for dashboard.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Hardware;
using Dip.Application.Features.Release;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Dashboard;

public sealed record IntelligenceDashboardDto(
    Guid ProjectId,
    int ActiveIncidents,
    int CriticalIncidents,
    int RunningJobs,
    int FailedJobs,
    int ReplaySessions,
    int RecentBudgetFailures,
    int OpenWorkItems,
    int RepositoryNodes,
    int RepositoryDependencies,
    string? ChangeRiskLevel,
    decimal? ChangeRiskScore,
    string? ReleaseDecision,
    decimal? ReleaseConfidence,
    HardwareBacktestSummary HardwareCalibration,
    DateTimeOffset GeneratedAtUtc);

public sealed class IntelligenceDashboardService
{
    private readonly IIncidentRepository _incidents;
    private readonly IDurableJobRepository _jobs;
    private readonly IGameReplayRepository _replay;
    private readonly IPerformanceBudgetRepository _budgets;
    private readonly IExternalWorkItemRepository _workItems;
    private readonly IRepositoryGraphRepository _graph;
    private readonly IChangeRiskRepository _risk;
    private readonly IReleaseReadinessRepository _release;
    private readonly HardwareBacktestingService _backtests;
    public IntelligenceDashboardService(IIncidentRepository incidents, IDurableJobRepository jobs, IGameReplayRepository replay, IPerformanceBudgetRepository budgets, IExternalWorkItemRepository workItems, IRepositoryGraphRepository graph, IChangeRiskRepository risk, IReleaseReadinessRepository release, HardwareBacktestingService backtests)
    {
        _incidents = incidents;
        _jobs = jobs;
        _replay = replay;
        _budgets = budgets;
        _workItems = workItems;
        _graph = graph;
        _risk = risk;
        _release = release;
        _backtests = backtests;
    }
    public async Task<IntelligenceDashboardDto> GetAsync(Guid projectId, CancellationToken ct)
    {
        var incidents = await _incidents.ListRecentAsync(projectId, 500, ct);
        var jobs = await _jobs.ListRecentAsync(projectId, 200, ct);
        var sessions = await _replay.ListSessionsAsync(projectId, ct);
        var budgets = await _budgets.ListEvaluationsAsync(projectId, 100, ct);
        var workItems = await _workItems.ListAsync(projectId, ct);
        var nodes = await _graph.ListNodesAsync(projectId, ct);
        var deps = await _graph.ListDependenciesAsync(projectId, ct);
        var risk = await _risk.GetLatestAsync(projectId, null, ct);
        var release = await _release.GetLatestAsync(projectId, ct);
        var calibration = await _backtests.SummarizeAsync(projectId, null, 1000, ct);
        return new(projectId, incidents.Count(x => x.Status != IncidentStatus.Resolved), incidents.Count(x => x.Status != IncidentStatus.Resolved && x.Severity == Severity.Critical), jobs.Count(x => x.Status is DurableJobStatus.Queued or DurableJobStatus.Running), jobs.Count(x => x.Status is DurableJobStatus.Failed or DurableJobStatus.TimedOut), sessions.Count, budgets.Count(x => x.Verdict == PerformanceBudgetVerdict.Failed), workItems.Count(x => x.Status == ExternalWorkItemStatus.Open), nodes.Count, deps.Count, risk?.RiskLevel.ToString(), risk?.RiskScore, release?.Decision.ToString(), release?.Confidence, calibration, DateTimeOffset.UtcNow);
    }
}
