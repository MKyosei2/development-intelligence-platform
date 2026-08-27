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
    private readonly ProjectTruthPolicy _policy;

    public ProjectTruthEvaluator(
        ClaimVerificationEvaluator claimVerification,
        EvidenceConfidenceEvaluator confidence,
        ProgressEvaluator progress,
        ProjectBlockerEvaluator blockers,
        ProjectRiskEvaluator risk)
        : this(
            claimVerification,
            confidence,
            progress,
            blockers,
            risk,
            ProjectTruthPolicy.Default)
    {
    }

    public ProjectTruthEvaluator(
        ClaimVerificationEvaluator claimVerification,
        EvidenceConfidenceEvaluator confidence,
        ProgressEvaluator progress,
        ProjectBlockerEvaluator blockers,
        ProjectRiskEvaluator risk,
        ProjectTruthPolicy policy)
    {
        _claimVerification = claimVerification ?? throw new ArgumentNullException(nameof(claimVerification));
        _confidence = confidence ?? throw new ArgumentNullException(nameof(confidence));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _blockers = blockers ?? throw new ArgumentNullException(nameof(blockers));
        _risk = risk ?? throw new ArgumentNullException(nameof(risk));
        _policy = (policy ?? throw new ArgumentNullException(nameof(policy))).Validate();
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

    private decimal CalculateHealth(
        ProgressAssessment progress,
        EvidenceConfidence confidence,
        IReadOnlyCollection<ProjectBlocker> blockers,
        int contradictionCount,
        int unverifiedClaimCount)
    {
        var score = 100m;
        score -= blockers.Count(item => item.Severity == "Critical") * _policy.CriticalBlockerHealthPenalty;
        score -= blockers.Count(item => item.Severity == "Blocker") * _policy.BlockerHealthPenalty;
        score -= contradictionCount * _policy.ContradictionHealthPenalty;
        score -= Math.Min(
            _policy.MaxUnverifiedClaimHealthPenalty,
            unverifiedClaimCount * _policy.UnverifiedClaimHealthPenalty);

        if (progress.ReportedProgress is { } reported)
        {
            var gap = Math.Max(0m, reported - progress.VerifiedProgress);
            score -= Math.Min(
                _policy.MaxProgressGapHealthPenalty,
                gap * _policy.ProgressGapHealthPenaltyFactor);
        }

        score -= confidence switch
        {
            EvidenceConfidence.Unverified => _policy.UnverifiedEvidenceHealthPenalty,
            EvidenceConfidence.Low => _policy.LowEvidenceHealthPenalty,
            EvidenceConfidence.Medium => _policy.MediumEvidenceHealthPenalty,
            EvidenceConfidence.High => _policy.HighEvidenceHealthPenalty,
            _ => 0m
        };

        return decimal.Round(Math.Clamp(score, 0m, 100m), 2);
    }

    private IReadOnlyList<string> BuildTopActions(
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
            && reported - progress.VerifiedProgress >= _policy.MediumRiskProgressGapThreshold
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
