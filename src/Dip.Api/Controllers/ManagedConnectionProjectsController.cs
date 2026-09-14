using System.Text.Json;
using Dip.Api.Authorization;
using Dip.Application.Features.Integrations;
using Dip.Application.Features.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/integrations/connections")]
[Produces("application/json")]
public sealed class ManagedConnectionProjectsController(
    IntegrationConnectionService integrations,
    ProjectService projects) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IntegrationConnectionDto>>> ListAvailable(
        [FromQuery] string? provider,
        CancellationToken ct)
    {
        var items = await integrations.ListAsync(ct);
        if (!string.IsNullOrWhiteSpace(provider))
            items = items
                .Where(item => item.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        return Ok(items.Where(item => item.Enabled).ToArray());
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpGet("{connectionId:guid}/resources")]
    public async Task<ActionResult<IReadOnlyList<ConnectorResourceDto>>> Resources(
        Guid connectionId,
        [FromQuery] string? search,
        [FromQuery] string? visibility,
        [FromQuery] int limit = 500,
        CancellationToken ct = default)
    {
        return Ok(await integrations.DiscoverAsync(
            connectionId,
            new ConnectorDiscoveryQuery(search, visibility, limit),
            ct));
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("{connectionId:guid}/import")]
    public async Task<ActionResult<ImportConnectionProjectResult>> Import(
        Guid connectionId,
        ImportConnectionProjectCommand command,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.ExternalResourceId) ||
            string.IsNullOrWhiteSpace(command.ExternalResourceName))
            return BadRequest(new
            {
                error = "invalid_resource",
                message = "ExternalResourceId and ExternalResourceName are required."
            });

        var resources = await integrations.DiscoverAsync(
            connectionId,
            new ConnectorDiscoveryQuery(
                Search: command.ExternalResourceName,
                Visibility: null,
                Limit: 500),
            ct);

        var resource = resources.FirstOrDefault(item =>
            item.ExternalId.Equals(
                command.ExternalResourceId,
                StringComparison.OrdinalIgnoreCase));

        if (resource is null)
            return NotFound(new
            {
                error = "resource_not_visible",
                message = "The selected resource is no longer visible to this connection."
            });

        if (resource.DipProjectId is { } existingProjectId)
            return Conflict(new
            {
                error = "repository_already_imported",
                projectId = existingProjectId,
                message = "This repository is already registered as a DIP project."
            });

        if (!resource.ResourceType.Equals("repository", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(resource.Url))
            return BadRequest(new
            {
                error = "resource_not_repository",
                message = "Difficulty-1 managed import currently supports repository resources."
            });

        var projectName = string.IsNullOrWhiteSpace(command.ProjectName)
            ? resource.Name
            : command.ProjectName.Trim();
        var defaultBranch = string.IsNullOrWhiteSpace(resource.DefaultBranch)
            ? "main"
            : resource.DefaultBranch;

        var created = await projects.CreateAsync(
            new CreateProjectCommand(
                projectName,
                resource.Url,
                defaultBranch,
                command.ReleaseDateUtc),
            RequestActor.Subject(User),
            RequestActor.Login(User),
            RequestActor.AuditIdentity(User),
            ct);

        var configurationJson = JsonSerializer.Serialize(new
        {
            defaultBranch,
            repositoryUrl = resource.Url
        });

        var binding = await integrations.BindProjectAsync(
            new CreateProjectSourceBindingCommand(
                created.Id,
                connectionId,
                resource.ExternalId,
                resource.FullName,
                resource.ResourceType,
                configurationJson),
            ct);

        var evidenceCreated = 0;
        var evidenceExisting = 0;
        string? warning = null;

        if (command.SyncEvidence)
        {
            try
            {
                var sync = await integrations.SyncBindingAsync(binding.Id, ct);
                evidenceCreated = sync.EvidenceCreated;
                evidenceExisting = sync.EvidenceAlreadyPresent;
                if (sync.Warnings.Count > 0)
                    warning = string.Join(" ", sync.Warnings);
            }
            catch (Exception exception) when (
                exception is HttpRequestException
                or InvalidOperationException
                or IOException)
            {
                warning =
                    "Project and source binding were created, but the first connector evidence sync failed: "
                    + Safe(exception.Message);
            }
        }

        return Ok(new ImportConnectionProjectResult(
            created.Id,
            created.Name,
            resource.Url,
            defaultBranch,
            binding.Id,
            evidenceCreated,
            evidenceExisting,
            warning));
    }

    private static string Safe(string value)
    {
        var normalized = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return normalized[..Math.Min(1000, normalized.Length)];
    }
}
