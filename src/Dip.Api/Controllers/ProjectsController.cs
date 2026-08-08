using Dip.Api.Authorization;
using Dip.Application.Features.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly ProjectService _service;

    public ProjectsController(ProjectService service)
    {
        _service = service;
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet]
    public Task<IReadOnlyList<ProjectDto>> List(
        CancellationToken cancellationToken) =>
        _service.ListAsync(cancellationToken);

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var project = await _service.GetAsync(id, cancellationToken);
        return project is null ? NotFound() : Ok(project);
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(
        CreateProjectCommand command,
        CancellationToken cancellationToken)
    {
        var project = await _service.CreateAsync(
            command,
            cancellationToken);
        return CreatedAtAction(
            nameof(Get),
            new { id = project.Id },
            project);
    }
}
