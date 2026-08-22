using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Projects;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(
    ProjectService service,
    ProjectAccessService access,
    IConfiguration configuration) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet]
    public async Task<IReadOnlyList<ProjectDto>> List(CancellationToken cancellationToken)
    {
        var all = await service.ListAsync(cancellationToken);
        if (!(configuration.GetValue<bool?>("Authentication:RequireAuthenticatedUser") ?? false)
            || User.IsInRole(Roles.Admin))
        {
            return all;
        }

        var allowed = await access.AccessibleProjectsAsync(
            RequestActor.Subject(User),
            cancellationToken);
        return all.Where(x => allowed.Contains(x.Id)).ToArray();
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "id")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var project = await service.GetAsync(id, cancellationToken);
        return project is null ? NotFound() : Ok(project);
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(
        CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var authenticated = User.Identity?.IsAuthenticated == true;
        var subject = authenticated ? RequestActor.Subject(User) : "dev-local";
        var login = authenticated ? RequestActor.Login(User) : "dev-local";
        var actor = authenticated ? RequestActor.AuditIdentity(User) : "system:dev-local";
        var project = await service.CreateAsync(
            command,
            subject,
            login,
            actor,
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, project);
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer, "id")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(
        Guid id,
        UpdateProjectCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.UpdateAsync(
                id,
                command,
                RequestActor.AuditIdentity(User),
                cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = "project_not_active", message = exception.Message });
        }
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner, "id")]
    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<ProjectDto>> Archive(
        Guid id,
        ProjectLifecycleCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.ArchiveAsync(
                id,
                RequestActor.AuditIdentity(User),
                command.Reason,
                cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = "invalid_project_lifecycle", message = exception.Message });
        }
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner, "id")]
    [AllowArchivedProjectMutation]
    [HttpPost("{id:guid}/restore")]
    public async Task<ActionResult<ProjectDto>> Restore(
        Guid id,
        ProjectLifecycleCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.RestoreAsync(
                id,
                RequestActor.AuditIdentity(User),
                command.Reason,
                cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = "invalid_project_lifecycle", message = exception.Message });
        }
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner, "id")]
    [AllowArchivedProjectMutation]
    [HttpPost("{id:guid}/delete")]
    public async Task<ActionResult<ProjectDto>> Delete(
        Guid id,
        ProjectLifecycleCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.DeleteAsync(
                id,
                RequestActor.AuditIdentity(User),
                command.Reason,
                cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "id")]
    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<IReadOnlyList<ProjectAuditEntryDto>>> Audit(
        Guid id,
        [FromQuery] int take = 200,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await service.GetAuditAsync(
                id,
                take,
                search,
                category,
                correlationId,
                fromUtc,
                toUtc,
                cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "id")]
    [HttpGet("{id:guid}/audit-page")]
    public async Task<ActionResult<ProjectAuditPageDto>> AuditPage(
        Guid id,
        [FromQuery] int take = 100,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? correlationId = null,
        [FromQuery] DateTimeOffset? fromUtc = null,
        [FromQuery] DateTimeOffset? toUtc = null,
        [FromQuery] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await service.GetAuditPageAsync(
                id,
                take,
                search,
                category,
                correlationId,
                fromUtc,
                toUtc,
                cursor,
                cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = "invalid_cursor", message = exception.Message });
        }
    }

}
