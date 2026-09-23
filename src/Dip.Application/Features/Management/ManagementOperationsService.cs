// Purpose: Provides the stable management-operations facade while focused handlers own role, approval, alert, sync-conflict, and archival behavior.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

public sealed class ManagementOperationsService
{
    private readonly IDevelopmentGraphRepository _repo;
    private readonly ISystemClock _clock;
    private readonly ManagementResponsibilityHandler _responsibilityHandler;
    private readonly ManagementResourceHandler _resourceHandler;
    private readonly ManagementPlanningHandler _planningHandler;
    private readonly ManagementRoleBindingHandler _roleBindings;
    private readonly ManagementApprovalHandler _approvals;
    private readonly ManagementAlertHandler _alerts;
    private readonly ManagementSyncConflictHandler _sync;
    private readonly ManagementArchiveHandler _archive;

    public ManagementOperationsService(
        DevelopmentGraphService graph,
        IDevelopmentGraphRepository repo,
        ManagementAnalyticsService analytics,
        ManagementDashboardService dashboard,
        IAuditLogRepository audit,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _repo = repo;
        _clock = clock;
        _responsibilityHandler = new(graph, repo);
        _resourceHandler = new(graph, repo);
        _planningHandler = new(graph, repo);
        _roleBindings = new(graph, repo, analytics, dashboard, audit, uow, clock);
        _approvals = new(graph, repo, analytics, dashboard, audit, uow, clock);
        _alerts = new(graph, repo, analytics, dashboard, audit, uow, clock, _approvals);
        _sync = new(graph, repo, analytics, dashboard, audit, uow, clock);
        _archive = new(graph, repo, analytics, dashboard, audit, uow, clock);
    }

    public Task<DevelopmentGraphNodeDto> UpsertResponsibilityAsync(UpsertResponsibilityCommand command, CancellationToken ct) => _responsibilityHandler.UpsertAsync(command, ct);

    public async Task<RaciOverviewDto> GetRaciAsync(Guid projectId, CancellationToken ct)
    {

        NeedProject(projectId);
        var nodes = await _repo.ListNodesAsync(projectId, DevelopmentGraphNodeType.ResponsibilityAssignment, ct);

        var items = nodes.Where(x => !x.Status.Equals("Archived",
            StringComparison.OrdinalIgnoreCase)).Select(ParseRaci).OrderBy(x => x.TargetType).ThenBy(x => x.TargetKey).ToArray();

        return new(projectId, items.Length, items.Count(x => string.IsNullOrWhiteSpace(x.AccountableRole)), items, _clock.UtcNow);

    }

    public Task<DevelopmentGraphNodeDto> UpsertManagementRoleBindingAsync(UpsertManagementRoleBindingCommand c, CancellationToken ct) => _roleBindings.UpsertManagementRoleBindingAsync(c, ct);
    public Task<ManagementRoleDirectoryDto> GetManagementRoleDirectoryAsync(Guid projectId, string currentActor, CancellationToken ct) => _roleBindings.GetManagementRoleDirectoryAsync(projectId, currentActor, ct);
    public Task<DevelopmentGraphNodeDto> UpsertSharedResourceMembershipAsync(UpsertSharedResourceMembershipCommand command, CancellationToken ct) => _resourceHandler.UpsertSharedMembershipAsync(command, ct);
    public Task<DevelopmentGraphNodeDto> UpsertWorkCalendarAsync(UpsertWorkCalendarCommand command, CancellationToken ct) => _planningHandler.UpsertWorkCalendarAsync(command, ct);

    public async Task<WorkCalendarOverviewDto> GetCalendarsAsync(Guid projectId, CancellationToken ct)
    {

        NeedProject(projectId);
        var nodes = await _repo.ListNodesAsync(projectId, DevelopmentGraphNodeType.WorkCalendar, ct);
        var items = nodes.Where(x => !x.Status.Equals("Archived",
            StringComparison.OrdinalIgnoreCase)).Select(ParseCalendar).OrderBy(x => x.TeamKey ?? string.Empty).ThenBy(x => x.Name).ToArray();
        return new(projectId, items, _clock.UtcNow);

    }

