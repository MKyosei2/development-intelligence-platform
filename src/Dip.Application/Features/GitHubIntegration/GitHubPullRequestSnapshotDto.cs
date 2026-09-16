// Purpose: Defines the GitHubPullRequestSnapshotDto application contract.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record GitHubPullRequestSnapshotDto(
    int Number,
    string Title,
    string State,
    string HeadSha,
    string HeadBranch,
    string HtmlUrl,
    bool Merged,
    IReadOnlyList<string> CommitShas,
    IReadOnlyList<GitHubPullRequestFileDto> Files,
    IReadOnlyList<GitHubPullRequestReviewDto> Reviews);
