// Purpose: Defines Q ue ue Me ss ag e repository behavior.
namespace Dip.Shared.Contracts.Events;

public sealed record QueueMessage<T>(
    string MessageId,
    string MessageType,
    string PayloadVersion,
    string CorrelationId,
    string? CausationId,
    int Attempt,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? NotBeforeUtc,
    T Payload);
