// Purpose: Exposes P 2 Jobs HTTP endpoints for the DIP API.
using Dip.Api.Authorization;
using Dip.Application.Features.Jobs;
using Dip.Application.Features.P2Intelligence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/p2/jobs")]
[Produces("application/json")]
public sealed class P2JobsController(P2JobEnqueueService jobs) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("fuzz")]
    public async Task<IActionResult> Fuzz([FromBody] FuzzCommand command, CancellationToken ct) => AcceptedJob(await jobs.FuzzAsync(command, ct));
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("mutation")]
    public async Task<IActionResult> Mutation([FromBody] MutationCommand command, CancellationToken ct) => AcceptedJob(await jobs.MutationAsync(command, ct));
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("soak")]
    public async Task<IActionResult> Soak([FromBody] SoakCommand command, CancellationToken ct) => AcceptedJob(await jobs.SoakAsync(command, ct));
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("bisect")]
    public async Task<IActionResult> Bisect([FromBody] GitBisectCommand command, CancellationToken ct) => AcceptedJob(await jobs.BisectAsync(command, ct));
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("media")]
    public async Task<IActionResult> Media([FromBody] MediaRegressionCommand command, CancellationToken ct) => AcceptedJob(await jobs.MediaAsync(command, ct));
    private IActionResult AcceptedJob(EnqueuedJobDto job) => Accepted($"/api/v1/jobs/{job.JobId}", job);
}
