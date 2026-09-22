// Purpose: Defines the ProductionEventDto application contract.
using System.Text.Json;
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProductionTools;

public sealed record ProductionEventDto(Guid EvidenceId, ProductionToolSource Source, ProductionToolEventKind Kind, string Title, string Summary, string? ActorId, string? MachineId, string? ArtifactId, string? ContentHash, DateTimeOffset ObservedAtUtc, string? MetadataJson);
