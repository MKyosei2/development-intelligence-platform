using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Domain.Enums;
using Dip.Application.Features.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;
[ApiController][Route("api/v1/work-items")]
public sealed class WorkItemsController(WorkItemService service):ControllerBase
{
    [Authorize(Policy=Policies.ProjectRead)][RequireProjectResource(ProjectMembershipRole.Viewer,ProjectResourceKind.Incident,"incidentId")][HttpGet("incidents/{incidentId:guid}/preview")] public Task<WorkItemPreview> Preview(Guid incidentId,CancellationToken ct)=>service.PreviewAsync(incidentId,ct);
    [Authorize(Policy=Policies.ProjectWrite)][RequireProjectResource(ProjectMembershipRole.Member,ProjectResourceKind.Incident,"incidentId")][HttpPost("incidents/{incidentId:guid}/github")] public Task<WorkItemLinkDto> Create(Guid incidentId,CancellationToken ct)=>service.CreateAsync(incidentId,ct);
}
