// Purpose: Defines the IPerformanceSimulationRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IPerformanceSimulationRepository
{
    Task AddAsync(PerformanceSimulationRun run, CancellationToken ct);
    Task<PerformanceSimulationRun?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PerformanceSimulationRun>> ListRecentAsync(Guid projectId, int take, CancellationToken ct);
}
