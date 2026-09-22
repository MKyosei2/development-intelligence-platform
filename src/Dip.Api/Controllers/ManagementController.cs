// Purpose: Exposes management work, analytics, risk, resource, scope, and dashboard endpoints.
using Dip.Api.Authorization;
using Dip.Application.Abstractions.Integrations;
using Dip.Application.Features.Assets;
using Dip.Application.Features.ChangeImpact;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.Decisions;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.FeatureControl;
using Dip.Application.Features.Integrations;
using Dip.Application.Features.Milestones;
using Dip.Application.Features.Management;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.Review;
using Dip.Application.Features.Scenarios;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectControlDecisionBriefDto = Dip.Application.Features.Decisions.DecisionBriefDto;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/project-control")]
public sealed class ManagementController(WorkManagementService work, ManagementAnalyticsService management, ManagementDashboardService managementDashboard) : ControllerBase
{
    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("teams")]
        public Task<DevelopmentGraphNodeDto> UpsertTeam([FromBody]UpsertTeamCommand command,CancellationToken ct)
            =>work.UpsertTeamAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("workstreams")]
        public Task<DevelopmentGraphNodeDto> UpsertWorkstream([FromBody]UpsertWorkstreamCommand command,CancellationToken ct)
            =>work.UpsertWorkstreamAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("tasks")]
        public Task<DevelopmentGraphNodeDto> UpsertTask([FromBody]UpsertTaskCommand command,CancellationToken ct)
            =>work.UpsertTaskAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("management-dependencies")]
        public async Task<IActionResult> LinkManagementDependency([FromBody]LinkManagementDependencyCommand command,CancellationToken ct)
        { await work.LinkDependencyAsync(command,ct);return NoContent(); }

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("blockers")]
        public Task<DevelopmentGraphNodeDto> UpsertBlocker([FromBody]UpsertBlockerCommand command,CancellationToken ct)
            =>work.UpsertBlockerAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("requirements")]
        public Task<DevelopmentGraphNodeDto> UpsertRequirement([FromBody]UpsertRequirementCommand command,CancellationToken ct)
            =>work.UpsertRequirementAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("requirement-links")]
        public async Task<IActionResult> LinkRequirement([FromBody]LinkRequirementCommand command,CancellationToken ct)
        { await work.LinkRequirementAsync(command,ct);return NoContent(); }

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("scope-baselines")]
        public Task<DevelopmentGraphNodeDto> CreateScopeBaseline([FromBody]CreateScopeBaselineCommand command,CancellationToken ct)
            =>work.CreateScopeBaselineAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("task-milestones")]
        public async Task<IActionResult> LinkTaskMilestone([FromBody]LinkTaskToMilestoneCommand command,CancellationToken ct)
        { await work.LinkTaskToMilestoneAsync(command,ct);return NoContent(); }

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/capacity")]
        public Task<CapacityOverviewDto> GetCapacity(Guid projectId,[FromQuery]int horizonWeeks=8,CancellationToken ct=default)
            =>management.GetCapacityAsync(projectId,horizonWeeks,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/schedule")]
        public Task<ManagementScheduleDto> GetSchedule(Guid projectId,CancellationToken ct)
            =>management.GetScheduleAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/blockers")]
        public Task<BlockerOverviewDto> GetBlockers(Guid projectId,CancellationToken ct)
            =>management.GetBlockersAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/management-dependencies")]
        public Task<DependencyOverviewDto> GetDependencies(Guid projectId,CancellationToken ct)
            =>management.GetDependenciesAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/management-milestones")]
        public Task<ManagementMilestoneOverviewDto> GetManagementMilestones(Guid projectId,CancellationToken ct)
            =>management.GetMilestonesAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/scope")]
        public Task<ScopeOverviewDto> GetScope(Guid projectId,CancellationToken ct)
            =>management.GetScopeAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("risks")]
        public Task<DevelopmentGraphNodeDto> UpsertRisk([FromBody]UpsertRiskCommand command,CancellationToken ct)
            =>work.UpsertRiskAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/risks")]
        public Task<RiskRegisterDto> GetRisks(Guid projectId,CancellationToken ct)
            =>management.GetRisksAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("management-decisions")]
        public Task<DevelopmentGraphNodeDto> UpsertManagementDecision([FromBody]UpsertManagementDecisionCommand command,CancellationToken ct)
            =>work.UpsertManagementDecisionAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("management-actions")]
        public Task<DevelopmentGraphNodeDto> UpsertManagementAction([FromBody]UpsertActionItemCommand command,CancellationToken ct)
            =>work.UpsertActionItemAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/decision-actions")]
        public Task<DecisionActionOverviewDto> GetDecisionActions(Guid projectId,CancellationToken ct)
            =>management.GetDecisionsAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("resource-allocations")]
        public Task<DevelopmentGraphNodeDto> UpsertResourceAllocation([FromBody]UpsertResourceAllocationCommand command,CancellationToken ct)
            =>work.UpsertResourceAllocationAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/resource-allocations")]
        public Task<ResourceAllocationOverviewDto> GetResourceAllocations(Guid projectId,[FromQuery]int horizonWeeks=8,CancellationToken ct=default)
            =>management.GetResourceAllocationsAsync(projectId,horizonWeeks,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
        [HttpPost("change-requests")]
        public Task<DevelopmentGraphNodeDto> UpsertChangeRequest([FromBody]UpsertChangeRequestCommand command,CancellationToken ct)
            =>work.UpsertChangeRequestAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/scope-changes")]
        public Task<ScopeChangeOverviewDto> GetScopeChanges(Guid projectId,CancellationToken ct)
            =>management.GetScopeChangesAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/management-alerts")]
        public Task<EscalationOverviewDto> GetManagementAlerts(Guid projectId,[FromQuery]int horizonWeeks=8,CancellationToken ct=default)
            =>management.GetAlertsAsync(projectId,horizonWeeks,ct);

    [Authorize(Policy=Policies.ProjectRead)]
        [HttpGet("projects/{projectId:guid}/management-dashboard")]
        public Task<ManagementDashboardDto> GetManagementDashboard(Guid projectId,[FromQuery]int horizonWeeks=8,CancellationToken ct=default)
            =>managementDashboard.GetAsync(projectId,horizonWeeks,ct);
}
