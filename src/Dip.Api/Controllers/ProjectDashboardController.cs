// Purpose: Exposes the project-control dashboard read model.
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
public sealed class ProjectDashboardController(ProjectControlDashboardService dashboard) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/dashboard")]
    public Task<ProjectControlDashboardDto> GetDashboard(Guid projectId, CancellationToken ct)
            => dashboard.GetAsync(projectId, ct);
}
