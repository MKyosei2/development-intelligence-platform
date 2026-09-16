// Purpose: Defines the GitHubPullRequestFileDto application contract.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record GitHubPullRequestFileDto(
    string Path,
    string Status,
    int Additions,
    int Deletions);
