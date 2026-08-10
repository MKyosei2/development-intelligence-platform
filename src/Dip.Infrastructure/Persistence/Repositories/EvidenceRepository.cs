using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class EvidenceRepository : IEvidenceRepository
{
    private readonly DipDbContext _db;
    public EvidenceRepository(DipDbContext db) => _db = db;

    public Task AddAsync(EvidenceRecord evidence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return _db.EvidenceRecords.AddAsync(evidence, cancellationToken).AsTask();
    }

    public Task AddRangeAsync(IEnumerable<EvidenceRecord> evidence, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _db.EvidenceRecords.AddRange(evidence);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(
        Guid projectId,
        DateTimeOffset? observedSince,
        CancellationToken cancellationToken)
    {
        var query = _db.EvidenceRecords.AsNoTracking().Where(item => item.ProjectId == projectId);
        if (observedSince is { } since)
            query = query.Where(item => item.ObservedAtUtc >= since);

        return await query
            .OrderByDescending(item => item.ObservedAtUtc)
            .Take(1000)
            .ToListAsync(cancellationToken);
    }
}
