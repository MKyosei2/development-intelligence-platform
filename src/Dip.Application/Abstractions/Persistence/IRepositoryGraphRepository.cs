// Purpose: Defines the IRepositoryGraphRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IRepositoryGraphRepository
{
    Task AddNodeAsync(RepositoryNode node, CancellationToken ct);
    Task<RepositoryNode?> GetNodeAsync(Guid id, CancellationToken ct);
    Task<RepositoryNode?> GetNodeByKeyAsync(Guid projectId, string key, CancellationToken ct);
    Task<IReadOnlyList<RepositoryNode>> ListNodesAsync(Guid projectId, CancellationToken ct);
    Task AddDependencyAsync(RepositoryDependency dependency, CancellationToken ct);
    Task<IReadOnlyList<RepositoryDependency>> ListDependenciesAsync(Guid projectId, CancellationToken ct);
    Task AddImpactAsync(CrossRepositoryImpact impact, CancellationToken ct);
    Task<CrossRepositoryImpact?> GetLatestImpactAsync(Guid projectId, string commitSha, CancellationToken ct);
}
