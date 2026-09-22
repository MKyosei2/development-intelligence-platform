// Purpose: Defines the UniversalGraphEdgeDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record UniversalGraphEdgeDto(DevelopmentGraphEdgeDto Edge, string RelationKind);
