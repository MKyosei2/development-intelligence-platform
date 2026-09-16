// Purpose: Defines the DevelopmentGraphNodeDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record DevelopmentGraphNodeDto(Guid Id,string Type,string Key,string Name,string Status,string? Provider,string? ExternalReference,string MetadataJson,DateTimeOffset LastObservedAtUtc);
