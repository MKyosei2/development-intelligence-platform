// Purpose: Exposes P ro je ct Ma na ge me nt Co mm an dC en te rC on tr ol le r HTTP endpoints.
using Dip.Api.Authorization;
using Dip.Application.Features.Management;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/project-control")]
[Produces("application/json")]
public sealed class ProjectManagementCommandCenterController(ProjectManagementCommandCenterService service) : ControllerBase
{
    [Authorize(Policy=Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer,"projectId")]
    [HttpGet("projects/{projectId:guid}/command-center")]
    [ProducesResponseType(typeof(ProjectManagementCommandCenterDto),StatusCodes.Status200OK)]
    public Task<ProjectManagementCommandCenterDto> Get(
        Guid projectId,
        [FromQuery]int horizonWeeks=8,
        [FromQuery]string? ownerRole=null,
        CancellationToken cancellationToken=default)
        =>service.GetAsync(projectId,Math.Clamp(horizonWeeks,1,26),ownerRole,cancellationToken);
}
