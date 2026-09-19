// Purpose: Centralizes deterministic release-readiness scoring constants.
namespace Dip.Application.Features.Release;

internal static class ReleaseReadinessPolicy
{
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
