// Purpose: Defines the QueueTransportTelemetryDto application contract.
namespace Dip.Application.Features.SystemStatus;

public sealed record QueueTransportTelemetryDto(
    bool Available,
    long? StoredEnvelopeCount,
    long? ActiveLeaseCount,
    long? RetryEnvelopeCount,
    long? DeadLetterEnvelopeCount,
    DateTimeOffset? LatestActivityAtUtc,
    string Detail);
