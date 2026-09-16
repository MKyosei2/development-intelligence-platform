// Purpose: Defines the I Gi tH ub Re po si to ry Ca ta lo g application boundary.
using Dip.Application.Features.GitHubIntegration;

namespace Dip.Application.Abstractions.GitHub;

public interface IGitHubRepositoryCatalog
{
    Task<GitHubConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<GitHubRepositorySummaryDto>> ListRepositoriesAsync(
        string? search,
        string? visibility,
        CancellationToken cancellationToken);

    Task<GitHubRepositoryDetailsDto> GetRepositoryAsync(
        string owner,
        string repository,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GitHubPullRequestSnapshotDto>> ListPullRequestsAsync(
        string owner,
        string repository,
        string state,
        CancellationToken cancellationToken);

    Task<GitHubWorkspaceSyncDto> SyncWorkspaceAsync(
        Guid projectId,
        string owner,
        string repository,
        string branch,
        CancellationToken cancellationToken);

    Task<GitHubWorkspaceSyncDto?> GetWorkspaceAsync(
        Guid projectId,
        CancellationToken cancellationToken);
}
