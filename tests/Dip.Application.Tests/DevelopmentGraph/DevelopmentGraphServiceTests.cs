// Purpose: Verifies DevelopmentGraphService fail-closed ingestion, graph filtering, idempotency, and unit-of-work boundaries.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.DevelopmentGraph;

public sealed class DevelopmentGraphServiceTests
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 9, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IngestVersionControlChange_RejectsOversizedEvidenceWithoutMutation()
    {
        var (service, repository, unitOfWork) = CreateService();
        var change = CreateChange(
            Enumerable.Range(0, 5001)
                .Select(index => new NormalizedChangedFile(
                    $"src/Game/File{index:D4}.cs",
                    "modified",
                    1,
                    1))
                .ToArray());

        Func<Task> act = () =>
            service.IngestVersionControlChangeAsync(
                change,
                CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidDataException>()
            .WithMessage("*exceeds the fail-closed limit of 5000*");

        repository.Nodes.Should().BeEmpty();
        repository.Edges.Should().BeEmpty();
        unitOfWork.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task IngestVersionControlChange_AcceptsBoundaryAndSavesExactlyOnce()
    {
        var (service, repository, unitOfWork) = CreateService();
        var change = CreateChange(
            Enumerable.Range(0, 5000)
                .Select(index => new NormalizedChangedFile(
                    $"src/Game/File{index:D4}.cs",
                    "modified",
                    1,
                    1))
                .ToArray());

        await service.IngestVersionControlChangeAsync(
            change,
            CancellationToken.None);

        repository.Nodes.Count(node => node.Type == DevelopmentGraphNodeType.File)
            .Should()
            .Be(5000);
        unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task IngestVersionControlChange_ReingestingSameRevisionDoesNotDuplicateGraph()
    {
        var (service, repository, unitOfWork) = CreateService();
        var change = CreateChange(
            [new NormalizedChangedFile("src/Game/Player.cs", "modified", 4, 2)]);

        await service.IngestVersionControlChangeAsync(
            change,
            CancellationToken.None);

        var nodeCount = repository.Nodes.Count;
        var edgeCount = repository.Edges.Count;

        await service.IngestVersionControlChangeAsync(
            change,
            CancellationToken.None);

        repository.Nodes.Should().HaveCount(nodeCount);
        repository.Edges.Should().HaveCount(edgeCount);
        unitOfWork.SaveCalls.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_DoesNotExposeEdgeWhenFilteredEndpointIsMissing()
    {
        var (service, repository, _) = CreateService();
        var projectId = Guid.NewGuid();
        var commit = Node(
            projectId,
            DevelopmentGraphNodeType.Commit,
            "commit:repo:abc",
            "abc");
        var file = Node(
            projectId,
            DevelopmentGraphNodeType.File,
            "file:repo:src/Game/Player.cs",
            "src/Game/Player.cs");

        repository.Nodes.AddRange([commit, file]);
        repository.Edges.Add(
            new DevelopmentGraphEdge(
                Guid.NewGuid(),
                projectId,
                commit.Id,
                file.Id,
                DevelopmentGraphEdgeType.Changes,
                50m,
                false,
                "{}",
                FixedNow));

        var result = await service.GetAsync(
            projectId,
            DevelopmentGraphNodeType.Commit,
            CancellationToken.None);

        result.Nodes.Should().ContainSingle();
        result.Nodes[0].Id.Should().Be(commit.Id);
        result.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUniversalAsync_ThrowsWhenStoredMetadataIsMalformed()
    {
        var (service, repository, _) = CreateService();
        var projectId = Guid.NewGuid();
        repository.Nodes.Add(
            new DevelopmentGraphNode(
                Guid.NewGuid(),
                projectId,
                DevelopmentGraphNodeType.Commit,
                "commit:repo:broken",
                "broken",
                "Observed",
                "GitHub",
                null,
                "{not-json}",
                FixedNow));

        Func<Task> act = async () =>
            await service.GetUniversalAsync(
                projectId,
                null,
                null,
                CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidDataException>()
            .WithMessage("Malformed metadata was encountered*");
    }

    [Fact]
    public async Task GetUniversalAsync_DoesNotExposeEdgeToNonUniversalEndpoint()
    {
        var (service, repository, _) = CreateService();
        var projectId = Guid.NewGuid();
        var universal = new DevelopmentGraphNode(
            Guid.NewGuid(),
            projectId,
            DevelopmentGraphNodeType.Commit,
            "commit:repo:abc",
            "abc",
            "Observed",
            "GitHub",
            null,
            "{\"domainId\":\"code\",\"kind\":\"commit\",\"schemaVersion\":\"1\"}",
            FixedNow);
        var nonUniversal = new DevelopmentGraphNode(
            Guid.NewGuid(),
            projectId,
            DevelopmentGraphNodeType.File,
            "file:repo:src/Game/Player.cs",
            "src/Game/Player.cs",
            "Observed",
            "GitHub",
            null,
            "{\"domainId\":\"code\"}",
            FixedNow);

        repository.Nodes.AddRange([universal, nonUniversal]);
        repository.Edges.Add(
            new DevelopmentGraphEdge(
                Guid.NewGuid(),
                projectId,
                universal.Id,
                nonUniversal.Id,
                DevelopmentGraphEdgeType.Changes,
                50m,
                false,
                "{\"relationKind\":\"changes\"}",
                FixedNow));

        var result = await service.GetUniversalAsync(
            projectId,
            null,
            null,
            CancellationToken.None);

        result.Nodes.Should().ContainSingle();
        result.Nodes[0].Node.Id.Should().Be(universal.Id);
        result.Edges.Should().BeEmpty();
    }

    [Fact]
    public async Task IngestVersionControlChange_PropagatesCancellationTokenToPersistenceBoundary()
    {
        var (service, repository, unitOfWork) = CreateService();
        using var source = new CancellationTokenSource();
        var token = source.Token;

        await service.IngestVersionControlChangeAsync(
            CreateChange(
                [new NormalizedChangedFile("src/Game/Player.cs", "modified")]),
            token);

        repository.ObservedTokens.Should().NotBeEmpty();
        repository.ObservedTokens.Should().OnlyContain(observed => observed == token);
        unitOfWork.LastToken.Should().Be(token);
    }

    private static (
        DevelopmentGraphService Service,
        RecordingDevelopmentGraphRepository Repository,
        RecordingUnitOfWork UnitOfWork) CreateService()
    {
        var repository = new RecordingDevelopmentGraphRepository();
        var unitOfWork = new RecordingUnitOfWork();
        var service = new DevelopmentGraphService(
            repository,
            unitOfWork,
            new FixedClock(FixedNow));

        return (service, repository, unitOfWork);
    }

    private static NormalizedVersionControlChange CreateChange(
        IReadOnlyList<NormalizedChangedFile> files) =>
        new(
            Guid.NewGuid(),
            VersionControlProvider.GitHub,
            "example/repository",
            "https://github.com/example/repository",
            "abc123",
            "main",
            "Test revision",
            "https://github.com/example/repository/commit/abc123",
            FixedNow,
            files);

    private static DevelopmentGraphNode Node(
        Guid projectId,
        DevelopmentGraphNodeType type,
        string key,
        string name) =>
        new(
            Guid.NewGuid(),
            projectId,
            type,
            key,
            name,
            "Observed",
            "GitHub",
            null,
            "{}",
            FixedNow);

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCalls++;
            LastToken = ct;
            return Task.FromResult(1);
        }
    }

    private sealed class RecordingDevelopmentGraphRepository :
        IDevelopmentGraphRepository
    {
        private readonly Dictionary<
            (Guid ProjectId, DevelopmentGraphNodeType Type, string Key),
            DevelopmentGraphNode> _nodeIndex = [];
        private readonly Dictionary<
            (Guid ProjectId, Guid SourceId, Guid TargetId, DevelopmentGraphEdgeType Type),
            DevelopmentGraphEdge> _edgeIndex = [];

        public List<DevelopmentGraphNode> Nodes { get; } = [];
        public List<DevelopmentGraphEdge> Edges { get; } = [];
        public List<CancellationToken> ObservedTokens { get; } = [];

        public Task<DevelopmentGraphNode?> GetNodeAsync(
            Guid id,
            CancellationToken ct)
        {
            Observe(ct);
            return Task.FromResult(
                Nodes.SingleOrDefault(node => node.Id == id));
        }

        public Task<DevelopmentGraphNode?> FindNodeAsync(
            Guid projectId,
            DevelopmentGraphNodeType type,
            string key,
            CancellationToken ct)
        {
            Observe(ct);
            _nodeIndex.TryGetValue((projectId, type, key), out var node);
            return Task.FromResult(node);
        }

        public Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(
            Guid projectId,
            DevelopmentGraphNodeType? type,
            CancellationToken ct)
        {
            Observe(ct);
            IReadOnlyList<DevelopmentGraphNode> result = Nodes
                .Where(
                    node => node.ProjectId == projectId
                            && (!type.HasValue || node.Type == type.Value))
                .OrderBy(node => node.Type)
                .ThenBy(node => node.Key)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task AddNodeAsync(
            DevelopmentGraphNode node,
            CancellationToken ct)
        {
            Observe(ct);
            Nodes.Add(node);
            _nodeIndex[(node.ProjectId, node.Type, node.Key)] = node;
            return Task.CompletedTask;
        }

        public Task<DevelopmentGraphEdge?> FindEdgeAsync(
            Guid projectId,
            Guid sourceNodeId,
            Guid targetNodeId,
            DevelopmentGraphEdgeType type,
            CancellationToken ct)
        {
            Observe(ct);
            _edgeIndex.TryGetValue(
                (projectId, sourceNodeId, targetNodeId, type),
                out var edge);
            return Task.FromResult(edge);
        }

        public Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(
            Guid projectId,
            CancellationToken ct)
        {
            Observe(ct);
            IReadOnlyList<DevelopmentGraphEdge> result = Edges
                .Where(edge => edge.ProjectId == projectId)
                .OrderBy(edge => edge.Type)
                .ToArray();
            return Task.FromResult(result);
        }

        public Task AddEdgeAsync(
            DevelopmentGraphEdge edge,
            CancellationToken ct)
        {
            Observe(ct);
            Edges.Add(edge);
            _edgeIndex[
                (edge.ProjectId, edge.SourceNodeId, edge.TargetNodeId, edge.Type)] = edge;
            return Task.CompletedTask;
        }

        private void Observe(CancellationToken ct) =>
            ObservedTokens.Add(ct);
    }
}
