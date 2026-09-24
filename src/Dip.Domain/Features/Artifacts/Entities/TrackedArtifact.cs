// Purpose: Defines T ra ck ed Ar ti fa ct domain behavior and invariants for a rt if ac ts.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

/// <summary>
/// Logical project artifact tracked independently of Git. A TrackedArtifact is
/// the stable identity of a path such as character.ma; TrackedArtifactVersion
/// captures every observed content/change version.
/// </summary>
public sealed class TrackedArtifact
{
    private TrackedArtifact() { }

    public TrackedArtifact(
        Guid id,
        Guid projectId,
        string pathKey,
        string displayPath,
        string extension,
        DateTimeOffset now)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        PathKey = Required(pathKey, 1000, nameof(pathKey));
        DisplayPath = Required(displayPath, 1000, nameof(displayPath));
        Extension = Optional(extension, 40, nameof(extension)) ?? string.Empty;
        LifecycleState = TrackedArtifactLifecycleState.Active;
        CurrentVersionNumber = 0;
        CreatedAtUtc = UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string PathKey { get; private set; } = string.Empty;
    public string DisplayPath { get; private set; } = string.Empty;
    public string Extension { get; private set; } = string.Empty;
    public TrackedArtifactLifecycleState LifecycleState { get; private set; }
    public int CurrentVersionNumber { get; private set; }
    public string? CurrentSha256 { get; private set; }
    public long? CurrentSizeBytes { get; private set; }
    public DateTimeOffset? CurrentFileModifiedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public int RecordContent(
        string displayPath,
        string pathKey,
        string extension,
        string sha256,
        long sizeBytes,
        DateTimeOffset? fileModifiedAtUtc,
        DateTimeOffset now)
    {
        if (sizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        DisplayPath = Required(displayPath, 1000, nameof(displayPath));
        PathKey = Required(pathKey, 1000, nameof(pathKey));
        Extension = Optional(extension, 40, nameof(extension)) ?? string.Empty;
        CurrentSha256 = Required(sha256, 64, nameof(sha256)).ToLowerInvariant();
        CurrentSizeBytes = sizeBytes;
        CurrentFileModifiedAtUtc = fileModifiedAtUtc;
        LifecycleState = TrackedArtifactLifecycleState.Active;
        CurrentVersionNumber++;
        UpdatedAtUtc = now;
        return CurrentVersionNumber;
    }

    public int RecordDeletion(DateTimeOffset now)
    {
        LifecycleState = TrackedArtifactLifecycleState.Deleted;
        CurrentSha256 = null;
        CurrentSizeBytes = null;
        CurrentFileModifiedAtUtc = null;
        CurrentVersionNumber++;
        UpdatedAtUtc = now;
        return CurrentVersionNumber;
    }

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
