// Purpose: Verifies transaction-bounded development-graph mutations persist related node and edge changes exactly once.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Tests.DevelopmentGraph;

public sealed class DevelopmentGraphMutationBatchTests
{
    [Fact]
    public async Task MutateAsync_SavesMultipleRelatedChangesExactlyOnce()
    {
        var repository = new Repository();
        var unitOfWork = new UnitOfWork();
        var service = new DevelopmentGraphService(
            repository,
            unitOfWork,
            new Clock(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero)));
        var projectId = Guid.NewGuid();

        await service.MutateAsync(
            async (mutation, ct) =>
            {
                var source = await mutation.UpsertNodeAsync(
                    new(projectId, DevelopmentGraphNodeType.Team, "team:a", "A"),
                    ct);
                var target = await mutation.UpsertNodeAsync(
                    new(projectId, DevelopmentGraphNodeType.Workstream, "workstream:b", "B"),
                    ct);
                await mutation.LinkAsync(
                    new(
                        projectId,
                        source.Id,
                        target.Id,
                        DevelopmentGraphEdgeType.RelatesTo,
                        80m,
                        false,
                        "{}"),
                    ct);
                return true;
            },
            CancellationToken.None);

        Assert.Equal(1, unitOfWork.SaveCalls);
        Assert.Equal(2, repository.Nodes.Count);
        Assert.Single(repository.Edges);
    }

    private sealed class Clock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class UnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class Repository : IDevelopmentGraphRepository
    {
        public List<DevelopmentGraphNode> Nodes { get; } = [];
        public List<DevelopmentGraphEdge> Edges { get; } = [];

        public Task<DevelopmentGraphNode?> GetNodeAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Nodes.FirstOrDefault(node => node.Id == id));

        public Task<DevelopmentGraphNode?> FindNodeAsync(
            Guid projectId,
            DevelopmentGraphNodeType type,
            string key,
            CancellationToken ct) =>
            Task.FromResult(Nodes.FirstOrDefault(node =>
                node.ProjectId == projectId && node.Type == type && node.Key == key));

        public Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(
            Guid projectId,
            DevelopmentGraphNodeType? type,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DevelopmentGraphNode>>(
                Nodes.Where(node =>
                        node.ProjectId == projectId && (!type.HasValue || node.Type == type.Value))
                    .ToArray());

        public Task AddNodeAsync(DevelopmentGraphNode node, CancellationToken ct)
        {
            Nodes.Add(node);
            return Task.CompletedTask;
        }

        public Task<DevelopmentGraphEdge?> FindEdgeAsync(
            Guid projectId,
            Guid sourceNodeId,
            Guid targetNodeId,
            DevelopmentGraphEdgeType type,
            CancellationToken ct) =>
            Task.FromResult(Edges.FirstOrDefault(edge =>
                edge.ProjectId == projectId
                && edge.SourceNodeId == sourceNodeId
                && edge.TargetNodeId == targetNodeId
                && edge.Type == type));

        public Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(
            Guid projectId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DevelopmentGraphEdge>>(
                Edges.Where(edge => edge.ProjectId == projectId).ToArray());

        public Task AddEdgeAsync(DevelopmentGraphEdge edge, CancellationToken ct)
        {
            Edges.Add(edge);
            return Task.CompletedTask;
        }
    }
}
