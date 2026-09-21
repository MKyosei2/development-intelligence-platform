// Purpose: Produces producer-facing project health, delivery, and risk insights from management evidence.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

public sealed record ProducerRiskItem(string Severity, string Category, string Title, string Detail, string? Reference);

public sealed record ProducerIntelligenceDto(
    Guid ProjectId,
    decimal VerifiedProgress,
    decimal? ReportedProgress,
    string ProgressConfidence,
    string TruthStatus,
    string RiskLevel,
    decimal HealthScore,
    int ClaimDiscrepancies,
    int HighClaimDiscrepancies,
    int CriticalAssetFindings,
    int OpenAssetFindings,
    int VisualFailures,
    string BuildHealth,
    string QaTrend,
    string AssetRisk,
    string EvidenceFreshness,
    int UnverifiedWork,
    IReadOnlyList<ProducerRiskItem> TopRisks,
    DateTimeOffset GeneratedAtUtc);

public sealed class ProducerIntelligenceService
{
    private readonly IProjectRepository _projects;
    private readonly IProjectTruthRepository _truth;
    private readonly IProjectClaimRepository _claims;
    private readonly IEvidenceRepository _evidence;
    private readonly IQualityAutomationRepository _quality;
    private readonly IClaimEvidenceAssessmentRepository _assessments;
    private readonly ISystemClock _clock;

    public ProducerIntelligenceService(
        IProjectRepository projects,
        IProjectTruthRepository truth,
        IProjectClaimRepository claims,
        IEvidenceRepository evidence,
        IQualityAutomationRepository quality,
        IClaimEvidenceAssessmentRepository assessments,
        ISystemClock clock)
    {
        _projects = projects;
        _truth = truth;
        _claims = claims;
        _evidence = evidence;
        _quality = quality;
        _assessments = assessments;
        _clock = clock;
    }

