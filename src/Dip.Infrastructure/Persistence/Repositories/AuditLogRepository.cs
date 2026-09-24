// Purpose: Persists and queries project audit logs with server-side filtering and cursor pagination.
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
        Guid projectId, int take, CancellationToken cancellationToken) =>
        await _db.AuditLogs.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 5000))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AuditLog>> SearchAsync(
        Guid projectId, string? search, string? category, string? correlationId,
        DateTimeOffset? fromUtc, DateTimeOffset? toUtc, int take,
        CancellationToken cancellationToken) =>
        await FilteredQuery(projectId, search, category, correlationId, fromUtc, toUtc)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(Math.Clamp(take, 1, 5000))
            .ToListAsync(cancellationToken);

    public async Task<AuditLogPage> SearchPageAsync(
        Guid projectId, string? search, string? category, string? correlationId,
        DateTimeOffset? fromUtc, DateTimeOffset? toUtc, DateTimeOffset? beforeUtc,
        Guid? beforeId, int take, CancellationToken cancellationToken)
    {
        var query = FilteredQuery(projectId, search, category, correlationId, fromUtc, toUtc);

        if (beforeUtc.HasValue)
        {
            var timestamp = beforeUtc.Value;
            if (beforeId.HasValue)
            {
                var id = beforeId.Value;
                query = query.Where(x =>
                    x.CreatedAtUtc < timestamp
                    || (x.CreatedAtUtc == timestamp && x.Id.CompareTo(id) < 0));
            }
            else
            {
                query = query.Where(x => x.CreatedAtUtc < timestamp);
            }
        }

        var limit = Math.Clamp(take, 1, 500);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);

        var last = hasMore ? rows.LastOrDefault() : null;
        return new AuditLogPage(rows, last?.CreatedAtUtc, last?.Id);
    }

    public Task<AuditLog?> GetLatestByCategoryAsync(
        Guid projectId, string category, CancellationToken cancellationToken) =>
        _db.AuditLogs.AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.Category == category)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, AuditLog>> GetLatestByCategoryAsync(
        IReadOnlyCollection<Guid> projectIds,
        string category,
        CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
            return new Dictionary<Guid, AuditLog>();

        var ids = projectIds.Distinct().ToArray();
        var rows = await _db.AuditLogs.AsNoTracking()
            .Where(x => ids.Contains(x.ProjectId) && x.Category == category)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return rows.GroupBy(x => x.ProjectId)
            .ToDictionary(group => group.Key, group => group.First());
    }

    private IQueryable<AuditLog> FilteredQuery(
        Guid projectId, string? search, string? category, string? correlationId,
        DateTimeOffset? fromUtc, DateTimeOffset? toUtc)
    {
        var query = _db.AuditLogs.AsNoTracking().Where(x => x.ProjectId == projectId);

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

        return query;
    }
}
