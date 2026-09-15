using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

/// <summary>Links a logical artifact version to an existing DIP ArtifactRecord.</summary>
public sealed class ArtifactPreview
{
    private ArtifactPreview() { }
    public ArtifactPreview(Guid id, Guid artifactVersionId, Guid artifactRecordId, ArtifactPreviewKind kind, DateTimeOffset createdAtUtc)
    {
        if (artifactVersionId == Guid.Empty) throw new ArgumentException("ArtifactVersionId is required.", nameof(artifactVersionId));
        if (artifactRecordId == Guid.Empty) throw new ArgumentException("ArtifactRecordId is required.", nameof(artifactRecordId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ArtifactVersionId = artifactVersionId;
        ArtifactRecordId = artifactRecordId;
        Kind = kind;
        CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid ArtifactVersionId { get; private set; }
    public Guid ArtifactRecordId { get; private set; }
    public ArtifactPreviewKind Kind { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
