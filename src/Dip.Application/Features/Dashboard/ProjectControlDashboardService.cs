// Purpose: Orchestrates the DIP workflow for P ro je ct Co nt ro lD as hb oa rd Se rv ic e.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Decisions;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Dashboard;

public sealed record ProjectControlDashboardDto(
    Guid ProjectId,int GraphNodes,int GraphEdges,int Repositories,int Components,int Features,int NotReadyFeatures,
    int Milestones,int BlockedMilestones,int Assets,int CriticalAssets,int OpenReviews,int FailedCiRuns,
    string DecisionStatus,IReadOnlyList<string> ActionsRequired,IReadOnlyList<string> MaterialRisks,DateTimeOffset GeneratedAtUtc);

public sealed class ProjectControlDashboardService
{
    private readonly IDevelopmentGraphRepository _graph;private readonly DecisionLogService _decisions;
    public ProjectControlDashboardService(IDevelopmentGraphRepository graph,DecisionLogService decisions){_graph=graph;_decisions=decisions;}
    public async Task<ProjectControlDashboardDto> GetAsync(Guid projectId,CancellationToken ct)
    {
        var nodes=await _graph.ListNodesAsync(projectId,null,ct);var edges=await _graph.ListEdgesAsync(projectId,ct);var brief=await _decisions.BuildBriefAsync(projectId,ct);
        return new(projectId,nodes.Count,edges.Count,nodes.Count(x=>x.Type==DevelopmentGraphNodeType.Repository),nodes.Count(x=>x.Type==DevelopmentGraphNodeType.Component),nodes.Count(x=>x.Type==DevelopmentGraphNodeType.Feature),nodes.Count(x=>x.Type==DevelopmentGraphNodeType.Feature&&x.Status is "NotReady" or "Blocked"),nodes.Count(x=>x.Type==DevelopmentGraphNodeType.Milestone),nodes.Count(x=>x.Type==DevelopmentGraphNodeType.Milestone&&x.Status=="Blocked"),nodes.Count(x=>x.Type==DevelopmentGraphNodeType.Asset),nodes.Count(x=>x.Type==DevelopmentGraphNodeType.Asset&&x.Status=="Critical"),nodes.Count(x=>x.Type==DevelopmentGraphNodeType.Review&&x.Status is not "Approved" and not "ReadyForMerge"),nodes.Count(x=>x.Type==DevelopmentGraphNodeType.CiRun&&IsFailed(x.Status)),brief.Status,brief.DecisionsRequired,brief.Risks,brief.GeneratedAtUtc);
    }
    private static bool IsFailed(string status)=>status.Equals("Failed",StringComparison.OrdinalIgnoreCase)||status.Equals("Failure",StringComparison.OrdinalIgnoreCase)||status.Equals("Error",StringComparison.OrdinalIgnoreCase);
}
