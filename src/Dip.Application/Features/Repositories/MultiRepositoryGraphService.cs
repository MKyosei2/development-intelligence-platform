using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Repositories;
public sealed record CreateRepositoryNodeCommand(Guid ProjectId,string Key,string RepositoryUrl,string DefaultBranch,string Role);
public sealed record CreateRepositoryDependencyCommand(Guid ProjectId,Guid SourceRepositoryId,Guid TargetRepositoryId,RepositoryDependencyKind Kind,string ContractKey,bool Critical);
public sealed record RepositoryNodeDto(Guid Id,string Key,string RepositoryUrl,string DefaultBranch,string Role);
public sealed record RepositoryDependencyDto(Guid Id,Guid SourceRepositoryId,Guid TargetRepositoryId,string Kind,string ContractKey,bool Critical);
public sealed record RepositoryGraphDto(IReadOnlyList<RepositoryNodeDto> Nodes,IReadOnlyList<RepositoryDependencyDto> Dependencies);
public sealed record AnalyzeCrossRepositoryImpactCommand(Guid ProjectId,Guid SourceRepositoryId,string CommitSha,IReadOnlyList<string> ChangedContracts);
public sealed record CrossRepositoryImpactDto(Guid Id,string CommitSha,string Level,IReadOnlyList<Guid> AffectedRepositoryIds,IReadOnlyList<string> Reasons,DateTimeOffset AnalyzedAtUtc);

public sealed class MultiRepositoryGraphService
{
    private readonly IRepositoryGraphRepository _repo;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public MultiRepositoryGraphService(IRepositoryGraphRepository repo,IUnitOfWork uow,ISystemClock clock){_repo=repo;_uow=uow;_clock=clock;}
    public async Task<RepositoryNodeDto> AddNodeAsync(CreateRepositoryNodeCommand c,CancellationToken ct){if(await _repo.GetNodeByKeyAsync(c.ProjectId,c.Key,ct) is not null)throw new InvalidOperationException($"Repository node '{c.Key}' already exists.");var e=new RepositoryNode(Guid.NewGuid(),c.ProjectId,c.Key,c.RepositoryUrl,c.DefaultBranch,c.Role,_clock.UtcNow);await _repo.AddNodeAsync(e,ct);await _uow.SaveChangesAsync(ct);return Map(e);}
    public async Task<RepositoryDependencyDto> AddDependencyAsync(CreateRepositoryDependencyCommand c,CancellationToken ct){var source=await _repo.GetNodeAsync(c.SourceRepositoryId,ct);var target=await _repo.GetNodeAsync(c.TargetRepositoryId,ct);if(source is null||target is null)throw new KeyNotFoundException("Both repository nodes must exist.");if(source.ProjectId!=c.ProjectId||target.ProjectId!=c.ProjectId)throw new InvalidOperationException("Repository dependency endpoints must belong to the requested project.");var e=new RepositoryDependency(Guid.NewGuid(),c.ProjectId,c.SourceRepositoryId,c.TargetRepositoryId,c.Kind,c.ContractKey,c.Critical,_clock.UtcNow);await _repo.AddDependencyAsync(e,ct);await _uow.SaveChangesAsync(ct);return Map(e);}
    public async Task<RepositoryGraphDto> GetAsync(Guid projectId,CancellationToken ct){var n=await _repo.ListNodesAsync(projectId,ct);var d=await _repo.ListDependenciesAsync(projectId,ct);return new(n.Select(Map).ToArray(),d.Select(Map).ToArray());}
    public async Task<CrossRepositoryImpactDto> AnalyzeImpactAsync(AnalyzeCrossRepositoryImpactCommand c,CancellationToken ct)
    {
        var source=await _repo.GetNodeAsync(c.SourceRepositoryId,ct);if(source is null)throw new KeyNotFoundException($"Repository node {c.SourceRepositoryId} was not found.");if(source.ProjectId!=c.ProjectId)throw new InvalidOperationException("Source repository belongs to another project.");
        var deps=await _repo.ListDependenciesAsync(c.ProjectId,ct);var affected=new HashSet<Guid>();var reasons=new List<string>();var queue=new Queue<Guid>();queue.Enqueue(c.SourceRepositoryId);var visited=new HashSet<Guid>{c.SourceRepositoryId};var critical=false;
        while(queue.Count>0){var current=queue.Dequeue();foreach(var dep in deps.Where(x=>x.SourceRepositoryId==current)){if(c.ChangedContracts.Count>0&&!c.ChangedContracts.Contains(dep.ContractKey,StringComparer.OrdinalIgnoreCase)&&dep.Kind is not RepositoryDependencyKind.Runtime and not RepositoryDependencyKind.Schema)continue;if(affected.Add(dep.TargetRepositoryId)){reasons.Add($"{dep.Kind} dependency '{dep.ContractKey}' propagates from {dep.SourceRepositoryId} to {dep.TargetRepositoryId}.");critical|=dep.Critical;if(visited.Add(dep.TargetRepositoryId))queue.Enqueue(dep.TargetRepositoryId);}}}
        var level=affected.Count==0?CrossRepositoryImpactLevel.None:critical?CrossRepositoryImpactLevel.Critical:affected.Count>=4?CrossRepositoryImpactLevel.High:affected.Count>=2?CrossRepositoryImpactLevel.Medium:CrossRepositoryImpactLevel.Low;var e=new CrossRepositoryImpact(Guid.NewGuid(),c.ProjectId,c.CommitSha,c.SourceRepositoryId,level,JsonSerializer.Serialize(affected),JsonSerializer.Serialize(reasons),_clock.UtcNow);await _repo.AddImpactAsync(e,ct);await _uow.SaveChangesAsync(ct);return new(e.Id,e.CommitSha,e.Level.ToString(),affected.ToArray(),reasons,e.AnalyzedAtUtc);
    }
    private static RepositoryNodeDto Map(RepositoryNode x)=>new(x.Id,x.Key,x.RepositoryUrl,x.DefaultBranch,x.Role);private static RepositoryDependencyDto Map(RepositoryDependency x)=>new(x.Id,x.SourceRepositoryId,x.TargetRepositoryId,x.Kind.ToString(),x.ContractKey,x.Critical);
}
