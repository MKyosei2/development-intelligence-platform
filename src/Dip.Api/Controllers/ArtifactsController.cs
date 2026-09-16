// Purpose: Exposes A rt if ac ts Co nt ro ll er HTTP endpoints.
using Dip.Api.Authorization;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;
[ApiController]
[Route("api/v1/artifacts")]
public sealed class ArtifactsController(ArtifactService artifacts):ControllerBase
{
    [Authorize(Policy=Policies.ProjectRead)][RequireProjectResource(ProjectMembershipRole.Viewer,ProjectResourceKind.Artifact,"artifactId")][HttpGet("{artifactId:guid}")]
    public async Task<IActionResult> Download(Guid artifactId,CancellationToken ct)
    {
        var opened=await artifacts.OpenAsync(artifactId,ct);if(opened is null)return NotFound();var(record,blob,content)=opened.Value;return File(content,blob.ContentType,record.FileName,enableRangeProcessing:true);
    }
}
