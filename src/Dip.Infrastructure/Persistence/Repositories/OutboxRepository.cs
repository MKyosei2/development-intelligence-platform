// Purpose: Implements O ut bo xR ep os it or y persistence behavior.
using Dip.Application.Abstractions.Persistence;using Dip.Domain.Entities;using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;
public sealed class OutboxRepository(DipDbContext db):IOutboxRepository
{
 public Task AddAsync(OutboxMessage message,CancellationToken ct)=>db.OutboxMessages.AddAsync(message,ct).AsTask();
 public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize,DateTimeOffset now,CancellationToken ct)=>await db.OutboxMessages.Where(x=>x.ProcessedAtUtc==null&&(x.NextAttemptAtUtc==null||x.NextAttemptAtUtc<=now)&&(x.LockedUntilUtc==null||x.LockedUntilUtc<=now)).OrderBy(x=>x.CreatedAtUtc).Take(Math.Clamp(batchSize,1,1000)).ToListAsync(ct);
 public Task<OutboxMessage?> GetByIdAsync(Guid id,CancellationToken ct)=>db.OutboxMessages.SingleOrDefaultAsync(x=>x.Id==id,ct);
}
