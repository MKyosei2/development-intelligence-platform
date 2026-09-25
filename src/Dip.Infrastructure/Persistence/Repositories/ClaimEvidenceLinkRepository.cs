// Purpose: Implements Claim Evidence Link Repository infrastructure responsibilities for persistence.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class ClaimEvidenceLinkRepository : IClaimEvidenceLinkRepository
{
    private readonly DipDbContext _db;
    public ClaimEvidenceLinkRepository(DipDbContext db) => _db = db;

    public async Task ReplaceForClaimAsync(
        Guid claimId,
        IReadOnlyCollection<ClaimEvidenceLink> links,
        CancellationToken cancellationToken)
    {
        var existing = await _db.ClaimEvidenceLinks
            .Where(item => item.ClaimId == claimId)
            .ToListAsync(cancellationToken);

        var desiredEvidenceIds = links
            .Select(item => item.EvidenceRecordId)
            .ToHashSet();

        var stale = existing
            .Where(item => !desiredEvidenceIds.Contains(item.EvidenceRecordId))
            .ToArray();

        if (stale.Length > 0)
            _db.ClaimEvidenceLinks.RemoveRange(stale);

        var existingEvidenceIds = existing
            .Select(item => item.EvidenceRecordId)
            .ToHashSet();

        var missing = links
            .Where(item => !existingEvidenceIds.Contains(item.EvidenceRecordId))
            .ToArray();

        if (missing.Length > 0)
            _db.ClaimEvidenceLinks.AddRange(missing);
    }
}
