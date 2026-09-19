// Purpose: Implements persistence operations for enterprise evidence integrity and distributed ingestion.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class EnterpriseEvidenceRepository(DipDbContext db)
    : IEnterpriseEvidenceRepository
{
    public Task<EvidenceIntegrityReceipt?> LatestReceiptAsync(
        Guid organizationId,
        Guid projectId,
        CancellationToken ct)
    {
        return db.EvidenceIntegrityReceipts
            .AsNoTracking()
            .Where(x =>
                x.OrganizationId == organizationId
                && x.ProjectId == projectId)
            .OrderByDescending(x => x.IngestedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public Task AddReceiptAsync(
        EvidenceIntegrityReceipt item,
        CancellationToken ct) =>
        db.EvidenceIntegrityReceipts.AddAsync(item, ct).AsTask();

    public async Task<IReadOnlyList<EvidenceIntegrityReceipt>> ListReceiptsAsync(
        Guid projectId,
        int take,
        CancellationToken ct)
    {
        return await db.EvidenceIntegrityReceipts
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.IngestedAtUtc)
            .Take(take)
            .ToArrayAsync(ct);
    }

    public Task<DistributedIngestionEvent?> FindByIdempotencyKeyAsync(
        Guid organizationId,
        string key,
        CancellationToken ct)
    {
        return db.DistributedIngestionEvents.SingleOrDefaultAsync(
            x =>
                x.OrganizationId == organizationId
                && x.IdempotencyKey == key,
            ct);
    }

    public Task AddIngestionAsync(
        DistributedIngestionEvent item,
        CancellationToken ct) =>
        db.DistributedIngestionEvents.AddAsync(item, ct).AsTask();

    public async Task<IReadOnlyList<DistributedIngestionEvent>> ListPendingAsync(
        Guid organizationId,
        int take,
        CancellationToken ct)
    {
        return await db.DistributedIngestionEvents
            .Where(x =>
                x.OrganizationId == organizationId
                && (x.State == DistributedIngestionState.Pending
                    || x.State == DistributedIngestionState.Failed))
            .OrderBy(x => x.SourceSequence)
            .Take(take)
            .ToArrayAsync(ct);
    }

    public Task<int> DeleteCompletedBeforeAsync(
        Guid organizationId,
        DateTimeOffset cutoff,
        CancellationToken ct)
    {
        return db.DistributedIngestionEvents
            .Where(x =>
                x.OrganizationId == organizationId
                && x.State == DistributedIngestionState.Completed
                && x.ReceivedAtUtc < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
