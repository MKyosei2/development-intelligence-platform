// Purpose: Defines the IEnterpriseEvidenceRepository application boundary.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
namespace Dip.Application.Abstractions.Persistence;

public interface IEnterpriseEvidenceRepository
{
    Task<EvidenceIntegrityReceipt?> LatestReceiptAsync(Guid organizationId,Guid projectId,CancellationToken ct);
    Task AddReceiptAsync(EvidenceIntegrityReceipt item,CancellationToken ct);
    Task<IReadOnlyList<EvidenceIntegrityReceipt>> ListReceiptsAsync(Guid projectId,int take,CancellationToken ct);
    Task<DistributedIngestionEvent?> FindByIdempotencyKeyAsync(Guid organizationId,string key,CancellationToken ct);
    Task AddIngestionAsync(DistributedIngestionEvent item,CancellationToken ct);
    Task<IReadOnlyList<DistributedIngestionEvent>> ListPendingAsync(Guid organizationId,int take,CancellationToken ct);
    Task<int> DeleteCompletedBeforeAsync(Guid organizationId,DateTimeOffset cutoff,CancellationToken ct);
}
