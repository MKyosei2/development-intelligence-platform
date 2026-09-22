// Purpose: Defines the IQaFindingRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IQaFindingRepository
{
    Task AddRangeAsync(IEnumerable<QaFinding> findings, CancellationToken ct);
    Task<QaFinding?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<QaFinding>> ListByRunAsync(Guid runId, CancellationToken ct);
    Task<IReadOnlyList<QaFinding>> ListRecentByProjectAsync(Guid projectId, int take, CancellationToken ct);
}
