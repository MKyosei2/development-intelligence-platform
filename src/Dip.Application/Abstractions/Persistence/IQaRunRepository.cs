// Purpose: Defines the IQaRunRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IQaRunRepository
{
    Task AddAsync(QaRun run, CancellationToken ct);
    Task<QaRun?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<QaRun>> ListRecentAsync(Guid projectId, int take, CancellationToken ct);
}
