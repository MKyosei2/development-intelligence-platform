// Purpose: Defines the I Gi tH ub Pu ll Re qu es tC om me nt Se rv ic e application boundary.
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
