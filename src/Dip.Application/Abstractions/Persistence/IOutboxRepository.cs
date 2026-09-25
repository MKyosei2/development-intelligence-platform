// Purpose: Persists and queries I Ou tb ox state for p er si st en ce.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;
public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, DateTimeOffset now, CancellationToken cancellationToken);
    Task<OutboxMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
