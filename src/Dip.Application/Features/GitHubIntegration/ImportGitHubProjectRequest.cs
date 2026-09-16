// Purpose: Defines the ImportGitHubProjectRequest application contract.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record ImportGitHubProjectRequest(
    string Repository,
    string? ProjectName = null,
    DateTimeOffset? ReleaseDateUtc = null,
    bool SyncWorkspace = true,
    bool RunInitialQa = true);
