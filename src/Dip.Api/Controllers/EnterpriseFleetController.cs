// Purpose: Exposes E nt er pr is eF le et HTTP endpoints for c on tr ol le rs.
using Dip.Api.Authorization;
using Dip.Application.Features.Enterprise;
using Dip.Application.Features.Fleet;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;

public sealed record CreateOrganizationRequest(string Name, string Slug);
public sealed record BindProjectTenantRequest(Guid ProjectId, string? PortfolioKey);
public sealed record CreateEnrollmentRequest(Guid ProjectId, int ExpiresMinutes = 60);
public sealed record EnrollAgentRequest(string EnrollmentCode, string AgentId, string MachineId, string PublicKeyPem, string Version, int PolicyVersion);
public sealed record HeartbeatRequest(string AgentId, string Version, int PolicyVersion, string? HealthJson);
public sealed record CreateAgentReleaseRequest(string Version, AgentReleaseChannel Channel, string PackageUri, string Sha256, string Signature, string MinimumServerVersion, int RolloutPercent = 10);

[ApiController]
[Route("api/v1/enterprise")]
public sealed class EnterpriseFleetController(EnterpriseTenantService tenants, AgentFleetService fleet) : ControllerBase
{
    [Authorize(Policy = Policies.Admin)][HttpGet("organizations")] public Task<IReadOnlyList<Dip.Domain.Entities.OrganizationTenant>> Organizations(CancellationToken ct) => tenants.ListOrganizationsAsync(ct);
    [Authorize(Policy = Policies.Admin)][HttpPost("organizations")] public async Task<IActionResult> CreateOrganization(CreateOrganizationRequest r, CancellationToken ct) => Ok(await tenants.CreateOrganizationAsync(r.Name, r.Slug, ct));
    [Authorize(Policy = Policies.Admin)][HttpPost("organizations/{organizationId:guid}/projects")] public async Task<IActionResult> Bind(Guid organizationId, BindProjectTenantRequest r, CancellationToken ct) { await tenants.BindProjectAsync(organizationId, r.ProjectId, r.PortfolioKey, ct); return NoContent(); }
    [Authorize(Policy = Policies.Admin)][HttpPost("organizations/{organizationId:guid}/enrollments")] public Task<CreateEnrollmentResult> Enrollment(Guid organizationId, CreateEnrollmentRequest r, CancellationToken ct) => fleet.CreateEnrollmentAsync(organizationId, r.ProjectId, TimeSpan.FromMinutes(Math.Clamp(r.ExpiresMinutes, 5, 10080)), ct);
    [AllowAnonymous][HttpPost("agents/enroll")] public async Task<IActionResult> Enroll(EnrollAgentRequest r, CancellationToken ct) => Ok(await fleet.EnrollAsync(new(r.EnrollmentCode, r.AgentId, r.MachineId, r.PublicKeyPem, r.Version, r.PolicyVersion), ct));
    [Authorize(Policy = Policies.ProjectWrite)][HttpPost("agents/heartbeat")] public async Task<IActionResult> Heartbeat(HeartbeatRequest r, CancellationToken ct) { await fleet.HeartbeatAsync(new(r.AgentId, r.Version, r.PolicyVersion, r.HealthJson), ct); return NoContent(); }
    [Authorize(Policy = Policies.Admin)][HttpGet("organizations/{organizationId:guid}/agents")] public Task<IReadOnlyList<Dip.Domain.Entities.AgentRegistration>> Agents(Guid organizationId, CancellationToken ct) => fleet.ListAsync(organizationId, ct);
    [Authorize(Policy = Policies.Admin)][HttpPost("organizations/{organizationId:guid}/releases")] public async Task<IActionResult> Release(Guid organizationId, CreateAgentReleaseRequest r, CancellationToken ct) => Ok(await fleet.CreateReleaseAsync(organizationId, r.Version, r.Channel, r.PackageUri, r.Sha256, r.Signature, r.MinimumServerVersion, r.RolloutPercent, ct));
    [Authorize(Policy = Policies.Admin)][HttpPost("releases/{releaseId:guid}/activate/{percent:int}")] public async Task<IActionResult> Activate(Guid releaseId, int percent, CancellationToken ct) { await fleet.ActivateReleaseAsync(releaseId, percent, ct); return NoContent(); }
    [Authorize(Policy = Policies.ProjectRead)][HttpGet("agents/{agentId}/updates")] public Task<AgentUpdateDecision> Update(string agentId, [FromQuery] AgentReleaseChannel channel, CancellationToken ct) => fleet.GetUpdateAsync(agentId, channel, ct);
}
