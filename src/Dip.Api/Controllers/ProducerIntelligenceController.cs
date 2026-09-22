// Purpose: Exposes HTTP endpoints for this API responsibility.
using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Management;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/producer-intelligence")]
[Produces("application/json")]
public sealed class ProducerIntelligenceController(ProducerIntelligenceService service) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet]
    public async Task<ActionResult<ProducerIntelligenceDto>> Get(Guid projectId, CancellationToken ct)
        => Ok(await service.GetAsync(projectId, ct));
}
