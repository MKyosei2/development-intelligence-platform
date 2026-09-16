// Purpose: Defines the GitHubPullRequestReviewDto application contract.
namespace Dip.Application.Features.GitHubIntegration;

public sealed record GitHubPullRequestReviewDto(
    string Reviewer,
    string State,
    DateTimeOffset? SubmittedAtUtc);
