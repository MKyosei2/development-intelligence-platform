// Purpose: Persists and queries data for this application boundary.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class ProjectTruthRepository : IProjectTruthRepository
{
    private readonly DipDbContext _db;
    public ProjectTruthRepository(DipDbContext db) => _db = db;

    public Task<ProjectTruthSnapshot?> GetLatestAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        _db.ProjectTruthSnapshots
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.EvaluatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectTruthSnapshot>> ListRecentAsync(
        Guid projectId,
        int take,
        CancellationToken cancellationToken) =>
        await _db.ProjectTruthSnapshots
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.EvaluatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);

    public Task AddAsync(ProjectTruthSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return _db.ProjectTruthSnapshots.AddAsync(snapshot, cancellationToken).AsTask();
    }
}
