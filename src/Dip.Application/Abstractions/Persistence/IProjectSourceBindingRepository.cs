// Purpose: Defines the IProjectSourceBindingRepository application boundary.
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Abstractions.Persistence;

public interface IProjectSourceBindingRepository
{
    Task<ProjectSourceBinding?> GetAsync(Guid id, CancellationToken ct);
    Task<ProjectSourceBinding?> FindAsync(Guid projectId, Guid connectionId, string externalResourceId, CancellationToken ct);
    Task<IReadOnlyList<ProjectSourceBinding>> ListByProjectAsync(Guid projectId, CancellationToken ct);
    Task AddAsync(ProjectSourceBinding binding, CancellationToken ct);
}
