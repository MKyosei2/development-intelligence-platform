// Purpose: Evaluates open incidents, durable-job health, and exact-revision change risk for release readiness.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Release;

internal sealed class ReleaseReadinessIncidentEvaluator(
    IIncidentRepository incidents,
    IDurableJobRepository jobs,
    IChangeRiskRepository risk)
{
    public async Task EvaluateAsync(ReleaseReadinessEvaluationContext context, CancellationToken ct)
    {
        var request = context.Request;
        var recentIncidents = await incidents.ListRecentAsync(request.ProjectId, 200, ct);
        var openCritical = recentIncidents.Count(x =>
            x.Status != IncidentStatus.Resolved && x.Severity == Severity.Critical);
        if (openCritical > 0)
        {
            context.Blockers.Add($"{openCritical} critical incident(s) remain unresolved.");
            context.Actions.Add("Resolve or explicitly mitigate all critical incidents with fresh evidence.");
        }

        var recentJobs = await jobs.ListRecentAsync(request.ProjectId, 100, ct);
        var failedJobs = recentJobs.Count(x => x.Status is DurableJobStatus.Failed or DurableJobStatus.TimedOut);
        var runningJobs = recentJobs.Count(x => x.Status is DurableJobStatus.Running or DurableJobStatus.Queued);
        if (failedJobs > 0)
        {
            context.Warnings.Add($"{failedJobs} recent durable job(s) failed or timed out.");
            context.Actions.Add("Review failed durable jobs and rerun release-relevant checks.");
        }
        if (runningJobs > 0)
            context.Warnings.Add($"{runningJobs} durable job(s) are still queued/running.");

        var latestRisk = await risk.GetLatestAsync(request.ProjectId, request.CommitSha, ct);
        if (latestRisk is null)
            return;

        if (latestRisk.RiskLevel == ChangeRiskLevel.Critical)
        {
            context.Blockers.Add($"Change risk for {request.CommitSha} is Critical ({latestRisk.RiskScore:0.#}/100).");
            context.Actions.Add("Run the expanded high-risk QA plan and reassess change risk.");
        }
        else if (latestRisk.RiskLevel == ChangeRiskLevel.High)
        {
            context.Warnings.Add($"Change risk is High ({latestRisk.RiskScore:0.#}/100).");
        }
    }
}
