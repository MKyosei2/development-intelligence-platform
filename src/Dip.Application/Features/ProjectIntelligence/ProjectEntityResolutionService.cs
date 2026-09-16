// Purpose: Coordinates P ro je ct En ti ty Re so lu ti on Se rv ic e application behavior for ProjectIntelligence.
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectIntelligence;

public sealed class ProjectEntityResolutionService
{
    private static readonly DevelopmentGraphNodeType[] CanonicalTypes=[DevelopmentGraphNodeType.Task,DevelopmentGraphNodeType.ActionItem,DevelopmentGraphNodeType.Requirement,DevelopmentGraphNodeType.Milestone,DevelopmentGraphNodeType.Feature,DevelopmentGraphNodeType.WorkItem];
    private static readonly DevelopmentGraphNodeType[] SourceTypes=[DevelopmentGraphNodeType.PlanItem,DevelopmentGraphNodeType.ExternalObservation,DevelopmentGraphNodeType.PullRequest,DevelopmentGraphNodeType.Commit];
    private readonly DevelopmentGraphService _graph;private readonly IDevelopmentGraphRepository _repo;private readonly ISystemClock _clock;
    public ProjectEntityResolutionService(DevelopmentGraphService graph,IDevelopmentGraphRepository repo,ISystemClock clock){_graph=graph;_repo=repo;_clock=clock;}

