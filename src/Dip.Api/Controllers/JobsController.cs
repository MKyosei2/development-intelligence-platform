// Purpose: Exposes J ob s HTTP endpoints for c on tr ol le rs.
using Dip.Api.Authorization;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Jobs;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/jobs")]
[Produces("application/json")]
public sealed class JobsController(DurableJobService jobs, ArtifactService artifacts) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectResource(ProjectMembershipRole.Viewer, ProjectResourceKind.DurableJob, "jobId")]
    [HttpGet("{jobId:guid}")]
    public async Task<ActionResult<DurableJobDto>> Get(Guid jobId, CancellationToken ct) { var result = await jobs.GetAsync(jobId, ct); return result is null ? NotFound() : Ok(result); }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}")]
    public Task<IReadOnlyList<DurableJobDto>> List(Guid projectId, [FromQuery] int take, CancellationToken ct) => jobs.ListAsync(projectId, take <= 0 ? 50 : take, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectResource(ProjectMembershipRole.Viewer, ProjectResourceKind.DurableJob, "jobId")]
    [HttpGet("{jobId:guid}/artifacts")]
    public Task<IReadOnlyList<ArtifactRecordDto>> Artifacts(Guid jobId, CancellationToken ct) => artifacts.ListForJobAsync(jobId, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectResource(ProjectMembershipRole.Member, ProjectResourceKind.DurableJob, "jobId")]
    [HttpPost("{jobId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid jobId, CancellationToken ct) => await jobs.RequestCancellationAsync(jobId, ct) ? Accepted(new { jobId, status = "CancellationRequested" }) : NotFound();

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectResource(ProjectMembershipRole.Member, ProjectResourceKind.DurableJob, "jobId")]
    [HttpPost("{jobId:guid}/retry")]
    public async Task<IActionResult> Retry(Guid jobId, CancellationToken ct) => await jobs.RetryAsync(jobId, ct) ? Accepted(new { jobId, status = "Queued" }) : NotFound();
}
