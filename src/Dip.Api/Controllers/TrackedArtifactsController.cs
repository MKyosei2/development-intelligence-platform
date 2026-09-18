// Purpose: Exposes HTTP endpoints for T ra ck ed Ar ti fa ct sC on tr ol le r.
using Dip.Api.Authorization;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/tracked-artifacts")]
[Produces("application/json")]
public sealed class TrackedArtifactsController(TrackedArtifactService service) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TrackedArtifactDto>>> List(
        Guid projectId,
        [FromQuery] int take = 500,
        CancellationToken ct = default)
        => Ok(await service.ListAsync(projectId, take, ct));

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("{artifactId:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<TrackedArtifactVersionDto>>> Versions(
        Guid projectId,
        Guid artifactId,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        try { return Ok(await service.ListVersionsAsync(projectId, artifactId, take, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
