using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

public sealed class ProjectTruthEvaluator
{
    private readonly ClaimVerificationEvaluator _claimVerification;
    private readonly EvidenceConfidenceEvaluator _confidence;
    private readonly ProgressEvaluator _progress;
    private readonly ProjectBlockerEvaluator _blockers;
    private readonly ProjectRiskEvaluator _risk;

    public ProjectTruthEvaluator(
        ClaimVerificationEvaluator claimVerification,
        EvidenceConfidenceEvaluator confidence,
        ProgressEvaluator progress,
        ProjectBlockerEvaluator blockers,
        ProjectRiskEvaluator risk)
    {
        _claimVerification = claimVerification;
        _confidence = confidence;
        _progress = progress;
        _blockers = blockers;
        _risk = risk;
    }

    public ProjectTruthAssessment Evaluate(
        IReadOnlyCollection<ProjectClaim> claims,
        IReadOnlyCollection<EvidenceRecord> evidence,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(claims);
        ArgumentNullException.ThrowIfNull(evidence);

        var latestClaims = claims
            .GroupBy(item => item.SubjectKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.ClaimedAtUtc).First())
            .ToArray();

        var claimAssessments = latestClaims
            .Select(claim => _claimVerification.Evaluate(claim, evidence, now))
            .ToArray();

        var assessmentMap = claimAssessments.ToDictionary(item => item.ClaimId);
        var confidence = _confidence.Evaluate(evidence, now);
        var progress = _progress.Evaluate(latestClaims, assessmentMap);
        var blockers = _blockers.Evaluate(latestClaims, assessmentMap, evidence, now);
        var contradictions = claimAssessments.Count(item =>
            item.Status == ClaimVerificationStatus.Contradicted);
        var unverified = claimAssessments.Count(item =>
            item.Status is ClaimVerificationStatus.Unverified or ClaimVerificationStatus.Stale);
        var risk = _risk.Evaluate(progress, confidence, blockers, contradictions, unverified);

        var truthStatus = risk switch
        {
            ProjectRiskLevel.Critical => ProjectTruthStatus.Blocked,
            ProjectRiskLevel.High => ProjectTruthStatus.AtRisk,
            ProjectRiskLevel.Medium => ProjectTruthStatus.Attention,
            _ when latestClaims.Length == 0 && evidence.Count > 0 => ProjectTruthStatus.Attention,
            _ when confidence >= EvidenceConfidence.High => ProjectTruthStatus.Healthy,
            _ when evidence.Count > 0 => ProjectTruthStatus.Attention,
            _ => ProjectTruthStatus.Unknown
        };

        var health = CalculateHealth(
            progress,
            confidence,
            blockers,
            contradictions,
            unverified);

        var topActions = BuildTopActions(blockers, confidence, progress, contradictions);
        var summary = BuildSummary(
            truthStatus,
            progress,
            confidence,
            blockers,
            contradictions,
            unverified);

        return new(
            truthStatus,
            risk,
            health,
            progress,
            confidence,
            claimAssessments,
            blockers,
            contradictions,
            unverified,
            summary,
            topActions);
    }

    private static decimal CalculateHealth(
        ProgressAssessment progress,
        EvidenceConfidence confidence,
        IReadOnlyCollection<ProjectBlocker> blockers,
        int contradictionCount,
        int unverifiedClaimCount)
    {
        var score = 100m;
        score -= blockers.Count(item => item.Severity == "Critical") * 25m;
        score -= blockers.Count(item => item.Severity == "Blocker") * 12m;
        score -= contradictionCount * 10m;
        score -= Math.Min(12m, unverifiedClaimCount * 3m);

        if (progress.ReportedProgress is { } reported)
        {
            var gap = Math.Max(0m, reported - progress.VerifiedProgress);
            score -= Math.Min(20m, gap * 0.25m);
        }

        score -= confidence switch
        {
            EvidenceConfidence.Unverified => 20m,
            EvidenceConfidence.Low => 15m,
            EvidenceConfidence.Medium => 8m,
            EvidenceConfidence.High => 3m,
            _ => 0m
        };

        return decimal.Round(Math.Clamp(score, 0m, 100m), 2);
    }

    private static IReadOnlyList<string> BuildTopActions(
        IReadOnlyCollection<ProjectBlocker> blockers,
        EvidenceConfidence confidence,
        ProgressAssessment progress,
        int contradictionCount)
    {
        var actions = blockers
            .Take(3)
            .Select(item => $"Review: {item.Title}")
            .ToList();

        if (contradictionCount > 0 && actions.Count < 3)
            actions.Add("Resolve report-versus-evidence mismatches before relying on the reported status.");

        if (confidence < EvidenceConfidence.High && actions.Count < 3)
            actions.Add("Collect fresh automated evidence for the most important project areas.");

        if (progress.ReportedProgress is { } reported
            && reported - progress.VerifiedProgress >= 20m
            && actions.Count < 3)
        {
            actions.Add("Verify the large gap between reported and evidence-backed progress.");
        }

        if (actions.Count == 0)
            actions.Add("No immediate evidence-backed action is required.");

        return actions;
    }

    private static string BuildSummary(
        ProjectTruthStatus status,
        ProgressAssessment progress,
        EvidenceConfidence confidence,
        IReadOnlyCollection<ProjectBlocker> blockers,
        int contradictions,
        int unverified)
    {
        var reported = progress.ReportedProgress is null
            ? "not reported"
            : $"{progress.ReportedProgress:0.#}% reported";

        return $"Status {status}. {reported}; {progress.VerifiedProgress:0.#}% is currently evidence-backed. "
               + $"Evidence confidence is {confidence}. {blockers.Count} action item(s), "
               + $"{contradictions} contradiction(s), {unverified} unverified/stale report(s).";
    }
}
