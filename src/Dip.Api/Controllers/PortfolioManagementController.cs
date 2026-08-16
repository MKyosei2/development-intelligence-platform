using Dip.Api.Authorization;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Management;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/management/portfolio")]
public sealed class PortfolioManagementController(PortfolioManagementService portfolio,IProjectRepository projects,ProjectAccessService access,IConfiguration configuration):ControllerBase
{
    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet]
    public async Task<PortfolioManagementDto> Get([FromQuery]int horizonWeeks=8,CancellationToken ct=default)
    {
        IReadOnlySet<Guid>? allowed=null;
        if((configuration.GetValue<bool?>("Authentication:RequireAuthenticatedUser")??false)&&!User.IsInRole(Roles.Admin))
            allowed=(await access.AccessibleProjectsAsync(RequestActor.Subject(User),ct)).ToHashSet();
        else
            allowed=(await projects.ListAsync(ct)).Select(x=>x.Id).ToHashSet();
        return await portfolio.GetAsync(allowed,horizonWeeks,ct);
    }
}
