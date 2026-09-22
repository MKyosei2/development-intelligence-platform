// Purpose: Owns management actor-to-role bindings and role-directory queries.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

internal sealed class ManagementRoleBindingHandler
{
    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly ManagementAnalyticsService _analytics;
    private readonly ManagementDashboardService _dashboard;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public ManagementRoleBindingHandler(DevelopmentGraphService graph, IDevelopmentGraphRepository repo, ManagementAnalyticsService analytics, ManagementDashboardService dashboard, IAuditLogRepository audit, IUnitOfWork uow, ISystemClock clock)
    {
        _graph = graph;
        _repo = repo;
        _analytics = analytics;
        _dashboard = dashboard;
        _audit = audit;
        _uow = uow;
        _clock = clock;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertManagementRoleBindingAsync(UpsertManagementRoleBindingCommand c, CancellationToken ct)
        {
    
            NeedProject(c.ProjectId);
            var actor=CleanRequired(c.ActorIdentity, 240, "ActorIdentity");
            var role=CleanRequired(c.Role, 120, "Role");
            var key=string.IsNullOrWhiteSpace(c.Key)?StableRoleBindingKey(actor, role):c.Key.Trim();
            NeedKey(key);
    
            var status=string.IsNullOrWhiteSpace(c.Status)?"Active":c.Status.Trim();
            var meta=JsonSerializer.Serialize(new{
                Key=key, ActorIdentity=actor, Role=role, DisplayName=Clean(c.DisplayName, 200)
            });
    
            return await _graph.UpsertNodeAsync(new(c.ProjectId, DevelopmentGraphNodeType.ManagementRoleBinding, $"role-binding:{key}",
                c.DisplayName??$"{role} · {actor}", status, "DIP", null, meta), ct);
    
        }
    
    public async Task<ManagementRoleDirectoryDto> GetManagementRoleDirectoryAsync(Guid projectId, string currentActor, CancellationToken ct)
        {
    
            NeedProject(projectId);
            var nodes=await _repo.ListNodesAsync(projectId, DevelopmentGraphNodeType.ManagementRoleBinding, ct);
            var bindings=nodes.Select(ParseRoleBinding).OrderBy(x=>x.Role).ThenBy(x=>x.DisplayName??x.ActorIdentity).ToArray();
            return new(projectId, CleanRequired(currentActor, 240, "CurrentActor"), bindings, _clock.UtcNow);
    
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
