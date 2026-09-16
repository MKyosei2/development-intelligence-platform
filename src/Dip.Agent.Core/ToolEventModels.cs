// Purpose: Implements T oo lE ve nt Mo de ls agent behavior.
using System.Text.Json;

namespace Dip.Agent.Core;

public enum ProductionToolSource
{
    Unknown = 0,
    Maya = 1,
    Unity = 2,
    Unreal = 3
}

public enum ProductionToolEventKind
{
    Unknown = 0,
    SceneOpened = 1,
    SceneSaved = 2,
    Imported = 3,
    ReferenceChanged = 4,
    Exported = 5,
    RenderCompleted = 6,
    PlayblastCompleted = 7,
    BuildStarted = 20,
    BuildCompleted = 21,
    AssetImported = 22,
    TestsCompleted = 23,
    PerformanceCaptured = 24,
    CookCompleted = 30,
    PackageCompleted = 31,
    AutomationTestsCompleted = 32,
    ScreenshotGenerated = 40,
    VideoGenerated = 41
}

public enum ProductionMediaKind
{
    Screenshot = 0,
    Video = 1,
    Render = 2,
    Playblast = 3
}

public sealed record ToolEventObservation(
    string EventId,
    ProductionToolSource Source,
    ProductionToolEventKind Kind,
    string? ToolVersion,
    string? RelativePath,
    string? ArtifactRelativePath,
    string? ContentHash,
    long? SizeBytes,
    DateTimeOffset ObservedAtUtc,
    JsonElement? Metadata = null,
    ProductionMediaKind? MediaKind = null,
    string? MediaRelativePath = null,
    string? MediaContentType = null);

public sealed record ToolBridgeMessage(string? Token, ToolEventObservation Event);

public sealed record QueuedToolEvent(
    string Id,
    ToolEventObservation Event,
    DateTimeOffset CreatedAtUtc,
    int Attempts);

public sealed record QueuedMediaUpload(
    string Id,
    string EventId,
    ProductionToolSource Source,
    ProductionMediaKind Kind,
    string RelativePath,
    string SpoolPath,
    string ContentType,
    string Sha256,
    long SizeBytes,
    DateTimeOffset CreatedAtUtc,
    int Attempts);

public sealed record ProductionToolBatchRequest(
    string AgentId,
    IReadOnlyList<ToolEventObservation> Events);
