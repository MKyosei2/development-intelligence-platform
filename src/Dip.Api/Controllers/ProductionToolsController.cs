using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.ProductionTools;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/production")]
[Produces("application/json")]
public sealed class ProductionToolsController(
    ProductionToolEventService events,
    ProductionEventQueryService query,
    ProductionMediaService media) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("events/batch")]
    public async Task<ActionResult<ProductionToolBatchResult>> Events(
        Guid projectId,
        ProductionToolBatchRequest request,
        CancellationToken ct)
        => Ok(await events.IngestAsync(projectId, request, ct));

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<ProductionEventDto>>> List(
        Guid projectId,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
        => Ok(await query.ListAsync(projectId, take, ct));

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("media")]
    [RequestFormLimits(MultipartBodyLengthLimit = 1_073_741_824)]
    [RequestSizeLimit(1_073_741_824)]
    public async Task<ActionResult<StoredProductionMediaDto>> Media(
        Guid projectId,
        [FromForm] string agentId,
        [FromForm] string eventId,
        [FromForm] ProductionToolSource source,
        [FromForm] ProductionMediaKind kind,
        [FromForm] string relativePath,
        [FromForm] string? sha256,
        [FromForm] IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "media_file_required" });

        await using var stream = file.OpenReadStream();
        return Ok(await media.StoreAsync(
            projectId,
            agentId,
            eventId,
            source,
            kind,
            relativePath,
            stream,
            file.ContentType,
            file.Length,
            sha256,
            ct));
    }
}
