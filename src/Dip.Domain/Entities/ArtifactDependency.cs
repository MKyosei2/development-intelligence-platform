using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class ArtifactDependency
{
    private ArtifactDependency() { }
    public ArtifactDependency(Guid id, Guid artifactId, string targetKey, ArtifactDependencyKind kind, DateTimeOffset observedAtUtc)
    {
        if (artifactId == Guid.Empty) throw new ArgumentException("ArtifactId is required.", nameof(artifactId));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ArtifactId = artifactId;
        TargetKey = Required(targetKey, 1000, nameof(targetKey));
        Kind = kind;
        ObservedAtUtc = observedAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid ArtifactId { get; private set; }
    public string TargetKey { get; private set; } = string.Empty;
    public ArtifactDependencyKind Kind { get; private set; }
    public DateTimeOffset ObservedAtUtc { get; private set; }
    private static string Required(string? value, int max, string name)
    {
        var normalized=value?.Trim()??string.Empty;
        if(normalized.Length is <1 || normalized.Length>max) throw new ArgumentException($"{name} must be 1-{max} characters.",name);
        return normalized;
    }
}
