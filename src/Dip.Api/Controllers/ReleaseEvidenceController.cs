using Dip.Api.Authorization;
using Dip.Application.Features.Release;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;
[ApiController][Route("api/v1/release-evidence")]
public sealed class ReleaseEvidenceController(ReleaseEvidenceService service):ControllerBase
{
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("runs")] public async Task<IActionResult> Run([FromBody]ReleaseEvidenceJobRequest request,CancellationToken ct){var result=await service.EnqueueAsync(request,ct);return Accepted(result.StatusUrl,result);}
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/latest")] public async Task<ActionResult<ReleaseEvidenceBundleDto>> Latest(Guid projectId,CancellationToken ct){var result=await service.GetLatestAsync(projectId,ct);return result is null?NotFound():Ok(result);}
}
