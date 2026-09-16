// Purpose: Defines the IEvidenceV2Repository application boundary.
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Abstractions.Persistence;

public interface IEvidenceV2Repository
{
    Task<EvidenceRecord?> FindBySourceEventAsync(Guid projectId,EvidenceSourceType sourceType,string sourceInstanceId,string sourceEventId,CancellationToken ct);
    Task<EvidenceRecord?> GetAsync(Guid projectId,Guid evidenceId,CancellationToken ct);
    Task AddAsync(EvidenceRecord evidence, CancellationToken ct);
}
