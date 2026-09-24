// Purpose: Implements G it Hu bV er si on Co nt ro lC on ne ct or infrastructure integration for i nt eg ra ti on s.
using Dip.Application.Abstractions.Integrations;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;
using Dip.Infrastructure.GitHub;
using Microsoft.Extensions.Options;
using Octokit;

namespace Dip.Infrastructure.Integrations;

public sealed class GitHubVersionControlConnector : IVersionControlConnector
{
    private readonly GitHubOptions _options; public VersionControlProvider Provider => VersionControlProvider.GitHub;
    public GitHubVersionControlConnector(IOptions<GitHubOptions> options) { _options = options.Value; }
    public async Task<NormalizedVersionControlChange> FetchAsync(VersionControlSyncRequest request, CancellationToken ct)
    {
        var parts = request.RepositoryLocator.Trim().TrimEnd('/').Replace("https://github.com/", string.Empty, StringComparison.OrdinalIgnoreCase).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new ArgumentException("GitHub repository must be owner/name or a github.com repository URL.", nameof(request));
        var owner = parts[^2];
        var repo = parts[^1].EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? parts[^1][..^4] : parts[^1];
        var client = new GitHubClient(new ProductHeaderValue(string.IsNullOrWhiteSpace(_options.ProductHeader) ? "development-intelligence-platform" : _options.ProductHeader));
        if (!string.IsNullOrWhiteSpace(_options.Token)) client.Credentials = new Credentials(_options.Token);
        ct.ThrowIfCancellationRequested();
        var commit = await client.Repository.Commit.Get(owner, repo, request.Revision);
        ct.ThrowIfCancellationRequested();
        var files = commit.Files.Select(f => new NormalizedChangedFile(f.Filename, f.Status.ToString(), f.Additions, f.Deletions)).ToArray();
        var branch = string.Empty;
        int? prNumber = request.PullRequestNumber;
        var title = commit.Commit.Message.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? commit.Sha;
        var url = commit.HtmlUrl;
        var reviews = new List<NormalizedReview>();
        if (prNumber.HasValue)
        {
            var pr = await client.PullRequest.Get(owner, repo, prNumber.Value);
            ct.ThrowIfCancellationRequested();
            branch = pr.Head.Ref;
            title = pr.Title;
            url = pr.HtmlUrl;
            var prFiles = await client.PullRequest.Files(owner, repo, prNumber.Value);
            files = prFiles.Select(f => new NormalizedChangedFile(f.FileName, f.Status.ToString(), f.Additions, f.Deletions)).ToArray();
            var reviewItems = await client.PullRequest.Review.GetAll(owner, repo, prNumber.Value);
            reviews.AddRange(reviewItems.Select(r => new NormalizedReview(r.User?.Login ?? "reviewer", r.State.ToString(), r.SubmittedAt)));
        }
        var releaseTag = (await client.Repository.Release.GetAll(owner, repo)).Where(r => string.Equals(r.TargetCommitish, request.Revision, StringComparison.OrdinalIgnoreCase) || string.Equals(r.TagName, request.Revision, StringComparison.OrdinalIgnoreCase)).OrderByDescending(r => r.CreatedAt).Select(r => r.TagName).FirstOrDefault();
        return new(request.ProjectId, Provider, request.RepositoryKey, $"https://github.com/{owner}/{repo}", commit.Sha, string.IsNullOrWhiteSpace(branch) ? "unknown" : branch, title, url, commit.Commit.Author.Date, files, prNumber, reviews, releaseTag, commit.Parents.Count > 1);
    }
}
