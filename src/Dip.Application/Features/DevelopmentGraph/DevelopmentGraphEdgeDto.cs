// Purpose: Defines the DevelopmentGraphEdgeDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record DevelopmentGraphEdgeDto(Guid Id,Guid SourceNodeId,Guid TargetNodeId,string Type,decimal Weight,bool Critical,string MetadataJson,DateTimeOffset LastObservedAtUtc);
