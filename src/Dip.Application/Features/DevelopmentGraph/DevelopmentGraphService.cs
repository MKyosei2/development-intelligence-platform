// Purpose: Coordinates D ev el op me nt Gr ap hS er vi ce application behavior for DevelopmentGraph.
using System.Text.Json;
using System.Text.Json.Nodes;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

public sealed class DevelopmentGraphService
{
    private readonly IDevelopmentGraphRepository _repo;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public DevelopmentGraphService(IDevelopmentGraphRepository repo,IUnitOfWork uow,ISystemClock clock){_repo=repo;_uow=uow;_clock=clock;}

    public async Task<DevelopmentGraphNodeDto> UpsertNodeAsync(UpsertDevelopmentGraphNodeCommand c,CancellationToken ct)
    {
        var existing=await _repo.FindNodeAsync(c.ProjectId,c.Type,c.Key,ct);
        if(existing is null){existing=new DevelopmentGraphNode(Guid.NewGuid(),c.ProjectId,c.Type,c.Key,c.Name,c.Status,c.Provider,c.ExternalReference,Json(c.MetadataJson),_clock.UtcNow);await _repo.AddNodeAsync(existing,ct);}
        else existing.Observe(c.Name,c.Status,c.Provider,c.ExternalReference,Json(c.MetadataJson),_clock.UtcNow);
        await _uow.SaveChangesAsync(ct);return Map(existing);
    }

    public async Task<DevelopmentGraphEdgeDto> LinkAsync(LinkDevelopmentGraphNodesCommand c,CancellationToken ct)
    {
        var source=await _repo.GetNodeAsync(c.SourceNodeId,ct)??throw new KeyNotFoundException($"Graph node {c.SourceNodeId} was not found.");
        var target=await _repo.GetNodeAsync(c.TargetNodeId,ct)??throw new KeyNotFoundException($"Graph node {c.TargetNodeId} was not found.");
        if(source.ProjectId!=c.ProjectId||target.ProjectId!=c.ProjectId)throw new InvalidOperationException("Graph edge endpoints must belong to the requested project.");
        var edge=await _repo.FindEdgeAsync(c.ProjectId,c.SourceNodeId,c.TargetNodeId,c.Type,ct);
        if(edge is null){edge=new DevelopmentGraphEdge(Guid.NewGuid(),c.ProjectId,c.SourceNodeId,c.TargetNodeId,c.Type,c.Weight,c.Critical,Json(c.MetadataJson),_clock.UtcNow);await _repo.AddEdgeAsync(edge,ct);}
        else edge.Observe(c.Weight,c.Critical,Json(c.MetadataJson),_clock.UtcNow);
        await _uow.SaveChangesAsync(ct);return Map(edge);
    }

    /// <summary>
    /// Writes an industry-neutral object without changing the database schema.
    /// The legacy enum remains the indexing/query fallback while the durable
    /// domain/kind/source identity lives in metadata.
    /// </summary>
    public async Task<UniversalGraphNodeDto> UpsertUniversalNodeAsync(UpsertUniversalGraphNodeCommand c,CancellationToken ct)
    {
        if(c.ProjectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(c));
        var domain=Identifier(c.DomainId,120,nameof(c.DomainId));
        var kind=Identifier(c.Kind,160,nameof(c.Kind));
        var schema=Identifier(c.SchemaVersion,40,nameof(c.SchemaVersion));
        var sourceObjectId=Optional(c.SourceObjectId,500,nameof(c.SourceObjectId));
        var metadata=MergeMetadata(c.MetadataJson,new Dictionary<string,string?>
        {
            ["domainId"]=domain,["kind"]=kind,["schemaVersion"]=schema,["sourceObjectId"]=sourceObjectId
        });
        var node=await UpsertNodeAsync(new(c.ProjectId,c.FallbackType,c.Key,c.Name,c.Status,c.Provider,c.ExternalReference,metadata),ct);
        return new(node,domain,kind,schema,sourceObjectId);
    }

