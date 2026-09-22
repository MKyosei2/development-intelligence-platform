// Purpose: Defines domain behavior and state for this feature area.
namespace Dip.Domain.Entities;
 public sealed class ProcessedWebhook { private ProcessedWebhook() { }
public ProcessedWebhook(string deliveryId, string eventName, string payloadHash, DateTimeOffset receivedAtUtc) { DeliveryId = Required(deliveryId, 200);
 EventName = Required(eventName, 100);
 PayloadHash = Required(payloadHash, 64);
 ReceivedAtUtc = receivedAtUtc;
 }
public string DeliveryId { get;
 private set;
 } = string.Empty;
 public string EventName { get;
 private set;
 } = string.Empty;
 public string PayloadHash { get;
 private set;
 } = string.Empty;
 public DateTimeOffset ReceivedAtUtc { get;
 private set;
 }
public DateTimeOffset? ProcessedAtUtc { get;
 private set;
 }
public int AttemptCount { get;
 private set;
 }
public string? LastError { get;
 private set;
 }
public void MarkProcessed(DateTimeOffset at) { ProcessedAtUtc = at;
 LastError = null;
 }
public void MarkFailed(string error) { AttemptCount++;
 LastError = Required(error, 4000);
 }
private static string Required(string? value, int max) { var text = value?.Trim() ?? string.Empty;
if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"Text length must be 1-{max}.", nameof(value));
 return text;
 } }
