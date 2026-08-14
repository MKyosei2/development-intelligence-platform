using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class ProjectMembershipRepository(DipDbContext db) : IProjectMembershipRepository
{
    public Task<ProjectMembership?> FindAsync(Guid projectId,string subjectId,CancellationToken ct)=>db.ProjectMemberships.FirstOrDefaultAsync(x=>x.ProjectId==projectId&&x.SubjectId==subjectId,ct);
    public async Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(Guid projectId,CancellationToken ct)=>await db.ProjectMemberships.AsNoTracking().Where(x=>x.ProjectId==projectId).OrderByDescending(x=>x.Role).ThenBy(x=>x.Login).ToListAsync(ct);
    public async Task<IReadOnlyList<ProjectMembership>> ListBySubjectAsync(string subjectId,CancellationToken ct)=>await db.ProjectMemberships.AsNoTracking().Where(x=>x.SubjectId==subjectId).OrderBy(x=>x.ProjectId).ToListAsync(ct);
    public Task AddAsync(ProjectMembership membership,CancellationToken ct)=>db.ProjectMemberships.AddAsync(membership,ct).AsTask();
    public void Remove(ProjectMembership membership)=>db.ProjectMemberships.Remove(membership);
}
