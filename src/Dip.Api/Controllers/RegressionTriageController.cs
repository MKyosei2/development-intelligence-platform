using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;
using Dip.Application.Features.Triage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;
[ApiController][Route("api/v1/regression-triage")]
public sealed class RegressionTriageController(RegressionTriageService service):ControllerBase
{
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost] public Task<RegressionTriageDto> Triage([FromBody]RegressionTriageRequest request,CancellationToken ct)=>service.TriageAsync(request,ct);
    [Authorize(Policy=Policies.ProjectRead)][RequireProjectResource(ProjectMembershipRole.Viewer,ProjectResourceKind.Incident,"incidentId")][HttpGet("incidents/{incidentId:guid}/latest")] public async Task<ActionResult<RegressionTriageDto>> Latest(Guid incidentId,CancellationToken ct){var result=await service.GetLatestAsync(incidentId,ct);return result is null?NotFound():Ok(result);}
}