    public async Task<EntityResolutionOverviewDto> ResolveProjectAsync(Guid projectId,bool persistHighConfidence,CancellationToken ct)
    {
        var nodes=await _repo.ListNodesAsync(projectId,null,ct);var edges=await _repo.ListEdgesAsync(projectId,ct);
        var canonical=nodes.Where(IsCanonical).ToArray();var sources=CurrentSources(nodes).ToArray();var items=new List<EntityMatchDto>();var resolved=0;var suggested=0;var unresolved=0;
        foreach(var source in sources)
        {
            ct.ThrowIfCancellationRequested();var existing=FindEquivalent(edges,source.Id,nodes);if(existing is not null){resolved++;items.Add(ToMatch(source,existing,100m,"Resolved","Existing explicit/automatic entity binding."));continue;}
            var ranked=canonical.Where(c=>c.Id!=source.Id).Select(c=>(Node:c,Score:Score(source,c),Reason:Reason(source,c))).Where(x=>x.Score>=55m).OrderByDescending(x=>x.Score).ThenBy(x=>x.Node.Name,StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
            if(ranked.Length==0){unresolved++;items.Add(ToMatch(source,null,0m,"Unresolved","No safe cross-source candidate was found."));continue;}
            var best=ranked[0];var margin=best.Score-(ranked.Length>1?ranked[1].Score:0m);var safe=best.Score>=90m||(best.Score>=82m&&margin>=15m);
            if(safe&&persistHighConfidence){await _graph.LinkAsync(new(projectId,source.Id,best.Node.Id,DevelopmentGraphEdgeType.EquivalentTo,best.Score,false,JsonSerializer.Serialize(new{automatic=true,best.Reason,resolvedAtUtc=_clock.UtcNow,domainAware=true})),ct);resolved++;items.Add(ToMatch(source,best.Node,best.Score,"Resolved",best.Reason));}
            else{suggested++;items.Add(ToMatch(source,best.Node,best.Score,"Suggested",best.Reason));}
        }
        return new(projectId,resolved,suggested,unresolved,items.OrderBy(x=>x.Resolution).ThenByDescending(x=>x.Confidence).Take(2000).ToArray(),_clock.UtcNow);
    }

    public async Task BindAsync(BindProjectEntityCommand command,CancellationToken ct)
    {
        if(command.ProjectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(command));var source=await Need(command.ProjectId,command.SourceType,command.SourceKey,true,ct);var target=await Need(command.ProjectId,command.TargetType,command.TargetKey,false,ct);
        await _graph.LinkAsync(new(command.ProjectId,source.Id,target.Id,DevelopmentGraphEdgeType.EquivalentTo,100m,false,JsonSerializer.Serialize(new{automatic=false,actor=command.Actor,reason=command.Reason,boundAtUtc=_clock.UtcNow})),ct);
        var key=$"binding:{source.Type}:{Strip(source.Key)}:{target.Type}:{Strip(target.Key)}";await _graph.UpsertNodeAsync(new(command.ProjectId,DevelopmentGraphNodeType.EntityBinding,key,$"{source.Name} = {target.Name}","Active","DIP",null,JsonSerializer.Serialize(new{SourceId=source.Id,TargetId=target.Id,command.Actor,command.Reason})),ct);
    }

    private async Task<DevelopmentGraphNode> Need(Guid projectId,string type,string key,bool source,CancellationToken ct)
    {
        if(!Enum.TryParse<DevelopmentGraphNodeType>(type,true,out var t))throw new ArgumentException($"Unknown node type '{type}'.");if(source&&!SourceTypes.Contains(t))throw new ArgumentException($"'{type}' cannot be used as an entity-resolution source.");
        var candidates=await _repo.ListNodesAsync(projectId,t,ct);var found=candidates.FirstOrDefault(x=>Strip(x.Key).Equals(key,StringComparison.OrdinalIgnoreCase)||x.Key.Equals(key,StringComparison.OrdinalIgnoreCase))??throw new KeyNotFoundException($"{type}:{key} was not found.");
        if(!source&&!IsCanonical(found))throw new ArgumentException($"'{type}' cannot be used as a canonical target.");
        return found;
    }

    private static bool IsCanonical(DevelopmentGraphNode n)
    {
        if(CanonicalTypes.Contains(n.Type))return true;
        var kind=MetaString(n,"kind").ToLowerInvariant();
        return kind is "media.shot" or "media.task" or "construction.task" or "construction.drawing" or "construction.rfi" or "construction.approval" or "generic.task";
    }

    private static IEnumerable<DevelopmentGraphNode> CurrentSources(IReadOnlyList<DevelopmentGraphNode> nodes)
    {
        var latestImport=nodes.Where(x=>x.Type==DevelopmentGraphNodeType.ProjectPlanImport&&x.Status.Equals("Active",StringComparison.OrdinalIgnoreCase)).OrderByDescending(x=>x.LastObservedAtUtc).FirstOrDefault();var importKey=latestImport is null?null:MetaString(latestImport,"ImportKey");
        foreach(var n in nodes.Where(x=>SourceTypes.Contains(x.Type)))
        {if(n.Type==DevelopmentGraphNodeType.PlanItem&&importKey is not null&&!string.Equals(MetaString(n,"ImportKey"),importKey,StringComparison.OrdinalIgnoreCase))continue;yield return n;}
    }

    private static DevelopmentGraphNode? FindEquivalent(IReadOnlyList<DevelopmentGraphEdge> edges,Guid sourceId,IReadOnlyList<DevelopmentGraphNode> nodes){var edge=edges.Where(x=>x.SourceNodeId==sourceId&&x.Type==DevelopmentGraphEdgeType.EquivalentTo).OrderByDescending(x=>x.LastObservedAtUtc).FirstOrDefault();return edge is null?null:nodes.FirstOrDefault(x=>x.Id==edge.TargetNodeId);}

    private static decimal Score(DevelopmentGraphNode source,DevelopmentGraphNode target)
    {
        var sKey=Normalize(ExtractLogicalKey(source));var tKey=Normalize(ExtractLogicalKey(target));decimal raw;
        if(sKey.Length>=3&&tKey.Length>=3&&sKey.Equals(tKey,StringComparison.OrdinalIgnoreCase))raw=100m;
        else
        {
            var references=References(source);
            if(references.Any(r=>r.Contains(ExtractLogicalKey(target),StringComparison.OrdinalIgnoreCase)||(!string.IsNullOrWhiteSpace(target.ExternalReference)&&r.Equals(target.ExternalReference,StringComparison.OrdinalIgnoreCase))))raw=97m;
            else if(source.Name.Contains(ExtractLogicalKey(target),StringComparison.OrdinalIgnoreCase)||target.Name.Contains(ExtractLogicalKey(source),StringComparison.OrdinalIgnoreCase))raw=94m;
            else
            {
                var a=Tokens(source.Name);var b=Tokens(target.Name);if(a.Count==0||b.Count==0)return 0m;var overlap=a.Intersect(b,StringComparer.OrdinalIgnoreCase).Count();var union=a.Union(b,StringComparer.OrdinalIgnoreCase).Count();var j=(decimal)overlap/Math.Max(1,union);var contain=(decimal)overlap/Math.Max(1,Math.Min(a.Count,b.Count));raw=Math.Round(Math.Clamp(j*70m+contain*30m,0m,92m),1);
            }
        }

        var sourceDomain=MetaString(source,"domainId");var targetDomain=MetaString(target,"domainId");
        if(!string.IsNullOrWhiteSpace(sourceDomain)&&!string.IsNullOrWhiteSpace(targetDomain))
            raw=sourceDomain.Equals(targetDomain,StringComparison.OrdinalIgnoreCase)?Math.Min(100m,raw+3m):Math.Max(0m,raw-15m);

        var sourceKind=KindFamily(MetaString(source,"kind"));var targetKind=KindFamily(MetaString(target,"kind"));
        if(sourceKind.Length>0&&sourceKind.Equals(targetKind,StringComparison.OrdinalIgnoreCase))raw=Math.Min(100m,raw+2m);
        return raw;
    }

    private static string Reason(DevelopmentGraphNode s,DevelopmentGraphNode t)
    {
        var sk=ExtractLogicalKey(s);var tk=ExtractLogicalKey(t);var baseReason=Normalize(sk)==Normalize(tk)?$"Exact logical key match '{sk}'.":References(s).Any(r=>r.Contains(tk,StringComparison.OrdinalIgnoreCase))?$"Source reference explicitly mentions '{tk}'.":s.Name.Contains(tk,StringComparison.OrdinalIgnoreCase)?$"Source title explicitly mentions target key '{tk}'.":"Normalized title-token similarity.";
        var sd=MetaString(s,"domainId");var td=MetaString(t,"domainId");if(!string.IsNullOrWhiteSpace(sd)&&!string.IsNullOrWhiteSpace(td))baseReason+=sd.Equals(td,StringComparison.OrdinalIgnoreCase)?$" Same domain '{sd}'.":$" Cross-domain candidate ({sd} -> {td}) was penalized.";
        return baseReason;
    }

    private static EntityMatchDto ToMatch(DevelopmentGraphNode s,DevelopmentGraphNode? t,decimal confidence,string resolution,string reason)=>new(s.Type.ToString(),Strip(s.Key),s.Name,s.Provider??"Unknown",t?.Type.ToString(),t is null?null:Strip(t.Key),t?.Name,confidence,resolution,reason,s.LastObservedAtUtc);
    private static string ExtractLogicalKey(DevelopmentGraphNode n){var meta=MetaString(n,"Key");if(!string.IsNullOrWhiteSpace(meta))return meta;var sourceObjectId=MetaString(n,"sourceObjectId");if(!string.IsNullOrWhiteSpace(sourceObjectId))return sourceObjectId;var k=Strip(n.Key);var parts=k.Split(':',StringSplitOptions.RemoveEmptyEntries);return parts.Length>1?parts[^1]:k;}
    private static IReadOnlyList<string> References(DevelopmentGraphNode n){try{using var d=JsonDocument.Parse(n.MetadataJson);var prop=d.RootElement.EnumerateObject().FirstOrDefault(x=>x.Name.Equals("References",StringComparison.OrdinalIgnoreCase));if(prop.Value.ValueKind==JsonValueKind.Array)return prop.Value.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.String).Select(x=>x.GetString()??"").Where(x=>x.Length>0).ToArray();}catch{}return [];}
    private static HashSet<string> Tokens(string value)=>Regex.Matches(value.ToLowerInvariant(),@"[\p{L}\p{N}]+",RegexOptions.CultureInvariant).Select(x=>x.Value).Where(x=>x.Length>1&&!Stop.Contains(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Stop=new(["the","and","for","task","issue","work","fix","対応","作業","タスク","実装"],StringComparer.OrdinalIgnoreCase);
    private static string Normalize(string value)=>new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string MetaString(DevelopmentGraphNode n,string name){try{using var d=JsonDocument.Parse(n.MetadataJson);if(d.RootElement.ValueKind!=JsonValueKind.Object)return "";foreach(var p in d.RootElement.EnumerateObject())if(p.Name.Equals(name,StringComparison.OrdinalIgnoreCase)&&p.Value.ValueKind==JsonValueKind.String)return p.Value.GetString()??"";}catch{}return "";}
    private static string KindFamily(string kind){if(string.IsNullOrWhiteSpace(kind))return "";var i=kind.LastIndexOf('.');return i>=0&&i<kind.Length-1?kind[(i+1)..]:kind;}
    private static string Strip(string value){var i=value.LastIndexOf(':');return i>=0?value[(i+1)..]:value;}
}
