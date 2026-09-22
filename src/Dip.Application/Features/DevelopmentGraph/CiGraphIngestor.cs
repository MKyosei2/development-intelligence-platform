// Purpose: Projects normalized CI runs into the Development Graph using a single save boundary.
using System.Text.Json;
using System.Text.Json.Nodes;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

internal sealed class CiGraphIngestor
{
    private readonly DevelopmentGraphMutationService _mutations;
    private readonly DevelopmentGraphQueryService _queries;
    private readonly IUnitOfWork _uow;

    internal CiGraphIngestor(DevelopmentGraphMutationService mutations, DevelopmentGraphQueryService queries, IUnitOfWork uow)
    {
        _mutations = mutations;
        _queries = queries;
        _uow = uow;
    }

    public async Task IngestCiRunAsync(NormalizedCiRun run, CancellationToken ct)
    {
        var nodes = new Dictionary<(DevelopmentGraphNodeType Type, string Key), DevelopmentGraphNode>();
        var edges = new Dictionary<(Guid Source, Guid Target, DevelopmentGraphEdgeType Type), DevelopmentGraphEdge>();

        var ci = await _mutations.UpsertNodeCoreAsync(
            new(
                run.ProjectId,
                DevelopmentGraphNodeType.CiRun,
                $"ci:{run.Provider}:{run.PipelineKey}:{run.RunId}",
                $"{run.PipelineKey} #{run.RunId}",
                run.Status,
                run.Provider.ToString(),
                run.Url,
                JsonSerializer.Serialize(new
                {
                    run.Revision,
                    run.Branch,
                    run.PassedTests,
                    run.FailedTests,
                    run.SkippedTests,
                    run.MetadataJson
                })),
            nodes,
            ct);

        if (!string.IsNullOrWhiteSpace(run.Revision))
        {
            var commit = await _queries.FindNodeAsync(
                run.ProjectId,
                DevelopmentGraphNodeType.Commit,
                $"commit:{run.RepositoryKey}:{run.Revision}",
                ct);

            if (commit is not null)
            {
                await _mutations.LinkCoreAsync(
                    new(run.ProjectId, commit.Id, ci.Id, DevelopmentGraphEdgeType.BuiltBy, 90m, run.FailedTests > 0, "{}"),
                    commit,
                    ci,
                    edges,
                    ct);
            }
        }

        await _uow.SaveChangesAsync(ct);
    }
}
