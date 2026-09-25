// Purpose: Persists and queries Project state for DIP.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class ProjectRepository(DipDbContext db) : IProjectRepository
{
    private const string LifecycleCategory = "project-lifecycle";

    public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Projects.SingleOrDefaultAsync(x => x.Id == id, ct);

    /// <summary>
    /// Operational list used by workers and portfolio services. Archived/deleted
    /// projects are intentionally excluded so background processing stops with the lifecycle.
    /// </summary>
    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct)
    {
        var lifecycle = await db.AuditLogs
            .AsNoTracking()
            .Where(x => x.Category == LifecycleCategory)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.ProjectId, x.Action, x.CreatedAtUtc })
            .ToListAsync(ct);

        var hidden = lifecycle
            .GroupBy(x => x.ProjectId)
            .Select(group => group.OrderByDescending(x => x.CreatedAtUtc).First())
            .Where(x => x.Action is "project.archived" or "project.deleted")
            .Select(x => x.ProjectId)
            .ToHashSet();

        return await db.Projects
            .AsNoTracking()
            .Where(x => !hidden.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Administrative list used by project settings so archived projects remain restorable.
    /// </summary>
    public async Task<IReadOnlyList<Project>> ListAllAsync(CancellationToken ct) =>
        await db.Projects.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);

    public Task AddAsync(Project project, CancellationToken ct) =>
        db.Projects.AddAsync(project, ct).AsTask();
}
