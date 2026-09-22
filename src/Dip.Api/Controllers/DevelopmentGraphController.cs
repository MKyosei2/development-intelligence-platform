// Purpose: Exposes Development Graph, impact, test-impact, and milestone endpoints under the stable project-control route.
using Dip.Api.Authorization;
using Dip.Application.Abstractions.Integrations;
using Dip.Application.Features.Assets;
using Dip.Application.Features.ChangeImpact;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.Decisions;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.FeatureControl;
using Dip.Application.Features.Integrations;
using Dip.Application.Features.Milestones;
using Dip.Application.Features.Management;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.Review;
using Dip.Application.Features.Scenarios;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectControlDecisionBriefDto = Dip.Application.Features.Decisions.DecisionBriefDto;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/project-control")]
public sealed class DevelopmentGraphController(DevelopmentGraphService graph, ChangeImpactAnalysisService impact, MilestoneIntelligenceService milestones, ProjectControlTestImpactService testImpact) : ControllerBase
{
    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/graph")]
        public Task<DevelopmentGraphDto> GetGraph(Guid projectId,[FromQuery]DevelopmentGraphNodeType? type,CancellationToken ct)
            =>graph.GetAsync(projectId,type,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/graph/universal")]
        public Task<UniversalGraphDto> GetUniversalGraph(Guid projectId,[FromQuery]string? domainId,[FromQuery]string? kind,CancellationToken ct)
            =>graph.GetUniversalAsync(projectId,domainId,kind,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("graph/nodes")]
        public Task<DevelopmentGraphNodeDto> UpsertNode([FromBody]UpsertDevelopmentGraphNodeCommand command,CancellationToken ct)
            =>graph.UpsertNodeAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("graph/universal/nodes")]
        public Task<UniversalGraphNodeDto> UpsertUniversalNode([FromBody]UpsertUniversalGraphNodeCommand command,CancellationToken ct)
            =>graph.UpsertUniversalNodeAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("graph/edges")]
        public Task<DevelopmentGraphEdgeDto> LinkNodes([FromBody]LinkDevelopmentGraphNodesCommand command,CancellationToken ct)
            =>graph.LinkAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("graph/universal/edges")]
        public Task<UniversalGraphEdgeDto> LinkUniversalNodes([FromBody]LinkUniversalGraphNodesCommand command,CancellationToken ct)
            =>graph.LinkUniversalAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("change-impact")]
        public Task<ChangeImpactResult> AnalyzeChangeImpact([FromBody]ChangeImpactCommand command,CancellationToken ct)
            =>impact.AnalyzeAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("test-impact")]
        public Task<ProjectControlTestImpactResult> AnalyzeTestImpact([FromBody]ProjectControlTestImpactRequest request,CancellationToken ct)
            =>testImpact.AnalyzeAsync(request.Command,request.RepositoryKey,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("milestones")]
        public Task<DevelopmentGraphNodeDto> UpsertMilestone([FromBody]UpsertMilestoneCommand command,CancellationToken ct)
            =>milestones.UpsertAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("milestone-dependencies")]
        public async Task<IActionResult> LinkMilestone([FromBody]MilestoneDependencyCommand command,CancellationToken ct)
        { await milestones.LinkDependencyAsync(command,ct);return NoContent(); }

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/critical-path")]
        public Task<CriticalPathResult> GetCriticalPath(Guid projectId,CancellationToken ct)
            =>milestones.AnalyzeAsync(projectId,ct);
}

public sealed record ProjectControlTestImpactRequest(string RepositoryKey, TestImpactCommand Command);
