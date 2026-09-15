using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class QualityAutomationRepository(DipDbContext db) : IQualityAutomationRepository
{
    public Task<QualityAutomationProfile?> GetProfileAsync(Guid projectId, CancellationToken ct)
        => db.QualityAutomationProfiles.FirstOrDefaultAsync(x => x.ProjectId == projectId, ct);

    public Task AddProfileAsync(QualityAutomationProfile profile, CancellationToken ct)
        => db.QualityAutomationProfiles.AddAsync(profile, ct).AsTask();

    public Task<GoldenMasterProfile?> GetGoldenAsync(Guid id, CancellationToken ct)
        => db.GoldenMasterProfiles.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<GoldenMasterProfile?> FindActiveGoldenAsync(Guid projectId, string captureKey, CancellationToken ct)
        => db.GoldenMasterProfiles
            .Where(x => x.ProjectId == projectId && x.CaptureKey == captureKey && x.Active)
            .OrderByDescending(x => x.ApprovedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<GoldenMasterProfile>> ListGoldensAsync(Guid projectId, CancellationToken ct)
        => await db.GoldenMasterProfiles.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.ApprovedAtUtc)
            .Take(1000)
            .ToListAsync(ct);

    public Task AddGoldenAsync(GoldenMasterProfile profile, CancellationToken ct)
        => db.GoldenMasterProfiles.AddAsync(profile, ct).AsTask();

    public Task AddComparisonAsync(VisualComparisonRecord record, CancellationToken ct)
        => db.VisualComparisonRecords.AddAsync(record, ct).AsTask();

    public async Task<IReadOnlyList<VisualComparisonRecord>> ListComparisonsAsync(Guid projectId, int take, CancellationToken ct)
        => await db.VisualComparisonRecords.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.ComparedAtUtc)
            .Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(ct);

    public Task<AssetQaFindingRecord?> FindOpenFindingAsync(Guid projectId, string fingerprint, CancellationToken ct)
        => db.AssetQaFindingRecords.FirstOrDefaultAsync(
            x => x.ProjectId == projectId && x.Fingerprint == fingerprint && x.State == AssetQaFindingState.Open,
            ct);

    public Task AddFindingAsync(AssetQaFindingRecord finding, CancellationToken ct)
        => db.AssetQaFindingRecords.AddAsync(finding, ct).AsTask();

    public async Task<IReadOnlyList<AssetQaFindingRecord>> ListFindingsAsync(Guid projectId, AssetQaFindingState? state, int take, CancellationToken ct)
    {
        var query = db.AssetQaFindingRecords.AsNoTracking().Where(x => x.ProjectId == projectId);
        if (state.HasValue) query = query.Where(x => x.State == state.Value);
        return await query.OrderByDescending(x => x.ObservedAtUtc)
            .Take(Math.Clamp(take, 1, 2000))
            .ToListAsync(ct);
    }
}

public sealed class ClaimEvidenceAssessmentRepository(DipDbContext db) : IClaimEvidenceAssessmentRepository
{
    public Task AddAsync(ClaimEvidenceAssessmentRecord assessment, CancellationToken ct)
        => db.ClaimEvidenceAssessmentRecords.AddAsync(assessment, ct).AsTask();

    public Task<ClaimEvidenceAssessmentRecord?> GetLatestForClaimAsync(Guid claimId, CancellationToken ct)
        => db.ClaimEvidenceAssessmentRecords.AsNoTracking()
            .Where(x => x.ClaimId == claimId)
            .OrderByDescending(x => x.AssessedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ClaimEvidenceAssessmentRecord>> ListForProjectAsync(Guid projectId, int take, CancellationToken ct)
        => await db.ClaimEvidenceAssessmentRecords.AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.AssessedAtUtc)
            .Take(Math.Clamp(take, 1, 2000))
            .ToListAsync(ct);
}
