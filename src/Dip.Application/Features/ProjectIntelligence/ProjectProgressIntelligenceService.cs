// Purpose: Reconstructs project progress by reconciling plan data, operational observations, and machine evidence.
using System.IO;

using System.Text.Json;

using Dip.Application.Abstractions.Persistence;

using Dip.Application.Abstractions.Time;

using Dip.Application.Features.ProjectTruth;

using Dip.Domain.Entities;

using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectIntelligence;

public sealed class ProjectProgressIntelligenceService
{

    private readonly IDevelopmentGraphRepository _graph;
    private readonly IEvidenceRepository _evidence;
    private readonly ISystemClock _clock;
    private readonly ProjectEntityResolutionService _resolver;

    public ProjectProgressIntelligenceService(IDevelopmentGraphRepository graph,IEvidenceRepository evidence,ISystemClock clock,ProjectEntityResolutionService resolver){
        _graph=graph;
        _evidence=evidence;
        _clock=clock;
        _resolver=resolver;
    }

    public async Task<ProjectIntelligenceLayerDto> GetLayerAsync(Guid projectId,CancellationToken ct)
    {
        var progress=await GetProgressAsync(projectId,ct);
        var gantt=BuildGantt(projectId,progress);
        var resolution=await _resolver.ResolveProjectAsync(projectId,false,ct);
        var digest=BuildDigest(projectId,progress);
        return new(projectId,progress,gantt,resolution,digest,_clock.UtcNow);
    }

