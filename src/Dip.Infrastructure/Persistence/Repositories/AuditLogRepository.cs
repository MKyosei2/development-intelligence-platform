using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly DipDbContext _db;
    public AuditLogRepository(DipDbContext db) => _db = db;

    public Task AddAsync(AuditLog entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return _db.AuditLogs.AddAsync(entry, cancellationToken).AsTask();
    }
}
