using Dip.Api.Authorization;
using Dip.Api.Hubs;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

public sealed record QualityIssueResponse(
    Guid Id,
    Guid ProjectId,
    Guid PipelineRunId,
    string Fingerprint,
    string Category,
    string Severity,
    string Title,
    string Description,
    string? Source,
    string? RuleId,
    string? EvidenceJson,
    string Status,
    int OccurrenceCount,
    Guid FirstSeenPipelineRunId,
    Guid LastSeenPipelineRunId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    DateTimeOffset? ResolvedAtUtc);

public sealed record QaTrendPointResponse(
    Guid RunId,
    string Language,
    string Status,
    int FindingCount,
    int CriticalFindingCount,
    int WarningFindingCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? FinishedAtUtc);

[ApiController]
[Route("api/v1/qa")]
[Produces("application/json")]
public sealed class QaController(
    QaOrchestrationService service,
    IQualityIssueRepository issues,
    IQaRunRepository runs,
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
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/issues")]
    [ProducesResponseType(typeof(IReadOnlyList<QualityIssueResponse>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<QualityIssueResponse>> Issues(
        Guid projectId,
        [FromQuery] int take = 200,
        CancellationToken cancellationToken = default) =>
        (await issues.ListByProjectAsync(projectId, Math.Clamp(take, 1, 500), cancellationToken))
        .Select(MapIssue)
        .ToArray();

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/issues/{issueId:guid}")]
    [ProducesResponseType(typeof(QualityIssueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QualityIssueResponse>> Issue(
        Guid projectId,
        Guid issueId,
        CancellationToken cancellationToken)
    {
        var issue = await issues.GetAsync(issueId, cancellationToken);
        if (issue is null || issue.ProjectId != projectId)
            return NotFound();
        return Ok(MapIssue(issue));
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/trends")]
    [ProducesResponseType(typeof(IReadOnlyList<QaTrendPointResponse>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<QaTrendPointResponse>> Trends(
        Guid projectId,
        [FromQuery] int take = 30,
        CancellationToken cancellationToken = default) =>
        (await runs.ListRecentAsync(projectId, Math.Clamp(take, 1, 100), cancellationToken))
        .OrderBy(x => x.CreatedAtUtc)
        .Select(x => new QaTrendPointResponse(
            x.Id,
            x.Language.ToString(),
            x.Status.ToString(),
            x.FindingCount,
            x.CriticalFindingCount,
            x.WarningFindingCount,
            x.CreatedAtUtc,
            x.FinishedAtUtc))
        .ToArray();

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

    private static QualityIssueResponse MapIssue(Dip.Domain.Entities.QualityIssue issue) => new(
        issue.Id,
        issue.ProjectId,
        issue.PipelineRunId,
        issue.Fingerprint,
        issue.Category,
        issue.Severity.ToString(),
        issue.Title,
        issue.Description,
        issue.Source,
        issue.RuleId,
        issue.EvidenceJson,
        issue.Status.ToString(),
        issue.OccurrenceCount,
        issue.FirstSeenPipelineRunId,
        issue.LastSeenPipelineRunId,
        issue.CreatedAtUtc,
        issue.LastSeenAtUtc,
        issue.ResolvedAtUtc);
}
