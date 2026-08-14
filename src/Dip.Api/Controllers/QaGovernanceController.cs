using Dip.Api.Authorization;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/qa-governance")]
[Produces("application/json")]
public sealed class QaGovernanceController(QaGovernanceService service,IQaBugMinimizer minimizer):ControllerBase
{
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/overview")]
    public Task<QaGovernanceOverviewDto> Overview(Guid projectId,CancellationToken ct)=>service.GetOverviewAsync(projectId,ct);
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/findings")]
    public Task<IReadOnlyList<QaFindingState>> Findings(Guid projectId,[FromQuery]int take=500,CancellationToken ct=default)=>service.ListFindingStatesAsync(projectId,take,ct);
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("findings/transition")]
    public async Task<IActionResult> Transition([FromBody]TransitionFindingCommand command,CancellationToken ct){await service.TransitionAsync(command,ct);return NoContent();}

    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/waivers")]
    public Task<IReadOnlyList<QaWaiver>> Waivers(Guid projectId,CancellationToken ct)=>service.ListWaiversAsync(projectId,ct);
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("waivers")]
    public async Task<object> CreateWaiver([FromBody]CreateWaiverCommand command,CancellationToken ct)=>new{id=await service.CreateWaiverAsync(command,ct)};
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("waivers/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeWaiver(Guid id,CancellationToken ct){await service.RevokeWaiverAsync(id,ct);return NoContent();}

    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/reliability")]
    public Task<IReadOnlyList<QaTestReliability>> Reliability(Guid projectId,[FromQuery]int take=500,CancellationToken ct=default)=>service.ListReliabilityAsync(projectId,take,ct);
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/baselines")]
    public Task<IReadOnlyList<QaRegressionBaseline>> Baselines(Guid projectId,[FromQuery]int take=500,CancellationToken ct=default)=>service.ListBaselinesAsync(projectId,take,ct);
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("baselines/rebaseline")]
    public async Task<IActionResult> Rebaseline([FromBody]RebaselineQaCommand command,CancellationToken ct){await service.RebaselineAsync(command,ct);return NoContent();}
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/health")]
    public Task<IReadOnlyList<QaSystemHealthSnapshot>> Health(Guid projectId,[FromQuery]int take=100,CancellationToken ct=default)=>service.ListSystemHealthAsync(projectId,take,ct);
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/trends")]
    public Task<IReadOnlyList<QaQualityTrendSnapshot>> Trends(Guid projectId,[FromQuery]int take=200,CancellationToken ct=default)=>service.ListTrendsAsync(projectId,take,ct);


    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/ownership-rules")]
    public Task<IReadOnlyList<QaOwnershipRule>> OwnershipRules(Guid projectId,CancellationToken ct)=>service.ListOwnershipRulesAsync(projectId,ct);
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/active-policy")]
    public Task<QaPolicy?> ActivePolicy(Guid projectId,CancellationToken ct)=>service.GetActivePolicyAsync(projectId,ct);
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/test-impact-rules")]
    public Task<IReadOnlyList<QaTestImpactRule>> TestImpactRules(Guid projectId,CancellationToken ct)=>service.ListTestImpactRulesAsync(projectId,ct);
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/retention-rules")]
    public Task<IReadOnlyList<QaArtifactRetentionRule>> RetentionRules(Guid projectId,CancellationToken ct)=>service.ListRetentionRulesAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("ownership-rules")]
    public async Task<object> Ownership([FromBody]CreateOwnershipRuleCommand command,CancellationToken ct)=>new{id=await service.AddOwnershipRuleAsync(command,ct)};
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("policies")]
    public async Task<object> Policy([FromBody]CreateQaPolicyCommand command,CancellationToken ct)=>new{id=await service.AddPolicyAsync(command,ct)};
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("test-impact-rules")]
    public async Task<object> TestImpact([FromBody]CreateTestImpactRuleCommand command,CancellationToken ct)=>new{id=await service.AddTestImpactRuleAsync(command,ct)};
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("retention-rules")]
    public async Task<object> Retention([FromBody]CreateRetentionRuleCommand command,CancellationToken ct)=>new{id=await service.AddRetentionRuleAsync(command,ct)};

    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("smart-test-selection")]
    public Task<IReadOnlyList<SmartTestSelectionDto>> SelectTests([FromBody]SmartTestSelectionRequest request,CancellationToken ct)=>service.SelectTestsAsync(request,ct);
    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("projects/{projectId:guid}/test-recommendations")]
    public Task<IReadOnlyList<TestRecommendationDto>> Recommend(Guid projectId,[FromBody]IReadOnlyList<string> changedPaths,CancellationToken ct)=>service.RecommendTestsAsync(projectId,changedPaths,ct);

    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/root-cause-correlation/{fingerprint}")]
    public Task<RootCauseCorrelationDto> Correlate(Guid projectId,string fingerprint,CancellationToken ct)=>service.CorrelateAsync(projectId,fingerprint,ct);
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/freshness")]
    public Task<QaFreshnessDto> Freshness(Guid projectId,[FromQuery]int maxAgeDays=14,CancellationToken ct=default)=>service.GetFreshnessAsync(projectId,maxAgeDays,ct);
    [AllowAnonymous][HttpGet("diagnostic-schema")]
    public DiagnosticSchemaDto Schema()=>QaGovernanceService.GetDiagnosticSchema();
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/cost")]
    public Task<QaCostSummaryDto> Cost(Guid projectId,[FromQuery]int take=200,CancellationToken ct=default)=>service.GetCostSummaryAsync(projectId,take,ct);

    [Authorize(Policy=Policies.ProjectRead)][HttpGet("projects/{projectId:guid}/minimizations")]
    public Task<IReadOnlyList<QaBugMinimizationRecord>> Minimizations(Guid projectId,[FromQuery]int take=100,CancellationToken ct=default)=>service.ListMinimizationsAsync(projectId,take,ct);

    [Authorize(Policy=Policies.ProjectWrite)][HttpPost("minimize")]
    public Task<BugMinimizationDto> Minimize([FromBody]BugMinimizationCommand command,CancellationToken ct)=>minimizer.MinimizeAsync(command,ct);
    [Authorize(Policy=Policies.ProjectRead)][HttpGet("benchmarks/cross-project")]
    public Task<CrossProjectBenchmarkComparisonDto> CrossProject([FromQuery]string definitionKey,[FromQuery]string metricName="latency",[FromQuery]string aggregation="p95",[FromQuery]int take=20,CancellationToken ct=default)=>service.CompareProjectsAsync(definitionKey,metricName,aggregation,take,ct);
}
