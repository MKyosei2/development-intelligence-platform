// Purpose: Implements persistence operations for EvidenceV2.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class EvidenceV2Repository(DipDbContext db) : IEvidenceV2Repository
{
    public Task<EvidenceRecord?> FindBySourceEventAsync(Guid projectId, EvidenceSourceType sourceType, string sourceInstanceId, string sourceEventId, CancellationToken ct) => db.EvidenceRecords.AsNoTracking().FirstOrDefaultAsync(item => item.ProjectId == projectId && item.SourceType == sourceType && item.SourceInstanceId == sourceInstanceId && item.SourceEventId == sourceEventId, ct);
    public Task<EvidenceRecord?> GetAsync(Guid projectId, Guid evidenceId, CancellationToken ct) => db.EvidenceRecords.AsNoTracking().FirstOrDefaultAsync(item => item.ProjectId == projectId && item.Id == evidenceId, ct);
    public Task AddAsync(EvidenceRecord evidence, CancellationToken ct) => db.EvidenceRecords.AddAsync(evidence, ct).AsTask();
}
