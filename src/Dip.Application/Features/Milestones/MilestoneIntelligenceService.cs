using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Milestones;

public sealed record UpsertMilestoneCommand(Guid ProjectId,string Key,string Name,DateTimeOffset PlannedStartUtc,DateTimeOffset DueAtUtc,int DurationDays,decimal CompletionPercent=0m,MilestoneState State=MilestoneState.Planned,string? ExternalReference=null);
public sealed record MilestoneDependencyCommand(Guid ProjectId,string PredecessorKey,string SuccessorKey,bool Critical=true);
public sealed record CriticalPathMilestone(string Key,string Name,int DurationDays,int EarliestStartDay,int EarliestFinishDay,int LatestStartDay,int LatestFinishDay,int SlackDays,bool IsCritical,DateTimeOffset DueAtUtc,decimal CompletionPercent,string State);
public sealed record CriticalPathResult(string Status,int TotalDurationDays,int CriticalMilestones,int AtRiskMilestones,IReadOnlyList<CriticalPathMilestone> Milestones,IReadOnlyList<string> CriticalPath,IReadOnlyList<string> Blockers,string Summary);

public sealed class MilestoneIntelligenceService
{
    private readonly DevelopmentGraphService _graph;private readonly IDevelopmentGraphRepository _repo;private readonly IP2IntelligenceRepository _records;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public MilestoneIntelligenceService(DevelopmentGraphService graph,IDevelopmentGraphRepository repo,IP2IntelligenceRepository records,IUnitOfWork uow,ISystemClock clock){_graph=graph;_repo=repo;_records=records;_uow=uow;_clock=clock;}

