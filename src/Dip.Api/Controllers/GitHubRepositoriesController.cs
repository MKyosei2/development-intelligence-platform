using System.Text.Json;
using Dip.Api.Authorization;
using Dip.Api.Hubs;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.GitHubIntegration;
using Dip.Application.Features.Projects;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

public sealed record GitHubCapabilityCheckResponse(
    string Name,
    string Status,
    string Message);

public sealed record GitHubConnectionTestResponse(
    bool Success,
    string Mode,
    string Message,
    int RepositoryCount,
    DateTimeOffset TestedAtUtc,
    string OverallStatus = "Unknown",
    string? Repository = null,
    IReadOnlyList<GitHubCapabilityCheckResponse>? Checks = null);

public sealed record PullRequestSummaryResponse(
    int Number,
    string Title,
    string Status,
    string? Url,
    int ChangedFiles,
    int Reviews,
    int CiRuns,
    bool Critical,
    string? ReviewState);

public sealed record PullRequestReviewResponse(
    string Reviewer,
    string State,
    string? Url);

public sealed record PullRequestCiResponse(
    string Name,
    string Status,
    string? Url);

public sealed record PullRequestPipelineResponse(
    Guid Id,
    string CommitSha,
    string Status,
    string CollectorType,
    string ExternalRunId,
    DateTimeOffset? FinishedAtUtc,
    int QualityIssueCount,
    int EvidenceCount);

public sealed record PullRequestDetailsResponse(
    PullRequestSummaryResponse Summary,
    IReadOnlyList<string> Revisions,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<PullRequestReviewResponse> Reviews,
    IReadOnlyList<PullRequestCiResponse> CiRuns,
    IReadOnlyList<PullRequestPipelineResponse> PipelineRuns,
    IReadOnlyList<PipelineQualityIssueResponse> QualityIssues,
    IReadOnlyList<PipelineEvidenceResponse> Evidence);