    public Task<DevelopmentGraphNodeDto> CreateApprovalAsync(CreateApprovalRequestCommand command, CancellationToken ct) => _approvals.CreateApprovalAsync(command, ct);
    public Task<ApprovalRequestDto> RespondApprovalAsync(ApprovalResponseCommand c, CancellationToken ct) => _approvals.RespondApprovalAsync(c, ct);
    public Task<ApprovalOverviewDto> GetApprovalsAsync(Guid projectId, CancellationToken ct) => _approvals.GetApprovalsAsync(projectId, ct);
    public Task<AlertStateDto> SetAlertDispositionAsync(AlertDispositionCommand c, CancellationToken ct) => _alerts.SetAlertDispositionAsync(c, ct);
    public Task<IReadOnlyList<NotificationHistoryEntryDto>> GetAlertHistoryAsync(Guid projectId, string alertKey, int take, CancellationToken ct) => _alerts.GetAlertHistoryAsync(projectId, alertKey, take, ct);
    public Task<IReadOnlyList<OperationalAlertDto>> GetOperationalAlertsAsync(Guid projectId, int horizonWeeks, CancellationToken ct) => _alerts.GetOperationalAlertsAsync(projectId, horizonWeeks, ct);
    public Task<DailyCockpitDto> GetDailyCockpitAsync(Guid projectId, int horizonWeeks, string? ownerRole, CancellationToken ct) => _alerts.GetDailyCockpitAsync(projectId, horizonWeeks, ownerRole, ct);
    public Task<OperatingCadenceDto> GetCadenceAsync(Guid projectId, string mode, int horizonWeeks, CancellationToken ct) => _alerts.GetCadenceAsync(projectId, mode, horizonWeeks, ct);
    public Task<DevelopmentGraphNodeDto> UpsertSourceAuthorityAsync(SourceAuthorityRuleCommand c, CancellationToken ct) => _sync.UpsertSourceAuthorityAsync(c, ct);
    public Task<SyncStateOverviewDto> GetSyncStateAsync(Guid projectId, CancellationToken ct) => _sync.GetSyncStateAsync(projectId, ct);
    public Task<SyncConflictDto> ResolveConflictAsync(ResolveSyncConflictCommand c, CancellationToken ct) => _sync.ResolveConflictAsync(c, ct);
    public Task ArchiveAsync(ArchiveManagementItemCommand c, CancellationToken ct) => _archive.ArchiveAsync(c, ct);
    public Task<DevelopmentGraphNodeDto> RecordAlertDeliveryAsync(Guid projectId, ManagementAlertDto alert, int level, DateTimeOffset deliveredAt, CancellationToken ct) => _alerts.RecordAlertDeliveryAsync(projectId, alert, level, deliveredAt, ct);
    internal Task<DevelopmentGraphNodeDto> RecordSyncConflictAsync(Guid projectId, string provider, string dataType, string itemKey, string dipValue, string externalValue, CancellationToken ct) => _sync.RecordSyncConflictAsync(projectId, provider, dataType, itemKey, dipValue, externalValue, ct);
    internal Task MarkConflictAppliedAsync(Guid projectId, string conflictKey, CancellationToken ct) => _sync.MarkConflictAppliedAsync(projectId, conflictKey, ct);
    internal Task<SourceAuthorityRuleDto?> AuthorityForAsync(Guid projectId, string dataType, CancellationToken ct) => _sync.AuthorityForAsync(projectId, dataType, ct);

    private ResponsibilityDto ParseRaci(DevelopmentGraphNode n) => new(WorkManagementService.MetaString(n, "Key", WorkManagementService.Strip(n.Key)),
            WorkManagementService.MetaString(n, "TargetType"), WorkManagementService.MetaString(n, "TargetKey"), Null(WorkManagementService.MetaString(n,
            "ResponsibleRole")), Null(WorkManagementService.MetaString(n, "AccountableRole")), WorkManagementService.MetaStringArray(n, "ConsultedRoles"),
            WorkManagementService.MetaStringArray(n, "InformedRoles"), n.Status);

