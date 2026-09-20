// Purpose: Defines the deterministic, configurable heuristic policy used to score Project Truth health and risk.
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectTruth;

/// <summary>
/// Versionable policy for deterministic Project Truth scoring and risk thresholds.
/// These values form an explainable heuristic policy, not a statistical probability model.
/// Defaults intentionally preserve stable historical semantics where they are defensible
/// while making previously implicit progress heuristics explicit.
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

    public decimal PartiallyConfirmedProgressFactor { get; init; } = 0.5m;
    public decimal StaleProgressFactor { get; init; } = 0.25m;

    /// <summary>
    /// Status-only progress inference is deliberately policy-driven instead of
    /// being hard-coded in the evaluator. A blocked/failed/unknown item without
    /// an explicit percentage is treated conservatively and never receives an
    /// arbitrary 50% credit. In-progress retains the historical midpoint fallback
    /// for integrations that only expose a coarse status vocabulary.
    /// </summary>
    public decimal InferredInProgressReportedProgress { get; init; } = 50m;
    public decimal InferredBlockedReportedProgress { get; init; } = 0m;
    public decimal InferredFailedReportedProgress { get; init; } = 0m;

    // null means the source did not provide enough information to infer a percentage.
    // Integrations that have a documented mapping may opt in to an explicit fallback.
    public decimal? InferredUnknownReportedProgress { get; init; } = null;

    /// <summary>
    /// Failure kinds that should be treated as project-level critical blockers.
    /// The vocabulary is cross-domain so software/game/media evidence can be normalized
    /// without pretending that all evidence sources are equivalent.
    /// </summary>
    public IReadOnlyList<string> CriticalEvidenceKindTokens { get; init; } =
    [
        "build", "runtime", "integration", "release", "pipeline", "crash", "memory-safety", "memory-growth",
        "undefined-behavior", "hang", "compile",
        "inspection", "safety", "approval", "delivery",
        "render", "publish", "review"
    ];

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
        RequireFactor(PartiallyConfirmedProgressFactor, nameof(PartiallyConfirmedProgressFactor));
        RequireFactor(StaleProgressFactor, nameof(StaleProgressFactor));
        RequirePercentage(InferredInProgressReportedProgress, nameof(InferredInProgressReportedProgress));
        RequirePercentage(InferredBlockedReportedProgress, nameof(InferredBlockedReportedProgress));
        RequirePercentage(InferredFailedReportedProgress, nameof(InferredFailedReportedProgress));
        if (InferredUnknownReportedProgress is { } inferredUnknown)
            RequirePercentage(inferredUnknown, nameof(InferredUnknownReportedProgress));

        if (CriticalEvidenceKindTokens is null
            || CriticalEvidenceKindTokens.Count == 0
            || CriticalEvidenceKindTokens.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Critical evidence kind tokens must contain at least one non-empty value.",
                nameof(CriticalEvidenceKindTokens));
        }

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

    private static void RequireFactor(decimal value, string name)
    {
        if (value is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(name, value, "Progress factors must be between 0 and 1.");
    }
}
