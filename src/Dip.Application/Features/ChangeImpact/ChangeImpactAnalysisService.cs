// Purpose: Coordinates C ha ng eI mp ac tA na ly si sS er vi ce application behavior for ChangeImpact.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ChangeImpact;

public sealed record ChangeImpactCommand(Guid ProjectId,string SubjectKey,IReadOnlyList<string> ChangedNodeKeys,int MaxDepth=4,int MaxNodes=250);
public sealed record ImpactedGraphNode(Guid Id,string Type,string Key,string Name,int Depth,decimal ImpactScore,IReadOnlyList<string> Reasons);
public sealed record ChangeImpactResult(string RiskLevel,decimal RiskScore,int BlastRadius,int CriticalPaths,IReadOnlyDictionary<string,int> AffectedByType,IReadOnlyList<ImpactedGraphNode> Affected,IReadOnlyList<string> Recommendations,string Summary);

public sealed class ChangeImpactAnalysisService
{
    private readonly IDevelopmentGraphRepository _graph;private readonly IP2IntelligenceRepository _records;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public ChangeImpactAnalysisService(IDevelopmentGraphRepository graph,IP2IntelligenceRepository records,IUnitOfWork uow,ISystemClock clock){_graph=graph;_records=records;_uow=uow;_clock=clock;}