    public async Task<ProjectProgressOverviewDto> GetProgressAsync(Guid projectId,CancellationToken ct)
    {

        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        var nodes=await _graph.ListNodesAsync(projectId,null,ct);
        var edges=await _graph.ListEdgesAsync(projectId,ct);
        var evidence=(await _evidence.ListForProjectAsync(projectId,_clock.UtcNow.AddDays(-120),ct)).OrderByDescending(x=>x.ObservedAtUtc).Take(10000).ToArray();
        var plan=LatestPlanItems(nodes).ToArray();
        var results=new List<ReconstructedProgressDto>();
        var contradictions=new List<PlanActualContradictionDto>();

        foreach(var item in plan)
        {

            var meta=Meta(item);
            var logical=Str(meta,"Key")??Strip(item.Key);
            var target=EquivalentTarget(item,nodes,edges);
            var targetKey=target is null?null:$"{target.Type}:{Strip(target.Key)}";
            var related=target is null?Array.Empty<DevelopmentGraphNode>():EquivalentSources(target.Id,nodes,edges).Where(x=>x.Id!=item.Id).ToArray();
            var observations=related.Where(x=>x.Type==DevelopmentGraphNodeType.ExternalObservation).ToArray();

            var planned=Dec(meta,"DeclaredProgressPercent");
            var operational=OperationalPercent(target,observations);
            var relevantEvidence=RelevantEvidence(logical,target,evidence).Take(30).ToArray();
            var machine=MachineReadiness(target,related,edges,nodes,relevantEvidence);
            var confidence=ReadinessConfidence(machine,related,relevantEvidence);
            var ev=BuildEvidence(observations,related,relevantEvidence).Take(30).ToArray();
            var sources=BuildSources(item,observations).Take(30).ToArray();
            var itemContradictions=new List<string>();
            var stale=new List<string>();

            var reportedSources=observations.Select(x=>(Node:x,Progress:ObservationPercent(x))).Where(x=>x.Progress.HasValue).ToArray();
            if(reportedSources.Length>=2&&reportedSources.Max(x=>x.Progress!.Value)-reportedSources.Min(x=>x.Progress!.Value)>=25m){
                var detail=string.Join(", ",reportedSources.OrderByDescending(x=>x.Node.LastObservedAtUtc).Take(6).Select(x=>$"{x.Node.Provider??"source"} {x.Progress!.Value:0.#}%"));
                itemContradictions.Add($"Operational sources disagree materially: {detail}.");
            }
            if(observations.Any(x=>IsClaimedComplete(x.Status))&&observations.Any(x=>ClaimStatusNormalizer.Classify(x.Status)==ClaimSemanticState.Blocked))itemContradictions.Add("Operational sources conflict: at least one source reports completion while another reports a blocker.");

            if(planned.HasValue&&operational.HasValue&&Math.Abs(planned.Value-operational.Value)>=25m)itemContradictions.Add($"Plan reports {planned:0.#}% while operational source reports {operational:0.#}%.");

            if(planned>=100m&&machine.HasValue&&machine.Value<80m)itemContradictions.Add($"Plan marks complete, but machine evidence readiness is only {machine:0.#}%.");

            if(observations.Any(x=>IsClaimedComplete(x.Status))&&!HasStrongEvidence(relevantEvidence,related,edges,nodes))itemContradictions.Add("A collaboration/issue source claims completion without strong machine-verifiable completion evidence.");

            if(HasFailedCi(target,related,edges,nodes)&&((planned??0m)>=80m||(operational??0m)>=80m))itemContradictions.Add("High reported progress conflicts with a failing CI/build signal.");

            var latestSignal=new[]{
                item.LastObservedAtUtc
            }
            .Concat(observations.Select(x=>x.LastObservedAtUtc)).Concat(relevantEvidence.Select(x=>x.ObservedAtUtc)).Max();
            if(!IsComplete(planned,operational,machine,target?.Status)&&_clock.UtcNow-latestSignal>TimeSpan.FromDays(7))stale.Add($"No fresh progress/evidence signal for {(_clock.UtcNow-latestSignal).Days} days.");

            var due=Date(meta,"DueAtUtc");
            if(due.HasValue&&due.Value<_clock.UtcNow&&!IsComplete(planned,operational,machine,target?.Status))stale.Add($"Planned due date {due.Value:yyyy-MM-dd} has passed without verified completion.");

            var status=Status(itemContradictions,stale,due,machine,planned,operational);
            results.Add(new(logical,item.Name,Str(meta,"ParentKey"),Str(meta,"Owner"),Date(meta,"PlannedStartUtc"),due,planned,operational,machine,confidence,status,targetKey,Bool(meta,"IsMilestone"),Strings(meta,"DependencyKeys"),itemContradictions,stale,ev,sources));

            foreach(var text in itemContradictions)contradictions.Add(new(logical,status=="AtRisk"?"High":"Medium",item.Name,text,"Open the linked source evidence and reconcile the plan/operational state before using the reported percentage for a management decision.",sources.Select(x=>$"{x.Provider}:{x.SourceId}").Take(8).ToArray()));

        }

        var unverified=results.Count(x=>x.Contradictions.Any(y=>y.Contains("without strong machine",StringComparison.OrdinalIgnoreCase)));
        return new(projectId,results.Count,results.Count(x=>x.Status=="OnTrack"),results.Count(x=>x.Status=="Watch"),results.Count(x=>x.Status=="AtRisk"),unverified,results.Count(x=>x.StaleSignals.Count>0),results.OrderByDescending(x=>Rank(x.Status)).ThenBy(x=>x.DueAtUtc??DateTimeOffset.MaxValue).ToArray(),contradictions.Take(500).ToArray(),_clock.UtcNow);

    }

    public async Task<WbsGanttIntelligenceDto> GetGanttAsync(Guid projectId,CancellationToken ct)=>BuildGantt(projectId,await GetProgressAsync(projectId,ct));

    public async Task<ProjectManagementDigestDto> GetDigestAsync(Guid projectId,CancellationToken ct)=>BuildDigest(projectId,await GetProgressAsync(projectId,ct));

    private WbsGanttIntelligenceDto BuildGantt(Guid projectId,ProjectProgressOverviewDto p)
    {

        var rows=p.Items.Select(x=>new GanttIntelligenceItemDto(x.PlanKey,x.Name,x.ParentKey,x.Owner,x.PlannedStartUtc,x.DueAtUtc,x.PlannedPercent,x.EvidenceReadinessPercent,x.Status,x.ReadinessConfidence,x.IsMilestone,x.Dependencies,x.Contradictions.Concat(x.StaleSignals).Take(8).ToArray())).ToArray();
        var start=rows.Where(x=>x.StartUtc.HasValue).Select(x=>x.StartUtc!.Value).DefaultIfEmpty().Min();
        var end=rows.Where(x=>x.DueAtUtc.HasValue).Select(x=>x.DueAtUtc!.Value).DefaultIfEmpty().Max();
        return new(projectId,start==default?null:start,end==default?null:end,rows,_clock.UtcNow);

    }

