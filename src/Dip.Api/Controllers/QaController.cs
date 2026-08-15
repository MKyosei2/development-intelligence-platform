using Dip.Api.Authorization;
using Dip.Api.Hubs;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/qa")]
[Produces("application/json")]
public sealed class QaController(
    QaOrchestrationService service,
    ProjectRealtimePublisher realtime) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("runs")]
    [ProducesResponseType(typeof(QaRunResultDto), StatusCodes.Status200OK)]
    public async Task<QaRunResultDto> Run(
        [FromBody] QaExecutionCommand command,
        CancellationToken cancellationToken)
    {
        await realtime.PublishAsync(
            command.ProjectId,
            "qa.started",
            "QA run started.",
            cancellationToken);

        try
        {
            var result = await service.RunAsync(command, cancellationToken);
            await realtime.PublishAsync(
                command.ProjectId,
                "qa.completed",
                $"QA completed with {result.FindingCount} finding(s).",
                cancellationToken);
            return result;
        }
        catch
        {
            await realtime.PublishAsync(
                command.ProjectId,
                "qa.failed",
                "QA run failed before completion.",
                CancellationToken.None);
            throw;
        }
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/crash-clusters")]
    [ProducesResponseType(typeof(IReadOnlyList<QaCrashClusterDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<QaCrashClusterDto>> CrashClusters(
        Guid projectId,
        [FromQuery] int take,
        CancellationToken cancellationToken) =>
        service.GetCrashClustersAsync(projectId, take <= 0 ? 50 : take, cancellationToken);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectResource(ProjectMembershipRole.Viewer, ProjectResourceKind.QaRun, "runId")]
    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(typeof(QaRunResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QaRunResultDto>> Get(
        Guid runId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(runId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
