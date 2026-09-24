// Purpose: Exposes P ro je ct Me mb er sh ip s HTTP endpoints for c on tr ol le rs.
using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

public sealed record UpsertProjectMembershipRequest(
    string SubjectId,
    string Login,
    ProjectMembershipRole Role,
    string Reason);

public sealed record RemoveProjectMembershipRequest(string Reason);

public sealed record TransferProjectOwnershipRequest(
    string NewOwnerSubjectId,
    string NewOwnerLogin,
    string Reason);

[ApiController]
[Route("api/v1/projects/{projectId:guid}/memberships")]
[Authorize(Policy = Policies.ProjectRead)]
public sealed class ProjectMembershipsController(ProjectAccessService access) : ControllerBase
{
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet]
    public Task<IReadOnlyList<ProjectMembershipDto>> List(
        Guid projectId,
        CancellationToken ct)
        => access.ListAsync(projectId, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner, "projectId")]
    [HttpPut("{subjectId}")]
    public async Task<IActionResult> Upsert(
        Guid projectId,
        string subjectId,
        [FromBody] UpsertProjectMembershipRequest request,
        CancellationToken ct)
    {
        if (!string.Equals(subjectId, request.SubjectId, StringComparison.Ordinal))
            return BadRequest(new { error = "route/body subject mismatch" });

        try
        {
            await access.UpsertAsync(
                projectId,
                request.SubjectId,
                request.Login,
                request.Role,
                RequestActor.AuditIdentity(User),
                request.Reason,
                ct);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = "owner_required", message = exception.Message });
        }
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner, "projectId")]
    [HttpDelete("{subjectId}")]
    public async Task<IActionResult> Remove(
        Guid projectId,
        string subjectId,
        [FromBody] RemoveProjectMembershipRequest request,
        CancellationToken ct)
    {
        try
        {
            await access.RemoveAsync(
                projectId,
                subjectId,
                RequestActor.AuditIdentity(User),
                request.Reason,
                ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = "owner_required", message = exception.Message });
        }
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner, "projectId")]
    [HttpPost("transfer-owner")]
    public async Task<IActionResult> TransferOwner(
        Guid projectId,
        [FromBody] TransferProjectOwnershipRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewOwnerSubjectId)
            || string.IsNullOrWhiteSpace(request.NewOwnerLogin))
        {
            return BadRequest(new
            {
                error = "new_owner_required",
                message = "New owner subject id and login are required."
            });
        }

        await access.TransferOwnershipAsync(
            projectId,
            RequestActor.Subject(User),
            request.NewOwnerSubjectId,
            request.NewOwnerLogin,
            RequestActor.AuditIdentity(User),
            request.Reason,
            ct);
        return NoContent();
    }
}
