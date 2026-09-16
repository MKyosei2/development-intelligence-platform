// Purpose: Coordinates F ea tu re Re ad in es sS er vi ce application behavior for FeatureControl.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.FeatureControl;

public sealed record UpsertFeatureCommand(Guid ProjectId,string Key,string Name,string? MilestoneKey=null,decimal BaseRisk=20m,string Status="Active",string MetadataJson="{}");
public sealed record UpsertAcceptanceCriterionCommand(Guid ProjectId,string FeatureKey,string CriterionKey,string Title,bool Required=true,decimal Weight=1m,IReadOnlyList<Guid>? EvidenceIds=null,string? Notes=null);
public sealed record AttachAcceptanceEvidenceCommand(Guid ProjectId,string FeatureKey,string CriterionKey,IReadOnlyList<Guid> EvidenceIds);
public sealed record CriterionReadiness(string Key,string Title,bool Required,decimal Weight,string State,IReadOnlyList<Guid> EvidenceIds,IReadOnlyList<string> EvidenceSummary);
public sealed record FeatureReadinessResult(string FeatureKey,string Name,string State,decimal ReadinessPercent,int PassedCriteria,int TotalCriteria,int RequiredFailures,IReadOnlyList<CriterionReadiness> Criteria,IReadOnlyList<string> Blockers,IReadOnlyList<string> Actions,string Summary);

public sealed class FeatureReadinessService
{
    private readonly DevelopmentGraphService _graph;private readonly IDevelopmentGraphRepository _repo;private readonly IEvidenceRepository _evidence;private readonly IP2IntelligenceRepository _records;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public FeatureReadinessService(DevelopmentGraphService graph,IDevelopmentGraphRepository repo,IEvidenceRepository evidence,IP2IntelligenceRepository records,IUnitOfWork uow,ISystemClock clock){_graph=graph;_repo=repo;_evidence=evidence;_records=records;_uow=uow;_clock=clock;}

