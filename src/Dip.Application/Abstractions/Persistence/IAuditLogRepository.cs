// Purpose: Defines the audit-log persistence boundary used by application workflows.
using Dip.Domain.Entities;

namespace Dip.Application.Abstractions.Persistence;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct);

    Task<IReadOnlyList<AuditLog>> ListByProjectAsync(
        Guid projectId,
        int take,
        CancellationToken ct);

    Task<IReadOnlyList<AuditLog>> SearchAsync(
        Guid projectId,
        string? search,
        string? category,
        string? correlationId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int take,
        CancellationToken ct);

    Task<AuditLogPage> SearchPageAsync(
        Guid projectId,
        string? search,
        string? category,
        string? correlationId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        DateTimeOffset? beforeUtc,
        Guid? beforeId,
        int take,
        CancellationToken ct);

    Task<AuditLog?> GetLatestByCategoryAsync(
        Guid projectId,
        string category,
        CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, AuditLog>> GetLatestByCategoryAsync(
        IReadOnlyCollection<Guid> projectIds,
        string category,
        CancellationToken ct);
}
