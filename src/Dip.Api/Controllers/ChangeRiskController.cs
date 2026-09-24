// Purpose: Exposes C ha ng eR is k HTTP endpoints for c on tr ol le rs.
using Dip.Api.Authorization;
using Dip.Application.Features.ChangeRisk;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;
[ApiController]
[Route("api/v1/change-risk")]
public sealed class ChangeRiskController(ChangeRiskIntelligenceService service) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)][HttpPost] public Task<ChangeRiskDto> Assess([FromBody] ChangeRiskRequest request, CancellationToken ct) => service.AssessAsync(request, ct);
    [Authorize(Policy = Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/latest")] public async Task<ActionResult<ChangeRiskDto>> Latest(Guid projectId, [FromQuery] string? commitSha, CancellationToken ct) { var result = await service.GetLatestAsync(projectId, commitSha, ct); return result is null ? NotFound() : Ok(result); }
}
