// Purpose: Exposes HTTP endpoints for R ep os it or yG ra ph Co nt ro ll er.
using Dip.Api.Authorization;
using Dip.Application.Features.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;
[ApiController][Route("api/v1/repository-graph")]
public sealed class RepositoryGraphController(MultiRepositoryGraphService service):ControllerBase
{
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("nodes")] public Task<RepositoryNodeDto> AddNode([FromBody]CreateRepositoryNodeCommand command,CancellationToken ct)=>service.AddNodeAsync(command,ct);
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("dependencies")] public Task<RepositoryDependencyDto> AddDependency([FromBody]CreateRepositoryDependencyCommand command,CancellationToken ct)=>service.AddDependencyAsync(command,ct);
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}")] public Task<RepositoryGraphDto> Get(Guid projectId,CancellationToken ct)=>service.GetAsync(projectId,ct);
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("impact")] public Task<CrossRepositoryImpactDto> Impact([FromBody]AnalyzeCrossRepositoryImpactCommand command,CancellationToken ct)=>service.AnalyzeImpactAsync(command,ct);
}
