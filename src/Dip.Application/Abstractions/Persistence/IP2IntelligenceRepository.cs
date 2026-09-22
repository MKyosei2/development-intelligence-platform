// Purpose: Defines the IP2IntelligenceRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IP2IntelligenceRepository
{
    Task AddAsync(P2IntelligenceRecord record, CancellationToken ct);
    Task<IReadOnlyList<P2IntelligenceRecord>> ListRecentAsync(Guid projectId, int take, CancellationToken ct);
    Task<IReadOnlyList<P2IntelligenceRecord>> ListByKindAsync(Guid projectId, Dip.Domain.Enums.P2IntelligenceKind kind, int take, CancellationToken ct);
}