    public async Task<UniversalGraphEdgeDto> LinkUniversalAsync(LinkUniversalGraphNodesCommand c,CancellationToken ct)
    {
        if(c.ProjectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(c));
        var relation=Identifier(c.RelationKind,160,nameof(c.RelationKind));
        var metadata=MergeMetadata(c.MetadataJson,new Dictionary<string,string?>{{"relationKind",relation}});
        var edge=await LinkAsync(new(c.ProjectId,c.SourceNodeId,c.TargetNodeId,c.FallbackType,c.Weight,c.Critical,metadata),ct);
        return new(edge,relation);
    }

    public async Task<UniversalGraphDto> GetUniversalAsync(Guid projectId,string? domainId,string? kind,CancellationToken ct)
    {
        if(projectId==Guid.Empty)throw new ArgumentException("ProjectId is required.",nameof(projectId));
        var nodes=await _repo.ListNodesAsync(projectId,null,ct);
        var universal=new List<UniversalGraphNodeDto>();
        foreach(var entity in nodes)
        {
            var dto=Map(entity);var domain=Meta(dto.MetadataJson,"domainId");var objectKind=Meta(dto.MetadataJson,"kind");
            if(string.IsNullOrWhiteSpace(domain)||string.IsNullOrWhiteSpace(objectKind))continue;
            if(!string.IsNullOrWhiteSpace(domainId)&&!domain.Equals(domainId,StringComparison.OrdinalIgnoreCase))continue;
            if(!string.IsNullOrWhiteSpace(kind)&&!objectKind.Equals(kind,StringComparison.OrdinalIgnoreCase))continue;
            universal.Add(new(dto,domain,objectKind,Meta(dto.MetadataJson,"schemaVersion")??"1",Meta(dto.MetadataJson,"sourceObjectId")));
        }
        var ids=universal.Select(x=>x.Node.Id).ToHashSet();var edges=new List<UniversalGraphEdgeDto>();
        foreach(var entity in await _repo.ListEdgesAsync(projectId,ct))
        {
            if(!ids.Contains(entity.SourceNodeId)&&!ids.Contains(entity.TargetNodeId))continue;var dto=Map(entity);var relation=Meta(dto.MetadataJson,"relationKind");
            if(!string.IsNullOrWhiteSpace(relation))edges.Add(new(dto,relation));
        }
        return new(projectId,universal.OrderBy(x=>x.DomainId).ThenBy(x=>x.Kind).ThenBy(x=>x.Node.Name).ToArray(),edges.ToArray());
    }

    public async Task<DevelopmentGraphDto> GetAsync(Guid projectId,DevelopmentGraphNodeType? type,CancellationToken ct)
    {
        var nodes=await _repo.ListNodesAsync(projectId,type,ct);var ids=nodes.Select(x=>x.Id).ToHashSet();var edges=(await _repo.ListEdgesAsync(projectId,ct)).Where(x=>ids.Contains(x.SourceNodeId)||ids.Contains(x.TargetNodeId)).Select(Map).ToArray();return new(projectId,nodes.Select(Map).ToArray(),edges);
    }