    private WorkCalendarDto ParseCalendar(DevelopmentGraphNode n)
    {

        using var d = JsonDocument.Parse(n.MetadataJson);
        var r = d.RootElement;
        var days = new List<DayOfWeek>();
        if (r.TryGetProperty("WorkingDays", out var wd) && wd.ValueKind == JsonValueKind.Array) foreach (var x in wd.EnumerateArray())
            {
                if (x.ValueKind == JsonValueKind.Number && x.TryGetInt32(out var i) && Enum.IsDefined(typeof(DayOfWeek), i)) days.Add((DayOfWeek)i);
                else if (x.ValueKind == JsonValueKind.String && Enum.TryParse<DayOfWeek>(x.GetString(), true, out var day)) days.Add(day);
            }
        var exceptions = new List<CapacityExceptionInput>();
        if (r.TryGetProperty("Exceptions", out var ex) && ex.ValueKind == JsonValueKind.Array) foreach (var x in ex.EnumerateArray())
            {
                if (!x.TryGetProperty("StartsAtUtc", out var s) || !s.TryGetDateTimeOffset(out var start) || !x.TryGetProperty("EndsAtUtc",
                    out var e) || !e.TryGetDateTimeOffset(out var end)) continue;
                var hours = x.TryGetProperty("Hours", out var h) && h.TryGetDecimal(out var hv) ? hv : 0m;
                var cat = x.TryGetProperty("Category", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "Other" : "Other";
                var label = x.TryGetProperty("Label", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
                exceptions.Add(new(start, end, hours, cat, label));
            }
        return new(WorkManagementService.MetaString(n, "Key", WorkManagementService.Strip(n.Key)), n.Name, Null(WorkManagementService.MetaString(n,
            "TeamKey")), WorkManagementService.MetaDecimal(n, "HoursPerDay", 8m), days.Count > 0 ? days : [DayOfWeek.Monday, DayOfWeek.Tuesday,
                DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], WorkManagementService.MetaDecimal(n, "RecurringMeetingHoursPerWeek"),
            WorkManagementService.MetaDecimal(n, "SupportDutyHoursPerWeek"), exceptions, WorkManagementService.MetaString(n, "TimeZone", "UTC"), n.Status);

    }

    internal static (DevelopmentGraphNodeType Type, string Key) ResolveManagementType(string type, string key)
    {

        NeedKey(key);
        return (type ?? string.Empty).Trim().ToLowerInvariant() switch
        {

            "task" => (DevelopmentGraphNodeType.Task, $"task:{key}"),
            "workstream" => (DevelopmentGraphNodeType.Workstream, $"workstream:{key}"),
            "team" => (DevelopmentGraphNodeType.Team, $"team:{key}"),
            "milestone" => (DevelopmentGraphNodeType.Milestone, $"milestone:{key}"),
            "requirement" => (DevelopmentGraphNodeType.Requirement, $"requirement:{key}"),
            "feature" => (DevelopmentGraphNodeType.Feature,
            $"feature:{key}"),
            "blocker" => (DevelopmentGraphNodeType.Blocker, $"blocker:{key}"),
            "risk" => (DevelopmentGraphNodeType.Risk, $"risk:{key}"),
            "decision" => (DevelopmentGraphNodeType.ManagementDecision, $"decision:{key}"),
            "action" => (DevelopmentGraphNodeType.ActionItem,
            $"action:{key}"),
            "change" or "changerequest" => (DevelopmentGraphNodeType.ChangeRequest, $"change:{key}"),
            "allocation" or "resourceallocation" => (DevelopmentGraphNodeType.ResourceAllocation, $"allocation:{key}"),
            "release" => (DevelopmentGraphNodeType.Release, key.StartsWith("release:", StringComparison.OrdinalIgnoreCase) ? key : $"release:{key}"),
            "raci" => (DevelopmentGraphNodeType.ResponsibilityAssignment, $"raci:{key}"),
            "calendar" => (DevelopmentGraphNodeType.WorkCalendar,
            $"calendar:{key}"),
            "approval" => (DevelopmentGraphNodeType.ApprovalRequest, $"approval:{key}"),
            _ => throw new ArgumentException($"Unsupported management target type '{type}'.", nameof(type))
        };

    }

    public static int RequiredEscalation(ManagementAlertDto alert, DateTimeOffset now, DateTimeOffset firstSeen)
    {
        var age = now - (firstSeen < alert.TriggeredAtUtc ? firstSeen : alert.TriggeredAtUtc);
        if (alert.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return age >= TimeSpan.FromHours(2) ? 3 : age >= TimeSpan.FromMinutes(30) ? 2 : 1;
        if (alert.Severity.Equals("High", StringComparison.OrdinalIgnoreCase)) return age >= TimeSpan.FromDays(1) ? 3 : age >= TimeSpan.FromHours(2) ? 2 : 1;
        return age >= TimeSpan.FromDays(2) ? 2 : 1;
    }

    private static void NeedProject(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(id));
    }

    private static void NeedKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > 240) throw new ArgumentException("Key is required and must be at most 240 characters.",
            nameof(key));
    }

    private static string? Null(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
