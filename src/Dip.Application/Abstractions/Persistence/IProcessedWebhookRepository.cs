// Purpose: Persists and queries I Pr oc es se dW eb ho ok state for p er si st en ce.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;
public interface IProcessedWebhookRepository
{
    Task<bool> ExistsAsync(string deliveryId, CancellationToken cancellationToken);
    Task<ProcessedWebhook?> GetAsync(string deliveryId, CancellationToken cancellationToken);
    Task AddAsync(ProcessedWebhook webhook, CancellationToken cancellationToken);
}
