// Purpose: Defines the DevelopmentGraphDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record DevelopmentGraphDto(Guid ProjectId, IReadOnlyList<DevelopmentGraphNodeDto> Nodes, IReadOnlyList<DevelopmentGraphEdgeDto> Edges);
