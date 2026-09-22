// Purpose: Exposes GitHub repository discovery, project import, and project-repository binding endpoints.
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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/integrations/github")]
[Produces("application/json")]
public sealed class GitHubRepositoryController(IGitHubRepositoryCatalog github, ProjectService projects, ProjectAccessService access, QaOrchestrationService qa, ProjectRealtimePublisher realtime, IConfiguration configuration, DevelopmentGraphService developmentGraph) : ControllerBase
{
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
            await new GitHubPullRequestBackfillService(developmentGraph).BackfillAsync(created.Id, details.Repository, pullRequests, cancellationToken);
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
