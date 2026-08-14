using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityGovernance;

public sealed record QaPolicyRules(int MaxCriticalFindings=0,int MaxWarningFindings=50,decimal MaxFlakyRatePercent=20m,int MaxInfrastructureFailures=0,int MaxEvidenceAgeDays=14,long MaxRunDurationMs=900000,bool BlockOnStaleEvidence=true,bool BlockOnUnavailableQa=true,decimal MaxCpuSeconds=600m,long MaxArtifactBytes=2147483648L);
public sealed record TransitionFindingCommand(Guid ProjectId,string Fingerprint,FindingLifecycleStatus Status,string Reason,long? ExpectedVersion=null);
public sealed record CreateWaiverCommand(Guid ProjectId,string Name,string Reason,string? Fingerprint,string? RuleId,string? PathPattern,DateTimeOffset ExpiresAtUtc);
public sealed record DecideWaiverCommand(string Reason,long? ExpectedVersion=null);
public sealed record CreateOwnershipRuleCommand(Guid ProjectId,string Name,string PathPattern,string Team,string? Workstream,int Priority=0,bool Enabled=true);
public sealed record CreateQaPolicyCommand(Guid ProjectId,string Name,string Version,QaPolicyRules Rules);
public sealed record PolicyLifecycleCommand(string Reason,long? ExpectedVersion=null);
public sealed record CreateTestImpactRuleCommand(Guid ProjectId,string PathPattern,string TestKey,string CommandJson,int Priority=0,bool Enabled=true);
public sealed record RebaselineQaCommand(Guid ProjectId,string SubjectKey,string MetricKey,string CommitSha,decimal Value);
public sealed record CreateRetentionRuleCommand(Guid ProjectId,string KindPattern,string RetentionClass,int RetentionDays,bool PreserveOnCritical=true,bool Enabled=true);
public sealed record SmartTestSelectionRequest(Guid ProjectId,IReadOnlyList<string> ChangedPaths,int MaximumTests=100);
public sealed record SmartTestSelectionDto(string TestKey,string CommandJson,int Priority,IReadOnlyList<string> MatchedPaths,string Reason);
public sealed record TestRecommendationDto(string TestKey,string Reason,string Priority,string? CommandJson);
public sealed record QaFreshnessDto(string Status,int AgeDays,int MaximumAgeDays,string? CommitSha,DateTimeOffset? CapturedAtUtc,string Reason,string Confidence="Unverified",decimal ConfidencePercent=0m);
public sealed record QaGovernanceOverviewDto(Guid ProjectId,string GateVerdict,string GateSummary,int OpenFindings,int ActiveWaivers,int FlakyTests,int RegressedBaselines,string SystemHealth,decimal TechnicalDebtScore,string Freshness,DateTimeOffset GeneratedAtUtc,string TrendDirection="Unknown",decimal TechnicalDebtDelta=0m,string EvidenceConfidence="Unverified",decimal EvidenceConfidencePercent=0m);
public sealed record RootCauseCorrelationDto(string Fingerprint,string? LastKnownGoodCommit,string? FirstBadCommit,string? LastObservedCommit,string? Team,string? Workstream,int Occurrences,IReadOnlyList<string> CandidateCommits,string Explanation);
public sealed record DiagnosticSchemaDto(string SchemaVersion,string NormalizerVersion,IReadOnlyList<string> SupportedFormats,string CompatibilityPolicy);
public sealed record QaCostSummaryDto(long Runs,long TotalWallDurationMs,long TotalToolDurationMs,long TotalArtifactBytes,decimal EstimatedCpuSeconds,long OverBudgetRuns);
public sealed record CrossProjectBenchmarkMetricDto(Guid ProjectId,string ProjectName,Guid RunId,string CommitSha,string EnvironmentFingerprint,decimal Value,string Unit,string Aggregation,decimal DifferenceFromBestPercent);
public sealed record CrossProjectBenchmarkComparisonDto(string DefinitionKey,string MetricName,IReadOnlyList<CrossProjectBenchmarkMetricDto> Projects,string Caution);
public sealed record BugMinimizationCommand(Guid ProjectId,Guid FindingId,string WorkspaceRelativePath,string Executable,IReadOnlyList<string> Arguments,int TimeoutSeconds=30);
public sealed record BugMinimizationDto(Guid Id,string Status,int OriginalUnits,int MinimizedUnits,IReadOnlyList<string> MinimizedArguments,string Detail);
public interface IQaBugMinimizer{Task<BugMinimizationDto> MinimizeAsync(BugMinimizationCommand command,CancellationToken ct);}
public sealed record RepositoryOwnershipResolution(string Team,string? Workstream,string Pattern,string Source);
public interface IRepositoryOwnershipResolver{Task<RepositoryOwnershipResolution?> ResolveAsync(string workspaceRelativePath,string? filePath,CancellationToken ct);}


