// Purpose: Implements persistence operations for ClaimEvidenceAssessment.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;
namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class ClaimEvidenceAssessmentRepository(DipDbContext db) : IClaimEvidenceAssessmentRepository
{
    public Task AddAsync(ClaimEvidenceAssessmentRecord assessment,CancellationToken ct)=>db.ClaimEvidenceAssessmentRecords.AddAsync(assessment,ct).AsTask();
    public Task<ClaimEvidenceAssessmentRecord?> GetLatestForClaimAsync(Guid claimId,CancellationToken ct)=>db.ClaimEvidenceAssessmentRecords.AsNoTracking().Where(x=>x.ClaimId==claimId).OrderByDescending(x=>x.AssessedAtUtc).FirstOrDefaultAsync(ct);
    public async Task<IReadOnlyList<ClaimEvidenceAssessmentRecord>> ListForProjectAsync(Guid projectId,int take,CancellationToken ct)=>await db.ClaimEvidenceAssessmentRecords.AsNoTracking().Where(x=>x.ProjectId==projectId).OrderByDescending(x=>x.AssessedAtUtc).Take(Math.Clamp(take,1,2000)).ToListAsync(ct);
}
