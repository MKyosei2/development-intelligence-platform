// Purpose: Defines the IClaimEvidenceAssessmentRepository application boundary.
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Abstractions.Persistence;

public interface IClaimEvidenceAssessmentRepository
{
    Task AddAsync(ClaimEvidenceAssessmentRecord assessment, CancellationToken ct);
    Task<ClaimEvidenceAssessmentRecord?> GetLatestForClaimAsync(Guid claimId, CancellationToken ct);
    Task<IReadOnlyList<ClaimEvidenceAssessmentRecord>> ListForProjectAsync(Guid projectId, int take, CancellationToken ct);
}
