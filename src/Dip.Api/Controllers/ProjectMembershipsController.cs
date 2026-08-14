using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

public sealed record UpsertProjectMembershipRequest(string SubjectId,string Login,ProjectMembershipRole Role,string Reason);
public sealed record RemoveProjectMembershipRequest(string Reason);

[ApiController]
[Route("api/v1/projects/{projectId:guid}/memberships")]
[Authorize(Policy=Policies.ProjectWrite)]
[RequireProjectRole(ProjectMembershipRole.Owner)]
public sealed class ProjectMembershipsController(ProjectAccessService access):ControllerBase
{
    [HttpGet] public Task<IReadOnlyList<ProjectMembershipDto>> List(Guid projectId,CancellationToken ct)=>access.ListAsync(projectId,ct);
    [HttpPut("{subjectId}")] public async Task<IActionResult> Upsert(Guid projectId,string subjectId,[FromBody]UpsertProjectMembershipRequest request,CancellationToken ct){if(!string.Equals(subjectId,request.SubjectId,StringComparison.Ordinal))return BadRequest(new{error="route/body subject mismatch"});await access.UpsertAsync(projectId,request.SubjectId,request.Login,request.Role,RequestActor.AuditIdentity(User),request.Reason,ct);return NoContent();}
    [HttpDelete("{subjectId}")] public async Task<IActionResult> Remove(Guid projectId,string subjectId,[FromBody]RemoveProjectMembershipRequest request,CancellationToken ct){await access.RemoveAsync(projectId,subjectId,RequestActor.AuditIdentity(User),request.Reason,ct);return NoContent();}
}
