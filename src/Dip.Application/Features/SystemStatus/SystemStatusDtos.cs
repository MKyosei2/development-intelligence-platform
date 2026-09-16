// Purpose: Coordinates S ys te mS ta tu sD to s application behavior for SystemStatus.
namespace Dip.Application.Features.SystemStatus;

public static class SystemComponentStates
{
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";
    public const string Unavailable = "Unavailable";
    public const string NotConfigured = "NotConfigured";
}

public sealed record SystemComponentStatusDto(
    string Key,
    string Name,
    string State,
    string Detail,
    DateTimeOffset CheckedAtUtc,
    double? DurationMs = null);

public sealed record QueueTransportTelemetryDto(
    bool Available,
    long? StoredEnvelopeCount,
    long? ActiveLeaseCount,
    long? RetryEnvelopeCount,
    long? DeadLetterEnvelopeCount,
    DateTimeOffset? LatestActivityAtUtc,
    string Detail);

public sealed record ContinuousOperationsDto(
    bool Available,
    DateTimeOffset? LatestEvidenceAtUtc,
    DateTimeOffset? LatestTruthEvaluationAtUtc,
    DateTimeOffset? LatestBackupVerificationAtUtc,
    long? QueuedJobCount,
    long? RunningJobCount,
    long? FailedOrTimedOutJobCount,
    QueueTransportTelemetryDto Queue,
    string Detail);

public sealed record SystemStatusDto(
    string Overall,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<SystemComponentStatusDto> Components)
{
    public ContinuousOperationsDto? Operations { get; init; }
}

public sealed record SystemLivenessDto(
    bool Live,
    string State,
    DateTimeOffset CheckedAtUtc);

public sealed record SystemReadinessDto(
    bool Ready,
    string State,
    DateTimeOffset CheckedAtUtc,
    IReadOnlyList<string> UnavailableComponents,
    IReadOnlyList<string> DegradedComponents);
