// Purpose: Provides the stable Development Graph facade while focused services own mutation, query, VCS ingestion, and CI ingestion.
using System.Text.Json;
using System.Text.Json.Nodes;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

public sealed partial class DevelopmentGraphService
{
    private readonly IUnitOfWork _uow;
    private readonly DevelopmentGraphMutationService _mutations;
    private readonly DevelopmentGraphQueryService _queries;
    private readonly VersionControlGraphIngestor _versionControl;
    private readonly CiGraphIngestor _ci;

    public DevelopmentGraphService(IDevelopmentGraphRepository repo, IUnitOfWork uow, ISystemClock clock)
    {
        _uow = uow;
        _mutations = new(repo, uow, clock);
        _queries = new(repo);
        _versionControl = new(_mutations, uow);
        _ci = new(_mutations, _queries, uow);
    }

    public Task<DevelopmentGraphNodeDto> UpsertNodeAsync(UpsertDevelopmentGraphNodeCommand command, CancellationToken ct) =>
        _mutations.UpsertNodeAsync(command, ct);

    public Task<DevelopmentGraphEdgeDto> LinkAsync(LinkDevelopmentGraphNodesCommand command, CancellationToken ct) =>
        _mutations.LinkAsync(command, ct);

    public Task<UniversalGraphNodeDto> UpsertUniversalNodeAsync(UpsertUniversalGraphNodeCommand command, CancellationToken ct) =>
        _mutations.UpsertUniversalNodeAsync(command, ct);

    public Task<UniversalGraphEdgeDto> LinkUniversalAsync(LinkUniversalGraphNodesCommand command, CancellationToken ct) =>
        _mutations.LinkUniversalAsync(command, ct);

    public Task<UniversalGraphDto> GetUniversalAsync(Guid projectId, string? domainId, string? kind, CancellationToken ct) =>
        _queries.GetUniversalAsync(projectId, domainId, kind, ct);

    public Task<DevelopmentGraphDto> GetAsync(Guid projectId, DevelopmentGraphNodeType? type, CancellationToken ct) =>
        _queries.GetAsync(projectId, type, ct);

    public Task IngestVersionControlChangeAsync(NormalizedVersionControlChange change, CancellationToken ct) =>
        _versionControl.IngestVersionControlChangeAsync(change, ct);

    public Task IngestCiRunAsync(NormalizedCiRun run, CancellationToken ct) =>
        _ci.IngestCiRunAsync(run, ct);

    public Task<DevelopmentGraphNode?> FindNodeAsync(Guid projectId, DevelopmentGraphNodeType type, string key, CancellationToken ct) =>
        _queries.FindNodeAsync(projectId, type, key, ct);

    public Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(Guid projectId, DevelopmentGraphNodeType? type, CancellationToken ct) =>
        _queries.ListNodesAsync(projectId, type, ct);

    public Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(Guid projectId, CancellationToken ct) =>
        _queries.ListEdgesAsync(projectId, ct);
}
