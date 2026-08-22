using Dip.Api.Authorization;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Authorization;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
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

    public PipelineRunsController(IPipelineRunRepository repository)
        => _repository = repository;

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PipelineRunResponse>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PipelineRunResponse>> List(
        Guid projectId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
        => (await _repository.ListByProjectAsync(projectId, take, cancellationToken))
            .Select(Map)
            .ToArray();

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectResource(ProjectMembershipRole.Viewer, ProjectResourceKind.PipelineRun, "id")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PipelineRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PipelineRunResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var run = await _repository.GetByIdAsync(id, cancellationToken);
        return run is null ? NotFound() : Ok(Map(run));
    }

    private static PipelineRunResponse Map(PipelineRun run)
        => new(
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
            run.FailureReason);
}
