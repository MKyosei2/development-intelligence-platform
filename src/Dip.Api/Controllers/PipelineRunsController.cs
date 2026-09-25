// Purpose: Exposes Pipeline Runs HTTP endpoints for the DIP API.
using Dip.Api.Authorization;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Authorization;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

public sealed record PipelineQualityIssueResponse(
    Guid Id,
    string Severity,
    string Status,
    string Title,
    string Category,
    string Fingerprint,
    string? EvidenceJson);

public sealed record PipelineEvidenceResponse(
    Guid Id,
    string EvidenceKind,
    string Outcome,
    string Title,
    string Summary,
    string Confidence,
    string? SourceReference,
    DateTimeOffset ObservedAtUtc);

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
    string? FailureReason,
    IReadOnlyList<PipelineQualityIssueResponse> QualityIssues,
    IReadOnlyList<PipelineEvidenceResponse> Evidence);

[ApiController]
[Route("api/v1/pipeline-runs")]
[Produces("application/json")]
public sealed class PipelineRunsController(
    IPipelineRunRepository repository,
    IQualityIssueRepository issues,
    IEvidenceRepository evidence) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PipelineRunResponse>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PipelineRunResponse>> List(
        Guid projectId,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var runs = await repository.ListByProjectAsync(projectId, take, cancellationToken);
        var ids = runs.Select(x => x.Id).ToArray();
        var relatedIssues = await issues.ListByPipelineRunsAsync(projectId, ids, cancellationToken);
        var relatedEvidence = await evidence.ListByPipelineRunsAsync(projectId, ids, cancellationToken);
        return runs.Select(run => Map(run, relatedIssues, relatedEvidence)).ToArray();
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectResource(ProjectMembershipRole.Viewer, ProjectResourceKind.PipelineRun, "id")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PipelineRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PipelineRunResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var run = await repository.GetByIdAsync(id, cancellationToken);
        if (run is null)
            return NotFound();
        var relatedIssues = await issues.ListByPipelineRunsAsync(run.ProjectId, new[] { id }, cancellationToken);
        var relatedEvidence = await evidence.ListByPipelineRunsAsync(run.ProjectId, new[] { id }, cancellationToken);
        return Ok(Map(run, relatedIssues, relatedEvidence));
    }

    private static PipelineRunResponse Map(
        PipelineRun run,
        IReadOnlyList<QualityIssue> issues,
        IReadOnlyList<EvidenceRecord> evidence)
    {
        var runIssues = issues
            .Where(issue => issue.PipelineRunId == run.Id
                            || issue.FirstSeenPipelineRunId == run.Id
                            || issue.LastSeenPipelineRunId == run.Id)
            .Select(issue => new PipelineQualityIssueResponse(
                issue.Id,
                issue.Severity.ToString(),
                issue.Status.ToString(),
                issue.Title,
                issue.Category,
                issue.Fingerprint,
                issue.EvidenceJson))
            .ToArray();
        var runEvidence = evidence
            .Where(item => item.PipelineRunId == run.Id)
            .Select(item => new PipelineEvidenceResponse(
                item.Id,
                item.EvidenceKind,
                item.Outcome.ToString(),
                item.Title,
                item.Summary,
                item.Confidence.ToString(),
                item.SourceReference,
                item.ObservedAtUtc))
            .ToArray();

        return new PipelineRunResponse(
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
            run.FailureReason,
            runIssues,
            runEvidence);
    }
}
