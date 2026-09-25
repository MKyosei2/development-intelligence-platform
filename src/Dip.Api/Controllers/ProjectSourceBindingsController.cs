// Purpose: Exposes Project Source Bindings HTTP endpoints for the DIP API.
using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Integrations;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

public sealed record CreateProjectSourceBindingRequest(
    Guid ConnectionId,
    string ExternalResourceId,
    string ExternalResourceName,
    string ResourceType,
    string? ConfigurationJson = null);

[ApiController]
[Route("api/v1/projects/{projectId:guid}/sources")]
[Produces("application/json")]
public sealed class ProjectSourceBindingsController(
    IntegrationConnectionService service) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectSourceBindingDto>>> List(
        Guid projectId,
        CancellationToken ct)
        => Ok(await service.ListBindingsAsync(projectId, ct));

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost]
    public async Task<ActionResult<ProjectSourceBindingDto>> Create(
        Guid projectId,
        CreateProjectSourceBindingRequest request,
        CancellationToken ct)
    {
        var created = await service.BindProjectAsync(
            new CreateProjectSourceBindingCommand(
                projectId,
                request.ConnectionId,
                request.ExternalResourceId,
                request.ExternalResourceName,
                request.ResourceType,
                request.ConfigurationJson),
            ct);

        return Created(
            $"/api/v1/projects/{projectId}/sources/{created.Id}",
            created);
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member, "projectId")]
    [HttpPost("{bindingId:guid}/sync")]
    public async Task<ActionResult<ProjectSourceSyncSummary>> Sync(
        Guid projectId,
        Guid bindingId,
        CancellationToken ct)
    {
        var bindings = await service.ListBindingsAsync(projectId, ct);
        if (bindings.All(item => item.Id != bindingId))
            return NotFound();

        return Ok(await service.SyncBindingAsync(bindingId, ct));
    }
}
