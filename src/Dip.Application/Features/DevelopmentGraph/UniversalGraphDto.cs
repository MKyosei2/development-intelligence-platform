// Purpose: Defines the UniversalGraphDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record UniversalGraphDto(Guid ProjectId, IReadOnlyList<UniversalGraphNodeDto> Nodes, IReadOnlyList<UniversalGraphEdgeDto> Edges);
