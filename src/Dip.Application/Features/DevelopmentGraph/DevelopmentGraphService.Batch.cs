// Purpose: Provides a transaction-bounded mutation session so related graph node and edge changes are saved once.
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

public sealed partial class DevelopmentGraphService
{
    internal async Task<TResult> MutateAsync<TResult>(
        Func<DevelopmentGraphMutationSession, CancellationToken, Task<TResult>> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        var result = await StageMutationAsync(action, ct);
        await _uow.SaveChangesAsync(ct);
        return result;
    }

    internal async Task<TResult> StageMutationAsync<TResult>(
        Func<DevelopmentGraphMutationSession, CancellationToken, Task<TResult>> action,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        var session = new DevelopmentGraphMutationSession(this);
        return await action(session, ct);
    }

    internal sealed class DevelopmentGraphMutationSession
    {
        private readonly DevelopmentGraphService _owner;
        private readonly Dictionary<(DevelopmentGraphNodeType Type, string Key), DevelopmentGraphNode> _nodes = new();
        private readonly Dictionary<(Guid Source, Guid Target, DevelopmentGraphEdgeType Type), DevelopmentGraphEdge> _edges = new();

        internal DevelopmentGraphMutationSession(DevelopmentGraphService owner)
        {
            _owner = owner;
        }

        internal async Task<DevelopmentGraphNodeDto> UpsertNodeAsync(
            UpsertDevelopmentGraphNodeCommand command,
            CancellationToken ct)
        {
            var node = await _owner._mutations.UpsertNodeCoreAsync(command, _nodes, ct);
            return DevelopmentGraphMapper.Map(node);
        }

        internal async Task<DevelopmentGraphEdgeDto> LinkAsync(
            LinkDevelopmentGraphNodesCommand command,
            CancellationToken ct)
        {
            var source = FindCachedNode(command.SourceNodeId)
                ?? await _owner._queries.GetNodeByIdAsync(command.SourceNodeId, ct)
                ?? throw new KeyNotFoundException($"Graph node {command.SourceNodeId} was not found.");
            var target = FindCachedNode(command.TargetNodeId)
                ?? await _owner._queries.GetNodeByIdAsync(command.TargetNodeId, ct)
                ?? throw new KeyNotFoundException($"Graph node {command.TargetNodeId} was not found.");

            var edge = await _owner._mutations.LinkCoreAsync(command, source, target, _edges, ct);
            return DevelopmentGraphMapper.Map(edge);
        }

        private DevelopmentGraphNode? FindCachedNode(Guid id) =>
            _nodes.Values.FirstOrDefault(node => node.Id == id);
    }
}
