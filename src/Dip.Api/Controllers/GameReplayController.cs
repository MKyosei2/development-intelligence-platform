// Purpose: Exposes HTTP endpoints for G am eR ep la yC on tr ol le r.
using Dip.Api.Authorization;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Replay;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/replay")]
public sealed class GameReplayController(GameReplayService service,ArtifactService artifacts):ControllerBase
{
    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("sessions")]
    public Task<ReplaySessionDto> Create([FromBody]CreateReplaySessionCommand command,CancellationToken ct)=>service.CreateSessionAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/sessions")]
    public Task<IReadOnlyList<ReplaySessionDto>> List(Guid projectId,CancellationToken ct)=>service.ListSessionsAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("runs")]
    public async Task<IActionResult> Run([FromBody]ReplayJobRequest request,CancellationToken ct){var result=await service.EnqueueAsync(request,ct);return Accepted(result.StatusUrl,result);}

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("projects/{projectId:guid}/inputs")]
    [RequestSizeLimit(64L*1024L*1024L)]
    public async Task<IActionResult> UploadInput(Guid projectId,IFormFile file,CancellationToken ct)
    {
        if(file.Length<=0)return BadRequest(new{error="Replay input file is empty."});
        var extension=Path.GetExtension(file.FileName).ToLowerInvariant();if(extension is not ".json" and not ".jsonl")return BadRequest(new{error="Replay input must be JSON or JSONL."});
        await using var stream=file.OpenReadStream();
            var record=await artifacts.StoreAsync(projectId,"replay-input",Path.GetFileName(file.FileName),stream,"application/json",new ArtifactLinkContext(),"replay",TimeSpan.FromDays(3650),ct);
            return Created($"/api/v1/artifacts/{record.Id}",new{artifactId=record.Id,record.FileName,record.CreatedAtUtc});
    }
}
