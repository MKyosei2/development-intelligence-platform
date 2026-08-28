namespace Dip.Application.Features.Management;

public sealed record ManagementTarget(string Type,string Key);
public sealed record ArchiveManagementItemCommand(Guid ProjectId,string Type,string Key,string Actor,string? Reason=null);

public sealed record UpsertResponsibilityCommand(
    Guid ProjectId,string Key,ManagementTarget Target,string? ResponsibleRole,string? AccountableRole,
    IReadOnlyList<string>? ConsultedRoles=null,IReadOnlyList<string>? InformedRoles=null,string Status="Active");
public sealed record ResponsibilityDto(
    string Key,string TargetType,string TargetKey,string? ResponsibleRole,string? AccountableRole,
    IReadOnlyList<string> ConsultedRoles,IReadOnlyList<string> InformedRoles,string Status);
public sealed record RaciOverviewDto(Guid ProjectId,int Assignments,int MissingAccountable,IReadOnlyList<ResponsibilityDto> Items,DateTimeOffset GeneratedAtUtc);

public sealed record UpsertManagementRoleBindingCommand(Guid ProjectId,string Key,string ActorIdentity,string Role,string? DisplayName=null,string Status="Active");
public sealed record ManagementRoleBindingDto(string Key,string ActorIdentity,string Role,string? DisplayName,string Status);
public sealed record ManagementRoleDirectoryDto(Guid ProjectId,string CurrentActor,IReadOnlyList<ManagementRoleBindingDto> Bindings,DateTimeOffset GeneratedAtUtc);

public sealed record UpsertSharedResourceMembershipCommand(Guid ProjectId,string TeamKey,string? SharedResourceKey,decimal? SharedPoolCapacityHoursPerWeek);

public sealed record CapacityExceptionInput(DateTimeOffset StartsAtUtc,DateTimeOffset EndsAtUtc,decimal Hours,string Category,string? Label=null);
public sealed record UpsertWorkCalendarCommand(
    Guid ProjectId,string Key,string Name,string? TeamKey,decimal HoursPerDay=8m,
    IReadOnlyList<DayOfWeek>? WorkingDays=null,decimal RecurringMeetingHoursPerWeek=0m,decimal SupportDutyHoursPerWeek=0m,
    IReadOnlyList<CapacityExceptionInput>? Exceptions=null,string TimeZone="UTC",string Status="Active");
public sealed record WorkCalendarDto(
    string Key,string Name,string? TeamKey,decimal HoursPerDay,IReadOnlyList<DayOfWeek> WorkingDays,
    decimal RecurringMeetingHoursPerWeek,decimal SupportDutyHoursPerWeek,IReadOnlyList<CapacityExceptionInput> Exceptions,
    string TimeZone,string Status);
public sealed record WorkCalendarOverviewDto(Guid ProjectId,IReadOnlyList<WorkCalendarDto> Calendars,DateTimeOffset GeneratedAtUtc);

public sealed record CreateApprovalRequestCommand(
    Guid ProjectId,string Key,string ApprovalType,string Title,ManagementTarget Target,IReadOnlyList<string> RequiredRoles,
    string RequestedBy,string? Rationale=null,DateTimeOffset? DueAtUtc=null,string Status="Pending",bool AllowSameActorMultipleRoles=false);
public sealed record ApprovalResponseCommand(Guid ProjectId,string Key,string Actor,string ActorRole,string Decision,string? Comment=null);
public sealed record ApprovalResponseDto(string Actor,string ActorRole,string Decision,string? Comment,DateTimeOffset RespondedAtUtc);
public sealed record ApprovalRequestDto(
    string Key,string ApprovalType,string Title,string TargetType,string TargetKey,string Status,string RequestedBy,string? Rationale,
    DateTimeOffset RequestedAtUtc,DateTimeOffset? DueAtUtc,IReadOnlyList<string> RequiredRoles,IReadOnlyList<ApprovalResponseDto> Responses,
    IReadOnlyList<string> PendingRoles,bool Overdue,bool AllowSameActorMultipleRoles=false);
public sealed record ApprovalOverviewDto(Guid ProjectId,int Pending,int Approved,int Rejected,int Overdue,IReadOnlyList<ApprovalRequestDto> Items,DateTimeOffset GeneratedAtUtc);

public sealed record AlertDispositionCommand(Guid ProjectId,string AlertKey,string Actor,string Disposition,DateTimeOffset? SnoozeUntilUtc=null,string? Comment=null);
public sealed record AlertStateDto(string AlertKey,string Status,string? Actor,string? Comment,DateTimeOffset UpdatedAtUtc,DateTimeOffset? SnoozeUntilUtc,int EscalationLevel,DateTimeOffset? LastDeliveredAtUtc,DateTimeOffset? FirstSeenAtUtc=null,DateTimeOffset? ViewedAtUtc=null,string? ViewedBy=null);
public sealed record OperationalAlertDto(ManagementAlertDto Alert,AlertStateDto State,bool Visible,bool RequiresEscalation,string RoutedRole);
public sealed record NotificationHistoryEntryDto(Guid Id,string AlertKey,string Action,string Actor,string? Comment,string? DetailJson,DateTimeOffset AtUtc);

