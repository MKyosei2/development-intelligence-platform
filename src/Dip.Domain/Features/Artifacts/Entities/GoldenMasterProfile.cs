// Purpose: Represents G ol de nM as te rP ro fi le in the Artifacts domain.
namespace Dip.Domain.Entities;

public sealed class GoldenMasterProfile
{
    private GoldenMasterProfile() { }

    public GoldenMasterProfile(
        Guid id,
        Guid projectId,
        string captureKey,
        Guid artifactRecordId,
        string sceneKey,
        string cameraKey,
        int width,
        int height,
        string maskJson,
        bool active,
        string approvedBy,
        DateTimeOffset approvedAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (artifactRecordId == Guid.Empty) throw new ArgumentException("ArtifactRecordId is required.", nameof(artifactRecordId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        CaptureKey = Required(captureKey, 500, nameof(captureKey));
        ArtifactRecordId = artifactRecordId;
        SceneKey = Required(sceneKey, 300, nameof(sceneKey));
        CameraKey = Required(cameraKey, 300, nameof(cameraKey));
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        MaskJson = Required(maskJson, 32000, nameof(maskJson));
        Active = active;
        ApprovedBy = Required(approvedBy, 300, nameof(approvedBy));
        ApprovedAtUtc = approvedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string CaptureKey { get; private set; } = string.Empty;
    public Guid ArtifactRecordId { get; private set; }
    public string SceneKey { get; private set; } = string.Empty;
    public string CameraKey { get; private set; } = string.Empty;
    public int Width { get; private set; }
    public int Height { get; private set; }
    public string MaskJson { get; private set; } = "[]";
    public bool Active { get; private set; }
    public string ApprovedBy { get; private set; } = string.Empty;
    public DateTimeOffset ApprovedAtUtc { get; private set; }

    public void Deactivate() => Active = false;

    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }
}
