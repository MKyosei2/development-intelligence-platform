// Purpose: Provides the G it Hu bP ul lR eq ue st Co mm en tS er vi ce infrastructure implementation.
using Dip.Application.Abstractions.GitHub;
using Microsoft.Extensions.Options;
using Octokit;

namespace Dip.Infrastructure.GitHub;

public sealed class GitHubPullRequestCommentService :
    IGitHubPullRequestCommentService
{
    private readonly GitHubClient _client;
    private readonly GitHubOptions _options;

    public GitHubPullRequestCommentService(
        GitHubClient client,
        IOptions<GitHubOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<long> CreateOrUpdateQualityReportAsync(
        string owner,
        string repository,
        int pullRequestNumber,
        string markdownBody,
        CancellationToken cancellationToken)
    {
        var marker = _options.QualityReportMarker;
        var body = $"{marker}{Environment.NewLine}{markdownBody.Trim()}";

        var comments = await ExecuteWithRateLimitRetryAsync(
            () => _client.Issue.Comment.GetAllForIssue(
                owner,
                repository,
                pullRequestNumber),
            cancellationToken);

        var existing = comments
            .FirstOrDefault(x => x.Body?.Contains(
                marker,
                StringComparison.Ordinal) == true);

        if (existing is null)
        {
            var created = await ExecuteWithRateLimitRetryAsync(
                () => _client.Issue.Comment.Create(
                    owner,
                    repository,
                    pullRequestNumber,
                    body),
                cancellationToken);

            return created.Id;
        }

        var updated = await ExecuteWithRateLimitRetryAsync(
            () => _client.Issue.Comment.Update(
                owner,
                repository,
                existing.Id,
                body),
            cancellationToken);

        return updated.Id;
    }

    private async Task<T> ExecuteWithRateLimitRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt < _options.MaxRetryAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await operation();
            }
            catch (RateLimitExceededException ex)
            {
                lastError = ex;
                var delay = ex.GetRetryAfterTimeSpan();
                if (delay <= TimeSpan.Zero)
                    delay = TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));

                await Task.Delay(
                    delay > TimeSpan.FromMinutes(2)
                        ? TimeSpan.FromMinutes(2)
                        : delay,
                    cancellationToken);
            }
            catch (ApiException ex) when ((int)ex.StatusCode >= 500)
            {
                lastError = ex;
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Pow(2, attempt + 1)),
                    cancellationToken);
            }
        }

        throw new InvalidOperationException(
            "GitHub API operation exhausted retry attempts.",
            lastError);
    }
}
