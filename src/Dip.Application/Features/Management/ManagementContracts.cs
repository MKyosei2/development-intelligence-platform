// Purpose: Coordinates M an ag em en tC on tr ac ts application behavior for Management.
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

public sealed record UpsertTeamCommand(Guid ProjectId,string Key,string Name,decimal CapacityHoursPerWeek,decimal BufferPercent=10m,string Status="Active",string? ExternalReference=null,string? SharedResourceKey=null,decimal? SharedPoolCapacityHoursPerWeek=null);
public sealed record UpsertWorkstreamCommand(Guid ProjectId,string Key,string Name,string TeamKey,decimal PlannedCapacityHoursPerWeek,DateTimeOffset PlannedStartUtc,DateTimeOffset DueAtUtc,decimal CompletionPercent=0m,string Status="Active",string? ExternalReference=null);
public sealed record UpsertTaskCommand(Guid ProjectId,string Key,string Name,string WorkstreamKey,decimal EstimateHours,decimal RemainingHours,DateTimeOffset PlannedStartUtc,DateTimeOffset DueAtUtc,decimal CompletionPercent=0m,string Status="Planned",string Priority="Medium",string? OwnerLabel=null,string? ExternalReference=null);
public sealed record LinkManagementDependencyCommand(Guid ProjectId,string PredecessorType,string PredecessorKey,string SuccessorType,string SuccessorKey,bool Critical=true);
public sealed record UpsertBlockerCommand(Guid ProjectId,string Key,string Title,string Reason,string Severity,DateTimeOffset OpenedAtUtc,string Status="Open",IReadOnlyList<string>? BlockedTaskKeys=null,IReadOnlyList<string>? BlockedWorkstreamKeys=null,IReadOnlyList<string>? BlockedMilestoneKeys=null,string? ExternalReference=null);
public sealed record UpsertRequirementCommand(Guid ProjectId,string Key,string Title,string Priority,string Status,string ScopeState="Included",decimal CompletionPercent=0m,string? AcceptanceSummary=null,string? ExternalReference=null);
public sealed record LinkRequirementCommand(Guid ProjectId,string RequirementKey,string TargetType,string TargetKey);
public sealed record CreateScopeBaselineCommand(Guid ProjectId,string Key,string Name,DateTimeOffset? CapturedAtUtc=null);
public sealed record LinkTaskToMilestoneCommand(Guid ProjectId,string TaskKey,string MilestoneKey);

public sealed record WorkloadBucket(DateTimeOffset WeekStartUtc,decimal CapacityHours,decimal PlannedHours,decimal UtilizationPercent,int ActiveTasks,int BlockedTasks,string Status);
public sealed record WorkstreamCapacityDto(string Key,string Name,string TeamKey,string TeamName,decimal CapacityHours,decimal PlannedHours,decimal RemainingHours,decimal UtilizationPercent,int ActiveTasks,int BlockedTasks,string Status,IReadOnlyList<WorkloadBucket> Weekly);
public sealed record TeamCapacityDto(string Key,string Name,decimal CapacityHours,decimal PlannedHours,decimal RemainingHours,decimal UtilizationPercent,int Workstreams,int ActiveTasks,int BlockedTasks,string Status,IReadOnlyList<WorkloadBucket> Weekly,string? SharedResourceKey=null,decimal? SharedPoolCapacityHoursPerWeek=null);
public sealed record CapacityOverviewDto(Guid ProjectId,int HorizonWeeks,IReadOnlyList<TeamCapacityDto> Teams,IReadOnlyList<WorkstreamCapacityDto> Workstreams,IReadOnlyList<string> Bottlenecks,DateTimeOffset GeneratedAtUtc);

public sealed record ManagementScheduleItem(Guid NodeId,string Type,string Key,string Name,string WorkstreamKey,DateTimeOffset PlannedStartUtc,DateTimeOffset DueAtUtc,DateTimeOffset ForecastFinishUtc,int DurationDays,decimal CompletionPercent,string Status,int SlackDays,bool IsCritical,int BlockedByCount);
public sealed record ManagementScheduleDto(Guid ProjectId,string Status,DateTimeOffset? PlannedFinishUtc,DateTimeOffset? ForecastFinishUtc,int DelayDays,int CriticalItems,int AtRiskItems,IReadOnlyList<ManagementScheduleItem> Items,IReadOnlyList<string> CriticalPath,IReadOnlyList<string> Warnings,DateTimeOffset GeneratedAtUtc);

public sealed record BlockerImpactDto(string Key,string Title,string Reason,string Severity,string Status,int AgeDays,int DirectlyBlocked,int DownstreamAffected,IReadOnlyList<string> BlockedItems,IReadOnlyList<string> DownstreamItems,string ManagementAction);
public sealed record BlockerOverviewDto(Guid ProjectId,int Open,int Critical,int AgingOverFiveDays,IReadOnlyList<BlockerImpactDto> Items,DateTimeOffset GeneratedAtUtc);

