// Purpose: Defines the LinkUniversalGraphNodesCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record LinkUniversalGraphNodesCommand(Guid ProjectId, Guid SourceNodeId, Guid TargetNodeId, string RelationKind, decimal Weight = 50m, bool Critical = false, DevelopmentGraphEdgeType FallbackType = DevelopmentGraphEdgeType.RelatesTo, string MetadataJson = "{}");
