// Purpose: Exposes Incidents HTTP endpoints for the DIP API.
using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Incidents;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;
[ApiController]
[Route("api/v1/incidents")]
[Produces("application/json")]
public sealed record ResolveIncidentRequest(string? FixedCommit);

public sealed class IncidentsController(IncidentAggregationService incidents) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}")]
    public Task<IReadOnlyList<IncidentSummaryDto>> List(Guid projectId, [FromQuery] int take, CancellationToken ct) => incidents.ListAsync(projectId, take <= 0 ? 50 : take, ct);
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectResource(ProjectMembershipRole.Viewer, ProjectResourceKind.Incident, "incidentId")]
    [HttpGet("{incidentId:guid}")]
    public async Task<ActionResult<IncidentTimelineDto>> Get(Guid incidentId, CancellationToken ct) { var timeline = await incidents.GetTimelineAsync(incidentId, ct); return timeline is null ? NotFound() : Ok(timeline); }
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectResource(ProjectMembershipRole.Member, ProjectResourceKind.Incident, "incidentId")]
    [HttpPost("{incidentId:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid incidentId, [FromBody] ResolveIncidentRequest request, CancellationToken ct) => await incidents.ResolveAsync(incidentId, request.FixedCommit, ct) ? Ok(new { incidentId, status = "Resolved" }) : NotFound();
}
