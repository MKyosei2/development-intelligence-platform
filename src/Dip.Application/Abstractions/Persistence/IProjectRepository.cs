// Purpose: Defines the IProjectRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct);
    Task<IReadOnlyList<Project>> ListAllAsync(CancellationToken ct)=>ListAsync(ct);
    Task AddAsync(Project project,CancellationToken ct);
}