    private ProjectManagementDigestDto BuildDigest(Guid projectId,ProjectProgressOverviewDto p)
    {

        var overall=p.AtRisk>0?"AtRisk":p.Watch>0||p.UnverifiedClaims>0?"Watch":"OnTrack";
        var attention=p.Items.Where(x=>x.Status=="AtRisk").Take(8).Select(x=>$"{x.Name}: {x.Contradictions.Concat(x.StaleSignals).FirstOrDefault()??"requires review"}").ToArray();
        var gaps=p.Items.Where(x=>x.ReadinessConfidence is "Low" or "Unverified").Take(8).Select(x=>$"{x.Name}: reported progress lacks enough independent implementation/CI/QA evidence.").ToArray();
        var diffs=p.Contradictions.Take(8).Select(x=>$"{x.Title}: {x.Explanation}").ToArray();
        var recent=p.Items.Where(x=>x.Status=="OnTrack"&&((x.EvidenceReadinessPercent??0m)>0m||(x.OperationalPercent??0m)>0m)).Take(8).Select(x=>$"{x.Name}: plan {x.PlannedPercent:0.#}% / operational {x.OperationalPercent:0.#}% / evidence {x.EvidenceReadinessPercent:0.#}% ({x.ReadinessConfidence}).").ToArray();
        var actions=new List<string>();
        if(p.AtRisk>0)actions.Add($"Reconcile {p.AtRisk} at-risk WBS item(s) against Redmine/GitHub/CI evidence.");
        if(p.UnverifiedClaims>0)actions.Add($"Request concrete evidence for {p.UnverifiedClaims} completion claim(s) before treating them as done.");
        if(p.StaleItems>0)actions.Add($"Confirm or re-plan {p.StaleItems} stale item(s) with no fresh source signal.");
        if(actions.Count==0)actions.Add("No immediate plan-versus-actual reconciliation action is required.");
        var summary=$"{p.PlanItems} WBS items: {p.OnTrack} on track, {p.Watch} watch, {p.AtRisk} at risk. {p.UnverifiedClaims} completion claim(s) remain insufficiently verified and {p.StaleItems} item(s) are stale.";
        return new(projectId,overall,summary,attention,gaps,diffs,recent,actions,_clock.UtcNow);

    }

    private static IEnumerable<DevelopmentGraphNode> LatestPlanItems(IReadOnlyList<DevelopmentGraphNode> nodes){
        var import=nodes.Where(x=>x.Type==DevelopmentGraphNodeType.ProjectPlanImport&&x.Status.Equals("Active",StringComparison.OrdinalIgnoreCase)).OrderByDescending(x=>x.LastObservedAtUtc).FirstOrDefault();
        if(import is null)return [];
        var key=Str(Meta(import),"ImportKey");
        return nodes.Where(x=>x.Type==DevelopmentGraphNodeType.PlanItem&&string.Equals(Str(Meta(x),"ImportKey"),key,StringComparison.OrdinalIgnoreCase)).OrderBy(x=>Date(Meta(x),"PlannedStartUtc")??DateTimeOffset.MaxValue).ThenBy(x=>x.Name);
    }

    private static DevelopmentGraphNode? EquivalentTarget(DevelopmentGraphNode source,IReadOnlyList<DevelopmentGraphNode> nodes,IReadOnlyList<DevelopmentGraphEdge> edges){
        var edge=edges.Where(x=>x.SourceNodeId==source.Id&&x.Type==DevelopmentGraphEdgeType.EquivalentTo).OrderByDescending(x=>x.LastObservedAtUtc).FirstOrDefault();
        return edge is null?null:nodes.FirstOrDefault(x=>x.Id==edge.TargetNodeId);
    }

