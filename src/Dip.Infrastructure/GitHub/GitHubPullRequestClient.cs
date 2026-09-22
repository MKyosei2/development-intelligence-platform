// Purpose: Owns paginated GitHub pull-request, commit, changed-file, and review retrieval.
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Features.GitHubIntegration;
using Dip.Infrastructure.QualityAutomation;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.GitHub;

internal sealed class GitHubPullRequestClient(GitHubAuthenticationProvider _auth)
{
    public async Task<IReadOnlyList<GitHubPullRequestSnapshotDto>> ListPullRequestsAsync(
            string owner,
            string repository,
            string state,
            CancellationToken cancellationToken)
    {
        GitHubJsonMapper.ValidateOwnerRepository(owner, repository);
        _auth.EnsureConfigured();

        var normalizedState = (state ?? "all").Trim().ToLowerInvariant() switch
        {
            "open" => "open",
            "closed" => "closed",
            "all" => "all",
            _ => throw new ArgumentException("Pull request state must be open, closed, or all.", nameof(state))
        };
        var repoPath = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}";
        var results = new List<GitHubPullRequestSnapshotDto>();
        var maxPages = Math.Clamp(_auth.Options.MaxPullRequestPages, 1, 50);

        for (var page = 1; page <= maxPages; page++)
        {
            using var request = await _auth.CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/pulls?state={normalizedState}&sort=updated&direction=desc&per_page=100&page={page}",
                cancellationToken);
            using var response = await _auth.HttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
            var count = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                count++;
                var number = GitHubJsonMapper.GetInt32(item, "number");
                if (number <= 0)
                    continue;
                var head = item.TryGetProperty("head", out var headNode) ? headNode : default;
                var commits = await LoadPullRequestCommitsAsync(repoPath, number, cancellationToken);
                var files = await LoadPullRequestFilesAsync(repoPath, number, cancellationToken);
                var reviews = await LoadPullRequestReviewsAsync(repoPath, number, cancellationToken);
                results.Add(new GitHubPullRequestSnapshotDto(
                    number,
                    GitHubJsonMapper.GetString(item, "title") ?? $"Pull request #{number}",
                    GitHubJsonMapper.GetString(item, "state") ?? "open",
                    head.ValueKind == JsonValueKind.Object ? GitHubJsonMapper.GetString(head, "sha") ?? string.Empty : string.Empty,
                    head.ValueKind == JsonValueKind.Object ? GitHubJsonMapper.GetString(head, "ref") ?? string.Empty : string.Empty,
                    GitHubJsonMapper.GetString(item, "html_url") ?? string.Empty,
                    GitHubJsonMapper.GetDate(item, "merged_at").HasValue,
                    commits.Count > 0
                        ? commits
                        : new[] { head.ValueKind == JsonValueKind.Object ? GitHubJsonMapper.GetString(head, "sha") ?? string.Empty : string.Empty }.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray(),
                    files,
                    reviews));
            }

            if (count < 100)
                break;
            if (page == maxPages)
                throw new InvalidOperationException($"Pull-request backfill reached the configured {maxPages * 100} item safety limit. Increase GitHub:MaxPullRequestPages to continue the explicit backfill.");
        }

        return results
            .OrderByDescending(item => item.Number)
            .ToArray();
    }

    private async Task<IReadOnlyList<string>> LoadPullRequestCommitsAsync(
            string repoPath,
            int number,
            CancellationToken cancellationToken)
    {
        var results = new List<string>();
        var maxPages = Math.Clamp(_auth.Options.MaxPullRequestCommitPages, 1, 3);
        for (var page = 1; page <= maxPages; page++)
        {
            using var request = await _auth.CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/pulls/{number}/commits?per_page=100&page={page}",
                cancellationToken);
            using var response = await _auth.HttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
            var count = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                count++;
                var sha = GitHubJsonMapper.GetString(item, "sha");
                if (!string.IsNullOrWhiteSpace(sha))
                    results.Add(sha);
            }
            if (count < 100)
                break;
            if (page == maxPages)
                throw new InvalidOperationException($"Pull request #{number} commit history reached the configured pagination safety limit.");
        }
        return results.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<IReadOnlyList<GitHubPullRequestFileDto>> LoadPullRequestFilesAsync(
            string repoPath,
            int number,
            CancellationToken cancellationToken)
    {
        var results = new List<GitHubPullRequestFileDto>();
        var maxPages = Math.Clamp(_auth.Options.MaxPullRequestFilePages, 1, 30);
        for (var page = 1; page <= maxPages; page++)
        {
            using var request = await _auth.CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/pulls/{number}/files?per_page=100&page={page}",
                cancellationToken);
            using var response = await _auth.HttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
            var count = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                count++;
                var path = GitHubJsonMapper.GetString(item, "filename") ?? string.Empty;
                if (string.IsNullOrWhiteSpace(path))
                    continue;
                results.Add(new GitHubPullRequestFileDto(
                    path,
                    GitHubJsonMapper.GetString(item, "status") ?? "modified",
                    GitHubJsonMapper.GetInt32(item, "additions"),
                    GitHubJsonMapper.GetInt32(item, "deletions")));
            }
            if (count < 100)
                break;
            if (page == maxPages)
                throw new InvalidOperationException($"Pull request #{number} changed-file history reached the configured pagination safety limit.");
        }
        return results;
    }

    private async Task<IReadOnlyList<GitHubPullRequestReviewDto>> LoadPullRequestReviewsAsync(
            string repoPath,
            int number,
            CancellationToken cancellationToken)
    {
        var results = new List<GitHubPullRequestReviewDto>();
        var maxPages = Math.Clamp(_auth.Options.MaxPullRequestReviewPages, 1, 50);
        for (var page = 1; page <= maxPages; page++)
        {
            using var request = await _auth.CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/pulls/{number}/reviews?per_page=100&page={page}",
                cancellationToken);
            using var response = await _auth.HttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
            var count = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                count++;
                var reviewer = item.TryGetProperty("user", out var user)
                    ? GitHubJsonMapper.GetString(user, "login") ?? "reviewer"
                    : "reviewer";
                results.Add(new GitHubPullRequestReviewDto(
                    reviewer,
                    GitHubJsonMapper.GetString(item, "state") ?? "commented",
                    GitHubJsonMapper.GetDate(item, "submitted_at")));
            }
            if (count < 100)
                break;
            if (page == maxPages)
                throw new InvalidOperationException($"Pull request #{number} review history reached the configured pagination safety limit.");
        }
        return results;
    }
}
