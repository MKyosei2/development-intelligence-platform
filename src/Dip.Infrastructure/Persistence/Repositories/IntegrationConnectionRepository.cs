// Purpose: Implements persistence operations for IntegrationConnection.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class IntegrationConnectionRepository(DipDbContext db) : IIntegrationConnectionRepository
{
    public Task<IntegrationConnection?> GetAsync(Guid id, CancellationToken ct) => db.IntegrationConnections.FirstOrDefaultAsync(item => item.Id == id, ct);
    public async Task<IReadOnlyList<IntegrationConnection>> ListAsync(CancellationToken ct) => await db.IntegrationConnections.AsNoTracking().OrderBy(item => item.Provider).ThenBy(item => item.DisplayName).ToListAsync(ct);
    public Task<IntegrationConnection?> GetDefaultForProviderAsync(string provider, CancellationToken ct) => db.IntegrationConnections.Where(item => item.Enabled && item.Provider == provider).OrderByDescending(item => item.LastTestedAtUtc).ThenBy(item => item.CreatedAtUtc).FirstOrDefaultAsync(ct);
    public Task AddAsync(IntegrationConnection connection, CancellationToken ct) => db.IntegrationConnections.AddAsync(connection, ct).AsTask();
    public void Remove(IntegrationConnection connection) => db.IntegrationConnections.Remove(connection);
}
