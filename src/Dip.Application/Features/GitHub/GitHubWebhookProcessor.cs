// Purpose: Implements Git Hub Webhook Processor responsibilities for git hub.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Shared.Contracts.Events;

namespace Dip.Application.Features.GitHub;

public sealed record GitHubWebhookProcessResult(
    bool WasDuplicate,
    string DeliveryId,
    string EventName);

public sealed class GitHubWebhookProcessor
{
    private readonly IProcessedWebhookRepository _webhooks;
    private readonly IProjectRepository _projects;
    private readonly IEvidenceRepository _evidence;
    private readonly IQueueClient _queue;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ProjectTruthService _truth;
    private readonly GitHubDevelopmentGraphIngestor _developmentGraph;

    public GitHubWebhookProcessor(
        IProcessedWebhookRepository webhooks,
        IProjectRepository projects,
        IEvidenceRepository evidence,
        IQueueClient queue,
        IUnitOfWork unitOfWork,
        ProjectTruthService truth,
        GitHubDevelopmentGraphIngestor developmentGraph)
    {
        _webhooks = webhooks;
        _projects = projects;
        _evidence = evidence;
        _queue = queue;
        _unitOfWork = unitOfWork;
        _truth = truth;
        _developmentGraph = developmentGraph;
    }

    public async Task<GitHubWebhookProcessResult> ProcessAsync(
        string deliveryId,
        string eventName,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deliveryId))
        {
            throw new ArgumentException(
                "X-GitHub-Delivery is required.",
                nameof(deliveryId));
        }

        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ArgumentException(
                "X-GitHub-Event is required.",
                nameof(eventName));
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new ArgumentException(
                "Webhook payload is required.",
                nameof(payloadJson));
        }

        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;

        var action = root.TryGetProperty(
            "action",
            out var actionNode)
            ? actionNode.GetString() ?? string.Empty
            : string.Empty;

        var repository =
            root.TryGetProperty(
                "repository",
                out var repositoryNode)
            && repositoryNode.TryGetProperty(
                "full_name",
                out var fullNameNode)
                ? fullNameNode.GetString() ?? string.Empty
                : string.Empty;

        var repositoryHtmlUrl =
            root.TryGetProperty("repository", out repositoryNode)
            && repositoryNode.TryGetProperty("html_url", out var repositoryUrlNode)
                ? repositoryUrlNode.GetString()
                : null;

        long? pullRequestNumber = null;
        string? pullRequestUrl = null;

        if (root.TryGetProperty("pull_request", out var pullRequestNode))
        {
            if (pullRequestNode.TryGetProperty("number", out var numberNode)
                && numberNode.TryGetInt64(out var number))
            {
                pullRequestNumber = number;
            }

            if (pullRequestNode.TryGetProperty("html_url", out var prUrlNode))
                pullRequestUrl = prUrlNode.GetString();
        }

        var sender =
            root.TryGetProperty(
                "sender",
                out var senderNode)
            && senderNode.TryGetProperty(
                "login",
                out var loginNode)
                ? loginNode.GetString() ?? string.Empty
                : string.Empty;

        var project = await FindProjectAsync(repository, cancellationToken);

        if (await _webhooks.ExistsAsync(deliveryId, cancellationToken))
        {
            // If a previous request committed the webhook/evidence but failed
            // while refreshing Project Truth, a GitHub retry repairs the view.
            if (project is not null)
            {
                await _developmentGraph.IngestAsync(project.Id, eventName, payloadJson, cancellationToken);
                await _truth.RecalculateAsync(project.Id, cancellationToken);
            }

            return new(
                true,
                deliveryId,
                eventName);
        }

        var receivedAtUtc = DateTimeOffset.UtcNow;
        var payloadHash = Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(payloadJson)));

        var entity = new ProcessedWebhook(
            deliveryId,
            eventName,
            payloadHash,
            receivedAtUtc);

        var message = new GitHubWebhookReceived(
            deliveryId,
            eventName,
            action,
            repository,
            pullRequestNumber,
            sender,
            payloadJson,
            receivedAtUtc);

        await _webhooks.AddAsync(entity, cancellationToken);

        if (project is not null)
        {
            var evidence = BuildEvidence(
                project.Id,
                eventName,
                action,
                sender,
                repository,
                repositoryHtmlUrl,
                pullRequestNumber,
                pullRequestUrl,
                root,
                receivedAtUtc);

            await _evidence.AddAsync(evidence, cancellationToken);
        }

        // Publish before recording the delivery as completed. If durable queue
        // persistence fails, GitHub can retry the same delivery instead of the
        // database permanently classifying an unqueued webhook as duplicate.
        await _queue.PublishAsync(
            "github-webhooks",
            message,
            cancellationToken);

        entity.MarkProcessed(DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (project is not null)
        {
            await _developmentGraph.IngestAsync(project.Id, eventName, payloadJson, cancellationToken);
            await _truth.RecalculateAsync(project.Id, cancellationToken);
        }

        return new(
            false,
            deliveryId,
            eventName);
    }

    private async Task<Project?> FindProjectAsync(
        string repository,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repository))
            return null;

        var projects = await _projects.ListAsync(cancellationToken);
        return projects.FirstOrDefault(project =>
            string.Equals(
                NormalizeRepository(project.RepositoryUrl),
                NormalizeRepository(repository),
                StringComparison.OrdinalIgnoreCase));
    }

    private static EvidenceRecord BuildEvidence(
        Guid projectId,
        string eventName,
        string action,
        string sender,
        string repository,
        string? repositoryHtmlUrl,
        long? pullRequestNumber,
        string? pullRequestUrl,
        JsonElement root,
        DateTimeOffset receivedAtUtc)
    {
        var normalizedEvent = eventName.Trim().ToLowerInvariant();
        var kind = "github-event";
        var outcome = EvidenceOutcome.Informational;
        var title = $"GitHub {eventName}";
        var summary = string.IsNullOrWhiteSpace(action)
            ? $"GitHub emitted {eventName}."
            : $"GitHub emitted {eventName}/{action}.";

        switch (normalizedEvent)
        {
            case "push":
                kind = "git-push";
                outcome = EvidenceOutcome.Changed;
                title = "Repository source changed";
                summary = $"{sender} pushed changes to {ReadString(root, "ref") ?? "the repository"}.";
                break;

            case "pull_request":
                {
                    var merged = false;
                    if (root.TryGetProperty("pull_request", out var pr)
                        && pr.TryGetProperty("merged", out var mergedNode)
                        && mergedNode.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        merged = mergedNode.GetBoolean();
                    }

                    if (string.Equals(action, "closed", StringComparison.OrdinalIgnoreCase) && merged)
                    {
                        kind = "integration";
                        outcome = EvidenceOutcome.Succeeded;
                        title = $"Pull request #{pullRequestNumber} merged";
                        summary = "A pull request was merged into the repository, providing objective integration evidence.";
                    }
                    else
                    {
                        kind = "pull-request";
                        outcome = EvidenceOutcome.Changed;
                        title = $"Pull request #{pullRequestNumber} {action}";
                        summary = $"Pull request activity was observed: {action}.";
                    }
                    break;
                }

            case "workflow_run":
                {
                    kind = "ci-workflow";
                    var conclusion = ReadNestedString(root, "workflow_run", "conclusion");
                    outcome = OutcomeFromConclusion(conclusion);
                    var workflowName = ReadNestedString(root, "workflow_run", "name") ?? "workflow";
                    title = $"CI workflow {workflowName}: {conclusion ?? "updated"}";
                    summary = $"GitHub Actions workflow '{workflowName}' concluded '{conclusion ?? "unknown"}'.";
                    break;
                }

            case "check_run":
                {
                    kind = "ci-check";
                    var conclusion = ReadNestedString(root, "check_run", "conclusion");
                    outcome = OutcomeFromConclusion(conclusion);
                    var checkName = ReadNestedString(root, "check_run", "name") ?? "check";
                    title = $"CI check {checkName}: {conclusion ?? "updated"}";
                    summary = $"GitHub check '{checkName}' concluded '{conclusion ?? "unknown"}'.";
                    break;
                }

            case "pull_request_review":
                {
                    kind = "pull-request-review";
                    var state = ReadNestedString(root, "review", "state") ?? action;
                    outcome = state.Equals("approved", StringComparison.OrdinalIgnoreCase)
                        ? EvidenceOutcome.Succeeded
                        : state.Equals("changes_requested", StringComparison.OrdinalIgnoreCase)
                            ? EvidenceOutcome.Warning
                            : EvidenceOutcome.Changed;
                    title = $"Pull request #{pullRequestNumber} review: {state}";
                    summary = $"GitHub review state '{state}' was observed for pull request #{pullRequestNumber}.";
                    break;
                }

            case "release":
                {
                    kind = "release";
                    outcome = action.Equals("published", StringComparison.OrdinalIgnoreCase)
                        ? EvidenceOutcome.Succeeded
                        : EvidenceOutcome.Changed;
                    var tag = ReadNestedString(root, "release", "tag_name") ?? "untagged";
                    title = $"GitHub release {tag}: {action}";
                    summary = $"Release tag '{tag}' emitted GitHub action '{action}'.";
                    break;
                }

            case "deployment":
            case "deployment_status":
                {
                    kind = "deployment";
                    var state = normalizedEvent == "deployment_status"
                        ? ReadNestedString(root, "deployment_status", "state") ?? action
                        : action;
                    outcome = OutcomeFromConclusion(state);
                    if (outcome == EvidenceOutcome.Unknown) outcome = EvidenceOutcome.Changed;
                    title = $"GitHub deployment: {state}";
                    summary = $"Deployment activity was observed with state '{state}'.";
                    break;
                }

            case "ping":
                kind = "github-connection";
                outcome = EvidenceOutcome.Informational;
                title = "GitHub webhook connection verified";
                summary = "GitHub webhook ping was received successfully.";
                break;
        }

        var sourceReference = pullRequestUrl ?? repositoryHtmlUrl;
        var compactPayload = JsonSerializer.Serialize(new
        {
            eventName,
            action,
            sender,
            repository,
            pullRequestNumber,
            reference = ReadString(root, "ref"),
            after = ReadString(root, "after")
        });

        return new EvidenceRecord(
            Guid.NewGuid(),
            projectId,
            null,
            "project",
            EvidenceSourceType.GitHub,
            kind,
            outcome,
            title,
            summary,
            // The controller validates GitHub's HMAC signature before this
            // processor runs, so the origin of the event is verified.
            EvidenceConfidence.Verified,
            sourceReference,
            compactPayload,
            receivedAtUtc,
            receivedAtUtc);
    }

    private static EvidenceOutcome OutcomeFromConclusion(string? conclusion) =>
        conclusion?.Trim().ToLowerInvariant() switch
        {
            "success" => EvidenceOutcome.Succeeded,
            "failure" => EvidenceOutcome.Failed,
            "timed_out" => EvidenceOutcome.Failed,
            "startup_failure" => EvidenceOutcome.Failed,
            "cancelled" => EvidenceOutcome.Warning,
            "neutral" => EvidenceOutcome.Informational,
            "skipped" => EvidenceOutcome.Informational,
            _ => EvidenceOutcome.Unknown
        };

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var node)
            && node.ValueKind == JsonValueKind.String
                ? node.GetString()
                : null;

    private static string? ReadNestedString(
        JsonElement root,
        string parent,
        string propertyName) =>
        root.TryGetProperty(parent, out var parentNode)
        && parentNode.TryGetProperty(propertyName, out var node)
        && node.ValueKind == JsonValueKind.String
            ? node.GetString()
            : null;

    private static string NormalizeRepository(string value)
    {
        var normalized = value.Trim().TrimEnd('/');
        if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^4];
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            normalized = uri.AbsolutePath.Trim('/');
        return normalized;
    }
}
