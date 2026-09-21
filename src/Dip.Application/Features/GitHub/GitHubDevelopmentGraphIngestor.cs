// Purpose: Projects GitHub events into the development graph while batching related graph mutations per event.
using System.Text.Json;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;

namespace Dip.Application.Features.GitHub;

/// <summary>
/// Idempotently projects GitHub push, pull-request, review, check, workflow,
/// release, and deployment events into the provider-neutral Development Graph.
/// </summary>
public sealed class GitHubDevelopmentGraphIngestor
{
    private readonly DevelopmentGraphService _graph;

    public GitHubDevelopmentGraphIngestor(DevelopmentGraphService graph)
    {
        _graph = graph;
    }

    public async Task IngestAsync(
        Guid projectId,
        string eventName,
        string payloadJson,
        CancellationToken ct)
    {
        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;
        var repository = Nested(root, "repository", "full_name") ?? "unknown";
        var repositoryUrl = Nested(root, "repository", "html_url") ?? repository;
        var action = Text(root, "action") ?? string.Empty;
        var observed = DateTimeOffset.UtcNow;

        switch (eventName.Trim().ToLowerInvariant())
        {
            case "push":
                await IngestPushAsync(projectId, root, repository, repositoryUrl, observed, ct);
                break;
            case "pull_request":
                await IngestPullRequestAsync(
                    projectId,
                    root,
                    repository,
                    repositoryUrl,
                    action,
                    observed,
                    ct);
                break;
            case "pull_request_review":
                await IngestReviewAsync(projectId, root, repository, action, ct);
                break;
            case "workflow_run":
                await IngestWorkflowRunAsync(projectId, root, repository, action, observed, ct);
                break;
            case "check_run":
                await IngestCheckRunAsync(projectId, root, repository, action, observed, ct);
                break;
            case "release":
                await IngestReleaseAsync(projectId, root, repository, repositoryUrl, action, ct);
                break;
            case "deployment":
            case "deployment_status":
                await IngestDeploymentAsync(projectId, root, repository, eventName, action, ct);
                break;
        }
    }

    private async Task IngestPushAsync(
        Guid projectId,
        JsonElement root,
        string repository,
        string repositoryUrl,
        DateTimeOffset observed,
        CancellationToken ct)
    {
        var revision = Text(root, "after") ?? Nested(root, "head_commit", "id") ?? "unknown";
        var branch = (Text(root, "ref") ?? "refs/heads/unknown")
            .Replace("refs/heads/", string.Empty, StringComparison.OrdinalIgnoreCase);
        var title = Nested(root, "head_commit", "message") ?? $"Push {revision}";
        var url = Nested(root, "head_commit", "url");
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("commits", out var commits) && commits.ValueKind == JsonValueKind.Array)
        {
            foreach (var commit in commits.EnumerateArray())
            {
                AddFiles(commit, "added", "added", files);
                AddFiles(commit, "modified", "modified", files);
                AddFiles(commit, "removed", "removed", files);
            }
        }

