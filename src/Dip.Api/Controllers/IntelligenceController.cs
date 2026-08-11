using Dip.Api.Authorization;
using Dip.Application.Features.Intelligence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/intelligence")]
[Produces("application/json")]
public sealed class IntelligenceController : ControllerBase
{
    private readonly P2OrchestrationService _p2;
    private readonly AdaptiveQaPlanner _planner;
    private readonly CommunityHardwareService _community;
    private readonly RootCauseIntelligenceService _root;
    private readonly ForecastAndDecisionService _forecast;
    private readonly P2AnalysisService _analysis;

    public IntelligenceController(
        P2OrchestrationService p2,
        AdaptiveQaPlanner planner,
        CommunityHardwareService community,
        RootCauseIntelligenceService root,
        ForecastAndDecisionService forecast,
        P2AnalysisService analysis)
    {
        _p2 = p2;
        _planner = planner;
        _community = community;
        _root = root;
        _forecast = forecast;
        _analysis = analysis;
    }

    [Authorize(Policy = Policies.ProjectWrite)] [HttpPost("minimum-hardware")]
    public Task<MinimumHardwareResult> Minimum([FromBody] MinimumHardwareRequest request, CancellationToken ct) => _p2.MinimumHardwareAsync(request, ct);
    [Authorize(Policy = Policies.ProjectWrite)] [HttpPost("adaptive-qa")]
    public Task<AdaptiveQaPlan> Plan([FromBody] AdaptiveQaRequest request, CancellationToken ct) => _planner.PlanAsync(request, ct);
    [Authorize(Policy = Policies.ProjectWrite)] [HttpPost("regression-origin")]
    public Task<RegressionResult> Regression([FromBody] RegressionRequest request, CancellationToken ct) => _p2.RegressionAsync(request, ct);
    [Authorize(Policy = Policies.ProjectWrite)] [HttpPost("reliability")]
    public Task<ReliabilityResult> Reliability([FromBody] ReliabilityRequest request, CancellationToken ct) => _p2.ReliabilityAsync(request, ct);
    [Authorize(Policy = Policies.ProjectWrite)] [HttpPost("media/{kind}")]
    public Task<MediaRegressionResult> Media(string kind, [FromBody] VisualRegressionRequest request, CancellationToken ct) => _p2.MediaAsync(request, kind, ct);
    [Authorize(Policy = Policies.ProjectWrite)] [HttpPost("fuzz")]
    public Task<FuzzResult> Fuzz([FromBody] FuzzRequest request, CancellationToken ct) => _p2.FuzzAsync(request, ct);
    [Authorize(Policy = Policies.ProjectWrite)] [HttpPost("mutation")]
    public Task<MutationResult> Mutation([FromBody] MutationRequest request, CancellationToken ct) => _p2.MutationAsync(request, ct);
    [Authorize(Policy = Policies.ProjectRead)] [HttpPost("root-cause")]
    public Task<RootCauseResult> Root([FromBody] RootCauseRequest request, CancellationToken ct) => _root.AnalyzeAsync(request, ct);
    [Authorize(Policy = Policies.ProjectRead)] [HttpGet("projects/{projectId:guid}/forecast")]
    public Task<ForecastResult> Forecast(Guid projectId, CancellationToken ct) => _forecast.ForecastAsync(projectId, ct);
    [Authorize(Policy = Policies.ProjectRead)] [HttpGet("projects/{projectId:guid}/brief")]
    public Task<PmDecisionBrief> Brief(Guid projectId, CancellationToken ct) => _forecast.BriefAsync(projectId, ct);
    [Authorize(Policy = Policies.ProjectRead)] [HttpGet("projects/{projectId:guid}/portability")]
    public Task<PortabilityResult> Portability(
        Guid projectId,
        [FromQuery] string targetPlatform,
        [FromQuery] decimal cpu = 100,
        [FromQuery] decimal gpu = 100,
        [FromQuery] decimal memory = 100,
        [FromQuery] decimal storage = 100,
        [FromQuery] decimal thermal = 100,
        CancellationToken ct = default) =>
        _analysis.PortabilityAsync(projectId, targetPlatform, cpu, gpu, memory, storage, thermal, ct);
    [Authorize(Policy = Policies.ProjectRead)] [HttpPost("projects/{projectId:guid}/governance")]
    public Task<EvidenceGovernanceResult> Governance(Guid projectId, [FromBody] IReadOnlyList<ProjectDependency>? dependencies, CancellationToken ct) =>
        _analysis.GovernanceAsync(projectId, dependencies, ct);
    [Authorize(Policy = Policies.ProjectWrite)] [HttpPost("community-hardware")]
    public async Task<IActionResult> Submit([FromBody] CommunityHardwareSubmission request, CancellationToken ct)
    {
        await _community.SubmitAsync(request, ct);
        return Accepted();
    }
    [AllowAnonymous] [HttpGet("community-hardware/{hardwareKey}")]
    public Task<CommunityHardwareAggregate> Aggregate(string hardwareKey, CancellationToken ct) => _community.AggregateAsync(hardwareKey, ct);
}
