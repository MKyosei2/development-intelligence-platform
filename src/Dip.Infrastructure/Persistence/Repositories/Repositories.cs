using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;
public sealed class ProjectRepository(DipDbContext db) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(Guid id,CancellationToken ct)=>db.Projects.SingleOrDefaultAsync(x=>x.Id==id,ct);
    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct)=>await db.Projects.AsNoTracking().OrderBy(x=>x.Name).ToListAsync(ct);
    public Task AddAsync(Project project,CancellationToken ct)=>db.Projects.AddAsync(project,ct).AsTask();
}
