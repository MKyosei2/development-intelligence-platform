using Dip.Api.Authorization;
using Dip.Application.Features.Hardware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/hardware")]
[Produces("application/json")]
public sealed class HardwareController(HardwareOrchestrationService service):ControllerBase
{
    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("projects/{projectId:guid}/calibrate")]
    public Task<HardwareCalibrationMeasurement> Calibrate(Guid projectId,CancellationToken ct)=>service.CalibrateAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("profiles/relative")]
    public Task<HardwareProfileDto> CreateProfile([FromBody]CreateRelativeHardwareProfileCommand command,CancellationToken ct)=>service.CreateRelativeProfileAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("profiles")]
    public Task<IReadOnlyList<HardwareProfileDto>> Profiles(CancellationToken ct)=>service.ListProfilesAsync(ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("simulate")]
    public Task<PerformanceSimulationDto> Simulate([FromBody]PerformanceSimulationCommand command,CancellationToken ct)=>service.SimulateAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("estimate-minimum")]
    public Task<MinimumHardwareEstimateDto> EstimateMinimum([FromBody]MinimumHardwareEstimateCommand command,CancellationToken ct)=>service.EstimateMinimumAsync(command,ct);
}
