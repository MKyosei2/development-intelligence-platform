// Purpose: Defines the IDurableJobRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IDurableJobRepository
{
    Task AddAsync(DurableJob job, CancellationToken ct);
    Task<DurableJob?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<DurableJob>> ListRecentAsync(Guid projectId, int take, CancellationToken ct);
    Task<DurableJob?> TryClaimAsync(Guid id, string owner, DateTimeOffset now, TimeSpan lease, CancellationToken ct);
    Task<IReadOnlyList<DurableJob>> ListExpiredLeasesAsync(DateTimeOffset now, int take, CancellationToken ct);
    Task<long> CountQueuedAsync(CancellationToken ct);
}
