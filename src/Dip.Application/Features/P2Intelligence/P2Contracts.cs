using Dip.Domain.Enums;

namespace Dip.Application.Features.P2Intelligence;

public sealed record CommunityHardwareSubmission(
    Guid ProjectId,
    string ProfileKey,
    string Platform,
    string HardwareKey,
    string OsFamily,
    string DriverFamily,
    decimal CpuSingleScore,
    decimal CpuMultiScore,
    decimal GpuScore,
    decimal MemoryScore,
    decimal StorageScore,
    decimal ThermalRetentionPercent,
    long AvailableMemoryMb,
    bool RealDevice,
    bool ConsentGranted,
    DateTimeOffset MeasuredAtUtc);


public sealed record CreateCommunityHardwareProfileCommand(
    Guid ProjectId,string ProfileKey,string Platform,string Name,int TargetFps = 30);

public sealed record HardwareProfileConfidenceDto(
    string ProfileKey,
    string Platform,
    int SampleCount,
    int RealDeviceSampleCount,
    int OsFamilyCount,
    int DriverFamilyCount,
    decimal MedianCpuSingle,
    decimal MedianCpuMulti,
    decimal MedianGpu,
    decimal MedianMemory,
    decimal MedianStorage,
    decimal MedianThermalRetention,
    decimal P10Gpu,
    decimal P90Gpu,
    string Confidence,
    decimal ConfidencePercent,
    DateTimeOffset? LatestSampleAtUtc);

public sealed record PortabilityCommand(
    Guid ProjectId,
    string SubjectKey,
    Guid SourceHardwareProfileId,
    Guid TargetHardwareProfileId,
    decimal RequiredCpuScore,
    decimal RequiredGpuScore,
    decimal RequiredMemoryScore,
    decimal RequiredStorageScore,
    long RequiredMemoryMb,
    decimal RequiredThermalRetention);

public sealed record PortabilityDimension(
    string Name,decimal Available,decimal Required,decimal Ratio,string Status,string Explanation);

public sealed record PortabilityResult(
    decimal Score,string Status,string Confidence,IReadOnlyList<PortabilityDimension> Dimensions,IReadOnlyList<string> Blockers);

public sealed record AdaptiveQaPlanCommand(
    Guid ProjectId,
    string SubjectKey,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<string>? ChangedTags = null,
    decimal MaxEstimatedMinutes = 60m);

public sealed record AdaptiveQaStep(
    string Id,string Name,string Category,string Reason,decimal RiskScore,decimal EstimatedMinutes,int Priority,bool Required);

public sealed record AdaptiveQaPlanDto(
    Guid Id,string ChangeSetHash,decimal RiskScore,decimal EstimatedMinutes,IReadOnlyList<AdaptiveQaStep> Steps,IReadOnlyList<string> SkippedReasons);

public sealed record ExperimentTarget(
    Guid ProjectId,
    string SubjectKey,
    string WorkspaceRelativePath,
    string Executable,
    IReadOnlyList<string>? Arguments = null,
    int TimeoutSeconds = 60);

public sealed record RepeatedRunCommand(
    ExperimentTarget Target,
    int Attempts = 20,
    decimal FlakyWarningPercent = 2m,
    decimal FlakyFailurePercent = 10m);

public sealed record FlakyRunAttempt(int Attempt,bool Passed,bool TimedOut,int? ExitCode,long DurationMs,string Fingerprint);
public sealed record FlakyResult(
    string Status,int Attempts,int Passed,int Failed,int TimedOut,decimal FailureRatePercent,decimal ConfidencePercent,
    IReadOnlyList<FlakyRunAttempt> Runs,string ArtifactReference);

public sealed record SoakCommand(
    ExperimentTarget Target,
    int DurationSeconds = 1800,
    int SampleIntervalSeconds = 5,
    decimal MemoryGrowthWarningPercent = 20m,
    decimal PerformanceDriftWarningPercent = 15m);

