// Purpose: Exposes E vi de nc eI ng es ti on HTTP endpoints for c on tr ol le rs.
using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Evidence;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/evidence")]
[Produces("application/json")]
public sealed class EvidenceIngestionController(
    EvidenceIngestionService ingestion) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("events")]
    public async Task<ActionResult<IngestEvidenceResult>> Ingest(
        Guid projectId,
        IngestEvidenceCommand command,
        CancellationToken ct)
    {
        if (projectId != command.ProjectId)
            return BadRequest(new
            {
                error = "project_mismatch",
                message = "Route projectId must match the evidence ProjectId."
            });

        var result = await ingestion.IngestAsync(command, ct);
        return result.Created
            ? Created($"/api/v1/projects/{result.ProjectId}/evidence/{result.EvidenceId}", result)
            : Ok(result);
    }
}
