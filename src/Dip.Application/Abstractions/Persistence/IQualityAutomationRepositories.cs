// Purpose: Defines the I Qu al it yA ut om at io nR ep os it or ie s application boundary.
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Abstractions.Persistence;

public interface IQualityAutomationRepository
{
    Task<QualityAutomationProfile?> GetProfileAsync(Guid projectId, CancellationToken ct);
    Task AddProfileAsync(QualityAutomationProfile profile, CancellationToken ct);

    Task<GoldenMasterProfile?> GetGoldenAsync(Guid id, CancellationToken ct);
    Task<GoldenMasterProfile?> FindActiveGoldenAsync(Guid projectId, string captureKey, CancellationToken ct);
    Task<IReadOnlyList<GoldenMasterProfile>> ListGoldensAsync(Guid projectId, CancellationToken ct);
    Task AddGoldenAsync(GoldenMasterProfile profile, CancellationToken ct);

    Task AddComparisonAsync(VisualComparisonRecord record, CancellationToken ct);
    Task<IReadOnlyList<VisualComparisonRecord>> ListComparisonsAsync(Guid projectId, int take, CancellationToken ct);

    Task<AssetQaFindingRecord?> FindOpenFindingAsync(Guid projectId, string fingerprint, CancellationToken ct);
    Task AddFindingAsync(AssetQaFindingRecord finding, CancellationToken ct);
    Task<IReadOnlyList<AssetQaFindingRecord>> ListFindingsAsync(Guid projectId, AssetQaFindingState? state, int take, CancellationToken ct);
}

public interface IClaimEvidenceAssessmentRepository
{
    Task AddAsync(ClaimEvidenceAssessmentRecord assessment, CancellationToken ct);
    Task<ClaimEvidenceAssessmentRecord?> GetLatestForClaimAsync(Guid claimId, CancellationToken ct);
    Task<IReadOnlyList<ClaimEvidenceAssessmentRecord>> ListForProjectAsync(Guid projectId, int take, CancellationToken ct);
}
