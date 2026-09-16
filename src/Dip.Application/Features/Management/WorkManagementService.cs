// Purpose: Coordinates W or kM an ag em en tS er vi ce application behavior for Management.
using System.Globalization;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.Milestones;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

public sealed class WorkManagementService
{
    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly ISystemClock _clock;

    public WorkManagementService(DevelopmentGraphService graph,IDevelopmentGraphRepository repo,ISystemClock clock)
    {
        _graph=graph;_repo=repo;_clock=clock;
    }

    public Task<DevelopmentGraphNodeDto> UpsertTeamAsync(UpsertTeamCommand c,CancellationToken ct)
    {
        ValidateKey(c.Key);if(c.CapacityHoursPerWeek<=0m)throw new ArgumentOutOfRangeException(nameof(c.CapacityHoursPerWeek));if(c.SharedPoolCapacityHoursPerWeek.HasValue&&c.SharedPoolCapacityHoursPerWeek.Value<=0m)throw new ArgumentOutOfRangeException(nameof(c.SharedPoolCapacityHoursPerWeek));
        var sharedKey=Trim(c.SharedResourceKey,200);if(!string.IsNullOrWhiteSpace(sharedKey)&&!c.SharedPoolCapacityHoursPerWeek.HasValue)throw new ArgumentException("SharedPoolCapacityHoursPerWeek is required when SharedResourceKey is configured.",nameof(c));var sharedCapacity=string.IsNullOrWhiteSpace(sharedKey)?null:c.SharedPoolCapacityHoursPerWeek;
        var meta=JsonSerializer.Serialize(new{c.Key,CapacityHoursPerWeek=Math.Clamp(c.CapacityHoursPerWeek,1m,100000m),BufferPercent=Math.Clamp(c.BufferPercent,0m,80m),SharedResourceKey=sharedKey,SharedPoolCapacityHoursPerWeek=sharedCapacity.HasValue?Math.Clamp(sharedCapacity.Value,1m,1000000m):(decimal?)null});
        return _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.Team,$"team:{c.Key}",c.Name,c.Status,"DIP",c.ExternalReference,meta),ct);
    }

    public async Task<DevelopmentGraphNodeDto> UpsertWorkstreamAsync(UpsertWorkstreamCommand c,CancellationToken ct)
    {
        ValidateKey(c.Key);ValidateKey(c.TeamKey);if(c.DueAtUtc<c.PlannedStartUtc)throw new ArgumentException("Workstream due date must not precede its start.",nameof(c));
        var team=await Need(c.ProjectId,DevelopmentGraphNodeType.Team,$"team:{c.TeamKey}","team",ct);
        var meta=JsonSerializer.Serialize(new{c.Key,c.TeamKey,PlannedCapacityHoursPerWeek=Math.Max(0m,c.PlannedCapacityHoursPerWeek),c.PlannedStartUtc,c.DueAtUtc,CompletionPercent=Math.Clamp(c.CompletionPercent,0m,100m)});
        var ws=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.Workstream,$"workstream:{c.Key}",c.Name,c.Status,"DIP",c.ExternalReference,meta),ct);
        await _graph.LinkAsync(new(c.ProjectId,ws.Id,team.Id,DevelopmentGraphEdgeType.AssignedTo,80m,false,"{}"),ct);
        return ws;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertTaskAsync(UpsertTaskCommand c,CancellationToken ct)
    {
        ValidateKey(c.Key);ValidateKey(c.WorkstreamKey);if(c.DueAtUtc<c.PlannedStartUtc)throw new ArgumentException("Task due date must not precede its start.",nameof(c));
        var ws=await Need(c.ProjectId,DevelopmentGraphNodeType.Workstream,$"workstream:{c.WorkstreamKey}","workstream",ct);
        var estimate=Math.Max(0m,c.EstimateHours);var remaining=Math.Clamp(c.RemainingHours,0m,Math.Max(estimate,c.RemainingHours));
        var meta=JsonSerializer.Serialize(new{c.Key,c.WorkstreamKey,EstimateHours=estimate,RemainingHours=remaining,c.PlannedStartUtc,c.DueAtUtc,CompletionPercent=Math.Clamp(c.CompletionPercent,0m,100m),Priority=NormalizePriority(c.Priority),OwnerLabel=Trim(c.OwnerLabel,200)});
        var task=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.Task,$"task:{c.Key}",c.Name,c.Status,"DIP",c.ExternalReference,meta),ct);
        await _graph.LinkAsync(new(c.ProjectId,task.Id,ws.Id,DevelopmentGraphEdgeType.BelongsTo,90m,false,"{}"),ct);
        return task;
    }

    public async Task LinkDependencyAsync(LinkManagementDependencyCommand c,CancellationToken ct)
    {
        var (st,sk)=ResolveTypeAndKey(c.PredecessorType,c.PredecessorKey);var (tt,tk)=ResolveTypeAndKey(c.SuccessorType,c.SuccessorKey);
        var source=await Need(c.ProjectId,st,sk,c.PredecessorType,ct);var target=await Need(c.ProjectId,tt,tk,c.SuccessorType,ct);
        await _graph.LinkAsync(new(c.ProjectId,source.Id,target.Id,DevelopmentGraphEdgeType.Blocks,c.Critical?100m:65m,c.Critical,JsonSerializer.Serialize(new{dependency="finish-to-start",management=true})),ct);
    }

    public async Task<DevelopmentGraphNodeDto> UpsertBlockerAsync(UpsertBlockerCommand c,CancellationToken ct)
    {
        ValidateKey(c.Key);var opened=c.OpenedAtUtc==default?_clock.UtcNow:c.OpenedAtUtc;
        var blockedTasks=(c.BlockedTaskKeys??[]).Where(NotBlank).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var blockedWs=(c.BlockedWorkstreamKeys??[]).Where(NotBlank).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var blockedMs=(c.BlockedMilestoneKeys??[]).Where(NotBlank).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var meta=JsonSerializer.Serialize(new{c.Key,c.Reason,Severity=NormalizeSeverity(c.Severity),OpenedAtUtc=opened,BlockedTaskKeys=blockedTasks,BlockedWorkstreamKeys=blockedWs,BlockedMilestoneKeys=blockedMs});
        var blocker=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.Blocker,$"blocker:{c.Key}",c.Title,c.Status,"DIP",c.ExternalReference,meta),ct);
        foreach(var key in blockedTasks){var n=await Need(c.ProjectId,DevelopmentGraphNodeType.Task,$"task:{key}","task",ct);await LinkBlocker(c.ProjectId,blocker.Id,n.Id,ct);}
        foreach(var key in blockedWs){var n=await Need(c.ProjectId,DevelopmentGraphNodeType.Workstream,$"workstream:{key}","workstream",ct);await LinkBlocker(c.ProjectId,blocker.Id,n.Id,ct);}
        foreach(var key in blockedMs){var n=await Need(c.ProjectId,DevelopmentGraphNodeType.Milestone,$"milestone:{key}","milestone",ct);await LinkBlocker(c.ProjectId,blocker.Id,n.Id,ct);}
        return blocker;
    }

    public Task<DevelopmentGraphNodeDto> UpsertRequirementAsync(UpsertRequirementCommand c,CancellationToken ct)
    {
        ValidateKey(c.Key);var meta=JsonSerializer.Serialize(new{c.Key,Priority=NormalizePriority(c.Priority),ScopeState=NormalizeScope(c.ScopeState),CompletionPercent=Math.Clamp(c.CompletionPercent,0m,100m),AcceptanceSummary=Trim(c.AcceptanceSummary,4000)});
        return _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.Requirement,$"requirement:{c.Key}",c.Title,c.Status,"DIP",c.ExternalReference,meta),ct);
    }

    public async Task LinkRequirementAsync(LinkRequirementCommand c,CancellationToken ct)
    {
        var req=await Need(c.ProjectId,DevelopmentGraphNodeType.Requirement,$"requirement:{c.RequirementKey}","requirement",ct);
        var (type,key)=ResolveTypeAndKey(c.TargetType,c.TargetKey);var target=await Need(c.ProjectId,type,key,c.TargetType,ct);
        await _graph.LinkAsync(new(c.ProjectId,target.Id,req.Id,DevelopmentGraphEdgeType.Satisfies,90m,false,"{}"),ct);
    }

    public async Task<DevelopmentGraphNodeDto> CreateScopeBaselineAsync(CreateScopeBaselineCommand c,CancellationToken ct)
    {
        ValidateKey(c.Key);var reqs=await _repo.ListNodesAsync(c.ProjectId,DevelopmentGraphNodeType.Requirement,ct);
        var included=reqs.Count(IsInCurrentScope);var keys=reqs.Where(IsInCurrentScope).Select(x=>Strip(x.Key)).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
        var captured=c.CapturedAtUtc??_clock.UtcNow;var meta=JsonSerializer.Serialize(new{c.Key,CapturedAtUtc=captured,BaselineRequirements=included,RequirementKeys=keys});
        return await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.ScopeBaseline,$"scope:{c.Key}",c.Name,"Active","DIP",null,meta),ct);
    }

    public async Task LinkTaskToMilestoneAsync(LinkTaskToMilestoneCommand c,CancellationToken ct)
    {
        var task=await Need(c.ProjectId,DevelopmentGraphNodeType.Task,$"task:{c.TaskKey}","task",ct);var milestone=await Need(c.ProjectId,DevelopmentGraphNodeType.Milestone,$"milestone:{c.MilestoneKey}","milestone",ct);
        await _graph.LinkAsync(new(c.ProjectId,task.Id,milestone.Id,DevelopmentGraphEdgeType.ScheduledFor,90m,false,"{}"),ct);
    }

    public async Task<DevelopmentGraphNodeDto> UpsertRiskAsync(UpsertRiskCommand c,CancellationToken ct)
    {
        ValidateKey(c.Key);
        var probability=Math.Clamp(c.ProbabilityPercent,0m,100m);var impact=Math.Clamp(c.ImpactPercent,0m,100m);var exposure=probability*impact/100m;
        var affected=(c.AffectedItems??[]).Where(x=>x is not null&&NotBlank(x.Type)&&NotBlank(x.Key)).DistinctBy(x=>$"{x.Type}:{x.Key}",StringComparer.OrdinalIgnoreCase).ToArray();
        var meta=JsonSerializer.Serialize(new{c.Key,c.Description,ProbabilityPercent=probability,ImpactPercent=impact,ExposureScore=exposure,c.Category,OwnerRole=Trim(c.OwnerRole,200),c.ReviewDueUtc,Trigger=Trim(c.Trigger,2000),Mitigation=Trim(c.Mitigation,4000),AffectedItems=affected});
        var risk=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.Risk,$"risk:{c.Key}",c.Title,c.Status,"DIP",c.ExternalReference,meta),ct);
        foreach(var item in affected)
        {
            var (type,key)=ResolveTypeAndKey(item.Type,item.Key);var target=await Need(c.ProjectId,type,key,item.Type,ct);
            await _graph.LinkAsync(new(c.ProjectId,risk.Id,target.Id,DevelopmentGraphEdgeType.Affects,Math.Clamp(exposure,10m,100m),exposure>=70m,JsonSerializer.Serialize(new{managementRisk=true})),ct);
        }
        return risk;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertManagementDecisionAsync(UpsertManagementDecisionCommand c,CancellationToken ct)
    {
        ValidateKey(c.Key);var related=(c.RelatedItems??[]).Where(x=>x is not null&&NotBlank(x.Type)&&NotBlank(x.Key)).DistinctBy(x=>$"{x.Type}:{x.Key}",StringComparer.OrdinalIgnoreCase).ToArray();
        var meta=JsonSerializer.Serialize(new{c.Key,c.Decision,c.Rationale,DecisionOwnerRole=Trim(c.DecisionOwnerRole,200),c.DecisionDueUtc,c.DecidedAtUtc,RelatedItems=related});
        var decision=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.ManagementDecision,$"decision:{c.Key}",c.Title,c.Status,"DIP",c.ExternalReference,meta),ct);
        foreach(var item in related)
        {
            var (type,key)=ResolveTypeAndKey(item.Type,item.Key);var target=await Need(c.ProjectId,type,key,item.Type,ct);
            await _graph.LinkAsync(new(c.ProjectId,decision.Id,target.Id,DevelopmentGraphEdgeType.RelatesTo,75m,false,JsonSerializer.Serialize(new{managementDecision=true})),ct);
        }
        return decision;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertActionItemAsync(UpsertActionItemCommand c,CancellationToken ct)
    {
        ValidateKey(c.Key);var priority=NormalizePriority(c.Priority);var completion=Math.Clamp(c.CompletionPercent,0m,100m);
        var meta=JsonSerializer.Serialize(new{c.Key,Priority=priority,OwnerRole=Trim(c.OwnerRole,200),c.DueAtUtc,CompletionPercent=completion,DecisionKey=Trim(c.DecisionKey,240),Target=c.Target});
        var action=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.ActionItem,$"action:{c.Key}",c.Title,c.Status,"DIP",c.ExternalReference,meta),ct);
        if(NotBlank(c.DecisionKey))
        {
            var decision=await Need(c.ProjectId,DevelopmentGraphNodeType.ManagementDecision,$"decision:{c.DecisionKey}","decision",ct);
            await _graph.LinkAsync(new(c.ProjectId,decision.Id,action.Id,DevelopmentGraphEdgeType.ResultsIn,90m,false,"{\"managementAction\":true}"),ct);
        }
        if(c.Target is not null&&NotBlank(c.Target.Type)&&NotBlank(c.Target.Key))
        {
            var (type,key)=ResolveTypeAndKey(c.Target.Type,c.Target.Key);var target=await Need(c.ProjectId,type,key,c.Target.Type,ct);
            var edgeType=type==DevelopmentGraphNodeType.Risk?DevelopmentGraphEdgeType.Mitigates:DevelopmentGraphEdgeType.RelatesTo;
            await _graph.LinkAsync(new(c.ProjectId,action.Id,target.Id,edgeType,85m,type==DevelopmentGraphNodeType.Risk,"{}"),ct);
        }
        return action;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertResourceAllocationAsync(UpsertResourceAllocationCommand c,CancellationToken ct)
    {
        ValidateKey(c.Key);ValidateKey(c.ToWorkstreamKey);if(c.CapacityHoursPerWeek<=0m)throw new ArgumentOutOfRangeException(nameof(c.CapacityHoursPerWeek));if(c.EndUtc<c.StartUtc)throw new ArgumentException("Allocation end must not precede start.",nameof(c));
        var target=await Need(c.ProjectId,DevelopmentGraphNodeType.Workstream,$"workstream:{c.ToWorkstreamKey}","workstream",ct);DevelopmentGraphNode? source=null;
        if(NotBlank(c.FromWorkstreamKey))source=await Need(c.ProjectId,DevelopmentGraphNodeType.Workstream,$"workstream:{c.FromWorkstreamKey}","workstream",ct);
        var meta=JsonSerializer.Serialize(new{c.Key,c.FromWorkstreamKey,c.ToWorkstreamKey,CapacityHoursPerWeek=Math.Clamp(c.CapacityHoursPerWeek,0.1m,100000m),c.StartUtc,c.EndUtc,Rationale=Trim(c.Rationale,4000),DecisionKey=Trim(c.DecisionKey,240)});
        var allocation=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.ResourceAllocation,$"allocation:{c.Key}",$"Allocation to {target.Name}",c.Status,"DIP",null,meta),ct);
        await _graph.LinkAsync(new(c.ProjectId,allocation.Id,target.Id,DevelopmentGraphEdgeType.AllocatesTo,90m,false,"{}"),ct);
        if(source is not null)await _graph.LinkAsync(new(c.ProjectId,allocation.Id,source.Id,DevelopmentGraphEdgeType.RelatesTo,60m,false,"{\"allocationSource\":true}"),ct);
        if(NotBlank(c.DecisionKey)){var decision=await Need(c.ProjectId,DevelopmentGraphNodeType.ManagementDecision,$"decision:{c.DecisionKey}","decision",ct);await _graph.LinkAsync(new(c.ProjectId,decision.Id,allocation.Id,DevelopmentGraphEdgeType.ResultsIn,90m,false,"{}"),ct);}
        return allocation;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertChangeRequestAsync(UpsertChangeRequestCommand c,CancellationToken ct)
    {
        ValidateKey(c.Key);var requirements=(c.RequirementKeys??[]).Where(NotBlank).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();var workstreams=(c.WorkstreamKeys??[]).Where(NotBlank).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var requested=c.RequestedAtUtc??_clock.UtcNow;var meta=JsonSerializer.Serialize(new{c.Key,c.Description,Priority=NormalizePriority(c.Priority),RequestedByRole=Trim(c.RequestedByRole,200),RequestedAtUtc=requested,EngineeringHoursDelta=c.EngineeringHoursDelta,ArtHoursDelta=c.ArtHoursDelta,QaHoursDelta=c.QaHoursDelta,ScheduleDaysDelta=c.ScheduleDaysDelta,RiskDeltaPercent=c.RiskDeltaPercent,RequirementKeys=requirements,WorkstreamKeys=workstreams,DecisionKey=Trim(c.DecisionKey,240)});
        var request=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.ChangeRequest,$"change:{c.Key}",c.Title,c.Status,"DIP",c.ExternalReference,meta),ct);
        foreach(var key in requirements){var target=await Need(c.ProjectId,DevelopmentGraphNodeType.Requirement,$"requirement:{key}","requirement",ct);await _graph.LinkAsync(new(c.ProjectId,request.Id,target.Id,DevelopmentGraphEdgeType.Impacts,85m,NormalizePriority(c.Priority) is "Critical" or "High","{}"),ct);}
        foreach(var key in workstreams){var target=await Need(c.ProjectId,DevelopmentGraphNodeType.Workstream,$"workstream:{key}","workstream",ct);await _graph.LinkAsync(new(c.ProjectId,request.Id,target.Id,DevelopmentGraphEdgeType.Impacts,85m,NormalizePriority(c.Priority) is "Critical" or "High","{}"),ct);}
        if(NotBlank(c.DecisionKey)){var decision=await Need(c.ProjectId,DevelopmentGraphNodeType.ManagementDecision,$"decision:{c.DecisionKey}","decision",ct);await _graph.LinkAsync(new(c.ProjectId,request.Id,decision.Id,DevelopmentGraphEdgeType.Requires,90m,true,"{}"),ct);}
        return request;
    }

    internal static bool IsInCurrentScope(DevelopmentGraphNode n)
    {
        if(n.Type!=DevelopmentGraphNodeType.Requirement)return false;var scope=MetaString(n,"ScopeState","Included");
        return !scope.Equals("Removed",StringComparison.OrdinalIgnoreCase)&&!scope.Equals("Deferred",StringComparison.OrdinalIgnoreCase)&&!n.Status.Equals("Cancelled",StringComparison.OrdinalIgnoreCase);
    }

    internal static string MetaString(DevelopmentGraphNode n,string name,string fallback="")
    {
        try{using var d=JsonDocument.Parse(n.MetadataJson);return d.RootElement.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??fallback:fallback;}catch{return fallback;}
    }
    internal static decimal MetaDecimal(DevelopmentGraphNode n,string name,decimal fallback=0m)
    {
        try{using var d=JsonDocument.Parse(n.MetadataJson);return d.RootElement.TryGetProperty(name,out var v)&&v.TryGetDecimal(out var x)?x:fallback;}catch{return fallback;}
    }
    internal static IReadOnlyList<string> MetaStringArray(DevelopmentGraphNode n,string name)
    {
        try{using var d=JsonDocument.Parse(n.MetadataJson);if(!d.RootElement.TryGetProperty(name,out var v)||v.ValueKind!=JsonValueKind.Array)return [];return v.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()??string.Empty).Where(x=>!string.IsNullOrWhiteSpace(x)).ToArray();}catch{return [];}
    }
    internal static DateTimeOffset MetaDate(DevelopmentGraphNode n,string name,DateTimeOffset fallback)
    {
        try{using var d=JsonDocument.Parse(n.MetadataJson);return d.RootElement.TryGetProperty(name,out var v)&&v.TryGetDateTimeOffset(out var x)?x:fallback;}catch{return fallback;}
    }
    internal static string Strip(string key)=>key.Contains(':')?key[(key.IndexOf(':')+1)..]:key;
    internal static bool IsDone(string status)=>status.Equals("Done",StringComparison.OrdinalIgnoreCase)||status.Equals("Completed",StringComparison.OrdinalIgnoreCase)||status.Equals("Closed",StringComparison.OrdinalIgnoreCase)||status.Equals("Resolved",StringComparison.OrdinalIgnoreCase);
    internal static bool IsBlocked(string status)=>status.Equals("Blocked",StringComparison.OrdinalIgnoreCase)||status.Equals("AtRisk",StringComparison.OrdinalIgnoreCase)||status.Equals("Critical",StringComparison.OrdinalIgnoreCase);

    private async Task<DevelopmentGraphNode> Need(Guid projectId,DevelopmentGraphNodeType type,string key,string label,CancellationToken ct)
        =>await _repo.FindNodeAsync(projectId,type,key,ct)??throw new KeyNotFoundException($"{label} '{Strip(key)}' was not found.");
    private async Task LinkBlocker(Guid projectId,Guid blockerId,Guid targetId,CancellationToken ct){await _graph.LinkAsync(new(projectId,blockerId,targetId,DevelopmentGraphEdgeType.Blocks,100m,true,"{\"managementBlocker\":true}"),ct);}
    private static (DevelopmentGraphNodeType Type,string Key) ResolveTypeAndKey(string type,string key)
    {
        ValidateKey(key);var normalized=type.Trim().ToLowerInvariant();return normalized switch
        {
            "task"=>(DevelopmentGraphNodeType.Task,$"task:{key}"),"workstream"=>(DevelopmentGraphNodeType.Workstream,$"workstream:{key}"),"team"=>(DevelopmentGraphNodeType.Team,$"team:{key}"),"milestone"=>(DevelopmentGraphNodeType.Milestone,$"milestone:{key}"),"requirement"=>(DevelopmentGraphNodeType.Requirement,$"requirement:{key}"),"feature"=>(DevelopmentGraphNodeType.Feature,$"feature:{key}"),"blocker"=>(DevelopmentGraphNodeType.Blocker,$"blocker:{key}"),"risk"=>(DevelopmentGraphNodeType.Risk,$"risk:{key}"),"decision"=>(DevelopmentGraphNodeType.ManagementDecision,$"decision:{key}"),"action"=>(DevelopmentGraphNodeType.ActionItem,$"action:{key}"),"change"=>(DevelopmentGraphNodeType.ChangeRequest,$"change:{key}"),_=>throw new ArgumentException($"Unsupported management node type '{type}'.",nameof(type))
        };
    }
    private static void ValidateKey(string key){if(string.IsNullOrWhiteSpace(key)||key.Trim().Length>240)throw new ArgumentException("Key is required and must be at most 240 characters.",nameof(key));}
    private static bool NotBlank(string? value)=>!string.IsNullOrWhiteSpace(value);
    private static string NormalizePriority(string? p)=>string.IsNullOrWhiteSpace(p)?"Medium":p.Trim() switch{"1"=>"Critical","2"=>"High","3"=>"Medium","4"=>"Low",var x=>CultureInfo.InvariantCulture.TextInfo.ToTitleCase(x.ToLowerInvariant())};
    private static string NormalizeSeverity(string? p)=>NormalizePriority(p);
    private static string NormalizeScope(string? s)=>string.IsNullOrWhiteSpace(s)?"Included":CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.Trim().ToLowerInvariant());
    private static string? Trim(string? value,int max)=>string.IsNullOrWhiteSpace(value)?null:value.Trim()[..Math.Min(value.Trim().Length,max)];
}