public sealed record SoakTelemetryPoint(
    int ElapsedSeconds,long WorkingSetMb,decimal CpuPercent,decimal? Fps,decimal? FrameTimeMs);
public sealed record SoakResult(
    string Status,long StartMemoryMb,long EndMemoryMb,decimal MemoryGrowthPercent,decimal? StartFps,decimal? EndFps,
    decimal? PerformanceDriftPercent,bool Crashed,bool TimedOut,IReadOnlyList<SoakTelemetryPoint> Samples,string ArtifactReference);

public sealed record GitBisectCommand(
    ExperimentTarget Target,string GoodCommit,string BadCommit,int MaxEvaluations = 20);
public sealed record GitBisectEvaluation(string Commit,string Status,bool Passed,long DurationMs);
public sealed record GitBisectResult(
    string Status,string? FirstBadCommit,string? LastGoodCommit,int Evaluations,IReadOnlyList<GitBisectEvaluation> History,string ArtifactReference);

public sealed record RegressionPoint(string Revision,decimal Value,DateTimeOffset? ObservedAtUtc = null);

public sealed record RegressionOriginCommand(
    Guid ProjectId,string SubjectKey,IReadOnlyList<RegressionPoint> Points,bool LowerIsBetter,decimal ThresholdPercent = 10m);

public sealed record DecisionBriefCommand(
    Guid ProjectId,IReadOnlyDictionary<string,IReadOnlyList<string>>? Dependencies = null);

public sealed record RegressionOriginResult(
    string Status,string? RegressionRevision,decimal BaselineValue,decimal RegressedValue,decimal ChangePercent,string Direction);

public sealed record MutationCaseSpec(
    string Id,string FileRelativePath,string OriginalText,string ReplacementText);
public sealed record MutationCommand(
    ExperimentTarget Target,IReadOnlyList<MutationCaseSpec> Mutations,int MaxMutations = 50);
public sealed record MutationCaseResult(string Id,bool Applied,bool Killed,int? ExitCode,string Detail);
public sealed record MutationResult(
    string Status,int Total,int Applied,int Killed,int Survived,decimal MutationScorePercent,IReadOnlyList<MutationCaseResult> Cases,string ArtifactReference);

public sealed record FuzzCommand(
    ExperimentTarget Target,
    string ArgumentPlaceholder = "{input}",
    int Cases = 100,
    int MaxInputBytes = 4096,
    int Seed = 1337);
public sealed record FuzzCaseResult(int Case,int Size,int? ExitCode,bool TimedOut,bool Crashed,string Fingerprint);
public sealed record FuzzResult(
    string Status,int Cases,int Crashes,int Timeouts,int UniqueFingerprints,IReadOnlyList<FuzzCaseResult> Results,string ArtifactReference);

public enum MediaRegressionType { Visual, Video, Audio }

public sealed record MediaRegressionCommand(
    Guid ProjectId,string SubjectKey,string WorkspaceRelativePath,string BaselinePath,string CurrentPath,MediaRegressionType Type,
    decimal WarningThresholdPercent = 2m,decimal FailureThresholdPercent = 10m,int VideoSampleFps = 2);

public sealed record MediaRegressionResult(
    string Status,MediaRegressionType Type,decimal DifferencePercent,decimal ConfidencePercent,
    string? FirstDifference,string Summary,string ArtifactReference,string DetailJson);

public sealed record RootCauseCommand(Guid ProjectId,string SubjectKey,string? Question = null,int EvidenceLimit = 40);
public sealed record RootCauseCandidate(string Title,decimal ConfidencePercent,string Rationale,IReadOnlyList<Guid> EvidenceIds);
public sealed record RootCauseResult(
    string Mode,string Status,IReadOnlyList<RootCauseCandidate> Candidates,IReadOnlyList<string> SuggestedActions,
    IReadOnlyList<Guid> GroundingEvidenceIds,string Summary);

public sealed record SimilarIncidentDto(
    Guid RecordId,string SubjectKey,string Kind,string Summary,decimal SimilarityPercent,DateTimeOffset CreatedAtUtc);

