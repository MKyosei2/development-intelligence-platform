using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class ProjectClaimRepository : IProjectClaimRepository
{
    private readonly DipDbContext _db;
    public ProjectClaimRepository(DipDbContext db) => _db = db;

    public Task<ProjectClaim?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.ProjectClaims.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ProjectClaim>> ListByProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken) =>
        await _db.ProjectClaims
            .Where(item => item.ProjectId == projectId)
            .OrderByDescending(item => item.ClaimedAtUtc)
            .Take(500)
            .ToListAsync(cancellationToken);

    public Task AddAsync(ProjectClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return _db.ProjectClaims.AddAsync(claim, cancellationToken).AsTask();
    }
}