    public async Task IngestVersionControlChangeAsync(NormalizedVersionControlChange change,CancellationToken ct)
    {
        var repo=await UpsertNodeAsync(new(change.ProjectId,DevelopmentGraphNodeType.Repository,$"repo:{change.RepositoryKey}",change.RepositoryKey,"Active",change.Provider.ToString(),change.RepositoryLocator,JsonSerializer.Serialize(new{change.RepositoryLocator})),ct);
        var branch=await UpsertNodeAsync(new(change.ProjectId,DevelopmentGraphNodeType.Branch,$"branch:{change.RepositoryKey}:{change.Branch}",change.Branch,"Active",change.Provider.ToString(),null,JsonSerializer.Serialize(new{change.RepositoryKey})),ct);
        await LinkAsync(new(change.ProjectId,repo.Id,branch.Id,DevelopmentGraphEdgeType.Contains,70m,false,"{}"),ct);
        var commit=await UpsertNodeAsync(new(change.ProjectId,DevelopmentGraphNodeType.Commit,$"commit:{change.RepositoryKey}:{change.Revision}",change.Title,string.IsNullOrWhiteSpace(change.ReleaseTag)?"Observed":"Released",change.Provider.ToString(),change.Url,JsonSerializer.Serialize(new{change.Revision,change.Branch,change.IsMerge,change.ReleaseTag})),ct);
        await LinkAsync(new(change.ProjectId,branch.Id,commit.Id,DevelopmentGraphEdgeType.Contains,80m,false,"{}"),ct);
        foreach(var file in change.Files.Take(5000))
        {
            var fileNode=await UpsertNodeAsync(new(change.ProjectId,DevelopmentGraphNodeType.File,$"file:{change.RepositoryKey}:{Normalize(file.Path)}",file.Path,file.Status,change.Provider.ToString(),null,JsonSerializer.Serialize(new{file.Status,file.Additions,file.Deletions})),ct);
            await LinkAsync(new(change.ProjectId,commit.Id,fileNode.Id,DevelopmentGraphEdgeType.Changes,Math.Clamp(30m+(file.Additions+file.Deletions)/10m,30m,100m),false,"{}"),ct);
            var componentKey=InferComponent(file.Path);if(componentKey is not null){var component=await UpsertNodeAsync(new(change.ProjectId,DevelopmentGraphNodeType.Component,$"component:{change.RepositoryKey}:{componentKey}",componentKey,"Active",change.Provider.ToString(),null,"{}"),ct);await LinkAsync(new(change.ProjectId,fileNode.Id,component.Id,DevelopmentGraphEdgeType.BelongsTo,70m,false,"{}"),ct);}
        }
        if(change.PullRequestNumber.HasValue)
        {
            var pr=await UpsertNodeAsync(new(change.ProjectId,DevelopmentGraphNodeType.PullRequest,$"pr:{change.RepositoryKey}:{change.PullRequestNumber.Value}",$"PR #{change.PullRequestNumber.Value}: {change.Title}","Open",change.Provider.ToString(),change.Url,JsonSerializer.Serialize(new{change.PullRequestNumber})),ct);
            await LinkAsync(new(change.ProjectId,pr.Id,commit.Id,DevelopmentGraphEdgeType.Contains,85m,false,"{}"),ct);
            foreach(var review in change.Reviews??Array.Empty<NormalizedReview>()){var key=$"review:{change.RepositoryKey}:{change.PullRequestNumber.Value}:{review.Reviewer}";var reviewNode=await UpsertNodeAsync(new(change.ProjectId,DevelopmentGraphNodeType.Review,key,review.Reviewer,review.State,change.Provider.ToString(),change.Url,JsonSerializer.Serialize(review)),ct);await LinkAsync(new(change.ProjectId,reviewNode.Id,pr.Id,DevelopmentGraphEdgeType.Reviews,75m,false,"{}"),ct);}
        }
        if(!string.IsNullOrWhiteSpace(change.ReleaseTag))
        {
            var release=await UpsertNodeAsync(new(change.ProjectId,DevelopmentGraphNodeType.Release,$"release:{change.RepositoryKey}:{change.ReleaseTag}",change.ReleaseTag!,"Candidate",change.Provider.ToString(),change.Url,JsonSerializer.Serialize(new{change.ReleaseTag,change.Revision})),ct);
            await LinkAsync(new(change.ProjectId,release.Id,commit.Id,DevelopmentGraphEdgeType.Targets,100m,true,"{}"),ct);
        }
    }

    public async Task IngestCiRunAsync(NormalizedCiRun run,CancellationToken ct)
    {
        var ci=await UpsertNodeAsync(new(run.ProjectId,DevelopmentGraphNodeType.CiRun,$"ci:{run.Provider}:{run.PipelineKey}:{run.RunId}",$"{run.PipelineKey} #{run.RunId}",run.Status,run.Provider.ToString(),run.Url,JsonSerializer.Serialize(new{run.Revision,run.Branch,run.PassedTests,run.FailedTests,run.SkippedTests,run.MetadataJson})),ct);
        if(!string.IsNullOrWhiteSpace(run.Revision))
        {
            var commit=await _repo.FindNodeAsync(run.ProjectId,DevelopmentGraphNodeType.Commit,$"commit:{run.RepositoryKey}:{run.Revision}",ct);
            if(commit is not null)await LinkAsync(new(run.ProjectId,commit.Id,ci.Id,DevelopmentGraphEdgeType.BuiltBy,90m,run.FailedTests>0,"{}"),ct);
        }
    }

