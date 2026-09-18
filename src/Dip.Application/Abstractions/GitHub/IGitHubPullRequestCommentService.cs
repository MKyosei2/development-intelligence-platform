// Purpose: Orchestrates the DIP workflow for I Gi tH ub Pu ll Re qu es tC om me nt Se rv ic e.
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
