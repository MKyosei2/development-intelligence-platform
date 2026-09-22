// Purpose: Exposes HTTP endpoints for this API responsibility.
using System.Diagnostics;
using Dip.Api.Authorization;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/qa-governance")]
[Produces("application/json")]
public sealed class QaGovernanceController(QaGovernanceQueryService query, QaGovernanceAdministrationService admin, IQaBugMinimizer minimizer) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/overview")]
    public Task<QaGovernanceOverviewDto> Overview(Guid projectId, CancellationToken ct) => query.GetOverviewAsync(projectId, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/findings")]
    public Task<PageResult<QaFindingStateDto>> Findings(Guid projectId, [FromQuery] string? cursor = null, [FromQuery] int pageSize = 100, CancellationToken ct = default) => query.FindFindingsAsync(projectId, cursor, pageSize, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member)]
    [HttpPost("findings/transition")]
    public async Task<IActionResult> Transition([FromBody] TransitionFindingCommand command, CancellationToken ct) { await admin.TransitionAsync(command, Actor(), ct); return NoContent(); }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/waivers")]
    public Task<IReadOnlyList<QaWaiverDto>> Waivers(Guid projectId, CancellationToken ct) => query.ListWaiversAsync(projectId, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Member)]
    [HttpPost("waivers")]
    public async Task<object> RequestWaiver([FromBody] CreateWaiverCommand command, CancellationToken ct) => new { id = await admin.RequestWaiverAsync(command, Actor(), ct) };

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner)]
    [HttpPost("projects/{projectId:guid}/waivers/{id:guid}/approve")]
    public async Task<IActionResult> ApproveWaiver(Guid projectId, Guid id, [FromBody] DecideWaiverCommand command, CancellationToken ct)
    {
        await EnsureWaiverProject(projectId, id, ct);
        await admin.ApproveWaiverAsync(id, command, Actor(), ct);
        return NoContent();
    }
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner)]
    [HttpPost("projects/{projectId:guid}/waivers/{id:guid}/reject")]
    public async Task<IActionResult> RejectWaiver(Guid projectId, Guid id, [FromBody] DecideWaiverCommand command, CancellationToken ct)
    {
        await EnsureWaiverProject(projectId, id, ct);
        await admin.RejectWaiverAsync(id, command, Actor(), ct);
        return NoContent();
    }
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner)]
    [HttpPost("projects/{projectId:guid}/waivers/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeWaiver(Guid projectId, Guid id, [FromBody] DecideWaiverCommand command, CancellationToken ct)
    {
        await EnsureWaiverProject(projectId, id, ct);
        await admin.RevokeWaiverAsync(id, command, Actor(), ct);
        return NoContent();
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/reliability")]
    public Task<IReadOnlyList<QaReliabilityDto>> Reliability(Guid projectId, [FromQuery] int take = 500, CancellationToken ct = default) => query.ListReliabilityAsync(projectId, take, ct);
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/baselines")]
    public Task<IReadOnlyList<QaBaselineDto>> Baselines(Guid projectId, [FromQuery] int take = 500, CancellationToken ct = default) => query.ListBaselinesAsync(projectId, take, ct);
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpPost("baselines/rebaseline")]
    public async Task<IActionResult> Rebaseline([FromBody] RebaselineQaCommand command, CancellationToken ct) { await admin.RebaselineAsync(command, Actor(), ct); return NoContent(); }
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/health")]
    public Task<IReadOnlyList<QaHealthDto>> Health(Guid projectId, [FromQuery] int take = 100, CancellationToken ct = default) => query.ListHealthAsync(projectId, take, ct);
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/trends")]
    public Task<IReadOnlyList<QaTrendDto>> Trends(Guid projectId, [FromQuery] int take = 200, CancellationToken ct = default) => query.ListTrendsAsync(projectId, take, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/ownership-rules")]
    public Task<IReadOnlyList<QaOwnershipRuleDto>> OwnershipRules(Guid projectId, CancellationToken ct) => query.ListOwnershipAsync(projectId, ct);
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/policies")]
    public Task<IReadOnlyList<QaPolicyDto>> ListPolicies(Guid projectId, CancellationToken ct) => query.ListPoliciesAsync(projectId, ct);
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/active-policy")]
    public Task<QaPolicyDto?> ActivePolicy(Guid projectId, CancellationToken ct) => query.GetActivePolicyAsync(projectId, ct);
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/test-impact-rules")]
    public Task<IReadOnlyList<QaTestImpactRuleDto>> TestImpactRules(Guid projectId, CancellationToken ct) => query.ListTestImpactAsync(projectId, ct);
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/retention-rules")]
    public Task<IReadOnlyList<QaRetentionRuleDto>> RetentionRules(Guid projectId, CancellationToken ct) => query.ListRetentionAsync(projectId, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpPost("ownership-rules")]
    public async Task<object> Ownership([FromBody] CreateOwnershipRuleCommand command, CancellationToken ct) => new { id = await admin.AddOwnershipRuleAsync(command, Actor(), ct) };
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpPost("policies")]
    public async Task<object> Policy([FromBody] CreateQaPolicyCommand command, CancellationToken ct) => new { id = await admin.CreatePolicyAsync(command, Actor(), ct) };
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpPost("projects/{projectId:guid}/policies/{id:guid}/approve")]
    public async Task<IActionResult> ApprovePolicy(Guid projectId, Guid id, [FromBody] PolicyLifecycleCommand command, CancellationToken ct)
    {
        await EnsurePolicyProject(projectId, id, ct);
        await admin.ApprovePolicyAsync(id, command, Actor(), ct);
        return NoContent();
    }
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner)]
    [HttpPost("projects/{projectId:guid}/policies/{id:guid}/activate")]
    public async Task<IActionResult> ActivatePolicy(Guid projectId, Guid id, [FromBody] PolicyLifecycleCommand command, CancellationToken ct)
    {
        await EnsurePolicyProject(projectId, id, ct);
        await admin.ActivatePolicyAsync(id, command, Actor(), ct);
        return NoContent();
    }
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner)]
    [HttpPost("projects/{projectId:guid}/policies/{id:guid}/archive")]
    public async Task<IActionResult> ArchivePolicy(Guid projectId, Guid id, [FromBody] PolicyLifecycleCommand command, CancellationToken ct)
    {
        await EnsurePolicyProject(projectId, id, ct);
        await admin.ArchivePolicyAsync(id, command, Actor(), ct);
        return NoContent();
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpPost("test-impact-rules")]
    public async Task<object> TestImpact([FromBody] CreateTestImpactRuleCommand command, CancellationToken ct) => new { id = await admin.AddTestImpactRuleAsync(command, Actor(), ct) };
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpPost("retention-rules")]
    public async Task<object> Retention([FromBody] CreateRetentionRuleCommand command, CancellationToken ct) => new { id = await admin.AddRetentionRuleAsync(command, Actor(), ct) };

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("smart-test-selection")]
    public Task<IReadOnlyList<SmartTestSelectionDto>> SelectTests([FromBody] SmartTestSelectionRequest request, CancellationToken ct) => query.SelectTestsAsync(request, ct);
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("projects/{projectId:guid}/test-recommendations")]
    public Task<IReadOnlyList<TestRecommendationDto>> Recommend(Guid projectId, [FromBody] IReadOnlyList<string> changedPaths, CancellationToken ct) => query.RecommendTestsAsync(projectId, changedPaths, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/root-cause-correlation/{fingerprint}")]
    public Task<RootCauseCorrelationDto> Correlate(Guid projectId, string fingerprint, CancellationToken ct) => query.CorrelateAsync(projectId, fingerprint, ct);
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/freshness")]
    public Task<QaFreshnessDto> Freshness(Guid projectId, [FromQuery] int maxAgeDays = 14, CancellationToken ct = default) => query.GetFreshnessAsync(projectId, maxAgeDays, ct);
    [AllowAnonymous]
    [HttpGet("diagnostic-schema")]
    public DiagnosticSchemaDto Schema() => QaGovernanceQueryService.GetDiagnosticSchema();
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/cost")]
    public Task<QaCostSummaryDto> Cost(Guid projectId, [FromQuery] int take = 200, CancellationToken ct = default) => query.GetCostSummaryAsync(projectId, take, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/minimizations")]
    public Task<IReadOnlyList<QaMinimizationRecordDto>> Minimizations(Guid projectId, [FromQuery] int take = 100, CancellationToken ct = default) => query.ListMinimizationsAsync(projectId, take, ct);
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("minimize")]
    public Task<BugMinimizationDto> Minimize([FromBody] BugMinimizationCommand command, CancellationToken ct) => minimizer.MinimizeAsync(command, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/release-overrides")]
    public Task<IReadOnlyList<QaReleaseGateOverrideDto>> Overrides(Guid projectId, [FromQuery] int take = 100, CancellationToken ct = default) => query.ListOverridesAsync(projectId, take, ct);
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpPost("release-overrides")]
    public async Task<object> RequestOverride([FromBody] RequestReleaseGateOverrideCommand command, CancellationToken ct) => new { id = await admin.RequestOverrideAsync(command, Actor(), ct) };
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner)]
    [HttpPost("projects/{projectId:guid}/release-overrides/{id:guid}/approve")]
    public async Task<IActionResult> ApproveOverride(Guid projectId, Guid id, [FromBody] DecideReleaseGateOverrideCommand command, CancellationToken ct)
    {
        await EnsureOverrideProject(projectId, id, ct);
        await admin.ApproveOverrideAsync(id, command, Actor(), ct);
        return NoContent();
    }
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner)]
    [HttpPost("projects/{projectId:guid}/release-overrides/{id:guid}/reject")]
    public async Task<IActionResult> RejectOverride(Guid projectId, Guid id, [FromBody] DecideReleaseGateOverrideCommand command, CancellationToken ct)
    {
        await EnsureOverrideProject(projectId, id, ct);
        await admin.RejectOverrideAsync(id, command, Actor(), ct);
        return NoContent();
    }
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner)]
    [HttpPost("projects/{projectId:guid}/release-overrides/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeOverride(Guid projectId, Guid id, [FromBody] DecideReleaseGateOverrideCommand command, CancellationToken ct)
    {
        await EnsureOverrideProject(projectId, id, ct);
        await admin.RevokeOverrideAsync(id, command, Actor(), ct);
        return NoContent();
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpGet("projects/{projectId:guid}/audit")]
    public Task<IReadOnlyList<GovernanceAuditLogDto>> Audit(Guid projectId, [FromQuery] int take = 200, CancellationToken ct = default) => query.ListAuditAsync(projectId, take, ct);
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpGet("projects/{projectId:guid}/backup-verifications")]
    public Task<IReadOnlyList<QaBackupVerificationDto>> BackupVerifications(Guid projectId, [FromQuery] int take = 100, CancellationToken ct = default) => query.ListBackupVerificationsAsync(projectId, take, ct);
    [Authorize(Policy = Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Owner)]
    [HttpPost("backup-verifications")]
    public async Task<object> RecordBackupVerification([FromBody] RecordBackupVerificationCommand command, CancellationToken ct) => new { id = await admin.RecordBackupVerificationAsync(command, Actor(), ct) };

    [Authorize(Policy = Policies.Admin)]
    [HttpGet("benchmarks/cross-project")]
    public Task<CrossProjectBenchmarkComparisonDto> CrossProject([FromQuery] string definitionKey, [FromQuery] string metricName = "latency", [FromQuery] string aggregation = "p95", [FromQuery] int take = 20, CancellationToken ct = default) => query.CompareProjectsAsync(definitionKey, metricName, aggregation, take, ct);

    private GovernanceAuditContext Actor() => new(RequestActor.AuditIdentity(User), HttpContext.TraceIdentifier, Activity.Current?.TraceId.ToString());
    private async Task EnsureWaiverProject(Guid projectId, Guid id, CancellationToken ct) { var x = (await query.ListWaiversAsync(projectId, ct)).FirstOrDefault(w => w.Id == id); if (x is null) throw new KeyNotFoundException("Waiver not found for the project."); }
    private async Task EnsurePolicyProject(Guid projectId, Guid id, CancellationToken ct) { var x = (await query.ListPoliciesAsync(projectId, ct)).FirstOrDefault(w => w.Id == id); if (x is null) throw new KeyNotFoundException("Policy not found for the project."); }
    private async Task EnsureOverrideProject(Guid projectId, Guid id, CancellationToken ct) { var x = (await query.ListOverridesAsync(projectId, 1000, ct)).FirstOrDefault(w => w.Id == id); if (x is null) throw new KeyNotFoundException("Release override not found for the project."); }
}
