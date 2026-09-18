// Purpose: Defines the shared contract represented by G it Hu bW eb ho ok Re ce iv ed.
namespace Dip.Shared.Contracts.Events;
public sealed record GitHubWebhookReceived(string DeliveryId,string EventName,string Action,string Repository,long? PullRequestNumber,string Sender,string PayloadJson,DateTimeOffset ReceivedAtUtc);
