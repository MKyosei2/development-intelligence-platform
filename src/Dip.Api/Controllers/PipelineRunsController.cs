using Dip.Application.Abstractions.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

public sealed record PipelineRunResponse(
    Guid Id,
    Guid ProjectId,
    string CommitSha,
    string ExternalRunId,
    int AttemptNumber,
    string Branch,
    string CollectorType,
    string CollectorVersion,
    string Status,
    DateTimeOffset QueuedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    string? FailureReason);

[ApiController]
[Route("api/v1/pipeline-runs")]
[Produces("application/json")]
public sealed class PipelineRunsController : ControllerBase
{
    private readonly IPipelineRunRepository _repository;
    public PipelineRunsController(IPipelineRunRepository repository) => _repository = repository;

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PipelineRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PipelineRunResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var run = await _repository.GetByIdAsync(id, cancellationToken);
        if (run is null) return NotFound();
        return Ok(new PipelineRunResponse(
            run.Id,
            run.ProjectId,
            run.CommitSha.Value,
            run.ExternalRunId,
            run.AttemptNumber,
            run.Branch,
            run.CollectorType,
            run.CollectorVersion,
            run.Status.ToString(),
            run.QueuedAtUtc,
            run.StartedAtUtc,
            run.FinishedAtUtc,
            run.FailureReason));
    }
}
