// Purpose: Defines the IProjectClaimRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IProjectClaimRepository
{
    Task<ProjectClaim?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ProjectClaim>> ListByProjectAsync(Guid projectId, CancellationToken ct);
    Task AddAsync(ProjectClaim claim, CancellationToken ct);
}
