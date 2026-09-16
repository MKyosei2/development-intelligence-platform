// Purpose: Defines the IClaimEvidenceLinkRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IClaimEvidenceLinkRepository
{
    Task ReplaceForClaimAsync(Guid claimId,IReadOnlyCollection<ClaimEvidenceLink> links,CancellationToken ct);
}
