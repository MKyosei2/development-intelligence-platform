// Purpose: Maps Development Graph domain entities to stable application DTOs.
using System.Text.Json;
using System.Text.Json.Nodes;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

internal static class DevelopmentGraphMapper
{
    internal static DevelopmentGraphNodeDto Map(DevelopmentGraphNode node) =>
            new(
                node.Id,
                node.Type.ToString(),
                node.Key,
                node.Name,
                node.Status,
                node.Provider,
                node.ExternalReference,
                node.MetadataJson,
                node.LastObservedAtUtc);

    internal static DevelopmentGraphEdgeDto Map(DevelopmentGraphEdge edge) =>
            new(
                edge.Id,
                edge.SourceNodeId,
                edge.TargetNodeId,
                edge.Type.ToString(),
                edge.Weight,
                edge.Critical,
                edge.MetadataJson,
                edge.LastObservedAtUtc);
}
