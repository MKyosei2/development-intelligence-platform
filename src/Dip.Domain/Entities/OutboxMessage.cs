namespace Dip.Domain.Entities;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(
        Guid id,
        string messageType,
        string payloadJson,
        string correlationId,
        string? causationId,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? notBeforeUtc = null)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        MessageType = Required(messageType, 300);
        PayloadJson = Required(payloadJson, 100_000);
        CorrelationId = Required(correlationId, 200);
        CausationId = Optional(causationId, 200);
        CreatedAtUtc = createdAtUtc;
        NotBeforeUtc = notBeforeUtc;
    }

    public Guid Id { get; private set; }
    public string MessageType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public string CorrelationId { get; private set; } = string.Empty;
    public string? CausationId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? NotBeforeUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTimeOffset? LockedUntilUtc { get; private set; }

    public bool CanProcess(DateTimeOffset now) =>
        ProcessedAtUtc is null &&
        (!NotBeforeUtc.HasValue || NotBeforeUtc <= now) &&
        (!LockedUntilUtc.HasValue || LockedUntilUtc <= now);

    public void Lock(DateTimeOffset untilUtc)
    {
        if (ProcessedAtUtc.HasValue)
            throw new InvalidOperationException("Processed message cannot be locked.");
        LockedUntilUtc = untilUtc;
    }

    public void MarkProcessed(DateTimeOffset at)
    {
        ProcessedAtUtc = at;
        LockedUntilUtc = null;
        LastError = null;
    }

    public void MarkFailed(string error, DateTimeOffset nextAttemptAt)
    {
        AttemptCount++;
        LastError = Required(error, 4000);
        NotBeforeUtc = nextAttemptAt;
        LockedUntilUtc = null;
    }

    private static string Required(string? value, int max)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max)
            throw new ArgumentException($"Text length must be 1-{max}.", nameof(value));
        return text;
    }

    private static string? Optional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        if (text.Length > max)
            throw new ArgumentException($"Text length must be <= {max}.", nameof(value));
        return text;
    }
}
