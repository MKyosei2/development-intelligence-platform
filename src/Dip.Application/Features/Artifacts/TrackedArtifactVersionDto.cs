// Purpose: Defines the TrackedArtifactVersionDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Artifacts;

public sealed record TrackedArtifactVersionDto(Guid Id, Guid ArtifactId, int VersionNumber, FileObservationKind ChangeKind, string DisplayPath, string? PreviousDisplayPath, string? Sha256, long? SizeBytes, DateTimeOffset? FileModifiedAtUtc, DateTimeOffset ObservedAtUtc, string SourceAgentId, string SourceRootId);
