// Purpose: Defines the GitHubRepositoryDetailsDto application contract.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record GitHubRepositoryDetailsDto(
    GitHubRepositorySummaryDto Repository,
    string? LatestCommitSha,
    string? LatestCommitMessage,
    DateTimeOffset? LatestCommitAtUtc,
    IReadOnlyList<GitHubWorkflowRunDto> WorkflowRuns,
    IReadOnlyList<GitHubPullRequestSnapshotDto>? PullRequests = null,
    GitHubCapabilityProbeDto? Capabilities = null);
