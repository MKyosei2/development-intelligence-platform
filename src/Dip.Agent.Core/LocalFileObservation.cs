// Purpose: Defines the LocalFileObservation application contract.
namespace Dip.Agent.Core;

public sealed record LocalFileObservation(
    string EventId,
    FileObservationKind Kind,
    string RelativePath,
    string? PreviousRelativePath,
    string? Extension,
    long? SizeBytes,
    string? Sha256,
    DateTimeOffset? FileModifiedAtUtc,
    DateTimeOffset ObservedAtUtc);
