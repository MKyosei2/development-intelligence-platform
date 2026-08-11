using Dip.Api.Authorization;
using Dip.Application.Features.Benchmarks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;
[ApiController]
[Route("api/v1/benchmarks")]
[Produces("application/json")]
public sealed class BenchmarksController(BenchmarkOrchestrationService benchmarks):ControllerBase
{
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("definitions")]
    public async Task<ActionResult<BenchmarkDefinitionDto>> Create([FromBody]CreateBenchmarkDefinitionCommand command,CancellationToken ct)=>Created(string.Empty,await benchmarks.CreateDefinitionAsync(command,ct));
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("definitions")]
    public Task<IReadOnlyList<BenchmarkDefinitionDto>> Definitions(CancellationToken ct)=>benchmarks.ListDefinitionsAsync(ct);
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("runs")]
    public async Task<IActionResult> Run([FromBody]BenchmarkJobRequest request,CancellationToken ct){var job=await benchmarks.EnqueueAsync(request,ct);return Accepted($"/api/v1/jobs/{job.JobId}",job);}
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<BenchmarkRunResultDto>> GetRun(Guid runId,CancellationToken ct){var result=await benchmarks.GetRunAsync(runId,ct);return result is null?NotFound():Ok(result);}
}
