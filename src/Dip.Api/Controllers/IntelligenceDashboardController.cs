using Dip.Api.Authorization;
using Dip.Application.Features.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;
[ApiController][Route("api/v1/intelligence-dashboard")]
public sealed class IntelligenceDashboardController(IntelligenceDashboardService service):ControllerBase
{
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}")] public Task<IntelligenceDashboardDto> Get(Guid projectId,CancellationToken ct)=>service.GetAsync(projectId,ct);
}
