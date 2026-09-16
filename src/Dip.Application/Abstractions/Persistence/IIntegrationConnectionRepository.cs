// Purpose: Defines the IIntegrationConnectionRepository application boundary.
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Abstractions.Persistence;

public interface IIntegrationConnectionRepository
{
    Task<IntegrationConnection?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<IntegrationConnection>> ListAsync(CancellationToken ct);
    Task<IntegrationConnection?> GetDefaultForProviderAsync(string provider,CancellationToken ct);
    Task AddAsync(IntegrationConnection connection, CancellationToken ct);
    void Remove(IntegrationConnection connection);
}
