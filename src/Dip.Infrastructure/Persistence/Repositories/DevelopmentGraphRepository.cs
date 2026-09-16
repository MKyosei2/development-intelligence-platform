// Purpose: Implements D ev el op me nt Gr ap hR ep os it or y persistence behavior.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class DevelopmentGraphRepository(DipDbContext db):IDevelopmentGraphRepository
{
    public Task<DevelopmentGraphNode?> GetNodeAsync(Guid id,CancellationToken ct)=>db.DevelopmentGraphNodes.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public Task<DevelopmentGraphNode?> FindNodeAsync(Guid projectId,DevelopmentGraphNodeType type,string key,CancellationToken ct)=>db.DevelopmentGraphNodes.SingleOrDefaultAsync(x=>x.ProjectId==projectId&&x.Type==type&&x.Key==key,ct);
    public async Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(Guid projectId,DevelopmentGraphNodeType? type,CancellationToken ct)=>await db.DevelopmentGraphNodes.AsNoTracking().Where(x=>x.ProjectId==projectId&&(!type.HasValue||x.Type==type.Value)).OrderBy(x=>x.Type).ThenBy(x=>x.Key).ToListAsync(ct);
    public Task AddNodeAsync(DevelopmentGraphNode node,CancellationToken ct)=>db.DevelopmentGraphNodes.AddAsync(node,ct).AsTask();
    public Task<DevelopmentGraphEdge?> FindEdgeAsync(Guid projectId,Guid sourceNodeId,Guid targetNodeId,DevelopmentGraphEdgeType type,CancellationToken ct)=>db.DevelopmentGraphEdges.SingleOrDefaultAsync(x=>x.ProjectId==projectId&&x.SourceNodeId==sourceNodeId&&x.TargetNodeId==targetNodeId&&x.Type==type,ct);
    public async Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(Guid projectId,CancellationToken ct)=>await db.DevelopmentGraphEdges.AsNoTracking().Where(x=>x.ProjectId==projectId).OrderBy(x=>x.Type).ToListAsync(ct);
    public Task AddEdgeAsync(DevelopmentGraphEdge edge,CancellationToken ct)=>db.DevelopmentGraphEdges.AddAsync(edge,ct).AsTask();
}
