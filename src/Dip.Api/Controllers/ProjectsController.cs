using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Projects;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(ProjectService service,ProjectAccessService access,IConfiguration configuration) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet]
    public async Task<IReadOnlyList<ProjectDto>> List(CancellationToken cancellationToken)
    {
        var all=await service.ListAsync(cancellationToken);if(!(configuration.GetValue<bool?>("Authentication:RequireAuthenticatedUser")??false)||User.IsInRole(Roles.Admin))return all;
        var allowed=await access.AccessibleProjectsAsync(RequestActor.Subject(User),cancellationToken);return all.Where(x=>allowed.Contains(x.Id)).ToArray();
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer,"id")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Get(Guid id,CancellationToken cancellationToken){var project=await service.GetAsync(id,cancellationToken);return project is null?NotFound():Ok(project);}

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectCommand command,CancellationToken cancellationToken)
    {
        var authenticated=User.Identity?.IsAuthenticated==true;var subject=authenticated?RequestActor.Subject(User):"dev-local";var login=authenticated?RequestActor.Login(User):"dev-local";var actor=authenticated?RequestActor.AuditIdentity(User):"system:dev-local";
        var project=await service.CreateAsync(command,subject,login,actor,cancellationToken);return CreatedAtAction(nameof(Get),new{id=project.Id},project);
    }
}
