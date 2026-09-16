// Purpose: Defines the GitHubProjectRepositoryDto application contract.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record GitHubProjectRepositoryDto(
    Guid ProjectId,
    string ProjectName,
    string RepositoryUrl,
    string DefaultBranch,
    GitHubConnectionStatusDto Connection,
    GitHubRepositoryDetailsDto? Repository,
    GitHubWorkspaceSyncDto? Workspace,
    string? Warning);
