// Purpose: Maps project-control changes to test-impact selections and development-graph relationships.
using Dip.Application.Features.P2Intelligence;
using Dip.Domain.Enums;

namespace Dip.Application.Features.DevelopmentGraph;

public sealed record ProjectControlTestImpactResult(TestImpactResult Selection, int GraphLinksCreated);

/// <summary>Bridges the existing Python/C++/C# test-impact selector into the Development Graph.</summary>
public sealed class ProjectControlTestImpactService
{
    private readonly P2TechnicalIntelligenceService _technical; private readonly DevelopmentGraphService _graph;
    public ProjectControlTestImpactService(P2TechnicalIntelligenceService technical, DevelopmentGraphService graph) { _technical = technical; _graph = graph; }
    public async Task<ProjectControlTestImpactResult> AnalyzeAsync(TestImpactCommand command, string repositoryKey, CancellationToken ct)
    {
        var result = await _technical.AnalyzeTestImpactAsync(command, ct); var links = 0;
        foreach (var changed in command.ChangedFiles)
        {
            var file = await _graph.UpsertNodeAsync(new(command.ProjectId, DevelopmentGraphNodeType.File, $"file:{repositoryKey}:{Normalize(changed)}", changed, "Changed", "DIP", null, "{}"), ct);
            foreach (var selected in result.Tests.Take(100))
            {
                var test = await _graph.UpsertNodeAsync(new(command.ProjectId, DevelopmentGraphNodeType.Test, $"test:{repositoryKey}:{Normalize(selected.TestPath)}", selected.TestPath, "Selected", "DIP", null, System.Text.Json.JsonSerializer.Serialize(new { selected.Score, selected.Reasons })), ct);
                await _graph.LinkAsync(new(command.ProjectId, file.Id, test.Id, DevelopmentGraphEdgeType.TestedBy, Math.Clamp(selected.Score, 20m, 100m), selected.Score >= 85m, "{}"), ct); links++;
            }
        }
        return new(result, links);
    }
    private static string Normalize(string path) => path.Replace('\\', '/').Trim('/');
}
