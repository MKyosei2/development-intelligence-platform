// Purpose: Coordinates P ro du ct io nT oo lC on tr ac ts application behavior for ProductionTools.
using System.Text.Json;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProductionTools;

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

public sealed record ProductionToolObservation(
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

public sealed record ProductionToolBatchRequest(
    string AgentId,
    IReadOnlyList<ProductionToolObservation> Events);

public sealed record ProductionToolEventResult(
    string EventId,
    Guid EvidenceId,
    bool Created,
    EvidenceEventType EvidenceEventType);

public sealed record ProductionToolBatchResult(
    int Received,
    int Accepted,
    int Duplicates,
    int Rejected,
    IReadOnlyList<ProductionToolEventResult> Results,
    IReadOnlyList<string> Warnings);

public sealed record ProductionEventDto(
    Guid EvidenceId,
    ProductionToolSource Source,
    ProductionToolEventKind Kind,
    string Title,
    string Summary,
    string? ActorId,
    string? MachineId,
    string? ArtifactId,
    string? ContentHash,
    DateTimeOffset ObservedAtUtc,
    string? MetadataJson);

public sealed record StoredProductionMediaDto(
    Guid ArtifactRecordId,
    Guid? ArtifactPreviewId,
    Guid? TrackedArtifactId,
    Guid? TrackedArtifactVersionId,
    string Sha256,
    long SizeBytes,
    string ContentType,
    ProductionMediaKind Kind,
    string RelativePath,
    Guid? VisualComparisonId = null,
    string? VisualComparisonVerdict = null);
