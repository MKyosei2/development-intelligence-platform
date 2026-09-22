// Purpose: Exposes project decision logs, decision briefs, and what-if simulation endpoints.
using Dip.Api.Authorization;
using Dip.Application.Abstractions.Integrations;
using Dip.Application.Features.Assets;
using Dip.Application.Features.ChangeImpact;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.Decisions;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.FeatureControl;
using Dip.Application.Features.Integrations;
using Dip.Application.Features.Management;
using Dip.Application.Features.Milestones;
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
public sealed class ProjectDecisionController(DecisionLogService decisions, WhatIfSimulationService scenarios) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("decisions")]
    public Task<DecisionLogDto> LogDecision([FromBody] LogDecisionCommand command, CancellationToken ct)
            => decisions.LogAsync(command, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/decisions")]
    public Task<IReadOnlyList<DecisionLogDto>> ListDecisions(Guid projectId, [FromQuery] int take = 50, CancellationToken ct = default)
            => decisions.ListAsync(projectId, take, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/decision-brief")]
    public Task<ProjectControlDecisionBriefDto> GetDecisionBrief(Guid projectId, CancellationToken ct)
            => decisions.BuildBriefAsync(projectId, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("what-if")]
    public Task<WhatIfSimulationResult> Simulate([FromBody] WhatIfSimulationCommand command, CancellationToken ct)
            => scenarios.SimulateAsync(command, ct);
}