public sealed record RequestReleaseGateOverrideCommand(Guid ProjectId,Guid? PolicyEvaluationId,Guid? QaRunId,string? CommitSha,string Reason,DateTimeOffset ExpiresAtUtc);
public sealed record DecideReleaseGateOverrideCommand(string Reason,long? ExpectedVersion=null);
public sealed record RecordBackupVerificationCommand(Guid ProjectId,string BackupKind,string BackupReference,string SourceCommit,string EnvironmentFingerprint,QaBackupVerificationStatus Status,string Detail);
public sealed record PageRequest(string? Cursor=null,int PageSize=100);
public sealed record PageResult<T>(IReadOnlyList<T> Items,string? NextCursor,int PageSize);
public sealed record QaFindingStateDto(Guid Id,Guid ProjectId,string Fingerprint,Guid LastFindingId,string SubjectKey,string? LastCommitSha,FindingLifecycleStatus Status,int Occurrences,string? Team,string? Workstream,QaOwnershipSource OwnershipSource,string? DispositionReason,string? UpdatedBy,DateTimeOffset FirstSeenAtUtc,DateTimeOffset LastSeenAtUtc,DateTimeOffset UpdatedAtUtc,long Version);
public sealed record QaWaiverDto(Guid Id,Guid ProjectId,string Name,string Reason,string RequestedBy,string? ApprovedBy,string? DecisionReason,string? Fingerprint,string? RuleId,string? PathPattern,WaiverStatus Status,DateTimeOffset CreatedAtUtc,DateTimeOffset UpdatedAtUtc,DateTimeOffset ExpiresAtUtc,DateTimeOffset? ApprovedAtUtc,DateTimeOffset? RevokedAtUtc,long Version);
public sealed record QaPolicyDto(Guid Id,Guid ProjectId,string Name,string Version,string RulesJson,QaPolicyLifecycleStatus LifecycleStatus,int Revision,Guid? SupersedesPolicyId,string? ApprovedBy,string? ActivatedBy,DateTimeOffset CreatedAtUtc,DateTimeOffset UpdatedAtUtc,DateTimeOffset? ApprovedAtUtc,DateTimeOffset? ActivatedAtUtc,long ConcurrencyVersion);
public sealed record QaReleaseGateOverrideDto(Guid Id,Guid ProjectId,Guid? PolicyEvaluationId,Guid? QaRunId,string CommitSha,string Reason,string RequestedBy,QaReleaseOverrideStatus Status,string? ApprovedBy,string? DecisionReason,DateTimeOffset CreatedAtUtc,DateTimeOffset UpdatedAtUtc,DateTimeOffset ExpiresAtUtc,DateTimeOffset? DecidedAtUtc,long Version);
public sealed record GovernanceAuditContext(string Actor,string? RequestId=null,string? CorrelationId=null);
public interface IQaGovernanceExecutionCoordinator{Task<bool> ExecuteOnceAsync(Guid projectId,Guid qaRunId,string governanceVersion,string inputHash,Func<CancellationToken,Task<string>> action,CancellationToken ct);}
public sealed record QaReliabilityDto(Guid Id,Guid ProjectId,string TestKey,int PassCount,int FailCount,int ConsecutivePasses,int ConsecutiveFailures,TestReliabilityClassification Classification,string? LastCommitSha,DateTimeOffset FirstObservedAtUtc,DateTimeOffset LastObservedAtUtc,decimal FailureRatePercent);
public sealed record QaBaselineDto(Guid Id,Guid ProjectId,string SubjectKey,string MetricKey,string BaselineCommit,string LastKnownGoodCommit,string? FirstBadCommit,decimal BaselineValue,decimal CurrentValue,decimal RegressionPercent,QaRegressionStatus Status,DateTimeOffset UpdatedAtUtc);
public sealed record QaHealthDto(Guid Id,Guid ProjectId,Guid? QaRunId,string Component,QaSystemHealthStatus Status,string Detail,DateTimeOffset ObservedAtUtc);
public sealed record QaTrendDto(Guid Id,Guid ProjectId,Guid QaRunId,int OpenFindingCount,int CriticalCount,int WarningCount,int FlakyCount,int ActiveWaiverCount,decimal TechnicalDebtScore,DateTimeOffset CapturedAtUtc);
public sealed record QaOwnershipRuleDto(Guid Id,Guid ProjectId,string Name,string PathPattern,string Team,string? Workstream,int Priority,bool Enabled,DateTimeOffset CreatedAtUtc,DateTimeOffset UpdatedAtUtc);
public sealed record QaTestImpactRuleDto(Guid Id,Guid ProjectId,string PathPattern,string TestKey,string CommandJson,int Priority,bool Enabled,DateTimeOffset CreatedAtUtc);
public sealed record QaRetentionRuleDto(Guid Id,Guid ProjectId,string KindPattern,string RetentionClass,int RetentionDays,bool PreserveOnCritical,bool Enabled,DateTimeOffset CreatedAtUtc);
public sealed record QaMinimizationRecordDto(Guid Id,Guid ProjectId,Guid FindingId,int OriginalUnits,int MinimizedUnits,QaMinimizationStatus Status,string Detail,DateTimeOffset CreatedAtUtc);
public sealed record QaBackupVerificationDto(Guid Id,Guid ProjectId,string BackupKind,string BackupReference,string SourceCommit,string EnvironmentFingerprint,QaBackupVerificationStatus Status,string Detail,DateTimeOffset VerifiedAtUtc);
public sealed record GovernanceAuditLogDto(Guid Id,Guid ProjectId,string Category,string Action,string Actor,string? SubjectKey,string? DetailJson,string? RequestId,string? CorrelationId,string? Reason,DateTimeOffset CreatedAtUtc);
