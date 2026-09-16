// Purpose: Defines the ContinuousOperationsDto application contract.
namespace Dip.Application.Features.SystemStatus;

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
