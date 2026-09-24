// Purpose: Persists and queries performance simulation runs used by hardware intelligence.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class PerformanceSimulationRepository(DipDbContext db) : IPerformanceSimulationRepository
{
    public Task AddAsync(PerformanceSimulationRun run, CancellationToken ct)
        => db.PerformanceSimulationRuns.AddAsync(run, ct).AsTask();

    public Task<PerformanceSimulationRun?> GetAsync(Guid id, CancellationToken ct)
        => db.PerformanceSimulationRuns.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<PerformanceSimulationRun>> ListRecentAsync(
        Guid projectId,
        int take,
        CancellationToken ct)
        => await db.PerformanceSimulationRuns
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);
}
