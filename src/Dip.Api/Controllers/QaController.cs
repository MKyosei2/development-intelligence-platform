// Purpose: Exposes QA HTTP endpoints for the DIP API.
using Dip.Api.Authorization;
using Dip.Api.Hubs;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
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


public sealed record BranchQaRunRequest(
    string Branch,
    string WorkspaceRelativePath,
    string Language = "auto",
    string? CommitSha = null,
    string? RuntimeExecutable = null,
    IReadOnlyList<string>? RuntimeArguments = null,
    int TimeoutSeconds = 120,
    bool AutomaticallyReproduce = true,
    bool RecordScreen = true,
    bool RecordSystemAudio = true,
    bool RefreshRemote = true);

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
    PmEvidenceTranslator pm,
    CommercialQualityOperationsService commercial,
    BranchQualityOperationsService branchQuality,
    ProjectRealtimePublisher realtime) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("runs")]
    [ProducesResponseType(typeof(QaRunResultDto), StatusCodes.Status200OK)]
    public async Task<QaRunResultDto> Run([FromBody] QaExecutionCommand command, CancellationToken cancellationToken)
    {
        await realtime.PublishAsync(command.ProjectId, "qa.started", "QA run started.", cancellationToken);
        try
        {
            var result = await service.RunAsync(command, cancellationToken);
            await realtime.PublishAsync(command.ProjectId, "qa.completed", $"QA completed with {result.FindingCount} finding(s).", cancellationToken);
            return result;
        }
        catch
        {
            await realtime.PublishAsync(command.ProjectId, "qa.failed", "QA run failed before completion.", CancellationToken.None);
            throw;
        }
    }


    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/branches")]
    [ProducesResponseType(typeof(BranchQualityOverviewDto), StatusCodes.Status200OK)]
    public Task<BranchQualityOverviewDto> Branches(
        Guid projectId,
        [FromQuery] string workspaceRelativePath,
        [FromQuery] bool refreshRemote = false,
        CancellationToken cancellationToken = default)
        => branchQuality.GetAsync(projectId, workspaceRelativePath, refreshRemote, cancellationToken);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("projects/{projectId:guid}/branches/run")]
    [ProducesResponseType(typeof(QaRunResultDto), StatusCodes.Status200OK)]
    public async Task<QaRunResultDto> RunBranch(
        Guid projectId,
        [FromBody] BranchQaRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Branch)) throw new ArgumentException("Branch is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.WorkspaceRelativePath)) throw new ArgumentException("WorkspaceRelativePath is required.", nameof(request));
        var language = LanguageId.Parse(string.IsNullOrWhiteSpace(request.Language) ? "auto" : request.Language);
        var command = new QaExecutionCommand(
            projectId,
            $"branch:{request.Branch.Trim()}",
            language,
            request.WorkspaceRelativePath,
            request.CommitSha,
            request.RuntimeExecutable,
            request.RuntimeArguments,
            Math.Clamp(request.TimeoutSeconds, 5, 1800),
            request.AutomaticallyReproduce,
            request.RecordScreen,
            request.RecordSystemAudio,
            request.Branch.Trim(),
            request.RefreshRemote);
        await realtime.PublishAsync(projectId, "qa.branch.started", $"Branch QA started for {request.Branch.Trim()}.", cancellationToken);
        try
        {
            var result = await service.RunAsync(command, cancellationToken);
            await realtime.PublishAsync(projectId, "qa.branch.completed", $"Branch QA completed for {request.Branch.Trim()} with {result.FindingCount} finding(s).", cancellationToken);
            return result;
        }
        catch
        {
            await realtime.PublishAsync(projectId, "qa.branch.failed", $"Branch QA failed for {request.Branch.Trim()}.", CancellationToken.None);
            throw;
        }
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/crash-clusters")]
    [ProducesResponseType(typeof(IReadOnlyList<QaCrashClusterDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<QaCrashClusterDto>> CrashClusters(Guid projectId, [FromQuery] int take, CancellationToken cancellationToken)
        => service.GetCrashClustersAsync(projectId, take <= 0 ? 50 : take, cancellationToken);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/command-center")]
    [ProducesResponseType(typeof(CommercialQaCommandCenterDto), StatusCodes.Status200OK)]
    public Task<CommercialQaCommandCenterDto> CommandCenter(Guid projectId, [FromQuery] int take = 40, CancellationToken cancellationToken = default)
        => commercial.GetAsync(projectId, Math.Clamp(take, 10, 100), cancellationToken);

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/issues")]
    [ProducesResponseType(typeof(IReadOnlyList<QualityIssueResponse>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<QualityIssueResponse>> Issues(Guid projectId, [FromQuery] int take = 200, CancellationToken cancellationToken = default)
        => (await issues.ListByProjectAsync(projectId, Math.Clamp(take, 1, 500), cancellationToken)).Select(MapIssue).ToArray();

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/pm-decisions")]
    [ProducesResponseType(typeof(IReadOnlyList<PmQaDecisionItemDto>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<PmQaDecisionItemDto>> PmDecisions(Guid projectId, [FromQuery] int take = 10, CancellationToken cancellationToken = default)
    {
        var requested = Math.Clamp(take, 1, 50);
        var rows = await issues.ListByProjectAsync(projectId, Math.Clamp(requested * 8, 50, 500), cancellationToken);
        return rows.Where(x => x.ResolvedAtUtc is null)
            .OrderByDescending(x => SeverityRank(x.Severity))
            .ThenByDescending(x => x.OccurrenceCount)
            .ThenByDescending(x => x.LastSeenAtUtc)
            .Take(requested)
            .Select(issue =>
            {
                var candidate = new QaFindingCandidate(issue.Category, issue.Severity, "quality-issue", issue.Title, issue.Description, issue.Source, RuleId: issue.RuleId, SourceFormat: "quality-issue", SourceFormatVersion: "1.0");
                var reproduced = (issue.EvidenceJson ?? string.Empty).Contains("Reproduced", StringComparison.OrdinalIgnoreCase);
                var decision = pm.Decide(candidate, reproduced);
                return new PmQaDecisionItemDto(issue.Id, issue.Fingerprint, issue.Category, issue.Severity.ToString(), issue.Title, issue.OccurrenceCount, issue.LastSeenAtUtc, decision);
            }).ToArray();
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/issues/{issueId:guid}")]
    [ProducesResponseType(typeof(QualityIssueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QualityIssueResponse>> Issue(Guid projectId, Guid issueId, CancellationToken cancellationToken)
    {
        var issue = await issues.GetAsync(issueId, cancellationToken);
        if (issue is null || issue.ProjectId != projectId) return NotFound();
        return Ok(MapIssue(issue));
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/trends")]
    [ProducesResponseType(typeof(IReadOnlyList<QaTrendPointResponse>), StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<QaTrendPointResponse>> Trends(Guid projectId, [FromQuery] int take = 30, CancellationToken cancellationToken = default)
        => (await runs.ListRecentAsync(projectId, Math.Clamp(take, 1, 100), cancellationToken)).OrderBy(x => x.CreatedAtUtc)
            .Select(x => new QaTrendPointResponse(x.Id, x.Language.ToString(), x.Status.ToString(), x.FindingCount, x.CriticalFindingCount, x.WarningFindingCount, x.CreatedAtUtc, x.FinishedAtUtc)).ToArray();

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectResource(ProjectMembershipRole.Viewer, ProjectResourceKind.QaRun, "runId")]
    [HttpGet("runs/{runId:guid}")]
    [ProducesResponseType(typeof(QaRunResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QaRunResultDto>> Get(Guid runId, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(runId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    private static int SeverityRank(Severity severity) => severity switch { Severity.Critical => 3, Severity.Warning => 2, _ => 1 };

    private static QualityIssueResponse MapIssue(Dip.Domain.Entities.QualityIssue issue) => new(
        issue.Id, issue.ProjectId, issue.PipelineRunId, issue.Fingerprint, issue.Category, issue.Severity.ToString(), issue.Title, issue.Description,
        issue.Source, issue.RuleId, issue.EvidenceJson, issue.Status.ToString(), issue.OccurrenceCount, issue.FirstSeenPipelineRunId, issue.LastSeenPipelineRunId,
        issue.CreatedAtUtc, issue.LastSeenAtUtc, issue.ResolvedAtUtc);
}
