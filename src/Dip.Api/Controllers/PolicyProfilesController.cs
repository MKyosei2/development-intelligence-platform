// Purpose: Exposes P ol ic yP ro fi le s HTTP endpoints for c on tr ol le rs.
using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Policies;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class PolicyProfilesController(
    PolicyProfileService profiles,
    PolicyResolver resolver,
    IConfiguration configuration) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpGet("admin/policies/global")]
    public async Task<ActionResult<PolicyProfileDto?>> GetGlobal(
        CancellationToken ct)
    {
        if (AdminDenied()) return Forbid();
        return Ok(await profiles.GetGlobalAsync(ct));
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPut("admin/policies/global")]
    public async Task<ActionResult<PolicyProfileDto>> PutGlobal(
        PolicyProfileDocument document,
        CancellationToken ct)
    {
        if (AdminDenied()) return Forbid();
        return Ok(await profiles.UpsertGlobalAsync(document, ct));
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/policy")]
    public async Task<ActionResult<PolicyProfileDto?>> GetProject(
        Guid projectId,
        CancellationToken ct)
        => Ok(await profiles.GetProjectAsync(projectId, ct));

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer, "projectId")]
    [HttpPut("projects/{projectId:guid}/policy")]
    public async Task<ActionResult<PolicyProfileDto>> PutProject(
        Guid projectId,
        PolicyProfileDocument document,
        CancellationToken ct)
        => Ok(await profiles.UpsertProjectAsync(projectId, document, ct));

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/policy/effective")]
    public async Task<ActionResult<EffectivePolicy>> GetEffective(
        Guid projectId,
        CancellationToken ct)
        => Ok(await resolver.ResolveAsync(projectId, agentId: null, ct));

    private bool AdminDenied()
    {
        var secured =
            configuration.GetValue<bool?>("Authentication:RequireAuthenticatedUser") ?? false;
        return secured && !User.IsInRole(Roles.Admin);
    }
}