    public async Task<ProducerIntelligenceDto> GetAsync(Guid projectId, CancellationToken ct)
    {
        if (await _projects.GetByIdAsync(projectId, ct) is null)
            throw new KeyNotFoundException($"Project {projectId} was not found.");
        var now = _clock.UtcNow;
        var truth = await _truth.GetLatestAsync(projectId, ct);
        var claims = await _claims.ListByProjectAsync(projectId, ct);
        var evidence = (await _evidence.ListForProjectAsync(projectId, now.AddDays(-60), ct)).OrderByDescending(x => x.ObservedAtUtc).ToArray();
        var assessments = await _assessments.ListForProjectAsync(projectId, 2000, ct);
        var latestAssessments = assessments.GroupBy(x => x.ClaimId).Select(g => g.OrderByDescending(x => x.AssessedAtUtc).First()).ToArray();
        var findings = await _quality.ListFindingsAsync(projectId, AssetQaFindingState.Open, 2000, ct);
        var comparisons = await _quality.ListComparisonsAsync(projectId, 1000, ct);

        var highDiscrepancy = latestAssessments.Count(x => x.DiscrepancyScore >= 70m || x.Status == ClaimEvidenceAssessmentStatus.Contradicted);
        var discrepancy = latestAssessments.Count(x => x.DiscrepancyScore >= 35m);
        var assessedClaimIds = latestAssessments.Select(x => x.ClaimId).ToHashSet();
        var unverified = claims.Count(x => !assessedClaimIds.Contains(x.Id)
            || latestAssessments.First(a => a.ClaimId == x.Id).Status == ClaimEvidenceAssessmentStatus.InsufficientEvidence);
        var criticalAsset = findings.Count(x => x.Severity == AssetQaSeverity.Critical);
        var visualFailures = comparisons.Count(x => x.ComparedAtUtc >= now.AddDays(-14)
            && x.Verdict is VisualComparisonVerdict.Failed or VisualComparisonVerdict.Incompatible);
        var latestBuild = evidence.FirstOrDefault(x => x.SourceType == EvidenceSourceType.Build
            || x.EvidenceKind.Contains("build", StringComparison.OrdinalIgnoreCase));
        var buildHealth = latestBuild is null ? "No recent build evidence" : latestBuild.Outcome switch
        {
            EvidenceOutcome.Succeeded => "Healthy",
            EvidenceOutcome.Failed => "Failed",
            EvidenceOutcome.Warning => "Warning",
            _ => "Unknown"
        };

        var recentQa = evidence.Where(IsQaEvidence).Where(x => x.ObservedAtUtc >= now.AddDays(-14)).ToArray();
        var previousQa = evidence.Where(IsQaEvidence).Where(x => x.ObservedAtUtc >= now.AddDays(-28) && x.ObservedAtUtc < now.AddDays(-14)).ToArray();
        var recentRate = FailureRate(recentQa);
        var previousRate = FailureRate(previousQa);
        var qaTrend = recentQa.Length == 0 ? "No recent QA evidence"
            : previousQa.Length == 0 ? $"Current failure signal {recentRate:0.#}%"
            : recentRate > previousRate + 5m ? $"Worsening ({previousRate:0.#}% → {recentRate:0.#}%)"
            : recentRate + 5m < previousRate ? $"Improving ({previousRate:0.#}% → {recentRate:0.#}%)"
            : $"Stable ({recentRate:0.#}%)";

        var assetRisk = criticalAsset > 0 ? "Critical"
            : findings.Any(x => x.Severity == AssetQaSeverity.Warning) ? "Warning"
            : findings.Count > 0 ? "Informational" : "Clear";
        var latestEvidenceAt = evidence.FirstOrDefault()?.ObservedAtUtc;
        var freshness = latestEvidenceAt is null ? "No evidence"
            : now - latestEvidenceAt.Value <= TimeSpan.FromDays(1) ? "Fresh (<24h)"
            : now - latestEvidenceAt.Value <= TimeSpan.FromDays(7) ? $"Aging ({(now - latestEvidenceAt.Value).Days}d)"
            : $"Stale ({(now - latestEvidenceAt.Value).Days}d)";

        var risks = new List<ProducerRiskItem>();
        if (highDiscrepancy > 0) risks.Add(new("High", "Claims", "Reported state conflicts with objective evidence", $"{highDiscrepancy} high-discrepancy claim assessment(s) require review.", $"/dashboard/{projectId}/claim-assessments"));
        if (criticalAsset > 0) risks.Add(new("Critical", "Assets", "Critical asset QA findings", $"{criticalAsset} critical finding(s) remain open.", $"/dashboard/{projectId}/quality-automation"));
        if (visualFailures > 0) risks.Add(new("High", "Visual QA", "Visual regressions detected", $"{visualFailures} failed/incompatible comparison(s) were observed in the last 14 days.", $"/dashboard/{projectId}/quality-automation"));
        if (buildHealth == "Failed") risks.Add(new("Critical", "Build", "Latest build evidence failed", latestBuild?.Summary ?? "Latest build failed.", latestBuild?.SourceReference));
        if (unverified > 0) risks.Add(new("Medium", "Evidence", "Unverified work remains", $"{unverified} project claim(s) do not yet have a sufficient Phase-4 assessment.", $"/dashboard/{projectId}/claim-assessments"));
        if (freshness.StartsWith("Stale", StringComparison.OrdinalIgnoreCase)) risks.Add(new("Medium", "Evidence", "Evidence is stale", freshness, null));

        return new ProducerIntelligenceDto(
            projectId,
            truth?.VerifiedProgress ?? 0m,
            truth?.ReportedProgress,
            truth?.EvidenceConfidence.ToString() ?? "Unverified",
            truth?.TruthStatus.ToString() ?? "Unknown",
            truth?.RiskLevel.ToString() ?? "Unknown",
            truth?.HealthScore ?? 0m,
            discrepancy,
            highDiscrepancy,
            criticalAsset,
            findings.Count,
            visualFailures,
            buildHealth,
            qaTrend,
            assetRisk,
            freshness,
            unverified,
            risks.Take(12).ToArray(),
            now);
    }

    private static bool IsQaEvidence(Dip.Domain.Entities.EvidenceRecord x)
        => x.SourceType is EvidenceSourceType.Quality or EvidenceSourceType.Test
           || x.EvidenceKind.Contains("qa", StringComparison.OrdinalIgnoreCase)
           || x.EvidenceKind.Contains("test", StringComparison.OrdinalIgnoreCase)
           || x.EvidenceKind.Contains("visual", StringComparison.OrdinalIgnoreCase);

    private static decimal FailureRate(IReadOnlyCollection<Dip.Domain.Entities.EvidenceRecord> rows)
    {
        if (rows.Count == 0) return 0m;
        var failed = rows.Count(x => x.Outcome == EvidenceOutcome.Failed);
        var warning = rows.Count(x => x.Outcome == EvidenceOutcome.Warning);
        return decimal.Round((failed + warning * .5m) * 100m / rows.Count, 2);
    }
}
