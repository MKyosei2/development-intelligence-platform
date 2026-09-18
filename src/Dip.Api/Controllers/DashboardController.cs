// Purpose: Exposes HTTP endpoints for D as hb oa rd Co nt ro ll er.
using Dip.Api.Authorization;
using Dip.Application.Features.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Produces("application/json")]
[Authorize(Policy = Policies.ProjectRead)]
public sealed class DashboardController : ControllerBase
{
    private readonly DashboardQueryService _service;

    public DashboardController(DashboardQueryService service)
    {
        _service = service;
    }

    [HttpGet("projects")]
    public Task<IReadOnlyList<ProjectDashboardSummary>> GetProjects(
        CancellationToken cancellationToken) =>
        _service.GetProjectsAsync(cancellationToken);

    [HttpGet("projects/{projectId:guid}")]
    [ProducesResponseType(typeof(ProjectDashboardDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDashboardDetails>> GetProject(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetProjectAsync(
            projectId,
            cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }
}
