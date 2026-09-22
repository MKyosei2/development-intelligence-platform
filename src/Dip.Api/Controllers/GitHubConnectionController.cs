// Purpose: Exposes GitHub authentication and repository-capability diagnostics.
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
public sealed class GitHubConnectionController(IGitHubRepositoryCatalog github, ProjectService projects) : ControllerBase
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
