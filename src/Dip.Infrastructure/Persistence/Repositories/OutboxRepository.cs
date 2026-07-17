using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly DipDbContext _db;

    public OutboxRepository(DipDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(
        OutboxMessage message,
        CancellationToken cancellationToken) =>
        _db.OutboxMessages.AddAsync(message, cancellationToken).AsTask();

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return await _db.OutboxMessages
            .Where(x =>
                x.ProcessedAtUtc == null &&
                (!x.NotBeforeUtc.HasValue || x.NotBeforeUtc <= now) &&
                (!x.LockedUntilUtc.HasValue || x.LockedUntilUtc <= now))
            .OrderBy(x => x.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task<OutboxMessage?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        _db.OutboxMessages
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
}