    private static IEnumerable<DevelopmentGraphNode> EquivalentSources(Guid targetId,IReadOnlyList<DevelopmentGraphNode> nodes,IReadOnlyList<DevelopmentGraphEdge> edges){
        var ids=edges.Where(x=>x.TargetNodeId==targetId&&x.Type==DevelopmentGraphEdgeType.EquivalentTo).Select(x=>x.SourceNodeId).ToHashSet();
        return nodes.Where(x=>ids.Contains(x.Id));
    }

    private static decimal? OperationalPercent(DevelopmentGraphNode? target,IReadOnlyList<DevelopmentGraphNode> observations){
        if(target is not null&&Dec(Meta(target),"CompletionPercent") is {
        }
        direct)return direct;
        var formal=observations.Where(x=>x.Provider?.Equals("Redmine",StringComparison.OrdinalIgnoreCase)==true).Select(x=>(x.LastObservedAtUtc,Value:ObservationPercent(x))).Where(x=>x.Value.HasValue).OrderByDescending(x=>x.LastObservedAtUtc).FirstOrDefault();
        if(formal.Value.HasValue)return formal.Value;
        return observations.Select(x=>(x.LastObservedAtUtc,Value:ObservationPercent(x))).Where(x=>x.Value.HasValue).OrderByDescending(x=>x.LastObservedAtUtc).Select(x=>x.Value).FirstOrDefault();
    }

    private static decimal? ObservationPercent(DevelopmentGraphNode observation){
        var p=Dec(Meta(observation),"ProgressPercent");
        return p??(IsClaimedComplete(observation.Status)?100m:null);
    }

    private static decimal? MachineReadiness(DevelopmentGraphNode? target,IReadOnlyList<DevelopmentGraphNode> related,IReadOnlyList<DevelopmentGraphEdge> edges,IReadOnlyList<DevelopmentGraphNode> nodes,IReadOnlyList<EvidenceRecord> evidence)
    {

        var signals=new List<(decimal Score,int Weight)>();
        foreach(var e in evidence.Where(IsMachine)){
            var score=e.Outcome switch{
                EvidenceOutcome.Succeeded=>100m,EvidenceOutcome.Changed=>75m,EvidenceOutcome.Informational=>70m,EvidenceOutcome.Warning=>55m,EvidenceOutcome.Failed=>20m,_=>50m
            }
            ;
            signals.Add((score,e.Confidence switch{
                EvidenceConfidence.Verified=>5,EvidenceConfidence.High=>4,EvidenceConfidence.Medium=>3,_=>2
            }
            ));
        }

        foreach(var pr in related.Where(x=>x.Type==DevelopmentGraphNodeType.PullRequest)){
            signals.Add((pr.Status.Contains("merged",StringComparison.OrdinalIgnoreCase)||pr.Status.Contains("closed",StringComparison.OrdinalIgnoreCase)?90m:65m,3));
            foreach(var ci in CiFor(pr,edges,nodes))signals.Add((IsSuccess(ci.Status)?100m:IsFailure(ci.Status)?15m:60m,5));
        }

        if(target is not null&&IsClaimedComplete(target.Status))signals.Add((85m,2));
        if(signals.Count==0)return null;
        var total=signals.Sum(x=>x.Weight);
        return Math.Round(signals.Sum(x=>x.Score*x.Weight)/total,1);

    }

    private static IEnumerable<DevelopmentGraphNode> CiFor(DevelopmentGraphNode pr,IReadOnlyList<DevelopmentGraphEdge> edges,IReadOnlyList<DevelopmentGraphNode> nodes){
        var commits=edges.Where(x=>x.SourceNodeId==pr.Id&&x.Type==DevelopmentGraphEdgeType.Contains).Select(x=>x.TargetNodeId).ToHashSet();
        var ci=edges.Where(x=>commits.Contains(x.SourceNodeId)&&x.Type==DevelopmentGraphEdgeType.BuiltBy).Select(x=>x.TargetNodeId).ToHashSet();
        return nodes.Where(x=>ci.Contains(x.Id)&&x.Type==DevelopmentGraphNodeType.CiRun);
    }

    private static bool HasFailedCi(DevelopmentGraphNode? target,IReadOnlyList<DevelopmentGraphNode> related,IReadOnlyList<DevelopmentGraphEdge> edges,IReadOnlyList<DevelopmentGraphNode> nodes)=>related.Where(x=>x.Type==DevelopmentGraphNodeType.PullRequest).SelectMany(x=>CiFor(x,edges,nodes)).Any(x=>IsFailure(x.Status));

