// Purpose: Defines the contract for creating or updating GitHub pull-request quality report comments.
namespace Dip.Application.Abstractions.GitHub;

public interface IGitHubPullRequestCommentService
{
    Task<long> CreateOrUpdateQualityReportAsync(
        string owner,
        string repository,
        int pullRequestNumber,
        string markdownBody,
        CancellationToken cancellationToken);
}