[ApiController]
[Route("api/v1/integrations/github")]
[Produces("application/json")]
public sealed class GitHubRepositoriesController(
    IGitHubRepositoryCatalog github,
    ProjectService projects,
    ProjectAccessService access,
    DevelopmentGraphService developmentGraph,
    IPipelineRunRepository pipelineRuns,
    IQualityIssueRepository qualityIssues,
    IEvidenceRepository evidence,
    QaOrchestrationService qa,
    ProjectRealtimePublisher realtime,
    IConfiguration configuration) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("status")]
    public Task<GitHubConnectionStatusDto> Status(CancellationToken cancellationToken)
        => github.GetStatusAsync(cancellationToken);

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpPost("test")]
    public async Task<GitHubConnectionTestResponse> TestConnection(
        CancellationToken cancellationToken)
    {
        var status = await github.GetStatusAsync(cancellationToken);
        if (!status.Connected)
            return FailedConnectionTest(status, status.Message, 0);

        try
        {
            var repositories = await github.ListRepositoriesAsync(null, null, cancellationToken);
            if (repositories.Count == 0)
            {
                return new GitHubConnectionTestResponse(
                    false,
                    status.Mode,
                    "Authentication PASS, but no repository is visible. Repository capabilities cannot be verified.",
                    0,
                    DateTimeOffset.UtcNow,
                    "Partial",
                    null,
                    new[]
                    {
                        new GitHubCapabilityCheckResponse("Authentication", "PASS", "GitHub credentials are accepted."),
                        new GitHubCapabilityCheckResponse("Repository access", "UNKNOWN", "No repository is visible to this installation/token.")
                    });
            }

            var probe = repositories[0];
            return await ProbeRepositoryAsync(status, repositories.Count, probe.Owner, probe.Name, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or IOException)
        {
            return FailedConnectionTest(status, "GitHub test failed: " + SafeMessage(exception.Message), 0);
        }
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpPost("projects/{projectId:guid}/test")]
    public async Task<ActionResult<GitHubConnectionTestResponse>> TestProjectConnection(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
            return NotFound();
        if (!TryParseRepository(project.RepositoryUrl, out var owner, out var repository))
        {
            return BadRequest(new
            {
                error = "not_github_repository",
                message = "The selected project is not backed by a github.com repository."
            });
        }

        var status = await github.GetStatusAsync(cancellationToken);
        if (!status.Connected)
            return Ok(FailedConnectionTest(status, status.Message, 0, $"{owner}/{repository}"));

        try
        {
            return Ok(await ProbeRepositoryAsync(status, 1, owner, repository, cancellationToken));
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or IOException)
        {
            return Ok(FailedConnectionTest(
                status,
                "Project repository probe failed: " + SafeMessage(exception.Message),
                0,
                $"{owner}/{repository}"));
        }
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("repositories")]
    public async Task<IReadOnlyList<GitHubRepositorySummaryDto>> Repositories(
        [FromQuery] string? search,
        [FromQuery] string? visibility,
        CancellationToken cancellationToken)
    {
        var repositories = await github.ListRepositoriesAsync(search, visibility, cancellationToken);
        var registered = await projects.ListAsync(cancellationToken);

        if ((configuration.GetValue<bool?>("Authentication:RequireAuthenticatedUser") ?? false)
            && !User.IsInRole(Roles.Admin))
        {
            var allowed = await access.AccessibleProjectsAsync(
                RequestActor.Subject(User),
                cancellationToken);
            registered = registered.Where(project => allowed.Contains(project.Id)).ToArray();
        }

        registered = registered
            .Where(project => !project.LifecycleState.Equals(
                ProjectService.DeletedState,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return repositories
            .Select(repository =>
            {
                var project = registered.FirstOrDefault(candidate =>
                    SameRepository(candidate.RepositoryUrl, repository.HtmlUrl));
                return project is null
                    ? repository
                    : repository with { DipProjectId = project.Id };
            })
            .ToArray();
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("repositories/{owner}/{repository}")]
    public Task<GitHubRepositoryDetailsDto> Repository(
        string owner,
        string repository,
        CancellationToken cancellationToken)
        => github.GetRepositoryAsync(owner, repository, cancellationToken);

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("import")]
    public async Task<ActionResult<ImportGitHubProjectResult>> Import(
        ImportGitHubProjectRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseRepository(request.Repository, out var owner, out var repository))
        {
            return BadRequest(new
            {
                error = "invalid_repository",
                message = "Repository must be owner/name or a github.com repository URL."
            });
        }

        var details = await github.GetRepositoryAsync(owner, repository, cancellationToken);
        var repositoryUrl = details.Repository.HtmlUrl;

        var existing = (await projects.ListAsync(cancellationToken))
            .FirstOrDefault(project =>
                !project.LifecycleState.Equals(ProjectService.DeletedState, StringComparison.OrdinalIgnoreCase)
                && SameRepository(project.RepositoryUrl, repositoryUrl));
        if (existing is not null)
        {
            return Conflict(new
            {
                error = "repository_already_imported",
                message = "This GitHub repository is already registered as a DIP project."
            });
        }

        var name = string.IsNullOrWhiteSpace(request.ProjectName)
            ? details.Repository.Name
            : request.ProjectName.Trim();
        var actor = RequestActor.AuditIdentity(User);
        var created = await projects.CreateAsync(
            new CreateProjectCommand(
                name,
                repositoryUrl,
                details.Repository.DefaultBranch,
                request.ReleaseDateUtc),
            RequestActor.Subject(User),
            RequestActor.Login(User),
            actor,
            cancellationToken);

        GitHubWorkspaceSyncDto? workspace = null;
        string? syncWarning = null;
        if (request.SyncWorkspace)
        {
            try
            {
                workspace = await github.SyncWorkspaceAsync(
                    created.Id,
                    owner,
                    repository,
                    details.Repository.DefaultBranch,
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is HttpRequestException
                    or IOException
                    or InvalidDataException
                    or InvalidOperationException)
            {
                syncWarning = "Project import succeeded, but the initial QA workspace synchronization failed: "
                    + SafeMessage(exception.Message);
            }
        }

        try
        {
            var pullRequests = await github.ListPullRequestsAsync(owner, repository, "all", cancellationToken);
            await BackfillPullRequestsAsync(created.Id, details.Repository, pullRequests, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            syncWarning = string.IsNullOrWhiteSpace(syncWarning)
                ? "Project import succeeded, but pull-request history backfill failed: " + SafeMessage(exception.Message)
                : syncWarning + " Pull-request history backfill also failed: " + SafeMessage(exception.Message);
        }

        QaRunResultDto? initialQa = null;
        if (request.RunInitialQa && workspace is not null)
        {
            try
            {
                await realtime.PublishAsync(
                    created.Id,
                    "qa.started",
                    "Initial repository QA started after GitHub import.",
                    cancellationToken);
                initialQa = await qa.RunAsync(
                    new QaExecutionCommand(
                        created.Id,
                        $"github-import:{workspace.Branch}:{ShortSha(workspace.CommitSha)}",
                        LanguageId.Auto,
                        workspace.WorkspaceRelativePath,
                        workspace.CommitSha,
                        null,
                        null,
                        TimeoutSeconds: 300,
                        AutomaticallyReproduce: true,
                        RecordScreen: false,
                        RecordSystemAudio: false),
                    cancellationToken);
                await realtime.PublishAsync(
                    created.Id,
                    "qa.completed",
                    $"Initial repository QA completed with {initialQa.FindingCount} finding(s).",
                    cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                syncWarning = string.IsNullOrWhiteSpace(syncWarning)
                    ? "Project import and workspace synchronization succeeded, but initial QA failed: " + SafeMessage(exception.Message)
                    : syncWarning + " Initial QA also failed: " + SafeMessage(exception.Message);
                await realtime.PublishAsync(
                    created.Id,
                    "qa.failed",
                    "Initial repository QA failed: " + SafeMessage(exception.Message),
                    CancellationToken.None);
            }
        }

        await realtime.PublishAsync(
            created.Id,
            workspace is not null ? "project.imported" : "project.imported.unsynced",
            workspace is not null
                ? "GitHub project imported and workspace synchronized."
                : syncWarning ?? "GitHub project imported without workspace synchronization.",
            cancellationToken);

        return Ok(new ImportGitHubProjectResult(
            created.Id,
            created.Name,
            created.RepositoryUrl,
            created.DefaultBranch,
            workspace is not null,
            workspace,
            syncWarning,
            initialQa?.QaRunId,
            initialQa?.Status,
            initialQa?.FindingCount ?? 0,
            initialQa?.CriticalFindingCount ?? 0));
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/repository")]
    public async Task<ActionResult<GitHubProjectRepositoryDto>> ProjectRepository(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
            return NotFound();

        var connection = await github.GetStatusAsync(cancellationToken);
        var workspace = await github.GetWorkspaceAsync(projectId, cancellationToken);
        if (!TryParseRepository(project.RepositoryUrl, out var owner, out var repository))
        {
            return Ok(new GitHubProjectRepositoryDto(
                project.Id,
                project.Name,
                project.RepositoryUrl,
                project.DefaultBranch,
                connection,
                null,
                workspace,
                "This project is not backed by a github.com repository."));
        }

        if (!connection.Connected)
        {
            return Ok(new GitHubProjectRepositoryDto(
                project.Id,
                project.Name,
                project.RepositoryUrl,
                project.DefaultBranch,
                connection,
                null,
                workspace,
                connection.Message));
        }

        try
        {
            var details = await github.GetRepositoryAsync(owner, repository, cancellationToken);
            return Ok(new GitHubProjectRepositoryDto(
                project.Id,
                project.Name,
                project.RepositoryUrl,
                project.DefaultBranch,
                connection,
                details,
                workspace,
                null));
        }
        catch (HttpRequestException exception)
        {
            return Ok(new GitHubProjectRepositoryDto(
                project.Id,
                project.Name,
                project.RepositoryUrl,
                project.DefaultBranch,
                connection,
                null,
                workspace,
                "GitHub repository metadata could not be loaded: " + exception.Message));
        }
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/pull-requests")]
    public async Task<ActionResult<IReadOnlyList<PullRequestSummaryResponse>>> PullRequests(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
            return NotFound();

        var graph = await developmentGraph.GetAsync(projectId, null, cancellationToken);
        var summaries = graph.Nodes
            .Where(node => node.Type.Equals(
                DevelopmentGraphNodeType.PullRequest.ToString(),
                StringComparison.OrdinalIgnoreCase))
            .Select(node => BuildPullRequestDetails(graph, node))
            .Where(details => details is not null)
            .Select(details => details!.Summary)
            .OrderByDescending(x => x.Number)
            .ToArray();
        return Ok(summaries);
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/pull-requests/{number:int}")]
    public async Task<ActionResult<PullRequestDetailsResponse>> PullRequest(
        Guid projectId,
        int number,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
            return NotFound();

        var graph = await developmentGraph.GetAsync(projectId, null, cancellationToken);
        var node = graph.Nodes.FirstOrDefault(candidate =>
            candidate.Type.Equals(DevelopmentGraphNodeType.PullRequest.ToString(), StringComparison.OrdinalIgnoreCase)
            && PullRequestNumber(candidate) == number);
        if (node is null)
            return NotFound();

        var details = BuildPullRequestDetails(graph, node);
        if (details is null)
            return NotFound();
        return Ok(await EnrichPullRequestDetailsAsync(projectId, details, cancellationToken));
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("projects/{projectId:guid}/sync")]
    public async Task<ActionResult<GitHubWorkspaceSyncDto>> Sync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
            return NotFound();
        if (!TryParseRepository(project.RepositoryUrl, out var owner, out var repository))
        {
            return BadRequest(new
            {
                error = "not_github_repository",
                message = "Only github.com projects can be synchronized by the GitHub repository connection."
            });
        }

        try
        {
            var result = await github.SyncWorkspaceAsync(
                projectId,
                owner,
                repository,
                project.DefaultBranch,
                cancellationToken);
            try
            {
                var details = await github.GetRepositoryAsync(owner, repository, cancellationToken);
                var pullRequests = await github.ListPullRequestsAsync(owner, repository, "all", cancellationToken);
                await BackfillPullRequestsAsync(projectId, details.Repository, pullRequests, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
            {
                await realtime.PublishAsync(
                    projectId,
                    "repository.pull_request_backfill_failed",
                    "Repository synchronized, but pull-request history backfill failed: " + SafeMessage(exception.Message),
                    CancellationToken.None);
            }
            await realtime.PublishAsync(
                projectId,
                "repository.synced",
                $"Repository synchronized at {result.CommitSha[..Math.Min(8, result.CommitSha.Length)]}.",
                cancellationToken);
            return Ok(result);
        }
        catch
        {
            await realtime.PublishAsync(
                projectId,
                "repository.sync_failed",
                "Repository synchronization failed.",
                CancellationToken.None);
            throw;
        }
    }

    private async Task BackfillPullRequestsAsync(
        Guid projectId,
        GitHubRepositorySummaryDto repository,
        IReadOnlyList<GitHubPullRequestSnapshotDto> pullRequests,
        CancellationToken cancellationToken)
    {
        foreach (var pullRequest in pullRequests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(pullRequest.HeadSha))
                continue;

            var revisions = pullRequest.CommitShas
                .Where(revision => !string.IsNullOrWhiteSpace(revision))
                .Append(pullRequest.HeadSha)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            foreach (var revision in revisions)
            {
                var isHead = revision.Equals(pullRequest.HeadSha, StringComparison.OrdinalIgnoreCase);
                var normalized = new NormalizedVersionControlChange(
                    projectId,
                    VersionControlProvider.GitHub,
                    repository.FullName,
                    repository.HtmlUrl,
                    revision,
                    string.IsNullOrWhiteSpace(pullRequest.HeadBranch) ? repository.DefaultBranch : pullRequest.HeadBranch,
                    pullRequest.Title,
                    pullRequest.HtmlUrl,
                    DateTimeOffset.UtcNow,
                    isHead
                        ? pullRequest.Files.Select(file => new NormalizedChangedFile(
                            file.Path,
                            file.Status,
                            file.Additions,
                            file.Deletions)).ToArray()
                        : Array.Empty<NormalizedChangedFile>(),
                    pullRequest.Number,
                    isHead
                        ? pullRequest.Reviews.Select(review => new NormalizedReview(
                            review.Reviewer,
                            review.State,
                            review.SubmittedAtUtc)).ToArray()
                        : Array.Empty<NormalizedReview>(),
                    null,
                    pullRequest.Merged);
                await developmentGraph.IngestVersionControlChangeAsync(normalized, cancellationToken);
            }

            var key = $"pr:{repository.FullName}:{pullRequest.Number}";
            var node = await developmentGraph.FindNodeAsync(
                projectId,
                DevelopmentGraphNodeType.PullRequest,
                key,
                cancellationToken);
            if (node is not null)
            {
                var state = pullRequest.Merged
                    ? "Merged"
                    : pullRequest.State.Equals("open", StringComparison.OrdinalIgnoreCase) ? "Open" : pullRequest.State;
                await developmentGraph.UpsertNodeAsync(new(
                    projectId,
                    DevelopmentGraphNodeType.PullRequest,
                    node.Key,
                    $"PR #{pullRequest.Number}: {pullRequest.Title}",
                    state,
                    "GitHub",
                    pullRequest.HtmlUrl,
                    node.MetadataJson), cancellationToken);
            }
        }
    }

    private static PullRequestDetailsResponse? BuildPullRequestDetails(
        DevelopmentGraphDto graph,
        DevelopmentGraphNodeDto pullRequest)
    {
        var number = PullRequestNumber(pullRequest);
        if (!number.HasValue)
            return null;

        var byId = graph.Nodes.ToDictionary(node => node.Id);
        var commitIds = graph.Edges
            .Where(edge =>
                edge.SourceNodeId == pullRequest.Id
                && edge.Type.Equals(DevelopmentGraphEdgeType.Contains.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(edge => edge.TargetNodeId)
            .Where(id => byId.TryGetValue(id, out var node)
                         && node.Type.Equals(DevelopmentGraphNodeType.Commit.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToHashSet();

        var revisionNodes = commitIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToArray();
        var revisions = revisionNodes
            .Select(node => MetaString(node.MetadataJson, "Revision") ?? StripKeyTail(node.Key))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fileIds = graph.Edges
            .Where(edge =>
                commitIds.Contains(edge.SourceNodeId)
                && edge.Type.Equals(DevelopmentGraphEdgeType.Changes.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(edge => edge.TargetNodeId)
            .ToHashSet();
        var changedFiles = fileIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Where(node => node.Type.Equals(DevelopmentGraphNodeType.File.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(node => node.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var reviewEdges = graph.Edges
            .Where(edge =>
                edge.TargetNodeId == pullRequest.Id
                && edge.Type.Equals(DevelopmentGraphEdgeType.Reviews.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var reviewNodes = reviewEdges
            .Select(edge => edge.SourceNodeId)
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Where(node => node.Type.Equals(DevelopmentGraphNodeType.Review.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var assessment = reviewNodes.FirstOrDefault(node =>
            node.Key.StartsWith("review-assessment:", StringComparison.OrdinalIgnoreCase));
        var reviews = reviewNodes
            .Where(node => !node.Key.StartsWith("review-assessment:", StringComparison.OrdinalIgnoreCase))
            .Select(node => new PullRequestReviewResponse(
                node.Name,
                node.Status,
                node.ExternalReference))
            .OrderBy(x => x.Reviewer, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var ciIds = graph.Edges
            .Where(edge =>
                commitIds.Contains(edge.SourceNodeId)
                && edge.Type.Equals(DevelopmentGraphEdgeType.BuiltBy.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(edge => edge.TargetNodeId)
            .ToHashSet();
        var ciRuns = ciIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Where(node => node.Type.Equals(DevelopmentGraphNodeType.CiRun.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(node => new PullRequestCiResponse(
                node.Name,
                node.Status,
                node.ExternalReference))
            .OrderByDescending(x => x.Status.Equals("failure", StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var critical = reviewEdges.Any(edge => edge.Critical)
                       || ciRuns.Any(run => IsFailure(run.Status))
                       || assessment?.Status is "Blocked" or "HighRisk";
        var title = PullRequestTitle(pullRequest.Name, number.Value);
        var summary = new PullRequestSummaryResponse(
            number.Value,
            title,
            pullRequest.Status,
            pullRequest.ExternalReference,
            changedFiles.Length,
            reviews.Length,
            ciRuns.Length,
            critical,
            assessment?.Status);

        return new(
            summary,
            revisions,
            changedFiles,
            reviews,
            ciRuns,
            Array.Empty<PullRequestPipelineResponse>(),
            Array.Empty<PipelineQualityIssueResponse>(),
            Array.Empty<PipelineEvidenceResponse>());
    }

    private async Task<PullRequestDetailsResponse> EnrichPullRequestDetailsAsync(
        Guid projectId,
        PullRequestDetailsResponse details,
        CancellationToken cancellationToken)
    {
        if (details.Revisions.Count == 0)
            return details;

        var runs = await pipelineRuns.ListByCommitShasAsync(projectId, details.Revisions, cancellationToken);
        var runIds = runs.Select(run => run.Id).ToArray();
        var issues = await qualityIssues.ListByPipelineRunsAsync(projectId, runIds, cancellationToken);
        var evidenceRecords = await evidence.ListByPipelineRunsAsync(projectId, runIds, cancellationToken);

        var issueDtos = issues
            .GroupBy(issue => issue.Id)
            .Select(group => group.First())
            .OrderByDescending(issue => issue.Severity)
            .ThenByDescending(issue => issue.LastSeenAtUtc)
            .Select(issue => new PipelineQualityIssueResponse(
                issue.Id,
                issue.Severity.ToString(),
                issue.Status.ToString(),
                issue.Title,
                issue.Category,
                issue.Fingerprint,
                issue.EvidenceJson))
            .ToArray();
        var evidenceDtos = evidenceRecords
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderByDescending(item => item.ObservedAtUtc)
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
        var pipelineDtos = runs
            .Select(run => new PullRequestPipelineResponse(
                run.Id,
                run.CommitSha.Value,
                run.Status.ToString(),
                run.CollectorType,
                run.ExternalRunId,
                run.FinishedAtUtc,
                issues.Count(issue => issue.PipelineRunId == run.Id
                                      || issue.FirstSeenPipelineRunId == run.Id
                                      || issue.LastSeenPipelineRunId == run.Id),
                evidenceRecords.Count(item => item.PipelineRunId == run.Id)))
            .ToArray();

        return details with
        {
            PipelineRuns = pipelineDtos,
            QualityIssues = issueDtos,
            Evidence = evidenceDtos
        };
    }

    private async Task<GitHubConnectionTestResponse> ProbeRepositoryAsync(
        GitHubConnectionStatusDto status,
        int repositoryCount,
        string owner,
        string repository,
        CancellationToken cancellationToken)
    {
        var details = await github.GetRepositoryAsync(owner, repository, cancellationToken);
        var capabilities = details.Capabilities;
        if (capabilities is null)
        {
            return new GitHubConnectionTestResponse(
                false,
                status.Mode,
                "Authentication and repository access passed, but capability diagnostics are unavailable.",
                repositoryCount,
                DateTimeOffset.UtcNow,
                "Partial",
                details.Repository.FullName,
                new[]
                {
                    new GitHubCapabilityCheckResponse("Authentication", "PASS", "GitHub credentials are accepted."),
                    new GitHubCapabilityCheckResponse("Repository", "PASS", "The selected repository is accessible."),
                    new GitHubCapabilityCheckResponse("Capabilities", "UNKNOWN", "Detailed capability probe was not returned.")
                });
        }

        var checks = new[]
        {
            new GitHubCapabilityCheckResponse("Authentication", "PASS", "GitHub credentials are accepted."),
            new GitHubCapabilityCheckResponse("Metadata", capabilities.Metadata ? "PASS" : "FAIL", capabilities.Metadata ? "Repository metadata is readable." : "Repository metadata permission is unavailable."),
            new GitHubCapabilityCheckResponse("Contents", capabilities.Contents ? "PASS" : "FAIL", capabilities.Contents ? "Default-branch commit/content access is available." : "Contents: Read is required."),
            new GitHubCapabilityCheckResponse("Pull Requests", capabilities.PullRequests ? "PASS" : "FAIL", capabilities.PullRequests ? "Pull-request access is available." : "Pull requests: Read is required."),
            new GitHubCapabilityCheckResponse("Actions", capabilities.Actions ? "PASS" : "FAIL", capabilities.Actions ? "Actions workflow-run access is available." : "Actions: Read is required.")
        };
        var success = checks.All(check => check.Status == "PASS");
        return new GitHubConnectionTestResponse(
            success,
            status.Mode,
            success ? "All required GitHub capabilities are available for the selected repository." : "GitHub is connected, but one or more required repository capabilities are unavailable.",
            repositoryCount,
            DateTimeOffset.UtcNow,
            success ? "Healthy" : "Partial",
            details.Repository.FullName,
            checks);
    }

    private static GitHubConnectionTestResponse FailedConnectionTest(
        GitHubConnectionStatusDto status,
        string message,
        int repositoryCount,
        string? repository = null)
        => new(
            false,
            status.Mode,
            message,
            repositoryCount,
            DateTimeOffset.UtcNow,
            "Failed",
            repository,
            new[] { new GitHubCapabilityCheckResponse("Authentication", "FAIL", message) });

    private static bool IsFailure(string value)
        => value.Equals("failure", StringComparison.OrdinalIgnoreCase)
           || value.Equals("failed", StringComparison.OrdinalIgnoreCase)
           || value.Equals("error", StringComparison.OrdinalIgnoreCase)
           || value.Equals("cancelled", StringComparison.OrdinalIgnoreCase);

    private static int? PullRequestNumber(DevelopmentGraphNodeDto node)
    {
        var metadata = MetaInt(node.MetadataJson, "PullRequestNumber");
        if (metadata.HasValue)
            return metadata;
        var tail = StripKeyTail(node.Key);
        return int.TryParse(tail, out var number) ? number : null;
    }

    private static string PullRequestTitle(string value, int number)
    {
        var prefix = $"PR #{number}:";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return value[prefix.Length..].Trim();
        return value;
    }

    private static int? MetaInt(string json, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.TryGetProperty(property, out var value)
                && value.TryGetInt32(out var result))
            {
                return result;
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }

    private static string? MetaString(string json, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.TryGetProperty(property, out var value))
                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        }
        catch (JsonException)
        {
        }
        return null;
    }

    private static string StripKeyTail(string key)
    {
        var index = key.LastIndexOf(':');
        return index >= 0 && index + 1 < key.Length ? key[(index + 1)..] : key;
    }

    private static bool TryParseRepository(
        string value,
        out string owner,
        out string repository)
    {
        owner = string.Empty;
        repository = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var text = value.Trim().TrimEnd('/');
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            if (!uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                return false;
            text = uri.AbsolutePath.Trim('/');
        }

        var parts = text.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;
        owner = parts[0];
        repository = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? parts[1][..^4]
            : parts[1];
        return owner.Length > 0 && repository.Length > 0;
    }

    private static bool SameRepository(string left, string right)
    {
        static string Normalize(string value)
        {
            var normalized = value.Trim().TrimEnd('/');
            return normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
                ? normalized[..^4]
                : normalized;
        }
        return Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string ShortSha(string value) =>
        value.Length <= 8 ? value : value[..8];

    private static string SafeMessage(string value)
    {
        var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length <= 300 ? normalized : normalized[..300];
    }
}
