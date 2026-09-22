// Purpose: Backfills GitHub pull-request history into the Development Graph for imports and workspace synchronization.
using System.Text.Json;
using Dip.Api.Authorization;
using Dip.Api.Hubs;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.GitHubIntegration;
using Dip.Application.Features.Projects;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

internal sealed class GitHubPullRequestBackfillService(DevelopmentGraphService developmentGraph)
{
    internal async Task BackfillAsync(
            Guid projectId,
            GitHubRepositorySummaryDto repository,
            IReadOnlyList<GitHubPullRequestSnapshotDto> pullRequests,
            CancellationToken cancellationToken)
    {
        foreach (var pullRequest in pullRequests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(pullRequest.HeadSha))
                continue;

            var revisions = pullRequest.CommitShas
                .Where(revision => !string.IsNullOrWhiteSpace(revision))
                .Append(pullRequest.HeadSha)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var revision in revisions)
            {
                var isHead = revision.Equals(pullRequest.HeadSha, StringComparison.OrdinalIgnoreCase);
                var normalized = new NormalizedVersionControlChange(
                    projectId,
                    VersionControlProvider.GitHub,
                    repository.FullName,
                    repository.HtmlUrl,
                    revision,
                    string.IsNullOrWhiteSpace(pullRequest.HeadBranch) ? repository.DefaultBranch : pullRequest.HeadBranch,
                    pullRequest.Title,
                    pullRequest.HtmlUrl,
                    DateTimeOffset.UtcNow,
                    isHead
                        ? pullRequest.Files.Select(file => new NormalizedChangedFile(
                            file.Path,
                            file.Status,
                            file.Additions,
                            file.Deletions)).ToArray()
                        : Array.Empty<NormalizedChangedFile>(),
                    pullRequest.Number,
                    isHead
                        ? pullRequest.Reviews.Select(review => new NormalizedReview(
                            review.Reviewer,
                            review.State,
                            review.SubmittedAtUtc)).ToArray()
                        : Array.Empty<NormalizedReview>(),
                    null,
                    pullRequest.Merged);
                await developmentGraph.IngestVersionControlChangeAsync(normalized, cancellationToken);
            }

            var key = $"pr:{repository.FullName}:{pullRequest.Number}";
            var node = await developmentGraph.FindNodeAsync(
                projectId,
                DevelopmentGraphNodeType.PullRequest,
                key,
                cancellationToken);
            if (node is not null)
            {
                var state = pullRequest.Merged
                    ? "Merged"
                    : pullRequest.State.Equals("open", StringComparison.OrdinalIgnoreCase) ? "Open" : pullRequest.State;
                await developmentGraph.UpsertNodeAsync(new(
                    projectId,
                    DevelopmentGraphNodeType.PullRequest,
                    node.Key,
                    $"PR #{pullRequest.Number}: {pullRequest.Title}",
                    state,
                    "GitHub",
                    pullRequest.HtmlUrl,
                    node.MetadataJson), cancellationToken);
            }
        }
    }
}
