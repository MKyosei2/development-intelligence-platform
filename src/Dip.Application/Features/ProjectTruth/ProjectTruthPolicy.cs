using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

/// <summary>
/// Versionable policy for deterministic Project Truth scoring and risk thresholds.
/// Defaults intentionally preserve the historical evaluator behavior; they are
/// a conservative baseline, not a claim of statistical optimality.
/// </summary>
public sealed record ProjectTruthPolicy
{
    public static ProjectTruthPolicy Default { get; } = new();

    public decimal CriticalBlockerHealthPenalty { get; init; } = 25m;
    public decimal BlockerHealthPenalty { get; init; } = 12m;
    public decimal ContradictionHealthPenalty { get; init; } = 10m;
    public decimal UnverifiedClaimHealthPenalty { get; init; } = 3m;
    public decimal MaxUnverifiedClaimHealthPenalty { get; init; } = 12m;
    public decimal ProgressGapHealthPenaltyFactor { get; init; } = 0.25m;
    public decimal MaxProgressGapHealthPenalty { get; init; } = 20m;

    public decimal UnverifiedEvidenceHealthPenalty { get; init; } = 20m;
    public decimal LowEvidenceHealthPenalty { get; init; } = 15m;
    public decimal MediumEvidenceHealthPenalty { get; init; } = 8m;
    public decimal HighEvidenceHealthPenalty { get; init; } = 3m;

    public int HighRiskContradictionThreshold { get; init; } = 2;
    public decimal MediumRiskProgressGapThreshold { get; init; } = 20m;
    public decimal NearCompletionReportedProgressThreshold { get; init; } = 80m;
    public EvidenceConfidence MediumRiskMaximumConfidence { get; init; } = EvidenceConfidence.Low;

    public ProjectTruthPolicy Validate()
    {
        RequireNonNegative(CriticalBlockerHealthPenalty, nameof(CriticalBlockerHealthPenalty));
        RequireNonNegative(BlockerHealthPenalty, nameof(BlockerHealthPenalty));
        RequireNonNegative(ContradictionHealthPenalty, nameof(ContradictionHealthPenalty));
        RequireNonNegative(UnverifiedClaimHealthPenalty, nameof(UnverifiedClaimHealthPenalty));
        RequireNonNegative(MaxUnverifiedClaimHealthPenalty, nameof(MaxUnverifiedClaimHealthPenalty));
        RequireNonNegative(ProgressGapHealthPenaltyFactor, nameof(ProgressGapHealthPenaltyFactor));
        RequireNonNegative(MaxProgressGapHealthPenalty, nameof(MaxProgressGapHealthPenalty));
        RequireNonNegative(UnverifiedEvidenceHealthPenalty, nameof(UnverifiedEvidenceHealthPenalty));
        RequireNonNegative(LowEvidenceHealthPenalty, nameof(LowEvidenceHealthPenalty));
        RequireNonNegative(MediumEvidenceHealthPenalty, nameof(MediumEvidenceHealthPenalty));
        RequireNonNegative(HighEvidenceHealthPenalty, nameof(HighEvidenceHealthPenalty));

        if (HighRiskContradictionThreshold < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(HighRiskContradictionThreshold),
                HighRiskContradictionThreshold,
                "The high-risk contradiction threshold must be at least 1.");
        }

        RequirePercentage(MediumRiskProgressGapThreshold, nameof(MediumRiskProgressGapThreshold));
        RequirePercentage(NearCompletionReportedProgressThreshold, nameof(NearCompletionReportedProgressThreshold));

        return this;
    }

    private static void RequireNonNegative(decimal value, string name)
    {
        if (value < 0m)
            throw new ArgumentOutOfRangeException(name, value, "Policy values cannot be negative.");
    }

    private static void RequirePercentage(decimal value, string name)
    {
        if (value is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(name, value, "Percentage thresholds must be between 0 and 100.");
    }
}
