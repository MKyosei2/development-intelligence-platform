// Purpose: Defines domain behavior and state for this feature area.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

/// <summary>
/// Point-in-time, evidence-backed project state prepared for non-technical
/// decision makers. The snapshot preserves both reported and verified progress.
/// </summary>
public sealed class ProjectTruthSnapshot
{
    private ProjectTruthSnapshot() { }

    public ProjectTruthSnapshot(
        Guid id,
        Guid projectId,
        ProjectTruthStatus truthStatus,
        ProjectRiskLevel riskLevel,
        decimal healthScore,
        decimal verifiedProgress,
        decimal? reportedProgress,
        EvidenceConfidence evidenceConfidence,
        int blockerCount,
        int contradictionCount,
        int unverifiedClaimCount,
        string summary,
        string blockersJson,
        string topActionsJson,
        DateTimeOffset evaluatedAtUtc)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (!Enum.IsDefined(truthStatus))
            throw new ArgumentOutOfRangeException(nameof(truthStatus));
        if (!Enum.IsDefined(riskLevel))
            throw new ArgumentOutOfRangeException(nameof(riskLevel));
        if (!Enum.IsDefined(evidenceConfidence))
            throw new ArgumentOutOfRangeException(nameof(evidenceConfidence));
        if (healthScore is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(healthScore));
        if (verifiedProgress is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(verifiedProgress));
        if (reportedProgress is < 0m or > 100m)
            throw new ArgumentOutOfRangeException(nameof(reportedProgress));
        if (blockerCount < 0)
            throw new ArgumentOutOfRangeException(nameof(blockerCount));
        if (contradictionCount < 0)
            throw new ArgumentOutOfRangeException(nameof(contradictionCount));
        if (unverifiedClaimCount < 0)
            throw new ArgumentOutOfRangeException(nameof(unverifiedClaimCount));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        TruthStatus = truthStatus;
        RiskLevel = riskLevel;
        HealthScore = decimal.Round(healthScore, 2);
        VerifiedProgress = decimal.Round(verifiedProgress, 2);
        ReportedProgress = reportedProgress is null ? null : decimal.Round(reportedProgress.Value, 2);
        EvidenceConfidence = evidenceConfidence;
        BlockerCount = blockerCount;
        ContradictionCount = contradictionCount;
        UnverifiedClaimCount = unverifiedClaimCount;
        Summary = Required(summary, 4000, nameof(summary));
        BlockersJson = Required(blockersJson, 32000, nameof(blockersJson));
        TopActionsJson = Required(topActionsJson, 16000, nameof(topActionsJson));
        EvaluatedAtUtc = evaluatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public ProjectTruthStatus TruthStatus { get; private set; }
    public ProjectRiskLevel RiskLevel { get; private set; }
    public decimal HealthScore { get; private set; }
    public decimal VerifiedProgress { get; private set; }
    public decimal? ReportedProgress { get; private set; }
    public EvidenceConfidence EvidenceConfidence { get; private set; }
    public int BlockerCount { get; private set; }
    public int ContradictionCount { get; private set; }
    public int UnverifiedClaimCount { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public string BlockersJson { get; private set; } = "[]";
    public string TopActionsJson { get; private set; } = "[]";
    public DateTimeOffset EvaluatedAtUtc { get; private set; }

    private static string Required(string? value, int maxLength, string parameterName)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 || normalized.Length > maxLength)
            throw new ArgumentException($"{parameterName} must be 1-{maxLength} characters.", parameterName);
        return normalized;
    }
}
