namespace Dip.Domain.Enums;

public enum VisualComparisonVerdict
{
    Unknown = 0,
    Passed = 1,
    Warning = 2,
    Failed = 3,
    Incompatible = 4
}

public enum AssetQaSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public enum AssetQaFindingState
{
    Open = 0,
    Resolved = 1,
    Suppressed = 2
}

public enum ClaimEvidenceAssessmentStatus
{
    Supported = 0,
    PartiallySupported = 1,
    InsufficientEvidence = 2,
    Contradicted = 3
}