    private static bool HasStrongEvidence(IReadOnlyList<EvidenceRecord> evidence,IReadOnlyList<DevelopmentGraphNode> related,IReadOnlyList<DevelopmentGraphEdge> edges,IReadOnlyList<DevelopmentGraphNode> nodes)=>evidence.Any(x=>IsMachine(x)&&(int)x.Confidence>=(int)EvidenceConfidence.High&&x.Outcome==EvidenceOutcome.Succeeded)||related.Where(x=>x.Type==DevelopmentGraphNodeType.PullRequest).SelectMany(x=>CiFor(x,edges,nodes)).Any(x=>IsSuccess(x.Status));

    private static IEnumerable<EvidenceRecord> RelevantEvidence(string logical,DevelopmentGraphNode? target,IReadOnlyList<EvidenceRecord> evidence){
        var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase){
            logical,$"task:{logical}"
        }
        ;
        if(target is not null){
            keys.Add(Strip(target.Key));
            keys.Add(target.Key);
        }
        return evidence.Where(x=>keys.Contains(x.SubjectKey)||(!string.IsNullOrWhiteSpace(x.SourceReference)&&target?.ExternalReference is not null&&x.SourceReference.Contains(target.ExternalReference,StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<ProgressEvidenceDto> BuildEvidence(IReadOnlyList<DevelopmentGraphNode> obs,IReadOnlyList<DevelopmentGraphNode> related,IReadOnlyList<EvidenceRecord> evidence){
        foreach(var e in evidence)yield return new(e.SourceType.ToString(),e.EvidenceKind,e.Summary,e.Confidence.ToString(),e.SourceReference,e.ObservedAtUtc,IsMachine(e),string.IsNullOrWhiteSpace(e.SourceReference)?[]:[e.SourceReference]);
        foreach(var o in obs){
            var m=Meta(o);
            yield return new(o.Provider??"Unknown",Str(m,"Kind")??"source",$"{o.Status}{(Dec(m,"ProgressPercent") is { } p?$" {
                p:0.#
            }
            %":"")}",o.Provider?.Equals("Redmine",StringComparison.OrdinalIgnoreCase)==true?"Medium":"Low",o.ExternalReference,o.LastObservedAtUtc,false,Strings(m,"References"));
        }
        foreach(var pr in related.Where(x=>x.Type==DevelopmentGraphNodeType.PullRequest))yield return new(pr.Provider??"GitHub","pull-request",$"{pr.Name} [{pr.Status}]","High",pr.ExternalReference,pr.LastObservedAtUtc,true,string.IsNullOrWhiteSpace(pr.ExternalReference)?[]:[pr.ExternalReference]);
    }

    private static IEnumerable<SourceProvenanceDto> BuildSources(DevelopmentGraphNode plan,IReadOnlyList<DevelopmentGraphNode> obs){
        var pm=Meta(plan);
        yield return new("Excel/WBS",Str(pm,"SourceReference")??Strip(plan.Key),Str(pm,"SourceSha256")??"",plan.ExternalReference,plan.LastObservedAtUtc,"High",Str(pm,"SourceSha256")??"");
        foreach(var o in obs){
            var m=Meta(o);
            yield return new(o.Provider??"Unknown",Str(m,"ExternalId")??Strip(o.Key),Str(m,"Revision")??"",o.ExternalReference,o.LastObservedAtUtc,o.Provider?.Equals("Redmine",StringComparison.OrdinalIgnoreCase)==true?"Medium":"Low",Str(m,"RawSha256")??"",Field(m,"Fields","Channel"),Field(m,"Fields","Author"));
        }
    }

    private static string ReadinessConfidence(decimal? machine,IReadOnlyList<DevelopmentGraphNode> related,IReadOnlyList<EvidenceRecord> evidence){
        if(machine is null)return related.Any(x=>x.Type==DevelopmentGraphNodeType.ExternalObservation)?"Low":"Unverified";
        if(evidence.Any(x=>IsMachine(x)&&x.Confidence==EvidenceConfidence.Verified))return "Verified";
        if(evidence.Any(x=>IsMachine(x)&&(int)x.Confidence>=(int)EvidenceConfidence.High)||related.Any(x=>x.Type==DevelopmentGraphNodeType.PullRequest))return "High";
        return "Medium";
    }

    private static string Status(IReadOnlyList<string> contradictions,IReadOnlyList<string> stale,DateTimeOffset? due,decimal? machine,decimal? plan,decimal? operational){
        if(stale.Any(x=>x.Contains("due date",StringComparison.OrdinalIgnoreCase))||contradictions.Count>=2||contradictions.Any(x=>x.Contains("failing CI",StringComparison.OrdinalIgnoreCase)))return "AtRisk";
        if(contradictions.Count>0||stale.Count>0||(plan.HasValue&&machine.HasValue&&plan.Value-machine.Value>=20m))return "Watch";
        return "OnTrack";
    }

    private static bool IsComplete(decimal? plan,decimal? operational,decimal? machine,string? status)=>(machine>=90m&&(operational>=90m||plan>=100m))||(status is not null&&IsClaimedComplete(status)&&machine>=80m);

    private static bool IsClaimedComplete(string status)=>ClaimStatusNormalizer.Classify(status) is ClaimSemanticState.Complete or ClaimSemanticState.Ready;

    private static bool IsMachine(EvidenceRecord e)=>e.SourceType is EvidenceSourceType.GitHub or EvidenceSourceType.Build or EvidenceSourceType.Test or EvidenceSourceType.Quality or EvidenceSourceType.Runtime or EvidenceSourceType.Performance or EvidenceSourceType.Unity or EvidenceSourceType.Collector;

    private static bool IsSuccess(string s)=>s.Contains("success",StringComparison.OrdinalIgnoreCase)||s.Contains("passed",StringComparison.OrdinalIgnoreCase)||s.Contains("green",StringComparison.OrdinalIgnoreCase)||s.Contains("completed",StringComparison.OrdinalIgnoreCase);
    private static bool IsFailure(string s)=>s.Contains("fail",StringComparison.OrdinalIgnoreCase)||s.Contains("error",StringComparison.OrdinalIgnoreCase)||s.Contains("cancel",StringComparison.OrdinalIgnoreCase);

    private static int Rank(string status)=>status=="AtRisk"?3:status=="Watch"?2:1;
    private static string Strip(string value){
        var i=value.LastIndexOf(':');
        return i>=0?value[(i+1)..]:value;
    }

    private static JsonElement Meta(DevelopmentGraphNode n)
    {
        try
        {
            using var document = JsonDocument.Parse(n.MetadataJson);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Development Graph node '{n.Key}' contains invalid metadata JSON.",
                ex);
        }
    }

    private static string? Str(JsonElement m,string name)=>m.TryGetProperty(name,out var x)&&x.ValueKind==JsonValueKind.String?x.GetString():null;
    private static decimal? Dec(JsonElement m,string name)=>m.TryGetProperty(name,out var x)&&x.TryGetDecimal(out var d)?d:null;
    private static DateTimeOffset? Date(JsonElement m,string name)=>m.TryGetProperty(name,out var x)&&x.ValueKind==JsonValueKind.String&&DateTimeOffset.TryParse(x.GetString(),out var d)?d:null;
    private static bool Bool(JsonElement m,string name)=>m.TryGetProperty(name,out var x)&&(x.ValueKind is JsonValueKind.True or JsonValueKind.False)&&x.GetBoolean();
    private static IReadOnlyList<string> Strings(JsonElement m,string name)=>m.TryGetProperty(name,out var x)&&x.ValueKind==JsonValueKind.Array?x.EnumerateArray().Where(v=>v.ValueKind==JsonValueKind.String).Select(v=>v.GetString()??"").Where(v=>v.Length>0).ToArray():[];
    private static string? Field(JsonElement m,string obj,string name)=>m.TryGetProperty(obj,out var o)&&o.ValueKind==JsonValueKind.Object&&o.TryGetProperty(name,out var x)&&x.ValueKind==JsonValueKind.String?x.GetString():null;

}
