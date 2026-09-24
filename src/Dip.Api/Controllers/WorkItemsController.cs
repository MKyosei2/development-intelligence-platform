// Purpose: Exposes W or kI te ms HTTP endpoints for c on tr ol le rs.
using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.WorkItems;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;
[ApiController]
[Route("api/v1/work-items")]
public sealed class WorkItemsController(WorkItemService service) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)][RequireProjectResource(ProjectMembershipRole.Viewer, ProjectResourceKind.Incident, "incidentId")][HttpGet("incidents/{incidentId:guid}/preview")] public Task<WorkItemPreview> Preview(Guid incidentId, CancellationToken ct) => service.PreviewAsync(incidentId, ct);
    [Authorize(Policy = Policies.ProjectRead)][HttpGet("providers")] public IActionResult Providers() => Ok(service.Providers);
    [Authorize(Policy = Policies.ProjectWrite)][RequireProjectResource(ProjectMembershipRole.Member, ProjectResourceKind.Incident, "incidentId")][HttpPost("incidents/{incidentId:guid}/github")] public Task<WorkItemLinkDto> GitHub(Guid incidentId, CancellationToken ct) => service.CreateAsync(incidentId, ExternalWorkItemProvider.GitHub, ct);
    [Authorize(Policy = Policies.ProjectWrite)][RequireProjectResource(ProjectMembershipRole.Member, ProjectResourceKind.Incident, "incidentId")][HttpPost("incidents/{incidentId:guid}/jira")] public Task<WorkItemLinkDto> Jira(Guid incidentId, CancellationToken ct) => service.CreateAsync(incidentId, ExternalWorkItemProvider.Jira, ct);
    [Authorize(Policy = Policies.ProjectWrite)][RequireProjectResource(ProjectMembershipRole.Member, ProjectResourceKind.Incident, "incidentId")][HttpPost("incidents/{incidentId:guid}/linear")] public Task<WorkItemLinkDto> Linear(Guid incidentId, CancellationToken ct) => service.CreateAsync(incidentId, ExternalWorkItemProvider.Linear, ct);
}
