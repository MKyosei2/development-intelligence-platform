// Purpose: Persists and queries P ro ce ss ed We bh oo kR ep os it or y data.
using Dip.Application.Abstractions.Persistence;using Dip.Domain.Entities;using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;
public sealed class ProcessedWebhookRepository(DipDbContext db):IProcessedWebhookRepository
{
 public Task<bool> ExistsAsync(string deliveryId,CancellationToken ct)=>db.ProcessedWebhooks.AnyAsync(x=>x.DeliveryId==deliveryId,ct);
 public Task<ProcessedWebhook?> GetAsync(string deliveryId,CancellationToken ct)=>db.ProcessedWebhooks.SingleOrDefaultAsync(x=>x.DeliveryId==deliveryId,ct);
 public Task AddAsync(ProcessedWebhook webhook,CancellationToken ct)=>db.ProcessedWebhooks.AddAsync(webhook,ct).AsTask();
}
