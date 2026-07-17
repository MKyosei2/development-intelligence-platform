using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Messaging;
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Shared.Contracts.Events;

namespace Dip.Application.Features.GitHub;

public sealed record GitHubWebhookProcessResult(
    bool WasDuplicate,
    string DeliveryId,
    string EventName);

public sealed class GitHubWebhookProcessor
{
    private readonly IProcessedWebhookRepository _webhooks;
    private readonly IQueueClient _queue;
    private readonly IUnitOfWork _unitOfWork;

    public GitHubWebhookProcessor(
        IProcessedWebhookRepository webhooks,
        IQueueClient queue,
        IUnitOfWork unitOfWork)
    {
        _webhooks = webhooks;
        _queue = queue;
        _unitOfWork = unitOfWork;
    }

    public async Task<GitHubWebhookProcessResult> ProcessAsync(
        string deliveryId,
        string eventName,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deliveryId))
            throw new ArgumentException("X-GitHub-Delivery is required.", nameof(deliveryId));
        if (string.IsNullOrWhiteSpace(eventName))
            throw new ArgumentException("X-GitHub-Event is required.", nameof(eventName));

        if (await _webhooks.ExistsAsync(deliveryId, cancellationToken))
            return new GitHubWebhookProcessResult(true, deliveryId, eventName);

        var payloadHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson)));

        var entity = new ProcessedWebhook(
            deliveryId,
            eventName,
            payloadHash,
            DateTimeOffset.UtcNow);

        await _webhooks.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;

        var action = root.TryGetProperty("action", out var actionNode)
            ? actionNode.GetString() ?? string.Empty
            : string.Empty;

        var repository = root.TryGetProperty("repository", out var repositoryNode)
                         && repositoryNode.TryGetProperty("full_name", out var fullNameNode)
            ? fullNameNode.GetString() ?? string.Empty
            : string.Empty;

        long? pullRequestNumber = null;
        if (root.TryGetProperty("pull_request", out var prNode)
            && prNode.TryGetProperty("number", out var numberNode)
            && numberNode.TryGetInt64(out var number))
        {
            pullRequestNumber = number;
        }

        var sender = root.TryGetProperty("sender", out var senderNode)
                     && senderNode.TryGetProperty("login", out var loginNode)
            ? loginNode.GetString() ?? string.Empty
            : string.Empty;

        var message = new GitHubWebhookReceived(
            deliveryId,
            eventName,
            action,
            repository,
            pullRequestNumber,
            sender,
            payloadJson,
            DateTimeOffset.UtcNow);

        await _queue.PublishAsync(
            "github-webhooks",
            message,
            cancellationToken);

        return new GitHubWebhookProcessResult(false, deliveryId, eventName);
    }
}
