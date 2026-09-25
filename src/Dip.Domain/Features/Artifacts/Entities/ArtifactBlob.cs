// Purpose: Defines Artifact Blob domain behavior and invariants.
namespace Dip.Domain.Entities;

public sealed class ArtifactBlob
{
    private ArtifactBlob() { }

    public ArtifactBlob(Guid id, string sha256, long sizeBytes, string contentType, string storageProvider, string storageKey, DateTimeOffset createdAtUtc)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Sha256 = Required(sha256, 64, nameof(sha256)).ToLowerInvariant();
        if (Sha256.Length != 64 || Sha256.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Sha256 must be a 64-character hexadecimal digest.", nameof(sha256));
        SizeBytes = sizeBytes >= 0 ? sizeBytes : throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        ContentType = Required(contentType, 200, nameof(contentType));
        StorageProvider = Required(storageProvider, 40, nameof(storageProvider));
        StorageKey = Required(storageKey, 1000, nameof(storageKey));
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public string StorageProvider { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }
}
