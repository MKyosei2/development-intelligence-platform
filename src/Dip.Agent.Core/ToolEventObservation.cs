// Purpose: Defines the ToolEventObservation application contract.
using System.Text.Json;

namespace Dip.Agent.Core;

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
