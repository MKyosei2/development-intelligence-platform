// Purpose: Defines the IManagementSyncAdapter application boundary.
namespace Dip.Application.Features.Management;

public interface IManagementSyncAdapter { string ProviderName { get;
 } Task<IReadOnlyList<ManagementExternalItem>> PullAsync(Guid projectId, CancellationToken ct);
 Task<ManagementExternalItem> UpsertAsync(Guid projectId, ManagementActionDto action, string? externalId, CancellationToken ct);
 }
