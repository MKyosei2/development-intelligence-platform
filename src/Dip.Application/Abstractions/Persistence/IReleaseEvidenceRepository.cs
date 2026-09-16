// Purpose: Defines the IReleaseEvidenceRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IReleaseEvidenceRepository
{
    Task AddAsync(ReleaseEvidenceBundle bundle,CancellationToken ct);
    Task<ReleaseEvidenceBundle?> GetLatestAsync(Guid projectId,CancellationToken ct);
    Task<IReadOnlyList<ReleaseEvidenceBundle>> ListAsync(Guid projectId,int take,CancellationToken ct);
}
