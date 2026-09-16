// Purpose: Defines the I Ou tb ox Re po si to ry application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;
public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message,CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize,DateTimeOffset now,CancellationToken cancellationToken);
    Task<OutboxMessage?> GetByIdAsync(Guid id,CancellationToken cancellationToken);
}
