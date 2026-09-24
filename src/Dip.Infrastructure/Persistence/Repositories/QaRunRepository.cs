// Purpose: Persists and queries QA run state for quality execution history.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class QaRunRepository(DipDbContext db) : IQaRunRepository
{
    public Task AddAsync(QaRun run, CancellationToken ct)
        => db.QaRuns.AddAsync(run, ct).AsTask();

    public Task<QaRun?> GetAsync(Guid id, CancellationToken ct)
        => db.QaRuns.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<QaRun>> ListRecentAsync(
        Guid projectId,
        int take,
        CancellationToken ct)
        => await db.QaRuns
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);
}
