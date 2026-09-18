// Purpose: Orchestrates the DIP workflow for A ss et In te ll ig en ce Se rv ic e.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Assets;

public sealed record AssetObservationCommand(Guid ProjectId,string RepositoryKey,string Path,string AssetType,long SizeBytes,string? ContentHash,IReadOnlyList<string>? Dependencies,IReadOnlyList<string>? Issues,string? CommitSha,string? Platform,bool ImportSucceeded=true,bool MissingReference=false,bool PlatformIncompatible=false);
public sealed record AssetIntelligenceResult(string AssetKey,string State,decimal RiskScore,bool DuplicateContent,IReadOnlyList<string> Issues,IReadOnlyList<string> Actions,string Summary);

public sealed class AssetIntelligenceService
{
    private readonly DevelopmentGraphService _graph;private readonly IDevelopmentGraphRepository _repo;private readonly IP2IntelligenceRepository _records;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public AssetIntelligenceService(DevelopmentGraphService graph,IDevelopmentGraphRepository repo,IP2IntelligenceRepository records,IUnitOfWork uow,ISystemClock clock){_graph=graph;_repo=repo;_records=records;_uow=uow;_clock=clock;}
    public async Task<AssetIntelligenceResult> ObserveAsync(AssetObservationCommand c,CancellationToken ct)
    {
        var issues=(c.Issues??Array.Empty<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();decimal score=0m;if(!c.ImportSucceeded){score+=45m;issues.Add("Asset import failed.");}if(c.MissingReference){score+=40m;issues.Add("Asset has a missing/broken reference.");}if(c.PlatformIncompatible){score+=35m;issues.Add("Asset is incompatible with at least one target platform.");}if(c.SizeBytes>256L*1024*1024){score+=30m;issues.Add($"Very large asset: {c.SizeBytes/1024d/1024d:0.#} MiB.");}else if(c.SizeBytes>64L*1024*1024){score+=12m;issues.Add($"Large asset: {c.SizeBytes/1024d/1024d:0.#} MiB.");}
        var all=await _repo.ListNodesAsync(c.ProjectId,DevelopmentGraphNodeType.Asset,ct);var duplicate=!string.IsNullOrWhiteSpace(c.ContentHash)&&all.Any(n=>!n.Key.Equals(Key(c.RepositoryKey,c.Path),StringComparison.OrdinalIgnoreCase)&&TryHash(n.MetadataJson)?.Equals(c.ContentHash,StringComparison.OrdinalIgnoreCase)==true);if(duplicate){score+=15m;issues.Add("Another asset node has the same content hash; duplicate content may be present.");}
        score=Math.Clamp(decimal.Round(score+Math.Min(20m,issues.Count*2m),2),0m,100m);var state=score>=80?"Critical":score>=50?"HighRisk":score>=20?"Warning":"Healthy";var key=Key(c.RepositoryKey,c.Path);var asset=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.Asset,key,c.Path,state,"AssetPipeline",null,JsonSerializer.Serialize(new{c.RepositoryKey,c.Path,c.AssetType,c.SizeBytes,c.ContentHash,c.CommitSha,c.Platform,c.ImportSucceeded,c.MissingReference,c.PlatformIncompatible,Issues=issues})),ct);
        foreach(var depPath in c.Dependencies??Array.Empty<string>()){if(string.IsNullOrWhiteSpace(depPath))continue;var dep=await _graph.UpsertNodeAsync(new(c.ProjectId,DevelopmentGraphNodeType.Asset,Key(c.RepositoryKey,depPath),depPath,"Observed","AssetPipeline",null,JsonSerializer.Serialize(new{RepositoryKey=c.RepositoryKey,Path=depPath})),ct);await _graph.LinkAsync(new(c.ProjectId,asset.Id,dep.Id,DevelopmentGraphEdgeType.DependsOn,75m,c.MissingReference,JsonSerializer.Serialize(new{kind="asset-reference"})),ct);}
        if(!string.IsNullOrWhiteSpace(c.CommitSha)){var commit=await _repo.FindNodeAsync(c.ProjectId,DevelopmentGraphNodeType.Commit,$"commit:{c.RepositoryKey}:{c.CommitSha}",ct);if(commit is not null)await _graph.LinkAsync(new(c.ProjectId,commit.Id,asset.Id,DevelopmentGraphEdgeType.Changes,80m,state=="Critical","{}"),ct);}
        var actions=new List<string>();if(!c.ImportSucceeded)actions.Add("Fix import settings/source data and re-import the asset.");if(c.MissingReference)actions.Add("Repair missing references before the next candidate build.");if(c.PlatformIncompatible)actions.Add("Validate platform-specific import/format settings.");if(duplicate)actions.Add("Confirm whether duplicate content is intentional; deduplicate if not.");if(score>=50)actions.Add("Require lead-art/asset-pipeline review before release.");var summary=$"Asset '{c.Path}' is {state} with risk {score:0.##}/100 and {issues.Count} issue(s).";var result=new AssetIntelligenceResult(key,state,score,duplicate,issues,actions.Distinct().ToArray(),summary);
        await _records.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),c.ProjectId,key,P2IntelligenceKind.AssetIntelligence,score>=50?P2IntelligenceStatus.Warning:P2IntelligenceStatus.Informational,score,EvidenceConfidence.High,summary,JsonSerializer.Serialize(result),null,_clock.UtcNow),ct);await _uow.SaveChangesAsync(ct);return result;
    }
    private static string Key(string repo,string path)=>$"asset:{repo}:{path.Replace('\\','/').Trim('/')}";
    private static string? TryHash(string json){try{using var d=JsonDocument.Parse(json);return d.RootElement.TryGetProperty("ContentHash",out var x)&&x.ValueKind==JsonValueKind.String?x.GetString():null;}catch{return null;}}
}
