// Purpose: Exposes HTTP endpoints for this API responsibility.
using Dip.Api.Authorization;
using Dip.Application.Features.Hardware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;
[ApiController]
[Route("api/v1/hardware-backtests")]
public sealed class HardwareBacktestsController(HardwareBacktestingService service) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)][HttpPost] public Task<HardwareBacktestDto> Record([FromBody] RecordHardwareBacktestCommand command, CancellationToken ct) => service.RecordAsync(command, ct);
    [Authorize(Policy = Policies.ProjectRead)][HttpGet("projects/{projectId:guid}")] public Task<IReadOnlyList<HardwareBacktestDto>> List(Guid projectId, [FromQuery] Guid? hardwareProfileId, [FromQuery] int take = 200, CancellationToken ct = default) => service.ListAsync(projectId, hardwareProfileId, take, ct);
    [Authorize(Policy = Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/summary")] public Task<HardwareBacktestSummary> Summary(Guid projectId, [FromQuery] Guid? hardwareProfileId, [FromQuery] int take = 1000, CancellationToken ct = default) => service.SummarizeAsync(projectId, hardwareProfileId, take, ct);
}
