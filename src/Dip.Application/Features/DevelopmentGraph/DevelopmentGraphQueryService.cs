// Purpose: Owns Development Graph read models and repository-backed node/edge queries.
using System.Text.Json;
using System.Text.Json.Nodes;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

internal sealed class DevelopmentGraphQueryService
{
    private readonly IDevelopmentGraphRepository _repo;

    internal DevelopmentGraphQueryService(IDevelopmentGraphRepository repo)
    {
        _repo = repo;
    }

    public async Task<UniversalGraphDto> GetUniversalAsync(
            Guid projectId,
            string? domainId,
            string? kind,
            CancellationToken ct)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        }

        var nodes = await _repo.ListNodesAsync(projectId, null, ct);
        var universal = new List<UniversalGraphNodeDto>();

        foreach (var entity in nodes)
        {
            var dto = DevelopmentGraphMapper.Map(entity);
            var context = $"development-graph node {entity.Id}";
            var domain = DevelopmentGraphMetadata.MetadataString(dto.MetadataJson, "domainId", context);
            var objectKind = DevelopmentGraphMetadata.MetadataString(dto.MetadataJson, "kind", context);

            if (string.IsNullOrWhiteSpace(domain)
                || string.IsNullOrWhiteSpace(objectKind))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(domainId)
                && !domain.Equals(domainId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(kind)
                && !objectKind.Equals(kind, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            universal.Add(
                new(
                    dto,
                    domain,
                    objectKind,
                    DevelopmentGraphMetadata.MetadataString(dto.MetadataJson, "schemaVersion", context) ?? "1",
                    DevelopmentGraphMetadata.MetadataString(dto.MetadataJson, "sourceObjectId", context)));
        }

        var ids = universal.Select(x => x.Node.Id).ToHashSet();
        var edges = new List<UniversalGraphEdgeDto>();

        foreach (var entity in await _repo.ListEdgesAsync(projectId, ct))
        {
            if (!ids.Contains(entity.SourceNodeId)
                || !ids.Contains(entity.TargetNodeId))
            {
                continue;
            }

            var dto = DevelopmentGraphMapper.Map(entity);
            var relation = DevelopmentGraphMetadata.MetadataString(
                dto.MetadataJson,
                "relationKind",
                $"development-graph edge {entity.Id}");

            if (!string.IsNullOrWhiteSpace(relation))
            {
                edges.Add(new(dto, relation));
            }
        }

        return new(
            projectId,
            universal
                .OrderBy(x => x.DomainId)
                .ThenBy(x => x.Kind)
                .ThenBy(x => x.Node.Name)
                .ToArray(),
            edges.ToArray());
    }

    public async Task<DevelopmentGraphDto> GetAsync(
            Guid projectId,
            DevelopmentGraphNodeType? type,
            CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, type, ct);
        var ids = nodes.Select(x => x.Id).ToHashSet();

        var edges = (await _repo.ListEdgesAsync(projectId, ct))
            .Where(x => ids.Contains(x.SourceNodeId) && ids.Contains(x.TargetNodeId))
            .Select(DevelopmentGraphMapper.Map)
            .ToArray();

        return new(projectId, nodes.Select(DevelopmentGraphMapper.Map).ToArray(), edges);
    }

    public Task<DevelopmentGraphNode?> FindNodeAsync(
            Guid projectId,
            DevelopmentGraphNodeType type,
            string key,
            CancellationToken ct) =>
            _repo.FindNodeAsync(projectId, type, key, ct);

    public Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(
            Guid projectId,
            DevelopmentGraphNodeType? type,
            CancellationToken ct) =>
            _repo.ListNodesAsync(projectId, type, ct);

    public Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(
            Guid projectId,
            CancellationToken ct) =>
            _repo.ListEdgesAsync(projectId, ct);

    internal Task<DevelopmentGraphNode?> GetNodeByIdAsync(Guid nodeId, CancellationToken ct) =>
        _repo.GetNodeAsync(nodeId, ct);
}