public sealed record DependencyLinkDto(string SourceType,string SourceKey,string SourceName,string TargetType,string TargetKey,string TargetName,string Relation,bool Critical,bool CrossWorkstream);
public sealed record DependencyOverviewDto(Guid ProjectId,int Links,int CriticalLinks,int CrossWorkstreamLinks,bool HasCycle,IReadOnlyList<string> CycleNodes,IReadOnlyList<DependencyLinkDto> Dependencies,DateTimeOffset GeneratedAtUtc);

public sealed record RequirementDto(string Key,string Title,string Priority,string Status,string ScopeState,decimal CompletionPercent,string? AcceptanceSummary,string? ExternalReference);
public sealed record ScopeOverviewDto(Guid ProjectId,string? BaselineKey,int BaselineRequirements,int CurrentIncludedRequirements,int AddedAfterBaseline,int DeferredOrRemoved,decimal ScopeGrowthPercent,int IncompleteHighPriority,IReadOnlyList<RequirementDto> Requirements,IReadOnlyList<string> Warnings,DateTimeOffset GeneratedAtUtc);

public sealed record ManagementMilestoneDto(string Key,string Name,string State,DateTimeOffset PlannedStartUtc,DateTimeOffset DueAtUtc,decimal RecordedCompletionPercent,decimal LinkedTaskCompletionPercent,int LinkedTasks,int BlockedTasks,bool IsPastDue,string Readiness);
public sealed record ManagementMilestoneOverviewDto(Guid ProjectId,int Total,int AtRisk,int Blocked,int Completed,IReadOnlyList<ManagementMilestoneDto> Milestones,DateTimeOffset GeneratedAtUtc);

public sealed record ManagementHealthDimension(string Name,string Status,decimal Score,string Explanation);
public sealed record ManagementDashboardDto(Guid ProjectId,string OverallStatus,decimal HealthScore,IReadOnlyList<ManagementHealthDimension> Dimensions,CapacityOverviewDto Capacity,ManagementScheduleDto Schedule,BlockerOverviewDto Blockers,DependencyOverviewDto Dependencies,ManagementMilestoneOverviewDto Milestones,ScopeOverviewDto Scope,RiskRegisterDto Risks,DecisionActionOverviewDto Decisions,ResourceAllocationOverviewDto Resources,ScopeChangeOverviewDto ScopeChanges,EscalationOverviewDto Alerts,ManagementSummaryDto Summary,IReadOnlyList<string> ActionsRequired,DateTimeOffset GeneratedAtUtc);



// Management decision-cycle contracts. These are intentionally role/workstream oriented;
// they do not calculate individual productivity scores.
public sealed record ManagementReference(string Type,string Key);

public sealed record UpsertRiskCommand(
    Guid ProjectId,string Key,string Title,string Description,decimal ProbabilityPercent,decimal ImpactPercent,
    string Status="Open",string Category="Delivery",string? OwnerRole=null,DateTimeOffset? ReviewDueUtc=null,
    string? Trigger=null,string? Mitigation=null,IReadOnlyList<ManagementReference>? AffectedItems=null,string? ExternalReference=null);

public sealed record UpsertManagementDecisionCommand(
    Guid ProjectId,string Key,string Title,string Decision,string Rationale,string Status="Proposed",
    string? DecisionOwnerRole=null,DateTimeOffset? DecisionDueUtc=null,DateTimeOffset? DecidedAtUtc=null,
    IReadOnlyList<ManagementReference>? RelatedItems=null,string? ExternalReference=null);

public sealed record UpsertActionItemCommand(
    Guid ProjectId,string Key,string Title,string Status="Open",string Priority="Medium",string? OwnerRole=null,
    DateTimeOffset? DueAtUtc=null,decimal CompletionPercent=0m,string? DecisionKey=null,
    ManagementReference? Target=null,string? ExternalReference=null);

public sealed record UpsertResourceAllocationCommand(
    Guid ProjectId,string Key,string ToWorkstreamKey,decimal CapacityHoursPerWeek,DateTimeOffset StartUtc,DateTimeOffset EndUtc,
    string Status="Proposed",string? FromWorkstreamKey=null,string? Rationale=null,string? DecisionKey=null);

public sealed record UpsertChangeRequestCommand(
    Guid ProjectId,string Key,string Title,string Description,string Status="Proposed",string Priority="Medium",
    string? RequestedByRole=null,DateTimeOffset? RequestedAtUtc=null,decimal EngineeringHoursDelta=0m,decimal ArtHoursDelta=0m,
    decimal QaHoursDelta=0m,int ScheduleDaysDelta=0,decimal RiskDeltaPercent=0m,
    IReadOnlyList<string>? RequirementKeys=null,IReadOnlyList<string>? WorkstreamKeys=null,string? DecisionKey=null,string? ExternalReference=null);

