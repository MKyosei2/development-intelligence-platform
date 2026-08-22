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


    public async Task<IReadOnlyList<AuditLog>> SearchAsync(
        Guid projectId,
        string? search,
        string? category,
        string? correlationId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _db.AuditLogs
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.Action.Contains(term)
                                     || x.Actor.Contains(term)
                                     || (x.SubjectKey != null && x.SubjectKey.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            var term = category.Trim();
            query = query.Where(x => x.Category.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            var term = correlationId.Trim();
            query = query.Where(x => x.CorrelationId != null && x.CorrelationId.Contains(term));
        }
        if (fromUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue)
            query = query.Where(x => x.CreatedAtUtc <= toUtc.Value);

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 5000))
            .ToListAsync(cancellationToken);
    }

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
