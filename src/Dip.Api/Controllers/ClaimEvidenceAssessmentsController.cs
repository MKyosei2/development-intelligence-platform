// Purpose: Exposes HTTP endpoints for this API responsibility.
using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/claim-assessments")]
[Produces("application/json")]
public sealed class ClaimEvidenceAssessmentsController(ClaimEvidenceAssessmentService service) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClaimEvidenceAssessmentDto>>> List(
        Guid projectId,
        [FromQuery] int take = 500,
        CancellationToken ct = default)
        => Ok(await service.ListAsync(projectId, take, ct));

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("{claimId:guid}")]
    public async Task<ActionResult<ClaimEvidenceAssessmentDto>> Assess(
        Guid projectId,
        Guid claimId,
        CancellationToken ct)
        => Ok(await service.AssessAsync(projectId, claimId, ct));
}
