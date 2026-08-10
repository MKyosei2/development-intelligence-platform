namespace Dip.Application.Features.Intelligence;

public sealed record CommunityHardwareSubmission(
    string HardwareKey,
    string Platform,
    string CpuArch,
    string GpuVendor,
    string OsFamily,
    string DriverFamily,
    decimal CpuSingleScore,
    decimal CpuMultiScore,
    decimal GpuScore,
    decimal MemoryScore,
    decimal StorageScore,
    decimal ThermalScore,
    long MemoryMb,
    string ConsentToken,
    string? ClientPseudonym = null);

public sealed record CommunityHardwareAggregate(
    string HardwareKey,
    int SampleCount,
    decimal CpuMedian,
    decimal CpuP10,
    decimal CpuP90,
    decimal GpuMedian,
    decimal GpuP10,
    decimal GpuP90,
    decimal MemoryMedian,
    decimal StorageMedian,
    decimal ThermalMedian,
    decimal ConfidencePercent);

public sealed record MinimumHardwareRequest(
    Guid ProjectId,
    string SubjectKey,
    string WorkspaceRelativePath,
    string RuntimeExecutable,
    IReadOnlyList<string> RuntimeArguments,
    int TargetFps,
    long MaximumMemoryMb,
    int MaxIterations = 8);

public sealed record MinimumHardwareResult(
    decimal CpuPercent,
    decimal GpuPercent,
    decimal MemoryPercent,
    decimal StoragePercent,
    decimal SafeCpuPercent,
    decimal SafeGpuPercent,
    decimal SafeMemoryPercent,
    decimal SafeStoragePercent,
    decimal ConfidencePercent,
    string EvidenceJson);

public sealed record AdaptiveQaRequest(
    Guid ProjectId,
    string SubjectKey,
    IReadOnlyList<string> ChangedPaths,
    int BudgetMinutes);

public sealed record QaPlanItem(
    string Kind,
    string Reason,
    int EstimatedMinutes,
    decimal RiskWeight,
    bool Required);

public sealed record AdaptiveQaPlan(
    IReadOnlyList<QaPlanItem> Items,
    int EstimatedMinutes,
    decimal HistoricalRisk,
    string Rationale);

public sealed record RegressionRequest(
    Guid ProjectId,
    string SubjectKey,
    string GoodCommit,
    string BadCommit,
    string WorkspaceRelativePath,
    string VerificationCommand,
    IReadOnlyList<string> VerificationArguments,
    int TimeoutSeconds = 300);

public sealed record RegressionResult(
    string? FirstBadCommit,
    IReadOnlyList<string> TestedCommits,
    string Status,
    string EvidenceJson);

public sealed record ReliabilityRequest(
    Guid ProjectId,
    string SubjectKey,
    string WorkspaceRelativePath,
    string Executable,
    IReadOnlyList<string> Arguments,
    int RepeatCount = 20,
    int SoakMinutes = 0);

public sealed record ReliabilityResult(
    int Runs,
    int Passes,
    int Failures,
    int Timeouts,
    decimal FailureRate,
    decimal FlakyScore,
    decimal MemoryGrowthMb,
    decimal PerformanceDriftPercent,
    string EvidenceJson);

public sealed record VisualRegressionRequest(
    Guid ProjectId,
    string SubjectKey,
    string WorkspaceRelativePath,
    string BaselinePath,
    string CurrentPath,
    decimal ThresholdPercent = 1m);

public sealed record MediaRegressionResult(
    bool Passed,
    decimal DifferencePercent,
    string Kind,
    string DiffArtifact,
    string EvidenceJson);

public sealed record RootCauseRequest(
    Guid ProjectId,
    string SubjectKey,
    string FindingText,
    string StackTrace,
    string DiffText,
    IReadOnlyList<string> LogLines,
    bool IncludeSuggestedAction = true);

public sealed record RootCauseCandidate(
    string Cause,
    decimal ConfidencePercent,
    IReadOnlyList<string> Evidence,
    string? SuggestedAction);

public sealed record RootCauseResult(
    IReadOnlyList<RootCauseCandidate> Candidates,
    string GroundingStatus);

public sealed record ForecastResult(
    decimal ReleaseRiskPercent,
    string Confidence,
    DateTimeOffset? EarliestCompletionUtc,
    DateTimeOffset? LatestCompletionUtc,
    IReadOnlyList<string> Reasons);

public sealed record PmDecisionItem(
    int Priority,
    string Subject,
    string Situation,
    string Why,
    string NextAction,
    string Confidence);

public sealed record PmDecisionBrief(
    IReadOnlyList<PmDecisionItem> Priorities,
    IReadOnlyList<string> Changes,
    IReadOnlyList<string> HealthChangeReasons,
    string Summary);
