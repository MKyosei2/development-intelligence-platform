using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

public sealed record UpsertTeamCommand(Guid ProjectId,string Key,string Name,decimal CapacityHoursPerWeek,decimal BufferPercent=10m,string Status="Active",string? ExternalReference=null);
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
public sealed record TeamCapacityDto(string Key,string Name,decimal CapacityHours,decimal PlannedHours,decimal RemainingHours,decimal UtilizationPercent,int Workstreams,int ActiveTasks,int BlockedTasks,string Status,IReadOnlyList<WorkloadBucket> Weekly);
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
public sealed record ManagementDashboardDto(Guid ProjectId,string OverallStatus,decimal HealthScore,IReadOnlyList<ManagementHealthDimension> Dimensions,CapacityOverviewDto Capacity,ManagementScheduleDto Schedule,BlockerOverviewDto Blockers,DependencyOverviewDto Dependencies,ManagementMilestoneOverviewDto Milestones,ScopeOverviewDto Scope,IReadOnlyList<string> ActionsRequired,DateTimeOffset GeneratedAtUtc);

public sealed record SpreadsheetSheetDto(string Name,IReadOnlyList<string> Headers,IReadOnlyList<IReadOnlyList<string>> Rows,bool Editable=false);
public sealed record ManagementWorkbookDto(Guid ProjectId,IReadOnlyList<SpreadsheetSheetDto> Sheets,DateTimeOffset GeneratedAtUtc);
public sealed record SpreadsheetImportResult(int Teams,int Workstreams,int Tasks,int Milestones,int Blockers,int Requirements,IReadOnlyList<string> Warnings);
public sealed record GoogleSheetsSyncCommand(Guid ProjectId,string SpreadsheetId,string AccessToken,bool PullEditableSheets=true,bool PushCalculatedSheets=true);
public sealed record GoogleSheetsSyncResult(int PulledSheets,int PushedSheets,SpreadsheetImportResult? Imported,IReadOnlyList<string> Warnings,DateTimeOffset SyncedAtUtc);
