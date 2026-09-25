// Purpose: Defines Outbox Message domain behavior and invariants for operations.
namespace Dip.Domain.Entities;
 public sealed class OutboxMessage { private OutboxMessage() { }
public OutboxMessage(Guid id, string messageType, string payloadJson, string correlationId, string? causationId, DateTimeOffset createdAtUtc) { Id = id == Guid.Empty ? Guid.NewGuid() : id;
 MessageType = Required(messageType, 500);
 PayloadJson = Required(payloadJson, 2_000_000);
 CorrelationId = Required(correlationId, 200);
 CausationId = Optional(causationId, 200);
 CreatedAtUtc = createdAtUtc;
 }
public Guid Id { get;
 private set;
 }
public string MessageType { get;
 private set;
 } = string.Empty;
 public string PayloadJson { get;
 private set;
 } = "{}";
 public string CorrelationId { get;
 private set;
 } = string.Empty;
 public string? CausationId { get;
 private set;
 }
public DateTimeOffset CreatedAtUtc { get;
 private set;
 }
public DateTimeOffset? LockedUntilUtc { get;
 private set;
 }
public DateTimeOffset? NextAttemptAtUtc { get;
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
public void Lock(DateTimeOffset lockedUntilUtc) { if (ProcessedAtUtc is not null) throw new InvalidOperationException("Processed outbox message cannot be locked.");
 LockedUntilUtc = lockedUntilUtc;
 AttemptCount++;
 }
public void MarkProcessed(DateTimeOffset processedAtUtc) { ProcessedAtUtc = processedAtUtc;
 LockedUntilUtc = null;
 NextAttemptAtUtc = null;
 LastError = null;
 }
public void MarkFailed(string error, DateTimeOffset nextAttemptAtUtc) { if (ProcessedAtUtc is not null) throw new InvalidOperationException("Processed outbox message cannot fail.");
 LastError = Required(error, 4000);
 NextAttemptAtUtc = nextAttemptAtUtc;
 LockedUntilUtc = null;
 }
private static string Required(string? value, int maxLength) { var normalized = value?.Trim() ?? string.Empty;
if (normalized.Length is < 1 || normalized.Length > maxLength) throw new ArgumentException($"Value length must be 1-{maxLength}.");
 return normalized;
 }
private static string? Optional(string? value, int maxLength) { if (string.IsNullOrWhiteSpace(value)) return null;
 var normalized = value.Trim();
if (normalized.Length > maxLength) throw new ArgumentException($"Value length must not exceed {maxLength}.");
 return normalized;
 } }