public sealed record ScheduleRiskDto(
    string Status,decimal RiskPercent,string Confidence,DateTimeOffset? ReleaseDateUtc,decimal EstimatedDaysRemaining,
    decimal ExpectedResolutionDays,int CriticalBlockers,int OpenBlockers,string Explanation);

public sealed record CompletionForecastDto(
    string Status,DateTimeOffset? Earliest,DateTimeOffset? MostLikely,DateTimeOffset? Latest,string Confidence,string Explanation);

public sealed record StagnationItem(
    string SubjectKey,DateTimeOffset? LastObjectiveEvidenceAtUtc,DateTimeOffset? LastReportedAtUtc,int DaysWithoutObjectiveEvidence,string Status,string Explanation);

public sealed record DependencyRiskCommand(Guid ProjectId,IReadOnlyDictionary<string,IReadOnlyList<string>> Dependencies);
public sealed record DependencyRiskItem(string SubjectKey,string Dependency,string Status,string Explanation);

public sealed record EvidenceFreshnessItem(
    Guid EvidenceId,string SubjectKey,string EvidenceKind,DateTimeOffset ObservedAtUtc,int AgeHours,string Freshness,string EffectiveConfidence);

public sealed record EvidenceConflictItem(
    string SubjectKey,string EvidenceKind,Guid AuthoritativeEvidenceId,IReadOnlyList<Guid> ConflictingEvidenceIds,string Resolution,string Explanation);

public sealed record DecisionPriority(
    int Priority,string SubjectKey,string Title,string Why,string SuggestedNextAction,string EvidenceConfidence,IReadOnlyList<Guid> EvidenceIds);

public sealed record DecisionBriefDto(
    Guid ProjectId,string OverallStatus,string Summary,IReadOnlyList<DecisionPriority> Priorities,ScheduleRiskDto ScheduleRisk,
    CompletionForecastDto CompletionForecast,IReadOnlyList<StagnationItem> Stagnation,IReadOnlyList<DependencyRiskItem> DependencyRisks);

public sealed record DailyChangeDto(
    int NewEvidence,int NewFailures,int ResolvedOrSucceeded,int NewClaims,int NewContradictions,IReadOnlyList<string> Highlights);

public sealed record HealthChangeDto(
    decimal? PreviousHealth,decimal? CurrentHealth,decimal? Difference,string Explanation,IReadOnlyList<string> Reasons);

public sealed record P2IntelligenceDto(
    Guid Id,string SubjectKey,string Kind,string Status,decimal? Score,string Confidence,string Summary,string PayloadJson,string? SourceReference,DateTimeOffset CreatedAtUtc);

public sealed record ExternalCommandResult(
    bool Started,bool Passed,bool TimedOut,int? ExitCode,long DurationMs,string Stdout,string Stderr,string Fingerprint,string ArtifactReference);

public interface IP2ExperimentRunner
{
    Task<FlakyResult> RunRepeatedAsync(RepeatedRunCommand command,CancellationToken cancellationToken);
    Task<SoakResult> RunSoakAsync(SoakCommand command,CancellationToken cancellationToken);
    Task<GitBisectResult> FindRegressionCommitAsync(GitBisectCommand command,CancellationToken cancellationToken);
    Task<MutationResult> RunMutationAsync(MutationCommand command,CancellationToken cancellationToken);
    Task<FuzzResult> RunFuzzAsync(FuzzCommand command,CancellationToken cancellationToken);
}

public interface IMediaRegressionRunner
{
    Task<MediaRegressionResult> AnalyzeAsync(MediaRegressionCommand command,CancellationToken cancellationToken);
}

public sealed record AiRootCauseRequest(string Question,string EvidenceJson);
public sealed record AiRootCauseResponse(string Model,string Json);

public interface IAiRootCauseClient
{
    Task<AiRootCauseResponse?> AnalyzeAsync(AiRootCauseRequest request,CancellationToken cancellationToken);
}
