using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;
using Dip.Application.Features.QualityAutomation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/qa")]
[Produces("application/json")]
public sealed class QaController(QaOrchestrationService service) : ControllerBase
{
    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("runs")]
    [ProducesResponseType(typeof(QaRunResultDto),StatusCodes.Status200OK)]
    public Task<QaRunResultDto> Run([FromBody] QaExecutionCommand command,CancellationToken ct)=>service.RunAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/crash-clusters")]
    [ProducesResponseType(typeof(IReadOnlyList<QaCrashClusterDto>),StatusCodes.Status200OK)]
    public Task<IReadOnlyList<QaCrashClusterDto>> CrashClusters(
        Guid projectId,[FromQuery]int take,CancellationToken ct)=>
        service.GetCrashClustersAsync(projectId,take<=0?50:take,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [RequireProjectResource(ProjectMembershipRole.Viewer,ProjectResourceKind.QaRun,"runId")]
    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(typeof(QaRunResultDto),StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QaRunResultDto>> Get(Guid runId,CancellationToken ct)
    {
        var result=await service.GetAsync(runId,ct);return result is null?NotFound():Ok(result);
    }
}
