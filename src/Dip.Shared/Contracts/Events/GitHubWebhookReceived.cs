namespace Dip.Shared.Contracts.Events;

public sealed record GitHubWebhookReceived(
    string DeliveryId,
    string EventName,
    string Action,
    string Repository,
    long? PullRequestNumber,
    string Sender,
    string PayloadJson,
    DateTimeOffset ReceivedAtUtc);
