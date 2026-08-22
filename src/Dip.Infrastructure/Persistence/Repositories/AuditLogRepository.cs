using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

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

    public async Task<IReadOnlyList<AuditLog>> ListByProjectAsync(
        Guid projectId,
        int take,
        CancellationToken cancellationToken) =>
        await _db.AuditLogs
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 5000))
            .ToListAsync(cancellationToken);

    public Task<AuditLog?> GetLatestByCategoryAsync(
        Guid projectId,
        string category,
        CancellationToken cancellationToken) =>
        _db.AuditLogs
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Category == category)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
}
