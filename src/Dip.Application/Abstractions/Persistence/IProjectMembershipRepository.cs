// Purpose: Defines the IProjectMembershipRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IProjectMembershipRepository
{
    Task<ProjectMembership?> FindAsync(Guid projectId, string subjectId, CancellationToken ct);
    Task<IReadOnlyList<ProjectMembership>> ListByProjectAsync(Guid projectId, CancellationToken ct);
    Task<IReadOnlyList<ProjectMembership>> ListBySubjectAsync(string subjectId, CancellationToken ct);
    Task AddAsync(ProjectMembership membership, CancellationToken ct);
    void Remove(ProjectMembership membership);
}
