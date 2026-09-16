// Purpose: Defines G it Hu bW eb ho ok Re ce iv ed repository behavior.
namespace Dip.Shared.Contracts.Events;
public sealed record GitHubWebhookReceived(string DeliveryId,string EventName,string Action,string Repository,long? PullRequestNumber,string Sender,string PayloadJson,DateTimeOffset ReceivedAtUtc);
