// Purpose: Implements Dashboard Read Repository infrastructure behavior for persistence.
using System.Text.Json;
using Dip.Application.Features.Dashboard;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class DashboardReadRepository : IDashboardReadRepository
{
    private const string LifecycleCategory = "project-lifecycle";
    private readonly DipDbContext _db;

    public DashboardReadRepository(DipDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProjectDashboardSummary>> GetProjectsAsync(
        CancellationToken cancellationToken)
    {
        var lifecycleEvents = await _db.AuditLogs
            .AsNoTracking()
            .Where(x => x.Category == LifecycleCategory)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.ProjectId, x.Action, x.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        var hiddenProjectIds = lifecycleEvents
            .GroupBy(x => x.ProjectId)
            .Select(group => group.OrderByDescending(x => x.CreatedAtUtc).First())
            .Where(x => x.Action is "project.archived" or "project.deleted")
            .Select(x => x.ProjectId)
            .ToHashSet();

        var projects = await _db.Projects
            .AsNoTracking()
            .Where(x => !hiddenProjectIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var results = new List<ProjectDashboardSummary>(projects.Count);

        foreach (var project in projects)
        {
            var latestRun = await _db.PipelineRuns
                .AsNoTracking()
                .Where(x => x.ProjectId == project.Id)
                .OrderByDescending(x => x.FinishedAtUtc ?? x.StartedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var latestGate = await _db.QualityGateResults
                .AsNoTracking()
                .Where(x => x.ProjectId == project.Id)
                .OrderByDescending(x => x.EvaluatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var truth = await _db.ProjectTruthSnapshots
                .AsNoTracking()
                .Where(x => x.ProjectId == project.Id)
                .OrderByDescending(x => x.EvaluatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var health = truth?.HealthScore ?? project.HealthScore.Value;

            results.Add(new(
                project.Id,
                project.Name,
                project.RepositoryUrl,
                project.DefaultBranch,
                health,
                truth?.TruthStatus.ToString() ?? "Unknown",
                truth?.RiskLevel.ToString() ?? LegacyRisk(health),
                truth?.VerifiedProgress ?? 0m,
                truth?.ReportedProgress,
                truth?.EvidenceConfidence.ToString() ?? EvidenceConfidence.Unverified.ToString(),
                truth?.BlockerCount ?? 0,
                truth?.ContradictionCount ?? 0,
                latestRun?.Status.ToString(),
                latestGate?.Status,
                truth?.EvaluatedAtUtc ?? latestGate?.EvaluatedAtUtc));
        }

        return results;
    }

    public async Task<ProjectDashboardDetails?> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var latestLifecycleAction = await _db.AuditLogs
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Category == LifecycleCategory)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => x.Action)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestLifecycleAction == "project.deleted")
            return null;

        var project = await _db.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                cancellationToken);

        if (project is null)
            return null;

        var latestRun = await _db.PipelineRuns
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.FinishedAtUtc ?? x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var latestGate = await _db.QualityGateResults
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.EvaluatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var truth = await _db.ProjectTruthSnapshots
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.EvaluatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var metrics = latestRun is null
            ? new List<DashboardMetricPoint>()
            : await _db.Metrics
                .AsNoTracking()
                .Where(x => x.PipelineRunId == latestRun.Id)
                .OrderBy(x => x.Name)
                .Select(x => new DashboardMetricPoint(
                    x.Name,
                    x.Value,
                    x.Unit,
                    x.DifferencePercent))
                .ToListAsync(cancellationToken);

        var issues = await _db.QualityIssues
            .AsNoTracking()
            .Where(x =>
                x.ProjectId == projectId
                && x.Status == IssueStatus.Open)
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.Title)
            .Take(50)
            .Select(x => new DashboardIssue(
                x.Id,
                x.Severity.ToString(),
                x.Category,
                x.Title,
                x.Status.ToString()))
            .ToListAsync(cancellationToken);

        var comparisons = await BuildReportVsRealityAsync(projectId, cancellationToken);
        var timeline = await BuildTimelineAsync(projectId, cancellationToken);
        var health = truth?.HealthScore ?? project.HealthScore.Value;
        var topActions = DeserializeList<string>(truth?.TopActionsJson);
        var blockers = DeserializeList<DashboardBlocker>(truth?.BlockersJson);

        if (topActions.Count == 0)
            topActions = ["Collect objective evidence or submit a project status report to establish Project Truth."];

        var recentQaEntities = await _db.QaRuns
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);
        var recentQaIds = recentQaEntities.Select(x => x.Id).ToArray();
        var reproducedByRun = recentQaIds.Length == 0
            ? new Dictionary<Guid, int>()
            : await _db.QaFindings.AsNoTracking()
                .Where(x => recentQaIds.Contains(x.QaRunId)
                            && x.ReproductionStatus == ReproductionStatus.Reproduced)
                .GroupBy(x => x.QaRunId)
                .Select(g => new { RunId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RunId, x => x.Count, cancellationToken);
        var recentQa = recentQaEntities.Select(x => new DashboardQaRun(
            x.Id,
            x.Language.ToString(),
            x.Status.ToString(),
            x.FindingCount,
            x.CriticalFindingCount,
            x.WarningFindingCount,
            reproducedByRun.TryGetValue(x.Id, out var reproduced) ? reproduced : 0,
            x.CreatedAtUtc)).ToArray();

        var recentHardware = await (from sim in _db.PerformanceSimulationRuns.AsNoTracking()
                                    join profile in _db.HardwareProfiles.AsNoTracking()
                                        on sim.HardwareProfileId equals profile.Id
                                    where sim.ProjectId == projectId
                                    orderby sim.CreatedAtUtc descending
                                    select new DashboardHardwareSimulation(
                                        sim.Id,
                                        profile.Name,
                                        profile.Platform,
                                        sim.Status.ToString(),
                                        sim.ConfidencePercent,
                                        sim.CreatedAtUtc))
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentP2 = await _db.P2IntelligenceRecords
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(12)
            .Select(x => new DashboardP2Signal(
                x.Id,
                x.Kind.ToString(),
                x.Status.ToString(),
                x.Score,
                x.Confidence.ToString(),
                x.Summary,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var decisionTraces = await _db.DecisionTraces
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(10)
            .Select(x => new DashboardDecisionTrace(
                x.Id,
                x.DecisionType,
                x.SubjectKey,
                x.PreviousValue,
                x.NewValue,
                x.Reason,
                x.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var details = new ProjectDashboardDetails(
            project.Id,
            project.Name,
            project.RepositoryUrl,
            project.DefaultBranch,
            health,
            truth?.TruthStatus.ToString() ?? "Unknown",
            truth?.RiskLevel.ToString() ?? LegacyRisk(health),
            truth?.VerifiedProgress ?? 0m,
            truth?.ReportedProgress,
            truth?.EvidenceConfidence.ToString() ?? EvidenceConfidence.Unverified.ToString(),
            truth?.BlockerCount ?? 0,
            truth?.ContradictionCount ?? 0,
            truth?.UnverifiedClaimCount ?? 0,
            truth?.Summary ?? "Project Truth has not been evaluated yet. New evidence or a status report will trigger evaluation.",
            topActions,
            blockers,
            comparisons,
            timeline,
            truth?.EvaluatedAtUtc,
            latestRun?.Id,
            latestRun?.Status.ToString(),
            latestRun?.FinishedAtUtc,
            latestGate?.Status,
            latestGate?.Score,
            latestGate?.CriticalIssueCount ?? 0,
            latestGate?.WarningIssueCount ?? 0,
            latestGate?.GetReasons() ?? Array.Empty<string>(),
            metrics,
            issues);

        return details with
        {
            RecentQaRuns = recentQa,
            RecentHardwareSimulations = recentHardware,
            RecentDecisionIntelligence = recentP2,
            DecisionTraces = decisionTraces
        };
    }

    private async Task<IReadOnlyList<DashboardClaimComparison>> BuildReportVsRealityAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var claims = await _db.ProjectClaims
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.ClaimedAtUtc)
            .Take(100)
            .ToListAsync(cancellationToken);

        var latestClaims = claims
            .GroupBy(item => item.SubjectKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.ClaimedAtUtc).First())
            .Take(25)
            .ToArray();

        if (latestClaims.Length == 0)
            return Array.Empty<DashboardClaimComparison>();

        var claimIds = latestClaims.Select(item => item.Id).ToArray();
        var links = await _db.ClaimEvidenceLinks
            .AsNoTracking()
            .Where(item => claimIds.Contains(item.ClaimId))
            .ToListAsync(cancellationToken);

        return latestClaims.Select(claim =>
        {
            var claimLinks = links.Where(item => item.ClaimId == claim.Id).ToArray();
            return new DashboardClaimComparison(
                claim.Id,
                claim.SubjectKey,
                claim.ClaimedStatus,
                claim.ClaimedProgress,
                claim.ClaimedBy,
                claim.VerificationStatus.ToString(),
                VerificationSummary(claim.VerificationStatus),
                claimLinks.Count(item => item.Relation == EvidenceRelation.Supports),
                claimLinks.Count(item => item.Relation == EvidenceRelation.Contradicts),
                claim.ClaimedAtUtc);
        }).ToArray();
    }

    private async Task<IReadOnlyList<DashboardTimelineItem>> BuildTimelineAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var evidence = await _db.EvidenceRecords
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.ObservedAtUtc)
            .Take(40)
            .ToListAsync(cancellationToken);

        var claims = await _db.ProjectClaims
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.ClaimedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        var audits = await _db.AuditLogs
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Category != "claim")
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);

        var items = new List<DashboardTimelineItem>(evidence.Count + claims.Count + audits.Count);
        items.AddRange(evidence.Select(item => new DashboardTimelineItem(
            item.ObservedAtUtc,
            $"Evidence / {item.SourceType}",
            item.Title,
            item.Summary,
            item.Confidence.ToString(),
            item.SubjectKey,
            item.SourceReference)));

        items.AddRange(claims.Select(item => new DashboardTimelineItem(
            item.ClaimedAtUtc,
            "Reported status",
            $"{item.SubjectKey}: {item.ClaimedStatus}",
            item.Description ?? $"Reported by {item.ClaimedBy}.",
            item.VerificationStatus.ToString(),
            item.SubjectKey,
            null)));

        items.AddRange(audits.Select(item => new DashboardTimelineItem(
            item.CreatedAtUtc,
            $"Audit / {item.Category}",
            item.Action,
            item.DetailJson ?? $"Actor: {item.Actor}",
            "Audit",
            item.SubjectKey,
            null)));

        return items
            .OrderByDescending(item => item.AtUtc)
            .Take(50)
            .ToArray();
    }

    private static string VerificationSummary(ClaimVerificationStatus status) => status switch
    {
        ClaimVerificationStatus.Confirmed => "Objective evidence supports the reported state.",
        ClaimVerificationStatus.PartiallyConfirmed => "Some evidence supports the report, but verification is incomplete.",
        ClaimVerificationStatus.Contradicted => "Current objective evidence conflicts with this report.",
        ClaimVerificationStatus.Stale => "This report needs newer objective evidence.",
        _ => "There is not enough objective evidence to verify this report."
    };

    private static List<T> DeserializeList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string LegacyRisk(decimal score) =>
        score < 60m ? "High"
        : score < 85m ? "Medium"
        : "Low";
}
