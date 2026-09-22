// Purpose: Owns operational alert state, escalation, routing, delivery audit, cockpit, and cadence behavior.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

internal sealed class ManagementAlertHandler
{
    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly ManagementAnalyticsService _analytics;
    private readonly ManagementDashboardService _dashboard;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public ManagementAlertHandler(DevelopmentGraphService graph, IDevelopmentGraphRepository repo, ManagementAnalyticsService analytics, ManagementDashboardService dashboard, IAuditLogRepository audit, IUnitOfWork uow, ISystemClock clock)
    {
        _graph = graph;
        _repo = repo;
        _analytics = analytics;
        _dashboard = dashboard;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<AlertStateDto> SetAlertDispositionAsync(AlertDispositionCommand c, CancellationToken ct)
        {
    
            NeedProject(c.ProjectId);
    
            if(string.IsNullOrWhiteSpace(c.AlertKey))throw new ArgumentException("AlertKey is required.", nameof(c));
    
            var requested=c.Disposition.Trim().ToLowerInvariant() switch{
    
                "read" or "view" or "viewed"=>"Read",
                "ack" or "acknowledge" or "acknowledged"=>"Acknowledged",
                "snooze" or "snoozed"=>"Snoozed",
                "resolve" or "resolved"=>"Resolved",
                "reopen" or "open"=>"Open",
                _=>throw new ArgumentException("Disposition must be read, acknowledge, snooze, resolve, or reopen.", nameof(c))
            };
    
            var key=AlertStateKey(c.AlertKey);
    
            var existing=await _repo.FindNodeAsync(c.ProjectId, DevelopmentGraphNodeType.AlertState, key, ct);
    
            var currentAlert=(await _analytics.GetAlertsAsync(c.ProjectId, 8, ct)).Alerts.FirstOrDefault(x=>x.Key.Equals(c.AlertKey,
                StringComparison.OrdinalIgnoreCase));
    
            var current=existing is null
            ?new AlertStateDto(c.AlertKey, "Open", null, null, currentAlert?.TriggeredAtUtc??_clock.UtcNow, null, 0, null,
                currentAlert?.TriggeredAtUtc??_clock.UtcNow)
            :ParseAlertState(existing);
    
            var disposition=requested=="Read"?current.Status:requested;
    
            if(requested=="Snoozed"&&(!c.SnoozeUntilUtc.HasValue||c.SnoozeUntilUtc<=_clock.UtcNow))
            throw new ArgumentException("SnoozeUntilUtc must be in the future.", nameof(c));
    
            var now=_clock.UtcNow;
    
            var level=current.EscalationLevel;
    
            var firstSeen=current.FirstSeenAtUtc??currentAlert?.TriggeredAtUtc??now;
    
            if(disposition=="Acknowledged"&&currentAlert is not null)
            level=Math.Max(level, RequiredEscalation(currentAlert, now, firstSeen));
    
            var actor=requested=="Read"?current.Actor:Clean(c.Actor, 200);
    
            var comment=requested=="Read"?current.Comment:Clean(c.Comment, 2000);
    
            var viewedAt=current.ViewedAtUtc??now;
    
            var viewedBy=current.ViewedBy??Clean(c.Actor, 200);
    
            var snooze=requested=="Read"?current.SnoozeUntilUtc:disposition=="Snoozed"?c.SnoozeUntilUtc:null;
    
            var meta=JsonSerializer.Serialize(new{
    
                AlertKey=c.AlertKey,
                Actor=actor,
                Comment=comment,
                UpdatedAtUtc=now,
                SnoozeUntilUtc=snooze,
                EscalationLevel=level,
                LastDeliveredAtUtc=current.LastDeliveredAtUtc,
                FirstSeenAtUtc=firstSeen,
                ViewedAtUtc=viewedAt,
                ViewedBy=viewedBy,
                AlertSeverity=currentAlert?.Severity??(existing is null?"Info":WorkManagementService.MetaString(existing, "AlertSeverity", "Info")),
                AlertCategory=currentAlert?.Category??(existing is null?"History":WorkManagementService.MetaString(existing, "AlertCategory", "History")),
                AlertTitle=currentAlert?.Title??(existing is null?c.AlertKey:WorkManagementService.MetaString(existing, "AlertTitle", c.AlertKey)),
                AlertExplanation=currentAlert?.Explanation??(existing is null?"Historical alert state.":WorkManagementService.MetaString(existing,
                    "AlertExplanation", "Historical alert state.")),
                AlertRecommendedAction=currentAlert?.RecommendedAction??(existing is null?"Review the recorded alert state.":WorkManagementService
                    .MetaString(existing, "AlertRecommendedAction", "Review the recorded alert state.")),
                AlertDueAtUtc=currentAlert?.DueAtUtc??(existing is null?null:ReadNullableDate(existing, "AlertDueAtUtc")),
                AlertSourceKey=currentAlert?.SourceKey??(existing is null?c.AlertKey:WorkManagementService.MetaString(existing, "AlertSourceKey", c.AlertKey))
            });
    
            await _graph.UpsertNodeAsync(new(c.ProjectId, DevelopmentGraphNodeType.AlertState, key, c.AlertKey, disposition, "DIP", null, meta), ct);
    
            if(requested!="Read"||current.ViewedAtUtc is null)
            {
    
                var action=requested switch{
    
                    "Read"=>"notification.viewed",
                    "Acknowledged"=>"notification.acknowledged",
                    "Snoozed"=>"notification.snoozed",
                    "Resolved"=>"notification.resolved",
                    "Open"=>"notification.reopened",
                    _=>"notification.updated"
                };
    
                await RecordNotificationAuditAsync(
                c.ProjectId,
                c.AlertKey,
                action,
                Clean(c.Actor, 200)??"unknown",
                Clean(c.Comment, 2000),
                JsonSerializer.Serialize(new{
                    PreviousStatus=current.Status, CurrentStatus=disposition, SnoozeUntilUtc=snooze, ViewedAtUtc=viewedAt, ViewedBy=viewedBy,
                        EscalationLevel=level
                }),
                now,
                ct);
    
            }
    
            var persisted=await _repo.FindNodeAsync(c.ProjectId, DevelopmentGraphNodeType.AlertState, key, ct)
            ??throw new InvalidOperationException("Persisted alert state could not be reloaded.");
    
            return ParseAlertState(persisted);
    
        }
    
    public async Task<IReadOnlyList<NotificationHistoryEntryDto>> GetAlertHistoryAsync(Guid projectId, string alertKey, int take, CancellationToken ct)
        {
    
            NeedProject(projectId);
    
            if(string.IsNullOrWhiteSpace(alertKey))throw new ArgumentException("AlertKey is required.", nameof(alertKey));
    
            var entries=await _audit.SearchAsync(projectId, alertKey, "notification", null, null, null, Math.Clamp(take, 1, 200), ct);
    
            return entries
            .Where(x=>string.Equals(x.SubjectKey, alertKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x=>x.CreatedAtUtc)
            .ThenByDescending(x=>x.Id)
            .Select(x=>new NotificationHistoryEntryDto(x.Id, alertKey, x.Action, x.Actor, x.Reason, x.DetailJson, x.CreatedAtUtc))
            .ToArray();
    
        }
    
    public async Task<IReadOnlyList<OperationalAlertDto>> GetOperationalAlertsAsync(Guid projectId, int horizonWeeks, CancellationToken ct)
        {
    
            var alerts=(await _analytics.GetAlertsAsync(projectId, horizonWeeks, ct)).Alerts;
            var allNodes=await _repo.ListNodesAsync(projectId, null, ct);
            var states=allNodes.Where(x=>x.Type==DevelopmentGraphNodeType.AlertState).ToDictionary(x=>WorkManagementService.MetaString(x, "AlertKey",
                WorkManagementService.Strip(x.Key)), StringComparer.OrdinalIgnoreCase);
            var raci=allNodes.Where(x=>x.Type==DevelopmentGraphNodeType.ResponsibilityAssignment&&!x.Status.Equals("Archived",
                StringComparison.OrdinalIgnoreCase)).Select(ParseRaci).ToArray();
            var now=_clock.UtcNow;
            var list=new List<OperationalAlertDto>();
    
            foreach(var alert in alerts){
                var state=states.TryGetValue(alert.Key, out var n)?ParseAlertState(n):new AlertStateDto(alert.Key, "Open", null, null, alert.TriggeredAtUtc,
                    null, 0, null, alert.TriggeredAtUtc);
                var visible=!state.Status.Equals("Resolved", StringComparison.OrdinalIgnoreCase)&&!(state.Status.Equals("Snoozed",
                    StringComparison.OrdinalIgnoreCase)&&state.SnoozeUntilUtc>now);
                var required=visible&&RequiredEscalation(alert, now, state.FirstSeenAtUtc??alert.TriggeredAtUtc)>state.EscalationLevel;
                list.Add(new(alert, state, visible, required, RouteRole(alert, raci, allNodes)));
            }
    
            var liveKeys=alerts.Select(x=>x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
    
            foreach(var node in states.Values.Where(x=>!liveKeys.Contains(WorkManagementService.MetaString(x, "AlertKey",
                WorkManagementService.Strip(x.Key)))))
            {
    
                var state=ParseAlertState(node);
                var title=WorkManagementService.MetaString(node, "AlertTitle");
                if(string.IsNullOrWhiteSpace(title))continue;
    
                var historical=new ManagementAlertDto(
                state.AlertKey,
                WorkManagementService.MetaString(node, "AlertSeverity", "Info"),
                WorkManagementService.MetaString(node, "AlertCategory", "History"),
                title,
                WorkManagementService.MetaString(node, "AlertExplanation", "Historical alert state."),
                WorkManagementService.MetaString(node, "AlertRecommendedAction", "Review the recorded alert state."),
                state.FirstSeenAtUtc??node.FirstObservedAtUtc,
                ReadNullableDate(node, "AlertDueAtUtc"),
                WorkManagementService.MetaString(node, "AlertSourceKey", state.AlertKey));
    
                list.Add(new(historical, state, false, false, RouteRole(historical, raci, allNodes)));
    
            }
    
            return list.OrderByDescending(x=>x.Visible).ThenByDescending(x=>SeverityRank(x.Alert.Severity)).ThenByDescending(x=>
                x.State.UpdatedAtUtc).ToArray();
    
        }
    
    public async Task<DailyCockpitDto> GetDailyCockpitAsync(Guid projectId, int horizonWeeks, string? ownerRole, CancellationToken ct)
        {
    
            var dashboard=await _dashboard.GetAsync(projectId, horizonWeeks, ct);
            var alerts=await GetOperationalAlertsAsync(projectId, horizonWeeks, ct);
            var approvals=await GetApprovalsAsync(projectId, ct);
            var decisions=dashboard.Decisions;
            var role=Null(ownerRole);
            var activeAlerts=alerts.Where(x=>x.Visible&&(role is null||x.RoutedRole.Equals(role, StringComparison.OrdinalIgnoreCase))).ToArray();
    
            var actions=decisions.Actions.Where(x=>!WorkManagementService.IsDone(x.Status)&&(role is null||string.Equals(x.OwnerRole, role,
                StringComparison.OrdinalIgnoreCase))).OrderBy(x=>x.DueAtUtc??DateTimeOffset.MaxValue).ToArray();
            var myDecisions=decisions.Decisions.Where(x=>x.Status is "Proposed" or "Pending").Where(x=>role is null||string.Equals(x.DecisionOwnerRole, role,
                StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.DecisionDueUtc??DateTimeOffset.MaxValue).ToArray();
            var myApprovals=approvals.Items.Where(x=>x.Status=="Pending").Where(x=>role is null||x.PendingRoles.Contains(role,
                StringComparer.OrdinalIgnoreCase)).OrderBy(x=>x.DueAtUtc??DateTimeOffset.MaxValue).ToArray();
    
            var now=new List<CockpitDecisionDto>();
            now.AddRange(activeAlerts.Take(8).Select(x=>new CockpitDecisionDto("Alert", x.Alert.Key, x.Alert.Title, x.Alert.Severity, x.Alert.RecommendedAction,
                x.RoutedRole, x.Alert.DueAtUtc)));
            now.AddRange(myApprovals.Take(5).Select(x=>new CockpitDecisionDto("Approval", x.Key, x.Title, x.Overdue?"Critical":"High",
                "Approve, reject, or request more evidence.", role??x.PendingRoles.FirstOrDefault(), x.DueAtUtc)));
            now.AddRange(myDecisions.Take(5).Select(x=>new CockpitDecisionDto("Decision", x.Key, x.Title, x.DecisionDueUtc<_clock.UtcNow?"Critical":"High",
                "Record an explicit decision with rationale.", x.DecisionOwnerRole, x.DecisionDueUtc)));
    
            return new(projectId, dashboard.OverallStatus, dashboard.HealthScore, now.OrderByDescending(x=>SeverityRank(x.Severity)).Take(15).ToArray(),
                activeAlerts, actions, myDecisions, myApprovals, dashboard.ActionsRequired.Take(12).ToArray(), _clock.UtcNow);
    
        }
    
    public async Task<OperatingCadenceDto> GetCadenceAsync(Guid projectId, string mode, int horizonWeeks, CancellationToken ct)
        {
    
            var cockpit=await GetDailyCockpitAsync(projectId, horizonWeeks, null, ct);
            var dashboard=await _dashboard.GetAsync(projectId, horizonWeeks, ct);
            var normalized=(mode??"daily").Trim().ToLowerInvariant();
    
            if(normalized=="daily")return new("Daily", "Daily operating review",
                $"{cockpit.Now.Count} item(s) need attention; {cockpit.MyOpenActions.Count} open action(s).",
                cockpit.Now.Take(8).Select(x=>$"[{x.Severity}] {x.Title}: {x.Action}").ToArray(), cockpit.Now.Take(8).ToArray(),
                cockpit.NextActions.Take(8).ToArray(), _clock.UtcNow);
    
            if(normalized=="weekly")
            {
    
                var talking=new List<string>{
                    $"Health {dashboard.HealthScore:0.#}/100 ({dashboard.OverallStatus})",
                        $"Schedule {dashboard.Schedule.Status}; forecast delay {dashboard.Schedule.DelayDays} day(s)",
                        $"Capacity: {dashboard.Capacity.Teams.Count(x=>x.Status is "OverCapacity" or "Blocked")} team(s) constrained",
                        $"Risk: {dashboard.Risks.Critical} critical, {dashboard.Risks.ReviewOverdue} review overdue",
                        $"Scope: {dashboard.ScopeChanges.Proposed} change request(s) proposed"
                };
    
                return new("Weekly", "Weekly project review", dashboard.Summary.PlainLanguageSummary, talking, cockpit.Now.Take(10).ToArray(),
                    dashboard.ActionsRequired.Take(10).ToArray(), _clock.UtcNow);
    
            }
    
            if(normalized=="release")
            {
    
                var decisions=new List<CockpitDecisionDto>(cockpit.Now);
                if(dashboard.Schedule.Status!="OnTrack")decisions.Add(new("Release", "schedule", "Schedule gate",
                    dashboard.Schedule.Status=="Blocked"?"Critical":"High", "Resolve or explicitly accept schedule risk before GO.", "PM",
                    dashboard.Schedule.PlannedFinishUtc));
                if(dashboard.Risks.Critical>0)decisions.Add(new("Release", "risk", "Risk gate", "Critical", "Resolve or formally accept critical release risk.",
                    "Producer", null));
    
                return new("Release", "Release decision review", $"Overall management gate: {dashboard.OverallStatus}.", new[]{
                    $"Schedule: {dashboard.Schedule.Status}", $"Open blockers: {dashboard.Blockers.Open}", $"Critical risks: {dashboard.Risks.Critical}",
                        $"Pending approvals: {cockpit.PendingApprovals.Count}"
                }, decisions.Take(12).ToArray(), dashboard.ActionsRequired.Take(10).ToArray(), _clock.UtcNow);
    
            }
    
            throw new ArgumentException("Mode must be daily, weekly, or release.", nameof(mode));
    
        }
    
    public async Task<DevelopmentGraphNodeDto> RecordAlertDeliveryAsync(Guid projectId, ManagementAlertDto alert, int level, DateTimeOffset deliveredAt,
            CancellationToken ct)
        {
    
            var key=AlertStateKey(alert.Key);
    
            var existing=await _repo.FindNodeAsync(projectId, DevelopmentGraphNodeType.AlertState, key, ct);
    
            var current=existing is null?new AlertStateDto(alert.Key, "Open", null, null, deliveredAt, null, 0, null,
                alert.TriggeredAtUtc):ParseAlertState(existing);
    
            var effectiveLevel=Math.Max(current.EscalationLevel, level);
    
            var meta=JsonSerializer.Serialize(new{
                AlertKey=alert.Key, current.Actor, current.Comment, UpdatedAtUtc=current.UpdatedAtUtc, current.SnoozeUntilUtc, EscalationLevel=effectiveLevel,
                    LastDeliveredAtUtc=deliveredAt, FirstSeenAtUtc=current.FirstSeenAtUtc??alert.TriggeredAtUtc, current.ViewedAtUtc, current.ViewedBy,
                    AlertSeverity=alert.Severity, AlertCategory=alert.Category, AlertTitle=alert.Title, AlertExplanation=alert.Explanation,
                    AlertRecommendedAction=alert.RecommendedAction, AlertDueAtUtc=alert.DueAtUtc, AlertSourceKey=alert.SourceKey
            });
    
            var node=await _graph.UpsertNodeAsync(new(projectId, DevelopmentGraphNodeType.AlertState, key, alert.Key, current.Status, "DIP", null, meta), ct);
    
            await RecordNotificationAuditAsync(
            projectId,
            alert.Key,
            "notification.delivered",
            "system:notification-worker",
            $"Delivered escalation level L{effectiveLevel}.",
            JsonSerializer.Serialize(new{
                Level=effectiveLevel, DeliveredAtUtc=deliveredAt, alert.Severity, alert.Category, alert.Title
            }),
            deliveredAt,
            ct);
    
            return node;
    
        }

    private async Task EnsureActorRoleBindingAsync(Guid projectId, string actor, string role, CancellationToken ct)
        {
    
            var nodes=await _repo.ListNodesAsync(projectId, DevelopmentGraphNodeType.ManagementRoleBinding, ct);
            var active=nodes.Where(x=>x.Status.Equals("Active", StringComparison.OrdinalIgnoreCase)).Select(ParseRoleBinding).ToArray();
    
            if (active.Length == 0)
            {
                throw new UnauthorizedAccessException(
                    "No active management role bindings are configured. A Project Owner must bind "
                    + "authenticated users to approval roles before formal approvals can be recorded.");
            }
    
            if(!active.Any(x=>x.ActorIdentity.Equals(actor, StringComparison.OrdinalIgnoreCase)&&x.Role.Equals(role,
                StringComparison.OrdinalIgnoreCase)))throw new UnauthorizedAccessException($"Actor '{actor}' is not bound to management role '{role}'.");
    
        }
    
    private async Task RecordNotificationAuditAsync(Guid projectId, string alertKey, string action, string actor, string? comment, string detailJson,
            DateTimeOffset at, CancellationToken ct)
        {
    
            await _audit.AddAsync(
            new AuditLog(
            Guid.NewGuid(),
            projectId,
            "notification",
            action,
            actor,
            alertKey,
            detailJson,
            at,
            reason:comment),
            ct);
    
            await _uow.SaveChangesAsync(ct);
    
        }
    
    private ResponsibilityDto ParseRaci(DevelopmentGraphNode n)=>new(WorkManagementService.MetaString(n, "Key", WorkManagementService.Strip(n.Key)),
            WorkManagementService.MetaString(n, "TargetType"), WorkManagementService.MetaString(n, "TargetKey"), Null(WorkManagementService.MetaString(n,
            "ResponsibleRole")), Null(WorkManagementService.MetaString(n, "AccountableRole")), WorkManagementService.MetaStringArray(n, "ConsultedRoles"),
            WorkManagementService.MetaStringArray(n, "InformedRoles"), n.Status);
    
    private ManagementRoleBindingDto ParseRoleBinding(DevelopmentGraphNode n)=>new(WorkManagementService.MetaString(n, "Key",
            WorkManagementService.Strip(n.Key)), WorkManagementService.MetaString(n, "ActorIdentity"), WorkManagementService.MetaString(n, "Role"),
            Null(WorkManagementService.MetaString(n, "DisplayName")), n.Status);
    
    private WorkCalendarDto ParseCalendar(DevelopmentGraphNode n)
        {
    
            using var d=JsonDocument.Parse(n.MetadataJson);
            var r=d.RootElement;
            var days=new List<DayOfWeek>();
            if(r.TryGetProperty("WorkingDays", out var wd)&&wd.ValueKind==JsonValueKind.Array)foreach(var x in wd.EnumerateArray()){
                if(x.ValueKind==JsonValueKind.Number&&x.TryGetInt32(out var i)&&Enum.IsDefined(typeof(DayOfWeek), i))days.Add((DayOfWeek)i);
                else if(x.ValueKind==JsonValueKind.String&&Enum.TryParse<DayOfWeek>(x.GetString(), true, out var day))days.Add(day);
            }
            var exceptions=new List<CapacityExceptionInput>();
            if(r.TryGetProperty("Exceptions", out var ex)&&ex.ValueKind==JsonValueKind.Array)foreach(var x in ex.EnumerateArray()){
                if(!x.TryGetProperty("StartsAtUtc", out var s)||!s.TryGetDateTimeOffset(out var start)||!x.TryGetProperty("EndsAtUtc",
                    out var e)||!e.TryGetDateTimeOffset(out var end))continue;
                var hours=x.TryGetProperty("Hours", out var h)&&h.TryGetDecimal(out var hv)?hv:0m;
                var cat=x.TryGetProperty("Category", out var c)&&c.ValueKind==JsonValueKind.String?c.GetString()??"Other":"Other";
                var label=x.TryGetProperty("Label", out var l)&&l.ValueKind==JsonValueKind.String?l.GetString():null;
                exceptions.Add(new(start, end, hours, cat, label));
            }
            return new(WorkManagementService.MetaString(n, "Key", WorkManagementService.Strip(n.Key)), n.Name, Null(WorkManagementService.MetaString(n,
                "TeamKey")), WorkManagementService.MetaDecimal(n, "HoursPerDay", 8m), days.Count>0?days:[DayOfWeek.Monday, DayOfWeek.Tuesday,
                DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], WorkManagementService.MetaDecimal(n, "RecurringMeetingHoursPerWeek"),
                WorkManagementService.MetaDecimal(n, "SupportDutyHoursPerWeek"), exceptions, WorkManagementService.MetaString(n, "TimeZone", "UTC"), n.Status);
    
        }
    
    private ApprovalRequestDto ParseApproval(DevelopmentGraphNode n)
        {
    
            using var d=JsonDocument.Parse(n.MetadataJson);
            var r=d.RootElement;
            var roles=WorkManagementService.MetaStringArray(n, "RequiredRoles");
            var responses=new List<ApprovalResponseDto>();
            if(r.TryGetProperty("Responses", out var a)&&a.ValueKind==JsonValueKind.Array)foreach(var x in a.EnumerateArray()){
                var actor=GetString(x, "Actor");
                var role=GetString(x, "ActorRole");
                var decision=GetString(x, "Decision");
                if(string.IsNullOrWhiteSpace(actor)||string.IsNullOrWhiteSpace(role)||string.IsNullOrWhiteSpace(decision))continue;
                DateTimeOffset when=_clock.UtcNow;
                if(x.TryGetProperty("RespondedAtUtc", out var at)&&at.TryGetDateTimeOffset(out var parsed))when=parsed;
                responses.Add(new(actor, role, decision, GetString(x, "Comment"), when));
            }
            var approved=responses.Where(x=>x.Decision=="Approved").Select(x=>x.ActorRole).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pending=roles.Where(x=>!approved.Contains(x)).ToArray();
            var requested=ReadDate(n, "RequestedAtUtc", n.FirstObservedAtUtc);
            var due=ReadNullableDate(n, "DueAtUtc");
            var allowSameActor=false;
            if(r.TryGetProperty("AllowSameActorMultipleRoles",
                out var allow)&&allow.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                allowSameActor=allow.GetBoolean();
            }
            return new(WorkManagementService.MetaString(n, "Key", WorkManagementService.Strip(n.Key)), WorkManagementService.MetaString(n, "ApprovalType"),
                n.Name, WorkManagementService.MetaString(n, "TargetType"), WorkManagementService.MetaString(n, "TargetKey"), n.Status,
                WorkManagementService.MetaString(n, "RequestedBy"), Null(WorkManagementService.MetaString(n, "Rationale")), requested, due, roles, responses,
                pending, n.Status=="Pending"&&due.HasValue&&due<_clock.UtcNow, allowSameActor);
    
        }
    
    private AlertStateDto ParseAlertState(DevelopmentGraphNode n)=>new(WorkManagementService.MetaString(n, "AlertKey", WorkManagementService.Strip(n.Key)),
            n.Status, Null(WorkManagementService.MetaString(n, "Actor")), Null(WorkManagementService.MetaString(n, "Comment")), ReadDate(n, "UpdatedAtUtc",
            n.LastObservedAtUtc), ReadNullableDate(n, "SnoozeUntilUtc"), (int)WorkManagementService.MetaDecimal(n, "EscalationLevel"), ReadNullableDate(n,
            "LastDeliveredAtUtc"), ReadNullableDate(n, "FirstSeenAtUtc")??n.FirstObservedAtUtc, ReadNullableDate(n, "ViewedAtUtc"),
            Null(WorkManagementService.MetaString(n, "ViewedBy")));
    
    private SyncConflictDto ParseConflict(DevelopmentGraphNode n)=>new(WorkManagementService.MetaString(n, "Key", WorkManagementService.Strip(n.Key)),
            WorkManagementService.MetaString(n, "Provider"), WorkManagementService.MetaString(n, "DataType"), WorkManagementService.MetaString(n, "ItemKey"),
            WorkManagementService.MetaString(n, "DipValue"), WorkManagementService.MetaString(n, "ExternalValue"), n.Status, WorkManagementService.MetaString(n,
            "Resolution"), Null(WorkManagementService.MetaString(n, "ResolvedBy")), ReadDate(n, "DetectedAtUtc", n.FirstObservedAtUtc), ReadNullableDate(n,
            "ResolvedAtUtc"));
    
    private async Task<DevelopmentGraphNode> NeedTarget(Guid projectId, ManagementTarget target, CancellationToken ct){
            var (type, key)=ResolveManagementType(target.Type, target.Key);
            return await _repo.FindNodeAsync(projectId, type, key, ct)??throw new KeyNotFoundException($"{target.Type} '{target.Key}' was not found.");
        }
    
    internal static (DevelopmentGraphNodeType Type, string Key) ResolveManagementType(string type, string key)
        {
    
            NeedKey(key);
            return (type??string.Empty).Trim().ToLowerInvariant() switch{
    
                "task"=>(DevelopmentGraphNodeType.Task, $"task:{key}"), "workstream"=>(DevelopmentGraphNodeType.Workstream, $"workstream:{key}"),
                    "team"=>(DevelopmentGraphNodeType.Team, $"team:{key}"), "milestone"=>(DevelopmentGraphNodeType.Milestone, $"milestone:{key}"),
                    "requirement"=>(DevelopmentGraphNodeType.Requirement, $"requirement:{key}"), "feature"=>(DevelopmentGraphNodeType.Feature,
                    $"feature:{key}"), "blocker"=>(DevelopmentGraphNodeType.Blocker, $"blocker:{key}"), "risk"=>(DevelopmentGraphNodeType.Risk, $"risk:{key}"),
                    "decision"=>(DevelopmentGraphNodeType.ManagementDecision, $"decision:{key}"), "action"=>(DevelopmentGraphNodeType.ActionItem,
                    $"action:{key}"), "change" or "changerequest"=>(DevelopmentGraphNodeType.ChangeRequest, $"change:{key}"),
                    "allocation" or "resourceallocation"=>(DevelopmentGraphNodeType.ResourceAllocation, $"allocation:{key}"),
                    "release"=>(DevelopmentGraphNodeType.Release, key.StartsWith("release:", StringComparison.OrdinalIgnoreCase)?key:$"release:{key}"),
                    "raci"=>(DevelopmentGraphNodeType.ResponsibilityAssignment, $"raci:{key}"), "calendar"=>(DevelopmentGraphNodeType.WorkCalendar,
                    $"calendar:{key}"), "approval"=>(DevelopmentGraphNodeType.ApprovalRequest, $"approval:{key}"),
                    _=>throw new ArgumentException($"Unsupported management target type '{type}'.", nameof(type))
            };
    
        }
    
    private static string StableRoleBindingKey(string actor,
            string role)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(actor+"|"+role)))
            .ToLowerInvariant()[..20];
    
    private static string AlertStateKey(string alertKey)=>
            $"alert-state:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(alertKey))).ToLowerInvariant()[..24]}";
    
    public static int RequiredEscalation(ManagementAlertDto alert, DateTimeOffset now, DateTimeOffset firstSeen){
            var age=now-(firstSeen<alert.TriggeredAtUtc?firstSeen:alert.TriggeredAtUtc);
            if(alert.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase))return age>=TimeSpan.FromHours(2)?3:age>=TimeSpan.FromMinutes(30)?2:1;
            if(alert.Severity.Equals("High", StringComparison.OrdinalIgnoreCase))return age>=TimeSpan.FromDays(1)?3:age>=TimeSpan.FromHours(2)?2:1;
            return age>=TimeSpan.FromDays(2)?2:1;
        }
    
    private static string RouteRole(ManagementAlertDto alert, IReadOnlyList<ResponsibilityDto> raci, IReadOnlyList<DevelopmentGraphNode> nodes)
        {
    
            var source=WorkManagementService.Strip(alert.SourceKey);
            var assignment=raci.FirstOrDefault(x=>x.TargetKey.Equals(source, StringComparison.OrdinalIgnoreCase)&&RaciTypeMatches(alert.Category,
                x.TargetType));
            var raciRole=assignment?.ResponsibleRole??assignment?.AccountableRole;
            if(!string.IsNullOrWhiteSpace(raciRole))return raciRole;
    
            DevelopmentGraphNode? node=alert.Category switch{
                "Risk"=>nodes.FirstOrDefault(x=>x.Type==DevelopmentGraphNodeType.Risk&&WorkManagementService.Strip(x.Key).Equals(source,
                    StringComparison.OrdinalIgnoreCase)),
                    "Action"=>nodes.FirstOrDefault(x=>x.Type==DevelopmentGraphNodeType.ActionItem&&WorkManagementService.Strip(x.Key).Equals(source,
                    StringComparison.OrdinalIgnoreCase)),
                    "ScopeChange"=>nodes.FirstOrDefault(x=>x.Type==DevelopmentGraphNodeType.ChangeRequest&&WorkManagementService.Strip(x.Key).Equals(source,
                    StringComparison.OrdinalIgnoreCase)),
                    "Capacity"=>nodes.FirstOrDefault(x=>x.Type==DevelopmentGraphNodeType.Workstream&&WorkManagementService.Strip(x.Key).Equals(source,
                    StringComparison.OrdinalIgnoreCase)),
                    "Blocker"=>nodes.FirstOrDefault(x=>x.Type==DevelopmentGraphNodeType.Blocker&&WorkManagementService.Strip(x.Key).Equals(source,
                    StringComparison.OrdinalIgnoreCase)), _=>null
            };
    
            if(node is not null){
                var owner=alert.Category switch{
                    "Risk"=>WorkManagementService.MetaString(node, "OwnerRole"), "Action"=>WorkManagementService.MetaString(node, "OwnerRole"),
                        "ScopeChange"=>WorkManagementService.MetaString(node, "RequestedByRole"), "Schedule"=>WorkManagementService.MetaString(node,
                        "OwnerLabel"), _=>string.Empty
                };
                if(!string.IsNullOrWhiteSpace(owner))return owner;
            }
    
            return alert.Category switch{
                "Capacity"=>"Project Manager", "Schedule"=>"Project Manager", "Risk"=>"Risk Owner", "Action"=>"Action Owner", "ScopeChange"=>"Producer",
                    "Blocker"=>"Lead", "Release"=>"Producer", _=>"Project Lead"
            };
    
        }
    
    private static bool RaciTypeMatches(string category, string targetType){
            var t=(targetType??string.Empty).Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty,
                StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
            return category switch{
                "Risk"=>t=="risk", "Action"=>t is "action" or "actionitem", "ScopeChange"=>t is "change" or "changerequest" or "scopechange",
                    "Capacity"=>t=="workstream", "Blocker"=>t=="blocker", "Schedule"=>t=="schedule", _=>false
            };
        }
    
    private static int SeverityRank(string x)=>x.Equals("Critical", StringComparison.OrdinalIgnoreCase)?4:x.Equals("High",
            StringComparison.OrdinalIgnoreCase)?3:x.Equals("Medium", StringComparison.OrdinalIgnoreCase)?2:1;
    
    private static string NormalizeApprovalStatus(string status)=>string.IsNullOrWhiteSpace(status)?"Pending":status.Trim() switch{
            "Proposed"=>"Pending", var x=>x
        };
    
    private static IReadOnlyList<string> CleanRoles(IEnumerable<string>? roles)=>(roles??[]).Where(x=>!string.IsNullOrWhiteSpace(x)).Select(x=>
            x.Trim()[..Math.Min(120, x.Trim().Length)]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    
    private static string CleanRequired(string? value, int max, string name){
            var v=Clean(value, max);
            if(string.IsNullOrWhiteSpace(v))throw new ArgumentException($"{name} is required.", name);
            return v;
        }
    
    private static string? Clean(string? value, int max)=>string.IsNullOrWhiteSpace(value)?null:value.Trim()[..Math.Min(max, value.Trim().Length)];
    
    private static string? Null(string? value)=>string.IsNullOrWhiteSpace(value)?null:value;
    
    private static void NeedProject(Guid id){
            if(id==Guid.Empty)throw new ArgumentException("ProjectId is required.", nameof(id));
        }
    
    private static void NeedKey(string key){
            if(string.IsNullOrWhiteSpace(key)||key.Trim().Length>240)throw new ArgumentException("Key is required and must be at most 240 characters.",
                nameof(key));
        }
    
    private static string GetString(JsonElement e, string name)=>e.TryGetProperty(name,
            out var x)&&x.ValueKind==JsonValueKind.String?x.GetString()??string.Empty:string.Empty;
    
    private static DateTimeOffset ReadDate(DevelopmentGraphNode n, string name, DateTimeOffset fallback)=>WorkManagementService.MetaDate(n, name,
            fallback);
    
    private static DateTimeOffset? ReadNullableDate(DevelopmentGraphNode n, string name){
            try{
                using var d=JsonDocument.Parse(n.MetadataJson);
                return d.RootElement.TryGetProperty(name, out var v)&&v.ValueKind!=JsonValueKind.Null&&v.TryGetDateTimeOffset(out var x)?x:null;
            }
            catch(JsonException){
                return null;
            }
        }
    
    private static string MergeMetadata(string json, IReadOnlyDictionary<string, object?> additions){
            var map=new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            try{
                using var d=JsonDocument.Parse(json);
                foreach(var p in d.RootElement.EnumerateObject())map[p.Name]=JsonSerializer.Deserialize<object?>(p.Value.GetRawText());
            }
            catch(JsonException){
                map.Clear();
            }
            foreach(var x in additions)map[x.Key]=x.Value;
            return JsonSerializer.Serialize(map);
        }
}
