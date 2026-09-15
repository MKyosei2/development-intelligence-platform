using Dip.Api.Authorization;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

public sealed record LocalFileBatchRequest(
    string AgentId,
    string RootId,
    IReadOnlyList<LocalFileObservation> Observations);

[ApiController]
[Route("api/v1/projects/{projectId:guid}/collector")]
[Produces("application/json")]
public sealed class LocalCollectorController(TrackedArtifactService service) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("file-events/batch")]
    public async Task<ActionResult<LocalFileBatchResult>> FileEvents(
        Guid projectId,
        LocalFileBatchRequest request,
        CancellationToken ct)
    {
        if (request.Observations is null)
            return BadRequest(new { error = "observations_required" });

        var result = await service.IngestAsync(
            new IngestLocalFileBatchCommand(projectId, request.AgentId, request.RootId, request.Observations),
            ct);
        return Ok(result);
    }
}
