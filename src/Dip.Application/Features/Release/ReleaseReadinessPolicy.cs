// Purpose: Centralizes deterministic release-readiness heuristic scoring while keeping it distinct from statistical probability.
namespace Dip.Application.Features.Release;

public static class ReleaseReadinessPolicy
{
    // This score is an explainable deterministic heuristic used for relative readiness,
    // not a probability that a release will succeed.
    private const decimal BaseConfidence = 45m;
    private const decimal MaximumEvidenceContribution = 35m;
    private const decimal EvidenceContributionPerItem = 2m;
    private const decimal ProjectTruthContribution = 10m;
    private const decimal PerformanceBudgetContribution = 10m;

    public static decimal CalculateConfidence(
        int evidenceCount,
        bool hasProjectTruth,
        bool hasPerformanceBudgetEvaluation)
    {
        var evidenceContribution = Math.Min(
            MaximumEvidenceContribution,
            Math.Max(0, evidenceCount) * EvidenceContributionPerItem);

        return Math.Clamp(
            BaseConfidence
            + evidenceContribution
            + (hasProjectTruth ? ProjectTruthContribution : 0m)
            + (hasPerformanceBudgetEvaluation ? PerformanceBudgetContribution : 0m),
            0m,
            100m);
    }
}
