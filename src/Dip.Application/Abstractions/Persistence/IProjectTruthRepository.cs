// Purpose: Defines the IProjectTruthRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IProjectTruthRepository
{
    Task<ProjectTruthSnapshot?> GetLatestAsync(Guid projectId,CancellationToken ct);
    Task<IReadOnlyList<ProjectTruthSnapshot>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);
    Task AddAsync(ProjectTruthSnapshot snapshot,CancellationToken ct);
}
