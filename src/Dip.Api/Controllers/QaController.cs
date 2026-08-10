using Dip.Api.Authorization;
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
    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(typeof(QaRunResultDto),StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QaRunResultDto>> Get(Guid runId,CancellationToken ct)
    {
        var result=await service.GetAsync(runId,ct);return result is null?NotFound():Ok(result);
    }
}