        await _graph.IngestVersionControlChangeAsync(
            new(
                projectId,
                VersionControlProvider.GitHub,
                repository,
                repositoryUrl,
                revision,
                branch,
                title,
                url,
                observed,
                files.Select(pair => new NormalizedChangedFile(pair.Key, pair.Value)).ToArray()),
            ct);
    }

    private async Task IngestPullRequestAsync(
        Guid projectId,
        JsonElement root,
        string repository,
        string repositoryUrl,
        string action,
        DateTimeOffset observed,
        CancellationToken ct)
    {
        var number = NestedInt(root, "pull_request", "number") ?? Int(root, "number");
        var sha = Nested(root, "pull_request", "head", "sha") ?? "unknown";
        var branch = Nested(root, "pull_request", "head", "ref") ?? "unknown";
        var title = Nested(root, "pull_request", "title") ?? $"Pull request #{number}";
        var url = Nested(root, "pull_request", "html_url");
        var merged = NestedBool(root, "pull_request", "merged") ?? false;

        await _graph.IngestVersionControlChangeAsync(
            new(
                projectId,
                VersionControlProvider.GitHub,
                repository,
                repositoryUrl,
                sha,
                branch,
                title,
                url,
                observed,
                [],
                number,
                [],
                null,
                merged),
            ct);

        var pullRequest = await _graph.FindNodeAsync(
            projectId,
            DevelopmentGraphNodeType.PullRequest,
            $"pr:{repository}:{number}",
            ct);
        if (pullRequest is not null)
        {
            await _graph.UpsertNodeAsync(
                new(
                    projectId,
                    DevelopmentGraphNodeType.PullRequest,
                    pullRequest.Key,
                    pullRequest.Name,
                    merged ? "Merged" : Nested(root, "pull_request", "state") ?? action,
                    "GitHub",
                    url,
                    pullRequest.MetadataJson),
                ct);
        }
    }

    private Task IngestReviewAsync(
        Guid projectId,
        JsonElement root,
        string repository,
        string action,
        CancellationToken ct)
    {
        var number = NestedInt(root, "pull_request", "number") ?? 0;
        var reviewer = Nested(root, "review", "user", "login") ?? "reviewer";
        var state = Nested(root, "review", "state") ?? action;
        var url = Nested(root, "review", "html_url") ?? Nested(root, "pull_request", "html_url");

        return _graph.MutateAsync(
            async (graph, token) =>
            {
                var pullRequest = await graph.UpsertNodeAsync(
                    new(
                        projectId,
                        DevelopmentGraphNodeType.PullRequest,
                        $"pr:{repository}:{number}",
                        $"PR #{number}",
                        "Open",
                        "GitHub",
                        Nested(root, "pull_request", "html_url"),
                        "{}"),
                    token);
                var review = await graph.UpsertNodeAsync(
                    new(
                        projectId,
                        DevelopmentGraphNodeType.Review,
                        $"review:{repository}:{number}:{reviewer}",
                        reviewer,
                        state,
                        "GitHub",
                        url,
                        JsonSerializer.Serialize(new { number, reviewer, state, action })),
                    token);
                await graph.LinkAsync(
                    new(
                        projectId,
                        review.Id,
                        pullRequest.Id,
                        DevelopmentGraphEdgeType.Reviews,
                        80m,
                        state.Equals("changes_requested", StringComparison.OrdinalIgnoreCase),
                        "{}"),
                    token);
                return true;
            },
            ct);
    }

    private Task IngestWorkflowRunAsync(
        Guid projectId,
        JsonElement root,
        string repository,
        string action,
        DateTimeOffset observed,
        CancellationToken ct)
    {
        var runId = Nested(root, "workflow_run", "id")
            ?? Nested(root, "workflow_run", "run_number")
            ?? "unknown";
        var pipeline = Nested(root, "workflow_run", "name") ?? "GitHub Actions";
        var status = Nested(root, "workflow_run", "conclusion")
            ?? Nested(root, "workflow_run", "status")
            ?? action;
        return _graph.IngestCiRunAsync(
            new(
                projectId,
                ExternalCiProvider.GitHubActions,
                repository,
                pipeline,
                runId,
                status,
                Nested(root, "workflow_run", "head_sha"),
                Nested(root, "workflow_run", "head_branch"),
                Nested(root, "workflow_run", "html_url"),
                observed),
            ct);
    }

    private Task IngestCheckRunAsync(
        Guid projectId,
        JsonElement root,
        string repository,
        string action,
        DateTimeOffset observed,
        CancellationToken ct)
    {
        var runId = Nested(root, "check_run", "id") ?? "unknown";
        var pipeline = Nested(root, "check_run", "name") ?? "GitHub Check";
        var status = Nested(root, "check_run", "conclusion")
            ?? Nested(root, "check_run", "status")
            ?? action;
        return _graph.IngestCiRunAsync(
            new(
                projectId,
                ExternalCiProvider.GitHubActions,
                repository,
                pipeline,
                runId,
                status,
                Nested(root, "check_run", "head_sha"),
                null,
                Nested(root, "check_run", "html_url"),
                observed),
            ct);
    }

    private Task IngestReleaseAsync(
        Guid projectId,
        JsonElement root,
        string repository,
        string repositoryUrl,
        string action,
        CancellationToken ct)
    {
        var tag = Nested(root, "release", "tag_name") ?? "untagged";
        var target = Nested(root, "release", "target_commitish") ?? tag;
        var url = Nested(root, "release", "html_url");

        return _graph.MutateAsync(
            async (graph, token) =>
            {
                var release = await graph.UpsertNodeAsync(
                    new(
                        projectId,
                        DevelopmentGraphNodeType.Release,
                        $"release:{repository}:{tag}",
                        tag,
                        action,
                        "GitHub",
                        url,
                        JsonSerializer.Serialize(new { tag, target, action })),
                    token);
                var repositoryNode = await graph.UpsertNodeAsync(
                    new(
                        projectId,
                        DevelopmentGraphNodeType.Repository,
                        $"repo:{repository}",
                        repository,
                        "Active",
                        "GitHub",
                        repositoryUrl,
                        "{}"),
                    token);
                await graph.LinkAsync(
                    new(
                        projectId,
                        repositoryNode.Id,
                        release.Id,
                        DevelopmentGraphEdgeType.Contains,
                        90m,
                        true,
                        "{}"),
                    token);
                return true;
            },
            ct);
    }

    private async Task IngestDeploymentAsync(
        Guid projectId,
        JsonElement root,
        string repository,
        string eventName,
        string action,
        CancellationToken ct)
    {
        var id = Nested(root, "deployment", "id") ?? "unknown";
        var environment = Nested(root, "deployment", "environment") ?? "deployment";
        var status = eventName.Equals("deployment_status", StringComparison.OrdinalIgnoreCase)
            ? Nested(root, "deployment_status", "state") ?? action
            : action;
        var sha = Nested(root, "deployment", "sha");
        var commit = string.IsNullOrWhiteSpace(sha)
            ? null
            : await _graph.FindNodeAsync(
                projectId,
                DevelopmentGraphNodeType.Commit,
                $"commit:{repository}:{sha}",
                ct);

        await _graph.MutateAsync(
            async (graph, token) =>
            {
                var deployment = await graph.UpsertNodeAsync(
                    new(
                        projectId,
                        DevelopmentGraphNodeType.Deployment,
                        $"deployment:{repository}:{id}",
                        environment,
                        status,
                        "GitHub",
                        Nested(root, "deployment_status", "target_url"),
                        JsonSerializer.Serialize(new { id, env = environment, status })),
                    token);
                if (commit is not null)
                {
                    await graph.LinkAsync(
                        new(
                            projectId,
                            commit.Id,
                            deployment.Id,
                            DevelopmentGraphEdgeType.Deploys,
                            90m,
                            false,
                            "{}"),
                        token);
                }
                return true;
            },
            ct);
    }

    private static void AddFiles(
        JsonElement commit,
        string property,
        string state,
        IDictionary<string, string> files)
    {
        if (!commit.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            return;
        foreach (var item in array.EnumerateArray())
        {
            var path = item.GetString();
            if (!string.IsNullOrWhiteSpace(path))
                files[path] = state;
        }
    }

    private static string? Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            }
            : null;

    private static int? Int(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var result)
            ? result
            : null;

    private static string? Nested(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
                return null;
        }
        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number => current.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? NestedInt(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
                return null;
        }
        return current.TryGetInt32(out var value) ? value : null;
    }

    private static bool? NestedBool(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out current))
                return null;
        }
        return current.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? current.GetBoolean()
            : null;
    }
}
