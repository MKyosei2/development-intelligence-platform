using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class ProcessedWebhookRepository :
    IProcessedWebhookRepository
{
    private readonly DipDbContext _db;

    public ProcessedWebhookRepository(DipDbContext db)
    {
        _db = db;
    }

    public Task<bool> ExistsAsync(
        string deliveryId,
        CancellationToken cancellationToken) =>
        _db.ProcessedWebhooks.AnyAsync(
            x => x.DeliveryId == deliveryId,
            cancellationToken);

    public Task<ProcessedWebhook?> GetAsync(
        string deliveryId,
        CancellationToken cancellationToken) =>
        _db.ProcessedWebhooks.SingleOrDefaultAsync(
            x => x.DeliveryId == deliveryId,
            cancellationToken);

    public Task AddAsync(
        ProcessedWebhook webhook,
        CancellationToken cancellationToken) =>
        _db.ProcessedWebhooks.AddAsync(
            webhook,
            cancellationToken).AsTask();
}