    public async Task<ChangeImpactResult> AnalyzeAsync(ChangeImpactCommand c,CancellationToken ct)
    {
        if(c.ChangedNodeKeys.Count==0)throw new ArgumentException("At least one changed graph node key is required.",nameof(c));
        var nodes=await _graph.ListNodesAsync(c.ProjectId,null,ct);var edges=await _graph.ListEdgesAsync(c.ProjectId,ct);var byId=nodes.ToDictionary(x=>x.Id);var seeds=nodes.Where(n=>c.ChangedNodeKeys.Contains(n.Key,StringComparer.OrdinalIgnoreCase)).ToArray();
        if(seeds.Length==0)throw new KeyNotFoundException("None of the supplied changed-node keys exist in the Development Graph.");
        var queue=new Queue<(Guid Id,int Depth,decimal Score,string Reason)>();var best=new Dictionary<Guid,(int Depth,decimal Score,List<string> Reasons)>();foreach(var s in seeds)queue.Enqueue((s.Id,0,100m,"Directly changed."));
        var maxDepth=Math.Clamp(c.MaxDepth,1,8);var maxNodes=Math.Clamp(c.MaxNodes,10,2000);var criticalPaths=0;
        while(queue.Count>0&&best.Count<maxNodes)
        {
            ct.ThrowIfCancellationRequested();var item=queue.Dequeue();if(item.Depth>maxDepth)continue;
            if(best.TryGetValue(item.Id,out var prior)&&prior.Score>=item.Score){if(!prior.Reasons.Contains(item.Reason,StringComparer.Ordinal))prior.Reasons.Add(item.Reason);continue;}
            best[item.Id]=(item.Depth,item.Score,[item.Reason]);
            foreach(var edge in edges.Where(e=>e.SourceNodeId==item.Id||e.TargetNodeId==item.Id))
            {
                var next=edge.SourceNodeId==item.Id?edge.TargetNodeId:edge.SourceNodeId;if(!byId.ContainsKey(next))continue;
                var direction=edge.SourceNodeId==item.Id?"outgoing":"incoming";var multiplier=EdgeMultiplier(edge.Type);var nextScore=Math.Clamp(item.Score*multiplier*(0.45m+edge.Weight/180m)+(edge.Critical?12m:0m),0m,100m);if(nextScore<8m)continue;
                if(edge.Critical)criticalPaths++;queue.Enqueue((next,item.Depth+1,nextScore,$"{direction} {edge.Type} relation from {byId[item.Id].Name}{(edge.Critical?" (critical)":"")}."));
            }
        }
        foreach(var s in seeds)best.Remove(s.Id);
        var affected=best.Select(kv=>{var n=byId[kv.Key];return new ImpactedGraphNode(n.Id,n.Type.ToString(),n.Key,n.Name,kv.Value.Depth,decimal.Round(kv.Value.Score,2),kv.Value.Reasons);}).OrderByDescending(x=>x.ImpactScore).ThenBy(x=>x.Depth).Take(maxNodes).ToArray();
        var byType=affected.GroupBy(x=>x.Type).ToDictionary(x=>x.Key,x=>x.Count(),StringComparer.OrdinalIgnoreCase);var high=affected.Count(x=>x.ImpactScore>=65m);var critical=affected.Count(x=>x.ImpactScore>=85m);var score=Math.Clamp(decimal.Round(Math.Min(65m,affected.Length*2.3m)+Math.Min(20m,high*4m)+Math.Min(15m,criticalPaths*2m),2),0m,100m);
        var level=score>=85?ProjectControlRiskLevel.Critical:score>=65?ProjectControlRiskLevel.High:score>=35?ProjectControlRiskLevel.Medium:ProjectControlRiskLevel.Low;var recommendations=new List<string>();if(byType.TryGetValue(nameof(DevelopmentGraphNodeType.Test),out var tests)&&tests>0)recommendations.Add($"Run the {tests} graph-linked impacted test(s), prioritizing the highest-impact paths.");if(byType.TryGetValue(nameof(DevelopmentGraphNodeType.Feature),out var features)&&features>0)recommendations.Add($"Revalidate {features} affected feature(s) before merge/release.");if(byType.TryGetValue(nameof(DevelopmentGraphNodeType.Asset),out var assets)&&assets>0)recommendations.Add($"Run asset/import validation for {assets} affected asset node(s).");if(criticalPaths>0)recommendations.Add("Require explicit review for the critical dependency paths reached by this change.");if(recommendations.Count==0)recommendations.Add("No broad dependency propagation was found; retain the normal QA plan.");
        var summary=$"{level} change impact: {affected.Length} graph node(s) are within blast radius {maxDepth}; {criticalPaths} critical relation traversal(s).";var result=new ChangeImpactResult(level.ToString(),score,affected.Length,criticalPaths,byType,affected,recommendations,summary);
        await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),c.ProjectId,c.SubjectKey,P2IntelligenceKind.ChangeImpact,level>=ProjectControlRiskLevel.High?P2IntelligenceStatus.Warning:P2IntelligenceStatus.Informational,score,EvidenceConfidence.High,summary,JsonSerializer.Serialize(result),null,_clock.UtcNow),ct);
        await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),c.ProjectId,c.SubjectKey,P2IntelligenceKind.BlastRadius,level>=ProjectControlRiskLevel.High?P2IntelligenceStatus.Warning:P2IntelligenceStatus.Informational,Math.Min(100m,affected.Length),EvidenceConfidence.High,$"Blast radius contains {affected.Length} affected development-graph node(s).",JsonSerializer.Serialize(new{affected=affected.Select(x=>new{x.Type,x.Key,x.Depth,x.ImpactScore}),byType,criticalPaths}),null,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);return result;
    }

    internal static decimal EdgeMultiplier(DevelopmentGraphEdgeType type)=>type switch
    {
        DevelopmentGraphEdgeType.DependsOn or DevelopmentGraphEdgeType.Requires or DevelopmentGraphEdgeType.Blocks=>0.92m,
        DevelopmentGraphEdgeType.Affects or DevelopmentGraphEdgeType.Impacts or DevelopmentGraphEdgeType.RegressedBy=>0.88m,
        DevelopmentGraphEdgeType.TestedBy or DevelopmentGraphEdgeType.VerifiedBy=>0.78m,
        DevelopmentGraphEdgeType.Contains or DevelopmentGraphEdgeType.BelongsTo=>0.72m,
        DevelopmentGraphEdgeType.Changes=>0.65m,
        _=>0.58m
    };
}
