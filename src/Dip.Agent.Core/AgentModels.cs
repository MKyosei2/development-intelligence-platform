// Purpose: Implements A ge nt Mo de ls agent behavior.
namespace Dip.Agent.Core;

public enum FileObservationKind
{
    Created = 0,
    Modified = 1,
    Deleted = 2,
    Renamed = 3
}

public sealed record FileSnapshot(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastWriteUtc,
    string Sha256);

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

public sealed record QueuedObservation(
    string Id,
    LocalFileObservation Observation,
    DateTimeOffset CreatedAtUtc,
    int Attempts);

public sealed record ScanResult(
    IReadOnlyList<LocalFileObservation> Observations,
    IReadOnlyDictionary<string, FileSnapshot> Snapshot,
    IReadOnlyList<string> Warnings);

public sealed record LocalFileBatchRequest(
    string AgentId,
    string RootId,
    IReadOnlyList<LocalFileObservation> Observations);