public sealed record RiskRegisterItemDto(
    string Key,string Title,string Description,string Status,string Category,decimal ProbabilityPercent,decimal ImpactPercent,
    decimal ExposureScore,string ExposureLevel,string? OwnerRole,DateTimeOffset? ReviewDueUtc,bool ReviewOverdue,string? Trigger,string? Mitigation,
    IReadOnlyList<string> AffectedItems,string ManagementAction);
public sealed record RiskRegisterDto(Guid ProjectId,int Open,int Critical,int ReviewOverdue,decimal TotalExposure,IReadOnlyList<RiskRegisterItemDto> Items,DateTimeOffset GeneratedAtUtc);

public sealed record ManagementDecisionDto(string Key,string Title,string Decision,string Rationale,string Status,string? DecisionOwnerRole,DateTimeOffset? DecisionDueUtc,DateTimeOffset? DecidedAtUtc,int OpenActions,IReadOnlyList<string> RelatedItems);
public sealed record ManagementActionDto(string Key,string Title,string Status,string Priority,string? OwnerRole,DateTimeOffset? DueAtUtc,bool Overdue,decimal CompletionPercent,string? DecisionKey,string? Target,string? TargetType=null,string? TargetKey=null);
public sealed record DecisionActionOverviewDto(Guid ProjectId,int DecisionsPending,int OpenActions,int OverdueActions,IReadOnlyList<ManagementDecisionDto> Decisions,IReadOnlyList<ManagementActionDto> Actions,DateTimeOffset GeneratedAtUtc);

public sealed record ResourceAllocationDto(string Key,string Status,string? FromWorkstreamKey,string ToWorkstreamKey,decimal CapacityHoursPerWeek,DateTimeOffset StartUtc,DateTimeOffset EndUtc,string? Rationale,string? DecisionKey);
public sealed record ResourceAllocationRecommendationDto(string WorkstreamKey,string WorkstreamName,string TeamName,decimal CurrentUtilizationPercent,decimal SuggestedAdditionalHoursPerWeek,string Reason);
public sealed record ResourceAllocationOverviewDto(Guid ProjectId,IReadOnlyList<ResourceAllocationDto> Allocations,IReadOnlyList<ResourceAllocationRecommendationDto> Recommendations,DateTimeOffset GeneratedAtUtc);

public sealed record ChangeRequestDto(string Key,string Title,string Description,string Status,string Priority,string? RequestedByRole,DateTimeOffset RequestedAtUtc,decimal EngineeringHoursDelta,decimal ArtHoursDelta,decimal QaHoursDelta,int ScheduleDaysDelta,decimal RiskDeltaPercent,IReadOnlyList<string> RequirementKeys,IReadOnlyList<string> WorkstreamKeys,string? DecisionKey,string ManagementImpact);
public sealed record ScopeChangeOverviewDto(Guid ProjectId,int Proposed,int Approved,int Rejected,decimal PendingEngineeringHours,decimal PendingArtHours,decimal PendingQaHours,int PendingScheduleDays,decimal PendingRiskDeltaPercent,IReadOnlyList<ChangeRequestDto> Changes,DateTimeOffset GeneratedAtUtc);

public sealed record ManagementAlertDto(string Key,string Severity,string Category,string Title,string Explanation,string RecommendedAction,DateTimeOffset TriggeredAtUtc,DateTimeOffset? DueAtUtc,string SourceKey);
public sealed record EscalationOverviewDto(Guid ProjectId,int Critical,int High,int Total,IReadOnlyList<ManagementAlertDto> Alerts,DateTimeOffset GeneratedAtUtc);

public sealed record ManagementSummaryDto(string OverallStatus,decimal HealthScore,string PrimaryConcern,IReadOnlyList<string> KeyDrivers,IReadOnlyList<string> DecisionsRequired,IReadOnlyList<string> RecommendedActions,string PlainLanguageSummary);

public sealed record SpreadsheetSheetDto(string Name,IReadOnlyList<string> Headers,IReadOnlyList<IReadOnlyList<string>> Rows,bool Editable=false);
public sealed record ManagementWorkbookDto(Guid ProjectId,IReadOnlyList<SpreadsheetSheetDto> Sheets,DateTimeOffset GeneratedAtUtc);
public sealed record SpreadsheetImportResult(int Teams,int Workstreams,int Tasks,int Milestones,int Blockers,int Requirements,int Risks,int Decisions,int Actions,int ChangeRequests,int ResourceAllocations,IReadOnlyList<string> Warnings);
public sealed record GoogleSheetsSyncCommand(Guid ProjectId,string SpreadsheetId,string AccessToken,bool PullEditableSheets=true,bool PushCalculatedSheets=true,bool DryRun=false);
public sealed record GoogleSheetsSyncResult(int PulledSheets,int PushedSheets,SpreadsheetImportResult? Imported,IReadOnlyList<string> Warnings,DateTimeOffset SyncedAtUtc,int Conflicts=0,bool DryRun=false);
