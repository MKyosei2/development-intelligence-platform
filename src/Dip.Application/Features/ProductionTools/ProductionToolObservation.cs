// Purpose: Defines the ProductionToolObservation application contract.
using System.Text.Json;
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProductionTools;

public sealed record ProductionToolObservation(string EventId, ProductionToolSource Source, ProductionToolEventKind Kind, string? ToolVersion, string? RelativePath, string? ArtifactRelativePath, string? ContentHash, long? SizeBytes, DateTimeOffset ObservedAtUtc, JsonElement? Metadata = null, ProductionMediaKind? MediaKind = null, string? MediaRelativePath = null, string? MediaContentType = null);
