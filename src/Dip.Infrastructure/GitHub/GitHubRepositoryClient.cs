// Purpose: Owns GitHub repository discovery, repository metadata, workflow capability probes, and repository filtering.
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

internal sealed class GitHubRepositoryClient(GitHubAuthenticationProvider _auth)
{
    public async Task<IReadOnlyList<GitHubRepositorySummaryDto>> ListRepositoriesAsync(
            string? search,
            string? visibility,
            CancellationToken cancellationToken)
    {
        _auth.EnsureConfigured();
        var results = new List<GitHubRepositorySummaryDto>();
        var maxPages = Math.Clamp(_auth.Options.MaxRepositoryPages, 1, 50);

        for (var page = 1; page <= maxPages; page++)
        {
            var path = _auth.Options.HasGitHubAppCredentials
                ? $"installation/repositories?per_page=100&page={page}"
                : $"user/repos?per_page=100&page={page}&sort=updated&affiliation=owner,collaborator,organization_member";

            using var request = await _auth.CreateRequestAsync(HttpMethod.Get, path, cancellationToken);
            using var response = await _auth.HttpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
            var array = _auth.Options.HasGitHubAppCredentials
                ? document.RootElement.GetProperty("repositories")
                : document.RootElement;

            var count = 0;
            foreach (var item in array.EnumerateArray())
            {
                count++;
                var mapped = GitHubJsonMapper.MapRepository(item);
                if (!GitHubJsonMapper.MatchesSearch(mapped, search) || !GitHubJsonMapper.MatchesVisibility(mapped, visibility))
                    continue;
                results.Add(mapped);
            }

            if (count < 100)
                break;
        }

        return results
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<GitHubRepositoryDetailsDto> GetRepositoryAsync(
            string owner,
            string repository,
            CancellationToken cancellationToken)
    {
        GitHubJsonMapper.ValidateOwnerRepository(owner, repository);
        _auth.EnsureConfigured();

        var repoPath = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}";
        using var repoRequest = await _auth.CreateRequestAsync(HttpMethod.Get, repoPath, cancellationToken);
        using var repoResponse = await _auth.HttpClient.SendAsync(repoRequest, cancellationToken);
        repoResponse.EnsureSuccessStatusCode();
        using var repoDocument = await GitHubJsonMapper.ReadJsonAsync(repoResponse, cancellationToken);
        var summary = GitHubJsonMapper.MapRepository(repoDocument.RootElement);

        var contentsReadable = false;
        string? latestSha = null;
        string? latestMessage = null;
        DateTimeOffset? latestAt = null;
        try
        {
            using var commitRequest = await _auth.CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/commits/{Uri.EscapeDataString(summary.DefaultBranch)}",
                cancellationToken);
            using var commitResponse = await _auth.HttpClient.SendAsync(commitRequest, cancellationToken);
            contentsReadable = commitResponse.IsSuccessStatusCode;
            if (contentsReadable)
            {
                using var commitDocument = await GitHubJsonMapper.ReadJsonAsync(commitResponse, cancellationToken);
                latestSha = GitHubJsonMapper.GetString(commitDocument.RootElement, "sha");
                if (commitDocument.RootElement.TryGetProperty("commit", out var commit))
                {
                    latestMessage = GitHubJsonMapper.GetString(commit, "message")?.Split('\n')[0];
                    if (commit.TryGetProperty("committer", out var committer))
                        latestAt = GitHubJsonMapper.GetDate(committer, "date");
                }
            }
        }
        catch (HttpRequestException)
        {
            contentsReadable = false;
        }

        var actionsReadable = false;
        var runs = new List<GitHubWorkflowRunDto>();
        try
        {
            using var runsRequest = await _auth.CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/actions/runs?branch={Uri.EscapeDataString(summary.DefaultBranch)}&per_page=30",
                cancellationToken);
            using var runsResponse = await _auth.HttpClient.SendAsync(runsRequest, cancellationToken);
            actionsReadable = runsResponse.IsSuccessStatusCode;
            if (actionsReadable)
            {
                using var runsDocument = await GitHubJsonMapper.ReadJsonAsync(runsResponse, cancellationToken);
                if (runsDocument.RootElement.TryGetProperty("workflow_runs", out var array))
                {
                    foreach (var run in array.EnumerateArray())
                        runs.Add(GitHubJsonMapper.MapWorkflowRun(run));
                }
            }
        }
        catch (HttpRequestException)
        {
            actionsReadable = false;
        }

        var pullRequestsReadable = false;
        try
        {
            using var pullsRequest = await _auth.CreateRequestAsync(
                HttpMethod.Get,
                $"{repoPath}/pulls?state=open&sort=updated&direction=desc&per_page=1&page=1",
                cancellationToken);
            using var pullsResponse = await _auth.HttpClient.SendAsync(pullsRequest, cancellationToken);
            pullRequestsReadable = pullsResponse.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            pullRequestsReadable = false;
        }

        var pullRequests = Array.Empty<GitHubPullRequestSnapshotDto>();

        return new GitHubRepositoryDetailsDto(
            summary,
            latestSha,
            latestMessage,
            latestAt,
            runs,
            pullRequests,
            new GitHubCapabilityProbeDto(
                Metadata: true,
                Contents: contentsReadable,
                PullRequests: pullRequestsReadable,
                Actions: actionsReadable));
    }
}
