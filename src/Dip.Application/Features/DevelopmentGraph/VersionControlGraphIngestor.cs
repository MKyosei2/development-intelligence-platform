// Purpose: Projects normalized version-control changes into the Development Graph using a single save boundary.
using System.Text.Json;
using System.Text.Json.Nodes;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

internal sealed class VersionControlGraphIngestor
{
    private const int MaxFilesPerVersionControlChange = 5000;
    private readonly DevelopmentGraphMutationService _mutations;
    private readonly IUnitOfWork _uow;

    internal VersionControlGraphIngestor(DevelopmentGraphMutationService mutations, IUnitOfWork uow)
    {
        _mutations = mutations;
        _uow = uow;
    }

    public async Task IngestVersionControlChangeAsync(
            NormalizedVersionControlChange change,
            CancellationToken ct)
        {
            if (change.Files.Count > MaxFilesPerVersionControlChange)
            {
                throw new InvalidDataException(
                    $"Version-control evidence contains {change.Files.Count} files, "
                    + $"which exceeds the fail-closed limit of {MaxFilesPerVersionControlChange}. "
                    + "The event was rejected instead of silently truncating evidence.");
            }
    
            var nodes = new Dictionary<(DevelopmentGraphNodeType Type, string Key), DevelopmentGraphNode>();
            var edges = new Dictionary<(Guid Source, Guid Target, DevelopmentGraphEdgeType Type), DevelopmentGraphEdge>();
    
            var repository = await _mutations.UpsertNodeCoreAsync(
                new(change.ProjectId, DevelopmentGraphNodeType.Repository, $"repo:{change.RepositoryKey}", change.RepositoryKey,
                    "Active", change.Provider.ToString(), change.RepositoryLocator,
                    JsonSerializer.Serialize(new { change.RepositoryLocator })),
                nodes,
                ct);
    
            var branch = await _mutations.UpsertNodeCoreAsync(
                new(change.ProjectId, DevelopmentGraphNodeType.Branch, $"branch:{change.RepositoryKey}:{change.Branch}",
                    change.Branch, "Active", change.Provider.ToString(), null,
                    JsonSerializer.Serialize(new { change.RepositoryKey })),
                nodes,
                ct);
    
            await _mutations.LinkCoreAsync(
                new(change.ProjectId, repository.Id, branch.Id, DevelopmentGraphEdgeType.Contains, 70m, false, "{}"),
                repository,
                branch,
                edges,
                ct);
    
            var commit = await _mutations.UpsertNodeCoreAsync(
                new(
                    change.ProjectId,
                    DevelopmentGraphNodeType.Commit,
                    $"commit:{change.RepositoryKey}:{change.Revision}",
                    change.Title,
                    string.IsNullOrWhiteSpace(change.ReleaseTag) ? "Observed" : "Released",
                    change.Provider.ToString(),
                    change.Url,
                    JsonSerializer.Serialize(new { change.Revision, change.Branch, change.IsMerge, change.ReleaseTag })),
                nodes,
                ct);
    
            await _mutations.LinkCoreAsync(
                new(change.ProjectId, branch.Id, commit.Id, DevelopmentGraphEdgeType.Contains, 80m, false, "{}"),
                branch,
                commit,
                edges,
                ct);
    
            foreach (var file in change.Files)
            {
                var fileNode = await _mutations.UpsertNodeCoreAsync(
                    new(
                        change.ProjectId,
                        DevelopmentGraphNodeType.File,
                        $"file:{change.RepositoryKey}:{DevelopmentGraphMetadata.Normalize(file.Path)}",
                        file.Path,
                        file.Status,
                        change.Provider.ToString(),
                        null,
                        JsonSerializer.Serialize(new { file.Status, file.Additions, file.Deletions })),
                    nodes,
                    ct);
    
                await _mutations.LinkCoreAsync(
                    new(
                        change.ProjectId,
                        commit.Id,
                        fileNode.Id,
                        DevelopmentGraphEdgeType.Changes,
                        Math.Clamp(30m + (file.Additions + file.Deletions) / 10m, 30m, 100m),
                        false,
                        "{}"),
                    commit,
                    fileNode,
                    edges,
                    ct);
    
                var componentKey = DevelopmentGraphMetadata.InferComponent(file.Path);
                if (componentKey is null)
                    continue;
    
                var component = await _mutations.UpsertNodeCoreAsync(
                    new(
                        change.ProjectId,
                        DevelopmentGraphNodeType.Component,
                        $"component:{change.RepositoryKey}:{componentKey}",
                        componentKey,
                        "Active",
                        change.Provider.ToString(),
                        null,
                        "{}"),
                    nodes,
                    ct);
    
                await _mutations.LinkCoreAsync(
                    new(change.ProjectId, fileNode.Id, component.Id, DevelopmentGraphEdgeType.BelongsTo, 70m, false, "{}"),
                    fileNode,
                    component,
                    edges,
                    ct);
            }
    
            if (change.PullRequestNumber.HasValue)
            {
                var pullRequest = await _mutations.UpsertNodeCoreAsync(
                    new(
                        change.ProjectId,
                        DevelopmentGraphNodeType.PullRequest,
                        $"pr:{change.RepositoryKey}:{change.PullRequestNumber.Value}",
                        $"PR #{change.PullRequestNumber.Value}: {change.Title}",
                        "Open",
                        change.Provider.ToString(),
                        change.Url,
                        JsonSerializer.Serialize(new { change.PullRequestNumber })),
                    nodes,
                    ct);
    
                await _mutations.LinkCoreAsync(
                    new(change.ProjectId, pullRequest.Id, commit.Id, DevelopmentGraphEdgeType.Contains, 85m, false, "{}"),
                    pullRequest,
                    commit,
                    edges,
                    ct);
    
                foreach (var review in change.Reviews ?? Array.Empty<NormalizedReview>())
                {
                    var key = $"review:{change.RepositoryKey}:{change.PullRequestNumber.Value}:{review.Reviewer}";
                    var reviewNode = await _mutations.UpsertNodeCoreAsync(
                        new(
                            change.ProjectId,
                            DevelopmentGraphNodeType.Review,
                            key,
                            review.Reviewer,
                            review.State,
                            change.Provider.ToString(),
                            change.Url,
                            JsonSerializer.Serialize(review)),
                        nodes,
                        ct);
    
                    await _mutations.LinkCoreAsync(
                        new(change.ProjectId, reviewNode.Id, pullRequest.Id, DevelopmentGraphEdgeType.Reviews, 75m, false, "{}"),
                        reviewNode,
                        pullRequest,
                        edges,
                        ct);
                }
            }
    
            if (!string.IsNullOrWhiteSpace(change.ReleaseTag))
            {
                var release = await _mutations.UpsertNodeCoreAsync(
                    new(
                        change.ProjectId,
                        DevelopmentGraphNodeType.Release,
                        $"release:{change.RepositoryKey}:{change.ReleaseTag}",
                        change.ReleaseTag!,
                        "Candidate",
                        change.Provider.ToString(),
                        change.Url,
                        JsonSerializer.Serialize(new { change.ReleaseTag, change.Revision })),
                    nodes,
                    ct);
    
                await _mutations.LinkCoreAsync(
                    new(change.ProjectId, release.Id, commit.Id, DevelopmentGraphEdgeType.Targets, 100m, true, "{}"),
                    release,
                    commit,
                    edges,
                    ct);
            }
    
            await _uow.SaveChangesAsync(ct);
        }
}
