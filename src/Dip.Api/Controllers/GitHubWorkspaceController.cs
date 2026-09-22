// Purpose: Exposes project GitHub workspace synchronization while reusing the shared pull-request backfill boundary.
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
public sealed class GitHubWorkspaceController(IGitHubRepositoryCatalog github, ProjectService projects, ProjectRealtimePublisher realtime, GitHubPullRequestBackfillService backfill) : ControllerBase
{
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
                    await backfill.BackfillAsync(projectId, details.Repository, pullRequests, cancellationToken);
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

    private static string SafeMessage(string value)
        {
            var normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            return normalized.Length <= 300 ? normalized : normalized[..300];
        }
}
