// Purpose: Persists and queries normalized QA findings for runs and projects.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class QaFindingRepository(DipDbContext db) : IQaFindingRepository
{
    public Task AddRangeAsync(IEnumerable<QaFinding> findings, CancellationToken ct)
    {
        db.QaFindings.AddRange(findings);
        return Task.CompletedTask;
    }

    public Task<QaFinding?> GetAsync(Guid id, CancellationToken ct)
        => db.QaFindings.SingleOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<QaFinding>> ListByRunAsync(
        Guid runId,
        CancellationToken ct)
        => await db.QaFindings
            .AsNoTracking()
            .Where(x => x.QaRunId == runId)
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.Title)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<QaFinding>> ListRecentByProjectAsync(
        Guid projectId,
        int take,
        CancellationToken ct)
        => await db.QaFindings
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.ObservedAtUtc)
            .Take(Math.Clamp(take, 1, 10000))
            .ToListAsync(ct);
}
