// Purpose: Exposes R el ea se Re ad in es s HTTP endpoints for c on tr ol le rs.
using Dip.Api.Authorization;
using Dip.Application.Features.Release;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/release-readiness")]
public sealed class ReleaseReadinessController(ReleaseReadinessService service) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("evaluate")]
    public Task<ReleaseReadinessDto> Evaluate([FromBody] EvaluateReleaseReadinessRequest request, CancellationToken ct) => service.EvaluateAsync(request, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("gate")]
    public async Task<ActionResult<ReleaseReadinessDto>> Gate([FromBody] EvaluateReleaseReadinessRequest request, [FromQuery] bool allowConditional = false, CancellationToken ct = default)
    {
        var result = await service.EvaluateAsync(request, ct);
        if (string.Equals(result.Decision, "NoGo", StringComparison.OrdinalIgnoreCase)) return Conflict(result);
        if (!allowConditional && string.Equals(result.Decision, "ConditionalGo", StringComparison.OrdinalIgnoreCase)) return StatusCode(StatusCodes.Status412PreconditionFailed, result);
        return Ok(result);
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/latest")]
    public async Task<ActionResult<ReleaseReadinessDto>> Latest(Guid projectId, CancellationToken ct) { var result = await service.GetLatestAsync(projectId, ct); return result is null ? NotFound() : Ok(result); }
}
