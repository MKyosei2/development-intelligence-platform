// Purpose: Defines the IDevelopmentGraphRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IDevelopmentGraphRepository
{
    Task<DevelopmentGraphNode?> GetNodeAsync(Guid id, CancellationToken ct);
    Task<DevelopmentGraphNode?> FindNodeAsync(Guid projectId, Dip.Domain.Enums.DevelopmentGraphNodeType type, string key, CancellationToken ct);
    Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(Guid projectId, Dip.Domain.Enums.DevelopmentGraphNodeType? type, CancellationToken ct);
    Task AddNodeAsync(DevelopmentGraphNode node, CancellationToken ct);
    Task<DevelopmentGraphEdge?> FindEdgeAsync(Guid projectId, Guid sourceNodeId, Guid targetNodeId, Dip.Domain.Enums.DevelopmentGraphEdgeType type, CancellationToken ct);
    Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(Guid projectId, CancellationToken ct);
    Task AddEdgeAsync(DevelopmentGraphEdge edge, CancellationToken ct);
}