public sealed record CockpitDecisionDto(string Type,string Key,string Title,string Severity,string Action,string? OwnerRole,DateTimeOffset? DueAtUtc);
public sealed record DailyCockpitDto(
    Guid ProjectId,string OverallStatus,decimal HealthScore,IReadOnlyList<CockpitDecisionDto> Now,
    IReadOnlyList<OperationalAlertDto> Alerts,IReadOnlyList<ManagementActionDto> MyOpenActions,
    IReadOnlyList<ManagementDecisionDto> PendingDecisions,IReadOnlyList<ApprovalRequestDto> PendingApprovals,
    IReadOnlyList<string> NextActions,DateTimeOffset GeneratedAtUtc);

public sealed record OperatingCadenceDto(string Mode,string Title,string Summary,IReadOnlyList<string> TalkingPoints,IReadOnlyList<CockpitDecisionDto> Decisions,IReadOnlyList<string> FollowUps,DateTimeOffset GeneratedAtUtc);

public sealed record SourceAuthorityRuleCommand(Guid ProjectId,string Key,string DataType,string Authority,string ConflictPolicy="Manual",int Priority=100,string Status="Active");
public sealed record SourceAuthorityRuleDto(string Key,string DataType,string Authority,string ConflictPolicy,int Priority,string Status);
public sealed record SyncConflictDto(string Key,string Provider,string DataType,string ItemKey,string DipValue,string ExternalValue,string Status,string Resolution,string? ResolvedBy,DateTimeOffset DetectedAtUtc,DateTimeOffset? ResolvedAtUtc);
public sealed record ResolveSyncConflictCommand(Guid ProjectId,string ConflictKey,string Resolution,string ResolvedBy);
public sealed record SyncStateOverviewDto(Guid ProjectId,IReadOnlyList<SourceAuthorityRuleDto> AuthorityRules,IReadOnlyList<SyncConflictDto> Conflicts,int OpenConflicts,DateTimeOffset GeneratedAtUtc);

public enum ManagementSyncDirection{Pull,Push,Bidirectional}

/// <summary>
/// Optional field-level rule used by spreadsheet/connector sync. The default
/// contract remains Action-level and backward compatible with existing callers.
/// </summary>
public sealed record ManagementSyncFieldRule(string Field,string Authority="Manual",string ConflictPolicy="Manual");

public sealed record ManagementSyncCommand(
    Guid ProjectId,string Provider,ManagementSyncDirection Direction=ManagementSyncDirection.Bidirectional,bool DryRun=false,
    string DataType="Action",IReadOnlyList<ManagementSyncFieldRule>? FieldRules=null,bool RequireRevisionMatch=true);

public sealed record ManagementExternalItem(
    string Provider,string ExternalId,string Key,string Title,string Status,string? OwnerRole,DateTimeOffset? DueAtUtc,
    DateTimeOffset UpdatedAtUtc,string Revision,string Url,IReadOnlyDictionary<string,string>? Fields=null);

public sealed record ManagementSyncChange(
    string Operation,string Key,string Provider,string? ExternalId,string Reason,string? DipValue,string? ExternalValue,string? Field=null);

public sealed record ManagementSyncResult(Guid ProjectId,string Provider,int Pulled,int Pushed,int Conflicts,bool DryRun,IReadOnlyList<ManagementSyncChange> Changes,DateTimeOffset SyncedAtUtc);
public interface IManagementSyncAdapter
{
    string ProviderName{get;}
    Task<IReadOnlyList<ManagementExternalItem>> PullAsync(Guid projectId,CancellationToken ct);
    Task<ManagementExternalItem> UpsertAsync(Guid projectId,ManagementActionDto action,string? externalId,CancellationToken ct);
}

public sealed record PortfolioProjectDto(Guid ProjectId,string Name,string OverallStatus,decimal HealthScore,string ScheduleStatus,int DelayDays,decimal PeakUtilizationPercent,int CriticalRisks,int OpenBlockers,int PendingDecisions,int OverdueActions,int PendingApprovals,int OpenSyncConflicts);
public sealed record SharedCapacityConflictDto(string TeamName,decimal AvailableHoursPerWeek,decimal PlannedHoursPerWeek,decimal ShortageHoursPerWeek,IReadOnlyList<string> Projects,string Recommendation);
public sealed record PortfolioManagementDto(int Projects,int AtRisk,int Critical,IReadOnlyList<PortfolioProjectDto> Items,IReadOnlyList<SharedCapacityConflictDto> SharedCapacityConflicts,IReadOnlyList<string> ExecutiveActions,DateTimeOffset GeneratedAtUtc);

public sealed record ManagementNotificationEnvelope(Guid ProjectId,string ProjectName,OperationalAlertDto OperationalAlert,int EscalationLevel,string Subject,string Body);
public interface IManagementNotificationChannel
{
    string ChannelName{get;}
    bool IsConfigured{get;}
    Task DeliverAsync(ManagementNotificationEnvelope message,CancellationToken ct);
}