    public Task<DevelopmentGraphNodeDto> UpsertAsync(UpsertMilestoneCommand c,CancellationToken ct)
    {
        if(c.DueAtUtc<c.PlannedStartUtc)throw new ArgumentException("Milestone due date must not precede its planned start.",nameof(c));
        var status=c.State.ToString();return _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.Milestone,$"milestone:{c.Key}",c.Name,status,"DIP",c.ExternalReference,JsonSerializer.Serialize(new{c.Key,c.PlannedStartUtc,c.DueAtUtc,DurationDays=Math.Clamp(c.DurationDays,1,3650),CompletionPercent=Math.Clamp(c.CompletionPercent,0m,100m),State=status})),ct);
    }

    public async Task LinkDependencyAsync(MilestoneDependencyCommand c,CancellationToken ct)
    {
        var predecessor=await _repo.FindNodeAsync(c.ProjectId,DevelopmentGraphNodeType.Milestone,$"milestone:{c.PredecessorKey}",ct)??throw new KeyNotFoundException($"Milestone '{c.PredecessorKey}' was not found.");
        var successor=await _repo.FindNodeAsync(c.ProjectId,DevelopmentGraphNodeType.Milestone,$"milestone:{c.SuccessorKey}",ct)??throw new KeyNotFoundException($"Milestone '{c.SuccessorKey}' was not found.");
        await _graph.LinkAsync(new(c.ProjectId,predecessor.Id,successor.Id,DevelopmentGraphEdgeType.Blocks,c.Critical?100m:70m,c.Critical,JsonSerializer.Serialize(new{dependency="finish-to-start"})),ct);
    }

    public async Task<CriticalPathResult> AnalyzeAsync(Guid projectId,CancellationToken ct)
    {
        var nodes=await _repo.ListNodesAsync(projectId,DevelopmentGraphNodeType.Milestone,ct);var ids=nodes.Select(x=>x.Id).ToHashSet();var edges=(await _repo.ListEdgesAsync(projectId,ct)).Where(x=>ids.Contains(x.SourceNodeId)&&ids.Contains(x.TargetNodeId)&&x.Type is DevelopmentGraphEdgeType.Blocks or DevelopmentGraphEdgeType.DependsOn).ToArray();
        if(nodes.Count==0)return new("NoMilestones",0,0,0,[],[],[],"No milestones are registered in the Development Graph.");
        var models=nodes.Select(Parse).ToDictionary(x=>x.Id);var indegree=nodes.ToDictionary(x=>x.Id,_=>0);foreach(var e in edges)indegree[e.TargetNodeId]++;var q=new Queue<Guid>(indegree.Where(x=>x.Value==0).Select(x=>x.Key));var order=new List<Guid>();while(q.Count>0){var id=q.Dequeue();order.Add(id);foreach(var e in edges.Where(x=>x.SourceNodeId==id)){if(--indegree[e.TargetNodeId]==0)q.Enqueue(e.TargetNodeId);}}
        if(order.Count!=nodes.Count){var result=new CriticalPathResult("CycleDetected",0,0,0,[],[],["Milestone dependency graph contains a cycle. Remove the cycle before relying on schedule forecasts."],"Critical path cannot be calculated while milestone dependencies contain a cycle.");await Store(projectId,result,P2IntelligenceStatus.Warning,85m,ct);return result;}
        var es=nodes.ToDictionary(x=>x.Id,_=>0);var ef=nodes.ToDictionary(x=>x.Id,_=>0);foreach(var id in order){var preds=edges.Where(x=>x.TargetNodeId==id).Select(x=>x.SourceNodeId).ToArray();es[id]=preds.Length==0?0:preds.Max(p=>ef[p]);ef[id]=es[id]+models[id].DurationDays;}
        var total=ef.Values.Max();var lf=nodes.ToDictionary(x=>x.Id,_=>total);var ls=nodes.ToDictionary(x=>x.Id,_=>0);foreach(var id in order.AsEnumerable().Reverse()){var succ=edges.Where(x=>x.SourceNodeId==id).Select(x=>x.TargetNodeId).ToArray();lf[id]=succ.Length==0?total:succ.Min(s=>ls[s]);ls[id]=lf[id]-models[id].DurationDays;}
        var list=order.Select(id=>{var m=models[id];var slack=Math.Max(0,ls[id]-es[id]);return new CriticalPathMilestone(m.Key,m.Name,m.DurationDays,es[id],ef[id],ls[id],lf[id],slack,slack==0,m.DueAtUtc,m.CompletionPercent,m.State);}).ToArray();var critical=list.Where(x=>x.IsCritical).Select(x=>x.Key).ToArray();var blockers=new List<string>();var now=_clock.UtcNow;foreach(var m in list.Where(x=>x.DueAtUtc<now&&x.CompletionPercent<100m))blockers.Add($"Milestone '{m.Name}' is past due with {m.CompletionPercent:0.#}% evidence-backed completion.");foreach(var m in list.Where(x=>string.Equals(x.State,"Blocked",StringComparison.OrdinalIgnoreCase)))blockers.Add($"Milestone '{m.Name}' is explicitly blocked.");var atRisk=list.Count(x=>x.DueAtUtc<now.AddDays(Math.Max(3,x.SlackDays+2))&&x.CompletionPercent<100m);var status=blockers.Count>0?"Blocked":atRisk>0?"AtRisk":"OnTrack";var result2=new CriticalPathResult(status,total,critical.Length,atRisk,list,critical,blockers,$"Critical path spans {total} planned day(s) across {critical.Length} critical milestone(s); {atRisk} milestone(s) are at risk.");await Store(projectId,result2,blockers.Count>0?P2IntelligenceStatus.Warning:P2IntelligenceStatus.Informational,Math.Clamp(blockers.Count*25m+atRisk*8m,0m,100m),ct);return result2;
    }

    private async Task Store(Guid projectId,CriticalPathResult result,P2IntelligenceStatus status,decimal score,CancellationToken ct){await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),projectId,"project",P2IntelligenceKind.MilestoneCriticalPath,status,score,EvidenceConfidence.High,result.Summary,JsonSerializer.Serialize(result),null,_clock.UtcNow),ct);await _uow.SaveChangesAsync(ct);}
    private static MilestoneModel Parse(DevelopmentGraphNode n){using var doc=JsonDocument.Parse(n.MetadataJson);var r=doc.RootElement;return new(n.Id,r.TryGetProperty("Key",out var k)?k.GetString()??n.Key.Replace("milestone:",""):n.Key.Replace("milestone:",""),n.Name,r.TryGetProperty("DurationDays",out var d)&&d.TryGetInt32(out var di)?Math.Max(1,di):1,r.TryGetProperty("DueAtUtc",out var due)&&due.TryGetDateTimeOffset(out var dto)?dto:DateTimeOffset.MaxValue,r.TryGetProperty("CompletionPercent",out var cp)&&cp.TryGetDecimal(out var dec)?Math.Clamp(dec,0m,100m):0m,n.Status);}
    private sealed record MilestoneModel(Guid Id,string Key,string Name,int DurationDays,DateTimeOffset DueAtUtc,decimal CompletionPercent,string State);
}
