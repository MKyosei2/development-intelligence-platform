// Purpose: Owns Development Graph node and edge mutations, including universal graph mutation metadata.
using System.Text.Json;
using System.Text.Json.Nodes;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

internal sealed class DevelopmentGraphMutationService
{
    private readonly IDevelopmentGraphRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    internal DevelopmentGraphMutationService(IDevelopmentGraphRepository repo, IUnitOfWork uow, ISystemClock clock)
    {
        _repo = repo;
        _uow = uow;
        _clock = clock;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertNodeAsync(
            UpsertDevelopmentGraphNodeCommand command,
            CancellationToken ct)
        {
            var node = await UpsertNodeCoreAsync(command, null, ct);
            await _uow.SaveChangesAsync(ct);
            return DevelopmentGraphMapper.Map(node);
        }
    
    public async Task<DevelopmentGraphEdgeDto> LinkAsync(
            LinkDevelopmentGraphNodesCommand command,
            CancellationToken ct)
        {
            var source = await _repo.GetNodeAsync(command.SourceNodeId, ct)
                ?? throw new KeyNotFoundException(
                    $"Graph node {command.SourceNodeId} was not found.");
            var target = await _repo.GetNodeAsync(command.TargetNodeId, ct)
                ?? throw new KeyNotFoundException(
                    $"Graph node {command.TargetNodeId} was not found.");
    
            var edge = await LinkCoreAsync(command, source, target, null, ct);
            await _uow.SaveChangesAsync(ct);
            return DevelopmentGraphMapper.Map(edge);
        }
    
    public async Task<UniversalGraphNodeDto> UpsertUniversalNodeAsync(
            UpsertUniversalGraphNodeCommand command,
            CancellationToken ct)
        {
            if (command.ProjectId == Guid.Empty)
            {
                throw new ArgumentException("ProjectId is required.", nameof(command));
            }
    
            var domain = DevelopmentGraphMetadata.Identifier(command.DomainId, 120, nameof(command.DomainId));
            var kind = DevelopmentGraphMetadata.Identifier(command.Kind, 160, nameof(command.Kind));
            var schema = DevelopmentGraphMetadata.Identifier(command.SchemaVersion, 40, nameof(command.SchemaVersion));
            var sourceObjectId = DevelopmentGraphMetadata.Optional(
                command.SourceObjectId,
                500,
                nameof(command.SourceObjectId));
    
            var metadata = DevelopmentGraphMetadata.MergeMetadata(
                command.MetadataJson,
                new Dictionary<string, string?>
                {
                    ["domainId"] = domain,
                    ["kind"] = kind,
                    ["schemaVersion"] = schema,
                    ["sourceObjectId"] = sourceObjectId
                });
    
            var node = await UpsertNodeAsync(
                new(
                    command.ProjectId,
                    command.FallbackType,
                    command.Key,
                    command.Name,
                    command.Status,
                    command.Provider,
                    command.ExternalReference,
                    metadata),
                ct);
    
            return new(
                node,
                domain,
                kind,
                schema,
                sourceObjectId);
        }
    
    public async Task<UniversalGraphEdgeDto> LinkUniversalAsync(
            LinkUniversalGraphNodesCommand command,
            CancellationToken ct)
        {
            if (command.ProjectId == Guid.Empty)
            {
                throw new ArgumentException("ProjectId is required.", nameof(command));
            }
    
            var relation = DevelopmentGraphMetadata.Identifier(
                command.RelationKind,
                160,
                nameof(command.RelationKind));
    
            var metadata = DevelopmentGraphMetadata.MergeMetadata(
                command.MetadataJson,
                new Dictionary<string, string?>
                {
                    ["relationKind"] = relation
                });
    
            var edge = await LinkAsync(
                new(
                    command.ProjectId,
                    command.SourceNodeId,
                    command.TargetNodeId,
                    command.FallbackType,
                    command.Weight,
                    command.Critical,
                    metadata),
                ct);
    
            return new(edge, relation);
        }
    
    internal async Task<DevelopmentGraphNode> UpsertNodeCoreAsync(
            UpsertDevelopmentGraphNodeCommand command,
            Dictionary<(DevelopmentGraphNodeType Type, string Key), DevelopmentGraphNode>? cache,
            CancellationToken ct)
        {
            var cacheKey = (command.Type, command.Key);
            if (cache is not null && cache.TryGetValue(cacheKey, out var cached))
            {
                cached.Observe(
                    command.Name,
                    command.Status,
                    command.Provider,
                    command.ExternalReference,
                    DevelopmentGraphMetadata.Json(command.MetadataJson),
                    _clock.UtcNow);
                return cached;
            }
    
            var node = await _repo.FindNodeAsync(command.ProjectId, command.Type, command.Key, ct);
            if (node is null)
            {
                node = new DevelopmentGraphNode(
                    Guid.NewGuid(),
                    command.ProjectId,
                    command.Type,
                    command.Key,
                    command.Name,
                    command.Status,
                    command.Provider,
                    command.ExternalReference,
                    DevelopmentGraphMetadata.Json(command.MetadataJson),
                    _clock.UtcNow);
                await _repo.AddNodeAsync(node, ct);
            }
            else
            {
                node.Observe(
                    command.Name,
                    command.Status,
                    command.Provider,
                    command.ExternalReference,
                    DevelopmentGraphMetadata.Json(command.MetadataJson),
                    _clock.UtcNow);
            }
    
            if (cache is not null)
                cache[cacheKey] = node;
    
            return node;
        }
    
    internal async Task<DevelopmentGraphEdge> LinkCoreAsync(
            LinkDevelopmentGraphNodesCommand command,
            DevelopmentGraphNode source,
            DevelopmentGraphNode target,
            Dictionary<(Guid Source, Guid Target, DevelopmentGraphEdgeType Type), DevelopmentGraphEdge>? cache,
            CancellationToken ct)
        {
            if (source.ProjectId != command.ProjectId || target.ProjectId != command.ProjectId)
            {
                throw new InvalidOperationException(
                    "Graph edge endpoints must belong to the requested project.");
            }
    
            var cacheKey = (command.SourceNodeId, command.TargetNodeId, command.Type);
            if (cache is not null && cache.TryGetValue(cacheKey, out var cached))
            {
                cached.Observe(command.Weight, command.Critical, DevelopmentGraphMetadata.Json(command.MetadataJson), _clock.UtcNow);
                return cached;
            }
    
            var edge = await _repo.FindEdgeAsync(
                command.ProjectId,
                command.SourceNodeId,
                command.TargetNodeId,
                command.Type,
                ct);
    
            if (edge is null)
            {
                edge = new DevelopmentGraphEdge(
                    Guid.NewGuid(),
                    command.ProjectId,
                    command.SourceNodeId,
                    command.TargetNodeId,
                    command.Type,
                    command.Weight,
                    command.Critical,
                    DevelopmentGraphMetadata.Json(command.MetadataJson),
                    _clock.UtcNow);
                await _repo.AddEdgeAsync(edge, ct);
            }
            else
            {
                edge.Observe(command.Weight, command.Critical, DevelopmentGraphMetadata.Json(command.MetadataJson), _clock.UtcNow);
            }
    
            if (cache is not null)
                cache[cacheKey] = edge;
    
            return edge;
        }
}
