// Purpose: Persists and queries reproduction attempts associated with QA findings.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class ReproductionAttemptRepository(DipDbContext db) : IReproductionAttemptRepository
{
    public Task AddAsync(ReproductionAttempt attempt, CancellationToken ct)
        => db.ReproductionAttempts.AddAsync(attempt, ct).AsTask();

    public async Task<IReadOnlyList<ReproductionAttempt>> ListByFindingAsync(
        Guid findingId,
        CancellationToken ct)
        => await db.ReproductionAttempts
            .AsNoTracking()
            .Where(x => x.QaFindingId == findingId)
            .OrderBy(x => x.AttemptNumber)
            .ToListAsync(ct);
}
