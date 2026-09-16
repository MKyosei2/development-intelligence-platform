// Purpose: Defines the LinkDevelopmentGraphNodesCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record LinkDevelopmentGraphNodesCommand(Guid ProjectId,Guid SourceNodeId,Guid TargetNodeId,DevelopmentGraphEdgeType Type,decimal Weight=50m,bool Critical=false,string MetadataJson="{}");
