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
public sealed class ProjectControlController(
    DevelopmentGraphService graph,
    ChangeImpactAnalysisService impact,
    MilestoneIntelligenceService milestones,
    ProjectControlTestImpactService testImpact,
    ReviewIntelligenceService reviews,
    FeatureReadinessService features,
    AssetIntelligenceService assets,
    DecisionLogService decisions,
    WhatIfSimulationService scenarios,
    VersionControlSyncService versionControl,
    ExternalCiIntegrationService ci,
    ProjectControlDashboardService dashboard,
    WorkManagementService work,
    ManagementAnalyticsService management,
    ManagementDashboardService managementDashboard,
    ManagementSpreadsheetService spreadsheets,
    SpreadsheetSchemaInferenceService spreadsheetInference):ControllerBase
{
    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/graph")]
    public Task<DevelopmentGraphDto> GetGraph(Guid projectId,[FromQuery]DevelopmentGraphNodeType? type,CancellationToken ct)
        =>graph.GetAsync(projectId,type,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/graph/universal")]
    public Task<UniversalGraphDto> GetUniversalGraph(Guid projectId,[FromQuery]string? domainId,[FromQuery]string? kind,CancellationToken ct)
        =>graph.GetUniversalAsync(projectId,domainId,kind,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("graph/nodes")]
    public Task<DevelopmentGraphNodeDto> UpsertNode([FromBody]UpsertDevelopmentGraphNodeCommand command,CancellationToken ct)
        =>graph.UpsertNodeAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("graph/universal/nodes")]
    public Task<UniversalGraphNodeDto> UpsertUniversalNode([FromBody]UpsertUniversalGraphNodeCommand command,CancellationToken ct)
        =>graph.UpsertUniversalNodeAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("graph/edges")]
    public Task<DevelopmentGraphEdgeDto> LinkNodes([FromBody]LinkDevelopmentGraphNodesCommand command,CancellationToken ct)
        =>graph.LinkAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("graph/universal/edges")]
    public Task<UniversalGraphEdgeDto> LinkUniversalNodes([FromBody]LinkUniversalGraphNodesCommand command,CancellationToken ct)
        =>graph.LinkUniversalAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("change-impact")]
    public Task<ChangeImpactResult> AnalyzeChangeImpact([FromBody]ChangeImpactCommand command,CancellationToken ct)
        =>impact.AnalyzeAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("test-impact")]
    public Task<ProjectControlTestImpactResult> AnalyzeTestImpact([FromBody]ProjectControlTestImpactRequest request,CancellationToken ct)
        =>testImpact.AnalyzeAsync(request.Command,request.RepositoryKey,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("milestones")]
    public Task<DevelopmentGraphNodeDto> UpsertMilestone([FromBody]UpsertMilestoneCommand command,CancellationToken ct)
        =>milestones.UpsertAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("milestone-dependencies")]
    public async Task<IActionResult> LinkMilestone([FromBody]MilestoneDependencyCommand command,CancellationToken ct)
    { await milestones.LinkDependencyAsync(command,ct);return NoContent(); }

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/critical-path")]
    public Task<CriticalPathResult> GetCriticalPath(Guid projectId,CancellationToken ct)
        =>milestones.AnalyzeAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("features")]
    public Task<DevelopmentGraphNodeDto> UpsertFeature([FromBody]UpsertFeatureCommand command,CancellationToken ct)
        =>features.UpsertFeatureAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("acceptance-criteria")]
    public Task<DevelopmentGraphNodeDto> UpsertCriterion([FromBody]UpsertAcceptanceCriterionCommand command,CancellationToken ct)
        =>features.UpsertCriterionAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("acceptance-evidence")]
    public Task<DevelopmentGraphNodeDto> AttachAcceptanceEvidence([FromBody]AttachAcceptanceEvidenceCommand command,CancellationToken ct)
        =>features.AttachEvidenceAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/features/{featureKey}/readiness")]
    public Task<FeatureReadinessResult> GetFeatureReadiness(Guid projectId,string featureKey,[FromQuery]int evidenceFreshnessDays=30,CancellationToken ct=default)
        =>features.EvaluateAsync(projectId,featureKey,evidenceFreshnessDays,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("reviews/analyze")]
    public Task<ReviewIntelligenceResult> AnalyzeReview([FromBody]ReviewIntelligenceCommand command,CancellationToken ct)
        =>reviews.AssessAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("assets/observe")]
    public Task<AssetIntelligenceResult> ObserveAsset([FromBody]AssetObservationCommand command,CancellationToken ct)
        =>assets.ObserveAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("decisions")]
    public Task<DecisionLogDto> LogDecision([FromBody]LogDecisionCommand command,CancellationToken ct)
        =>decisions.LogAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/decisions")]
    public Task<IReadOnlyList<DecisionLogDto>> ListDecisions(Guid projectId,[FromQuery]int take=50,CancellationToken ct=default)
        =>decisions.ListAsync(projectId,take,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/decision-brief")]
    public Task<ProjectControlDecisionBriefDto> GetDecisionBrief(Guid projectId,CancellationToken ct)
        =>decisions.BuildBriefAsync(projectId,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("what-if")]
    public Task<WhatIfSimulationResult> Simulate([FromBody]WhatIfSimulationCommand command,CancellationToken ct)
        =>scenarios.SimulateAsync(command,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("version-control/sync")]
    public Task<NormalizedVersionControlChange> SyncVersionControl([FromBody]VersionControlSyncRequest request,CancellationToken ct)
        =>versionControl.SyncAsync(request,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("ci/sync")]
    public Task<NormalizedCiRun> SyncCi([FromBody]ExternalCiSyncRequest request,CancellationToken ct)
        =>ci.SyncAsync(request,ct);

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("ci/ingest")]
    public Task<NormalizedCiRun> IngestCi([FromBody]NormalizedCiRun run,CancellationToken ct)
        =>ci.IngestAsync(run,ct);

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

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/management.xlsx")]
    public async Task<IActionResult> ExportManagementWorkbook(Guid projectId,CancellationToken ct)
    { var bytes=await spreadsheets.ExportXlsxAsync(projectId,ct);return File(bytes,"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",$"dip-management-{projectId:N}.xlsx"); }

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("projects/{projectId:guid}/management.xlsx/import")]
    [RequestSizeLimit(25_000_000)]
    public async Task<SpreadsheetImportResult> ImportManagementWorkbook(Guid projectId,IFormFile file,CancellationToken ct)
    { if(file is null||file.Length==0)throw new ArgumentException("An .xlsx file is required.",nameof(file));await using var stream=file.OpenReadStream();return await spreadsheets.ImportXlsxAsync(projectId,stream,ct); }

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpPost("projects/{projectId:guid}/management.xlsx/analyze")]
    [RequestSizeLimit(25_000_000)]
    public async Task<SpreadsheetSchemaAnalysisDto> AnalyzeManagementWorkbook(Guid projectId,IFormFile file,CancellationToken ct)
    { if(file is null||file.Length==0)throw new ArgumentException("An .xlsx file is required.",nameof(file));await using var stream=file.OpenReadStream();return await spreadsheetInference.AnalyzeXlsxAsync(projectId,stream,ct); }

    [Authorize(Policy=Policies.ProjectWrite)]
    [HttpPost("projects/{projectId:guid}/management.xlsx/import-flexible")]
    [RequestSizeLimit(25_000_000)]
    public async Task<FlexibleSpreadsheetImportResult> ImportFlexibleManagementWorkbook(Guid projectId,IFormFile file,[FromQuery]string? domainId=null,[FromQuery]bool dryRun=true,CancellationToken ct=default)
    { if(file is null||file.Length==0)throw new ArgumentException("An .xlsx file is required.",nameof(file));await using var stream=file.OpenReadStream();return await spreadsheetInference.ImportFlexibleXlsxAsync(projectId,stream,domainId,dryRun,ct); }

    [Authorize(Policy=Policies.ProjectWrite)]
    [RequireProjectRole(ProjectMembershipRole.Maintainer)]
    [HttpPost("google-sheets/sync")]
    public Task<GoogleSheetsSyncResult> SyncGoogleSheets([FromBody]GoogleSheetsSyncCommand command,CancellationToken ct)
        =>spreadsheets.SyncGoogleAsync(command,ct);

    [Authorize(Policy=Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/dashboard")]
    public Task<ProjectControlDashboardDto> GetDashboard(Guid projectId,CancellationToken ct)
        =>dashboard.GetAsync(projectId,ct);
}

public sealed record ProjectControlTestImpactRequest(string RepositoryKey,TestImpactCommand Command);