    public async Task<DevelopmentGraphNodeDto> UpsertFeatureAsync(UpsertFeatureCommand c,CancellationToken ct)
    {
        var metadata=Merge(c.MetadataJson,new Dictionary<string,object?>{{"FeatureKey",c.Key},{"BaseRisk",Math.Clamp(c.BaseRisk,0m,100m)}});var feature=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.Feature,$"feature:{c.Key}",c.Name,c.Status,"DIP",null,metadata),ct);
        if(!string.IsNullOrWhiteSpace(c.MilestoneKey)){var milestone=await _repo.FindNodeAsync(c.ProjectId,DevelopmentGraphNodeType.Milestone,$"milestone:{c.MilestoneKey}",ct);if(milestone is not null)await _graph.LinkAsync(new(c.ProjectId,milestone.Id,feature.Id,DevelopmentGraphEdgeType.Contains,80m,false,"{}"),ct);}
        return feature;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertCriterionAsync(UpsertAcceptanceCriterionCommand c,CancellationToken ct)
    {
        var feature=await RequireFeature(c.ProjectId,c.FeatureKey,ct);var evidence=c.EvidenceIds??Array.Empty<Guid>();var criterion=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.AcceptanceCriterion,$"criterion:{c.FeatureKey}:{c.CriterionKey}",c.Title,evidence.Count>0?"EvidenceAttached":"Unverified","DIP",null,JsonSerializer.Serialize(new{c.FeatureKey,c.CriterionKey,c.Required,Weight=Math.Clamp(c.Weight,0.1m,100m),EvidenceIds=evidence,Notes=c.Notes})),ct);await _graph.LinkAsync(new(c.ProjectId,feature.Id,criterion.Id,DevelopmentGraphEdgeType.Contains,c.Required?100m:60m,c.Required,"{}"),ct);return criterion;
    }

    public async Task<DevelopmentGraphNodeDto> AttachEvidenceAsync(AttachAcceptanceEvidenceCommand c,CancellationToken ct)
    {
        var criterion=await _repo.FindNodeAsync(c.ProjectId,DevelopmentGraphNodeType.AcceptanceCriterion,$"criterion:{c.FeatureKey}:{c.CriterionKey}",ct)??throw new KeyNotFoundException($"Acceptance criterion '{c.CriterionKey}' was not found for feature '{c.FeatureKey}'.");
        using var doc=JsonDocument.Parse(criterion.MetadataJson);var root=doc.RootElement;var required=root.TryGetProperty("Required",out var r)&&r.GetBoolean();var weight=root.TryGetProperty("Weight",out var w)&&w.TryGetDecimal(out var wd)?wd:1m;var notes=root.TryGetProperty("Notes",out var n)&&n.ValueKind==JsonValueKind.String?n.GetString():null;
        var updated=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.AcceptanceCriterion,criterion.Key,criterion.Name,c.EvidenceIds.Count>0?"EvidenceAttached":"Unverified","DIP",criterion.ExternalReference,JsonSerializer.Serialize(new{FeatureKey=c.FeatureKey,CriterionKey=c.CriterionKey,Required=required,Weight=weight,EvidenceIds=c.EvidenceIds,Notes=notes})),ct);
        var evidence=await _evidence.ListForProjectAsync(c.ProjectId,null,ct);foreach(var e in evidence.Where(x=>c.EvidenceIds.Contains(x.Id))){var evidenceNode=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.Evidence,$"evidence:{e.Id}",e.Title,e.Outcome.ToString(),e.SourceType.ToString(),e.SourceReference,JsonSerializer.Serialize(new{e.EvidenceKind,e.Outcome,e.Summary,e.ObservedAtUtc})),ct);await _graph.LinkAsync(new(c.ProjectId,updated.Id,evidenceNode.Id,DevelopmentGraphEdgeType.VerifiedBy,e.Outcome==EvidenceOutcome.Succeeded?100m:70m,required,JsonSerializer.Serialize(new{evidenceId=e.Id})),ct);}return updated;
    }

    public async Task<FeatureReadinessResult> EvaluateAsync(Guid projectId,string featureKey,int evidenceFreshnessDays=30,CancellationToken ct=default)
    {
        var feature=await RequireFeature(projectId,featureKey,ct);var edges=await _repo.ListEdgesAsync(projectId,ct);var criterionIds=edges.Where(x=>x.SourceNodeId==feature.Id&&x.Type==DevelopmentGraphEdgeType.Contains).Select(x=>x.TargetNodeId).ToHashSet();var nodes=await _repo.ListNodesAsync(projectId,DevelopmentGraphNodeType.AcceptanceCriterion,ct);var criteria=nodes.Where(x=>criterionIds.Contains(x.Id)).ToArray();var evidence=await _evidence.ListForProjectAsync(projectId,_clock.UtcNow.AddDays(-Math.Clamp(evidenceFreshnessDays,1,365)),ct);var evidenceById=evidence.ToDictionary(x=>x.Id);var resultCriteria=new List<CriterionReadiness>();decimal totalWeight=0,passedWeight=0;var requiredFailures=0;var blockers=new List<string>();var actions=new List<string>();
        foreach(var c in criteria)
        {
            using var doc=JsonDocument.Parse(c.MetadataJson);var root=doc.RootElement;var key=root.TryGetProperty("CriterionKey",out var k)?k.GetString()??c.Key:c.Key;var required=root.TryGetProperty("Required",out var rr)&&rr.GetBoolean();var weight=root.TryGetProperty("Weight",out var ww)&&ww.TryGetDecimal(out var wv)?Math.Max(.1m,wv):1m;var ids=root.TryGetProperty("EvidenceIds",out var ei)&&ei.ValueKind==JsonValueKind.Array?ei.EnumerateArray().Where(x=>x.TryGetGuid(out _)).Select(x=>x.GetGuid()).Distinct().ToArray():[];var linked=ids.Where(evidenceById.ContainsKey).Select(id=>evidenceById[id]).ToArray();var failed=linked.Any(x=>x.Outcome==EvidenceOutcome.Failed);var success=linked.Any(x=>x.Outcome==EvidenceOutcome.Succeeded)&&!failed;var warning=!failed&&!success&&linked.Any(x=>x.Outcome==EvidenceOutcome.Warning);var state=failed?"Failed":success?"Passed":warning?"Warning":"Unverified";await _graph.UpsertNodeAsync(new(projectId,DevelopmentGraphNodeType.AcceptanceCriterion,c.Key,c.Name,state,c.Provider,c.ExternalReference,c.MetadataJson),ct);foreach(var e in linked){var evidenceNode=await _graph.UpsertNodeAsync(new(projectId,DevelopmentGraphNodeType.Evidence,$"evidence:{e.Id}",e.Title,e.Outcome.ToString(),e.SourceType.ToString(),e.SourceReference,JsonSerializer.Serialize(new{e.EvidenceKind,e.Outcome,e.Summary,e.ObservedAtUtc})),ct);await _graph.LinkAsync(new(projectId,c.Id,evidenceNode.Id,DevelopmentGraphEdgeType.VerifiedBy,e.Outcome==EvidenceOutcome.Succeeded?100m:70m,required,JsonSerializer.Serialize(new{evidenceId=e.Id})),ct);}totalWeight+=weight;if(success)passedWeight+=weight;if(required&&!success){requiredFailures++;blockers.Add($"Required criterion '{c.Name}' is {state.ToLowerInvariant()}.");actions.Add($"Provide fresh passing evidence for '{c.Name}'.");}resultCriteria.Add(new(key,c.Name,required,weight,state,ids,linked.Select(x=>$"{x.Outcome}: {x.Title}").Take(6).ToArray()));
        }
        var percent=criteria.Length==0?0m:decimal.Round(totalWeight==0?0m:passedWeight/totalWeight*100m,2);var stateResult=criteria.Length==0?ReadinessState.Unknown:requiredFailures>0?ReadinessState.NotReady:percent>=95m?ReadinessState.Ready:ReadinessState.NeedsAttention;var summary=$"Feature '{feature.Name}' readiness is {percent:0.##}% with {resultCriteria.Count(x=>x.State=="Passed")}/{criteria.Length} acceptance criteria passing and {requiredFailures} required gap(s).";var result=new FeatureReadinessResult(featureKey,feature.Name,stateResult.ToString(),percent,resultCriteria.Count(x=>x.State=="Passed"),criteria.Length,requiredFailures,resultCriteria,blockers,actions.Distinct().ToArray(),summary);
        await _graph.UpsertNodeAsync(new(projectId,DevelopmentGraphNodeType.Feature,feature.Key,feature.Name,stateResult.ToString(),feature.Provider,feature.ExternalReference,Merge(feature.MetadataJson,new Dictionary<string,object?>{{"ReadinessPercent",percent},{"RequiredFailures",requiredFailures},{"EvaluatedAtUtc",_clock.UtcNow}})),ct);
        await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),projectId,$"feature:{featureKey}",P2IntelligenceKind.FeatureReadiness,requiredFailures>0?P2IntelligenceStatus.Warning:P2IntelligenceStatus.Informational,100m-percent,EvidenceConfidence.High,summary,JsonSerializer.Serialize(result),feature.ExternalReference,_clock.UtcNow),ct);await _uow.SaveChangesAsync(ct);return result;
    }

    private async Task<DevelopmentGraphNode> RequireFeature(Guid projectId,string key,CancellationToken ct)=>await _repo.FindNodeAsync(projectId,DevelopmentGraphNodeType.Feature,$"feature:{key}",ct)??throw new KeyNotFoundException($"Feature '{key}' was not found.");
    private static string Merge(string json,IReadOnlyDictionary<string,object?> additions){Dictionary<string,object?> map;try{map=JsonSerializer.Deserialize<Dictionary<string,object?>>(string.IsNullOrWhiteSpace(json)?"{}":json)??new();}catch{map=new();}foreach(var kv in additions)map[kv.Key]=kv.Value;return JsonSerializer.Serialize(map);}
}
