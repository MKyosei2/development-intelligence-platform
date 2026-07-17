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
