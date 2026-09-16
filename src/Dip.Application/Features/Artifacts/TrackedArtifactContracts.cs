// Purpose: Coordinates T ra ck ed Ar ti fa ct Co nt ra ct s application behavior for Artifacts.
using Dip.Domain.Enums;

namespace Dip.Application.Features.Artifacts;

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

public sealed record IngestLocalFileBatchCommand(
    Guid ProjectId,
    string AgentId,
    string RootId,
    IReadOnlyList<LocalFileObservation> Observations);

public sealed record FileObservationResult(
    string EventId,
    Guid ArtifactId,
    Guid VersionId,
    int VersionNumber,
    bool Created,
    bool EvidenceCreated,
    string DisplayPath);

public sealed record LocalFileBatchResult(
    int Received,
    int Accepted,
    int Duplicate,
    int Rejected,
    IReadOnlyList<FileObservationResult> Results,
    IReadOnlyList<string> Warnings);

public sealed record TrackedArtifactDto(
    Guid Id,
    Guid ProjectId,
    string DisplayPath,
    string Extension,
    TrackedArtifactLifecycleState State,
    int CurrentVersionNumber,
    string? CurrentSha256,
    long? CurrentSizeBytes,
    DateTimeOffset? CurrentFileModifiedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record TrackedArtifactVersionDto(
    Guid Id,
    Guid ArtifactId,
    int VersionNumber,
    FileObservationKind ChangeKind,
    string DisplayPath,
    string? PreviousDisplayPath,
    string? Sha256,
    long? SizeBytes,
    DateTimeOffset? FileModifiedAtUtc,
    DateTimeOffset ObservedAtUtc,
    string SourceAgentId,
    string SourceRootId);