    public Task<DevelopmentGraphNode?> FindNodeAsync(Guid projectId,DevelopmentGraphNodeType type,string key,CancellationToken ct)=>_repo.FindNodeAsync(projectId,type,key,ct);
    public Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(Guid projectId,DevelopmentGraphNodeType? type,CancellationToken ct)=>_repo.ListNodesAsync(projectId,type,ct);
    public Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(Guid projectId,CancellationToken ct)=>_repo.ListEdgesAsync(projectId,ct);

    private static string? InferComponent(string path){var p=Normalize(path).Split('/',StringSplitOptions.RemoveEmptyEntries);if(p.Length<2)return null;if(p[0].Equals("src",StringComparison.OrdinalIgnoreCase)||p[0].Equals("tests",StringComparison.OrdinalIgnoreCase)||p[0].Equals("collectors",StringComparison.OrdinalIgnoreCase)||p[0].Equals("services",StringComparison.OrdinalIgnoreCase))return $"{p[0]}/{p[1]}";return p.Length>=3&&p[0].Equals("Assets",StringComparison.OrdinalIgnoreCase)?$"Assets/{p[1]}":p[0];}
    private static string Normalize(string p)=>p.Replace('\\','/').Trim('/');
    private static string Json(string value){try{using var _=JsonDocument.Parse(string.IsNullOrWhiteSpace(value)?"{}":value);return string.IsNullOrWhiteSpace(value)?"{}":value;}catch(JsonException ex){throw new ArgumentException("MetadataJson must contain valid JSON.",nameof(value),ex);}}
    private static string MergeMetadata(string json,IReadOnlyDictionary<string,string?> fields)
    {
        JsonNode? parsed;try{parsed=JsonNode.Parse(string.IsNullOrWhiteSpace(json)?"{}":json);}catch(JsonException ex){throw new ArgumentException("MetadataJson must contain valid JSON.",nameof(json),ex);}
        var obj=parsed as JsonObject;if(obj is null){obj=new JsonObject();if(parsed is not null)obj["payload"]=parsed.DeepClone();}
        foreach(var pair in fields){if(pair.Value is null)obj.Remove(pair.Key);else obj[pair.Key]=pair.Value;}
        return obj.ToJsonString();
    }
    private static string Identifier(string value,int max,string name){var v=value?.Trim()??string.Empty;if(v.Length is <1||v.Length>max)throw new ArgumentException($"{name} must be 1-{max} characters.",name);if(v.Any(ch=>!(char.IsLetterOrDigit(ch)||ch is '.' or '-' or '_' or ':' or '/')))throw new ArgumentException($"{name} contains unsupported characters.",name);return v;}
    private static string? Optional(string? value,int max,string name){if(string.IsNullOrWhiteSpace(value))return null;var v=value.Trim();if(v.Length>max)throw new ArgumentException($"{name} must be at most {max} characters.",name);return v;}
    private static string? Meta(string json,string name){try{using var d=JsonDocument.Parse(json);if(d.RootElement.ValueKind==JsonValueKind.Object&&d.RootElement.TryGetProperty(name,out var v)&&v.ValueKind==JsonValueKind.String)return v.GetString();}catch{}return null;}
    private static DevelopmentGraphNodeDto Map(DevelopmentGraphNode x)=>new(x.Id,x.Type.ToString(),x.Key,x.Name,x.Status,x.Provider,x.ExternalReference,x.MetadataJson,x.LastObservedAtUtc);
    private static DevelopmentGraphEdgeDto Map(DevelopmentGraphEdge x)=>new(x.Id,x.SourceNodeId,x.TargetNodeId,x.Type.ToString(),x.Weight,x.Critical,x.MetadataJson,x.LastObservedAtUtc);
}
