// Purpose: Exposes P ro je ct Cl ai ms HTTP endpoints for c on tr ol le rs.
using Dip.Api.Authorization;
using Dip.Application.Features.ProjectTruth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

public sealed record CreateProjectClaimRequest(
    string SubjectKey,
    string ClaimedStatus,
    decimal? ClaimedProgress,
    string? ClaimedBy,
    string? Description);

[ApiController]
[Route("api/v1/projects/{projectId:guid}/claims")]
public sealed class ProjectClaimsController : ControllerBase
{
    private readonly ProjectClaimService _service;

    public ProjectClaimsController(ProjectClaimService service)
    {
        _service = service;
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet]
    public Task<IReadOnlyList<ProjectClaimDto>> List(
        Guid projectId,
        CancellationToken cancellationToken) =>
        _service.ListAsync(projectId, cancellationToken);

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost]
    [ProducesResponseType(typeof(ProjectClaimDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ProjectClaimDto>> Create(
        Guid projectId,
        CreateProjectClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor))
            actor = string.IsNullOrWhiteSpace(request.ClaimedBy) ? "unknown" : request.ClaimedBy.Trim();

        var result = await _service.CreateAsync(
            new CreateProjectClaimCommand(
                projectId,
                request.SubjectKey,
                request.ClaimedStatus,
                request.ClaimedProgress,
                actor,
                request.Description),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }
}
