using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly DipDbContext _db;

    public OutboxRepository(DipDbContext db) => _db = db;

    public Task AddAsync(
        OutboxMessage message,
        CancellationToken cancellationToken) =>
        _db.OutboxMessages.AddAsync(message, cancellationToken).AsTask();

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        return await _db.OutboxMessages
            .Where(x =>
                x.ProcessedAtUtc == null
                && (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now)
                && (x.LockedUntilUtc == null || x.LockedUntilUtc <= now))
            .OrderBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task<OutboxMessage?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        _db.OutboxMessages.SingleOrDefaultAsync(
            x => x.Id == id,
            cancellationToken);
}
