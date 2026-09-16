// Purpose: Represents T ra ck ed Ar ti fa ct Ve rs io n in the Artifacts domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class TrackedArtifactVersion
{
    private TrackedArtifactVersion() { }

    public TrackedArtifactVersion(
        Guid id,
        Guid artifactId,
        int versionNumber,
        FileObservationKind changeKind,
        string sourceEventId,
        string sourceAgentId,
        string sourceRootId,
        string displayPath,
        string? previousDisplayPath,
        string? sha256,
        long? sizeBytes,
        DateTimeOffset? fileModifiedAtUtc,
        DateTimeOffset observedAtUtc,
        DateTimeOffset createdAtUtc)
    {
        if (artifactId == Guid.Empty)
            throw new ArgumentException("ArtifactId is required.", nameof(artifactId));
        if (versionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(versionNumber));
        if (!Enum.IsDefined(changeKind))
            throw new ArgumentOutOfRangeException(nameof(changeKind));
        if (sizeBytes is < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ArtifactId = artifactId;
        VersionNumber = versionNumber;
        ChangeKind = changeKind;
        SourceEventId = Required(sourceEventId, 300, nameof(sourceEventId));
        SourceAgentId = Required(sourceAgentId, 300, nameof(sourceAgentId));
        SourceRootId = Required(sourceRootId, 200, nameof(sourceRootId));
        DisplayPath = Required(displayPath, 1000, nameof(displayPath));
        PreviousDisplayPath = Optional(previousDisplayPath, 1000, nameof(previousDisplayPath));
        Sha256 = Optional(sha256, 64, nameof(sha256))?.ToLowerInvariant();
        SizeBytes = sizeBytes;
        FileModifiedAtUtc = fileModifiedAtUtc;
        ObservedAtUtc = observedAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ArtifactId { get; private set; }
    public int VersionNumber { get; private set; }
    public FileObservationKind ChangeKind { get; private set; }
    public string SourceEventId { get; private set; } = string.Empty;
    public string SourceAgentId { get; private set; } = string.Empty;
    public string SourceRootId { get; private set; } = string.Empty;
    public string DisplayPath { get; private set; } = string.Empty;
    public string? PreviousDisplayPath { get; private set; }
    public string? Sha256 { get; private set; }
    public long? SizeBytes { get; private set; }
    public DateTimeOffset? FileModifiedAtUtc { get; private set; }
    public DateTimeOffset ObservedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Required(string? value, int max, string name)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 || normalized.Length > max)
            throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return normalized;
    }

    private static string? Optional(string? value, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > max)
            throw new ArgumentException($"{name} must be at most {max} characters.", name);
        return normalized;
    }
}
