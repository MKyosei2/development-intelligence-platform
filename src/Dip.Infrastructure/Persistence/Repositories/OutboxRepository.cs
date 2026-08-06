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
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        return _db.OutboxMessages
            .AddAsync(message, cancellationToken)
            .AsTask();
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
        int batchSize,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (batchSize is < 1 or > 500)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        return await _db.OutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.ProcessedAtUtc == null
                && (message.NextAttemptAtUtc == null
                    || message.NextAttemptAtUtc <= now)
                && (message.LockedUntilUtc == null
                    || message.LockedUntilUtc <= now))
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public Task<OutboxMessage?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return Task.FromResult<OutboxMessage?>(null);

        return _db.OutboxMessages.SingleOrDefaultAsync(
            message => message.Id == id,
            cancellationToken);
    }
}
