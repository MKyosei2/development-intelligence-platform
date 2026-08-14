using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityGovernance;

public sealed record QaPolicyRules(int MaxCriticalFindings=0,int MaxWarningFindings=50,decimal MaxFlakyRatePercent=20m,int MaxInfrastructureFailures=0,int MaxEvidenceAgeDays=14,long MaxRunDurationMs=900000,bool BlockOnStaleEvidence=true,bool BlockOnUnavailableQa=true,decimal MaxCpuSeconds=600m,long MaxArtifactBytes=2147483648L);
public sealed record TransitionFindingCommand(Guid ProjectId,string Fingerprint,FindingLifecycleStatus Status,string Reason,string Actor);
public sealed record CreateWaiverCommand(Guid ProjectId,string Name,string Reason,string ApprovedBy,string? Fingerprint,string? RuleId,string? PathPattern,DateTimeOffset ExpiresAtUtc);
public sealed record CreateOwnershipRuleCommand(Guid ProjectId,string Name,string PathPattern,string Team,string? Workstream,int Priority=0,bool Enabled=true);
public sealed record CreateQaPolicyCommand(Guid ProjectId,string Name,string Version,QaPolicyRules Rules,bool Enabled=true);
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
