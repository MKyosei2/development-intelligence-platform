// Purpose: Defines the IQualityAutomationRepository application boundary.
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
