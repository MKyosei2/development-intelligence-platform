// Purpose: Implements persistence operations for ProjectSourceBinding.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class ProjectSourceBindingRepository(DipDbContext db) : IProjectSourceBindingRepository
{
    public Task<ProjectSourceBinding?> GetAsync(Guid id, CancellationToken ct) => db.ProjectSourceBindings.FirstOrDefaultAsync(item => item.Id == id, ct);
    public Task<ProjectSourceBinding?> FindAsync(Guid projectId, Guid connectionId, string externalResourceId, CancellationToken ct) => db.ProjectSourceBindings.FirstOrDefaultAsync(item => item.ProjectId == projectId && item.ConnectionId == connectionId && item.ExternalResourceId == externalResourceId, ct);
    public async Task<IReadOnlyList<ProjectSourceBinding>> ListByProjectAsync(Guid projectId, CancellationToken ct) => await db.ProjectSourceBindings.AsNoTracking().Where(item => item.ProjectId == projectId).OrderBy(item => item.ResourceType).ThenBy(item => item.ExternalResourceName).ToListAsync(ct);
    public Task AddAsync(ProjectSourceBinding binding, CancellationToken ct) => db.ProjectSourceBindings.AddAsync(binding, ct).AsTask();
}
