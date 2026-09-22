// Purpose: Defines the UpsertUniversalGraphNodeCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record UpsertUniversalGraphNodeCommand(Guid ProjectId, string DomainId, string Kind, string Key, string Name, string Status = "Observed", string? Provider = null, string? ExternalReference = null, string? SourceObjectId = null, string SchemaVersion = "1", DevelopmentGraphNodeType FallbackType = DevelopmentGraphNodeType.ExternalObservation, string MetadataJson = "{}");
