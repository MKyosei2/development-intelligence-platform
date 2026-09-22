// Purpose: Defines the IReleaseReadinessRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IReleaseReadinessRepository
{
    Task AddAsync(ReleaseReadinessSnapshot snapshot, CancellationToken ct);
    Task<ReleaseReadinessSnapshot?> GetLatestAsync(Guid projectId, CancellationToken ct);
    Task<IReadOnlyList<ReleaseReadinessSnapshot>> ListAsync(Guid projectId, int take, CancellationToken ct);
}
