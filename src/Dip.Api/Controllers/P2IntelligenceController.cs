// Purpose: Exposes P2 In te ll ig en ce Co nt ro ll er HTTP endpoints.
using Dip.Api.Authorization;
using Dip.Application.Features.P2Intelligence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/p2")]
[Produces("application/json")]
public sealed class P2IntelligenceController(
    HardwareCommunityIntelligenceService hardware,
    AdaptiveQaPlanner planner,
    P2TechnicalIntelligenceService technical,
    EvidenceGroundedRootCauseService rootCause,
    ProjectDecisionIntelligenceService decisions) : ControllerBase
{
    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("community-hardware")]
    public Task<HardwareProfileConfidenceDto> SubmitCommunityHardware(
        [FromBody]CommunityHardwareSubmission command,CancellationToken ct)=>hardware.SubmitAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("community-hardware/confidence")]
    public Task<HardwareProfileConfidenceDto> CommunityHardwareConfidence(
        [FromQuery]string profileKey,[FromQuery]string platform,CancellationToken ct)=>hardware.GetConfidenceAsync(profileKey,platform,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("community-hardware/profile")]
    public Task<Dip.Application.Features.Hardware.HardwareProfileDto> CreateCommunityProfile(
        [FromBody]CreateCommunityHardwareProfileCommand command,CancellationToken ct)=>hardware.CreateCommunityProfileAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("portability")]
    public Task<PortabilityResult> Portability([FromBody]PortabilityCommand command,CancellationToken ct)=>hardware.AnalyzePortabilityAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("adaptive-qa-plan")]
    public Task<AdaptiveQaPlanDto> AdaptiveQaPlan([FromBody]AdaptiveQaPlanCommand command,CancellationToken ct)=>planner.PlanAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("reliability/flaky-test")]
    public Task<FlakyResult> Flaky([FromBody]RepeatedRunCommand command,CancellationToken ct)=>technical.RunFlakyAsync(command,false,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("reliability/runtime")]
    public Task<FlakyResult> RuntimeReliability([FromBody]RepeatedRunCommand command,CancellationToken ct)=>technical.RunFlakyAsync(command,true,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("reliability/soak")]
    public Task<SoakResult> Soak([FromBody]SoakCommand command,CancellationToken ct)=>technical.RunSoakAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("regression/bisect")]
    public Task<GitBisectResult> Bisect([FromBody]GitBisectCommand command,CancellationToken ct)=>technical.FindRegressionCommitAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("regression/origin")]
    public Task<RegressionOriginResult> RegressionOrigin([FromBody]RegressionOriginCommand command,CancellationToken ct)=>
        technical.DetectRegressionOriginAsync(command.ProjectId,command.SubjectKey,command.Points,command.LowerIsBetter,command.ThresholdPercent,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("mutation")]
    public Task<MutationResult> Mutation([FromBody]MutationCommand command,CancellationToken ct)=>technical.RunMutationAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("fuzz")]
    public Task<FuzzResult> Fuzz([FromBody]FuzzCommand command,CancellationToken ct)=>technical.RunFuzzAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("test-impact")]
    public Task<TestImpactResult> TestImpact([FromBody]TestImpactCommand command,CancellationToken ct)=>
        technical.AnalyzeTestImpactAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("debug/symbolicate")]
    public Task<NativeSymbolicationResult> Symbolicate([FromBody]NativeSymbolicationCommand command,CancellationToken ct)=>
        technical.SymbolicateAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("game/regression")]
    public Task<GameRegressionResult> GameRegression([FromBody]GameRegressionCommand command,CancellationToken ct)=>
        technical.AnalyzeGameRegressionAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("media/regression")]
    public Task<MediaRegressionResult> Media([FromBody]MediaRegressionCommand command,CancellationToken ct)=>technical.AnalyzeMediaAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("root-cause")]
    public Task<RootCauseResult> RootCause([FromBody]RootCauseCommand command,CancellationToken ct)=>rootCause.AnalyzeAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/similar-incidents")]
    public Task<IReadOnlyList<SimilarIncidentDto>> SimilarIncidents(
        Guid projectId,[FromQuery]string subjectKey,[FromQuery]string query,[FromQuery]int take,CancellationToken ct)=>
        rootCause.FindSimilarIncidentsAsync(projectId,subjectKey,query,take<=0?10:take,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/freshness")]
    public Task<IReadOnlyList<EvidenceFreshnessItem>> Freshness(Guid projectId,CancellationToken ct)=>decisions.GetFreshnessAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/conflicts")]
    public Task<IReadOnlyList<EvidenceConflictItem>> Conflicts(Guid projectId,CancellationToken ct)=>decisions.GetConflictsAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/schedule-risk")]
    public Task<ScheduleRiskDto> ScheduleRisk(Guid projectId,CancellationToken ct)=>decisions.GetScheduleRiskAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/completion-forecast")]
    public Task<CompletionForecastDto> CompletionForecast(Guid projectId,CancellationToken ct)=>decisions.GetCompletionForecastAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/stagnation")]
    public Task<IReadOnlyList<StagnationItem>> Stagnation(Guid projectId,CancellationToken ct)=>decisions.GetStagnationAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("dependency-risk")]
    public Task<IReadOnlyList<DependencyRiskItem>> DependencyRisk([FromBody]DependencyRiskCommand command,CancellationToken ct)=>decisions.AnalyzeDependenciesAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/daily-change")]
    public Task<DailyChangeDto> DailyChange(Guid projectId,CancellationToken ct)=>decisions.GetDailyChangeAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/health-change")]
    public Task<HealthChangeDto> HealthChange(Guid projectId,CancellationToken ct)=>decisions.GetHealthChangeAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/historical-risk")]
    public Task<IReadOnlyList<P2IntelligenceDto>> HistoricalRisk(Guid projectId,CancellationToken ct)=>decisions.GetHistoricalRiskAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("decision-brief")]
    public Task<DecisionBriefDto> DecisionBrief([FromBody]DecisionBriefCommand command,CancellationToken ct)
    {
        DependencyRiskCommand? dependencies=command.Dependencies is null?null:new(command.ProjectId,command.Dependencies);
        return decisions.GetDecisionBriefAsync(command.ProjectId,dependencies,ct);
    }

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/decision-traces")]
    public Task<IReadOnlyList<Dip.Domain.Entities.DecisionTrace>> DecisionTraces(Guid projectId,[FromQuery]int take,CancellationToken ct)=>
        decisions.GetDecisionTracesAsync(projectId,take<=0?20:take,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/intelligence")]
    public Task<IReadOnlyList<P2IntelligenceDto>> Intelligence(Guid projectId,[FromQuery]int take,CancellationToken ct)=>
        technical.ListAsync(projectId,take<=0?50:take,ct);
}
