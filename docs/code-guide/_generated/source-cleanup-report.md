# Source Cleanup Report

- Generated: 2026-09-16 07:46:58
- Branch: `refactor/source-organization-20260916-070617-807`
- Backup: `C:\Users\kyose\AppData\Local\Temp\dip-source-cleanup-20260916-074627`
- Renamed paths: 0
- Organized/moved items: 0
- Comment-normalized files: 766
- Static audit issues: 2

## Scope

This cleanup covers physical source organization, file naming, and source comments only.
Build, tests, CI, Windows App Control, runtime verification, and GitHub Actions are intentionally outside this cleanup result.

## Static audit issues

- Single-type filename mismatch: src\Dip.Infrastructure\Persistence\Repositories\Repositories.cs -> expected ProjectRepository.cs
- Legacy Difficulty name remains in active path: tests\Dip.Application.Tests\DifficultyFiveFoundationTests.cs

## Git diff stat

```text
 .editorconfig                                      | 45 +++++++++++
 scripts/Backup-Dip.ps1                             |  1 +
 scripts/Bootstrap-ProjectMemberships.ps1           |  1 +
 scripts/Build-NativeTools.ps1                      |  1 +
 scripts/Complete-PortfolioRelease.ps1              |  1 +
 scripts/Generate-ArtifactTracking-Migration.ps1    |  3 +-
 scripts/Generate-EnterpriseFleet-Migration.ps1     |  3 +-
 scripts/Generate-EvidenceIntegration-Migration.ps1 |  3 +-
 scripts/Generate-ProductionTools-Migration.ps1     |  5 +-
 scripts/Generate-QualityAutomation-Migration.ps1   |  5 +-
 scripts/Initialize-SelfHostedPostgres.ps1          |  1 +
 scripts/Invoke-ManualCI.ps1                        |  1 +
 scripts/Invoke-QaBugZoo.ps1                        |  1 +
 scripts/New-SelfHostedFallbackEvidence.ps1         |  1 +
 scripts/Publish-Portfolio.ps1                      |  1 +
 scripts/Publish-PortfolioRelease.ps1               |  1 +
 scripts/Request-FinalVerification.ps1              |  1 +
 scripts/Restore-Dip.ps1                            |  1 +
 scripts/Run-DIP-Agent.ps1                          |  3 +-
 scripts/Run-GovernanceLoadTest.ps1                 |  1 +
 scripts/Run-PortfolioBenchmarks.ps1                |  1 +
 scripts/Run-UniversalQaBenchmark.ps1               |  1 +
 scripts/Set-DIP-ProductionBridgeEnvironment.ps1    |  3 +-
 scripts/Start-DIP-Desktop.ps1                      |  3 +-
 scripts/Start-DIP.ps1                              |  1 +
 scripts/Stop-DIP.ps1                               |  1 +
 scripts/Test-P1-Capture.ps1                        |  1 +
 scripts/Verify-ActionsPolicy.ps1                   |  1 +
 scripts/Verify-ArtifactTracking-Local.ps1          |  5 +-
 scripts/Verify-DatabaseMigrations.ps1              |  1 +
 scripts/Verify-DisasterRecovery.ps1                |  1 +
 scripts/Verify-EnterpriseFleet-Local.ps1           |  5 +-
 scripts/Verify-EvidenceIntegration-Local.ps1       |  5 +-
 scripts/Verify-GitHubCiPreflight.ps1               |  3 +-
 scripts/Verify-GuiPhase1.ps1                       |  1 +
 scripts/Verify-GuiPhase1Static.ps1                 |  1 +
 scripts/Verify-GuiRuntime.ps1                      |  1 +
 scripts/Verify-P1.ps1                              |  1 +
 scripts/Verify-P2.ps1                              |  1 +
 scripts/Verify-PortfolioFormal.ps1                 |  1 +
 scripts/Verify-PortfolioReadiness.ps1              |  1 +
 scripts/Verify-ProductionTools-Local.ps1           |  5 +-
 scripts/Verify-ProjectControl.ps1                  |  1 +
 scripts/Verify-QaGovernance.ps1                    |  1 +
 scripts/Verify-QualityAutomation-Local.ps1         |  5 +-
 scripts/Verify-SelfHostedWindowsPreflight.ps1      |  1 +
 scripts/Verify-SelfHostedWindowsRuntime.ps1        |  1 +
 scripts/Verify-Solution.ps1                        |  1 +
 scripts/Verify-UnityCollector.ps1                  |  1 +
 scripts/Verify-UniversalQa.ps1                     |  1 +
 scripts/build-all.ps1                              |  1 +
 scripts/format-all.ps1                             |  1 +
 scripts/generate-sample-data.ps1                   |  1 +
 scripts/package-release.ps1                        |  1 +
 scripts/setup-development.ps1                      |  1 +
 scripts/test-all.ps1                               |  1 +
 scripts/validate-schemas.ps1                       |  1 +
 scripts/verify-environment.ps1                     |  1 +
 src/Dip.Agent.Core/AgentApiClient.cs               |  1 +
 src/Dip.Agent.Core/AgentModels.cs                  |  1 +
 src/Dip.Agent.Core/AgentOptions.cs                 |  1 +
 src/Dip.Agent.Core/AgentRuntime.cs                 |  1 +
 src/Dip.Agent.Core/EnterpriseAgentClient.cs        |  1 +
 src/Dip.Agent.Core/MediaSpool.cs                   |  1 +
 src/Dip.Agent.Core/ProjectFileScanner.cs           |  1 +
 src/Dip.Agent.Core/SqliteAgentStateStore.cs        |  1 +
 src/Dip.Agent.Core/ToolEventModels.cs              |  1 +
 src/Dip.Agent.Windows/AgentConfiguration.cs        |  1 +
 src/Dip.Agent.Windows/CollectorWorker.cs           |  1 +
 src/Dip.Agent.Windows/FleetWorker.cs               |  1 +
 src/Dip.Agent.Windows/Program.cs                   |  1 +
 src/Dip.Agent.Windows/ToolBridgeWorker.cs          |  1 +
 src/Dip.Api/Authorization/Policies.cs              |  1 +
 .../Authorization/ProductionSecurityGuard.cs       |  1 +
 .../Authorization/ProjectScopeAuthorization.cs     |  1 +
 .../AgentPolicyDistributionController.cs           |  1 +
 src/Dip.Api/Controllers/ArtifactsController.cs     |  1 +
 src/Dip.Api/Controllers/AuthController.cs          |  1 +
 src/Dip.Api/Controllers/BenchmarksController.cs    |  1 +
 src/Dip.Api/Controllers/CapabilitiesController.cs  |  1 +
 src/Dip.Api/Controllers/ChangeRiskController.cs    |  1 +
 .../ClaimEvidenceAssessmentsController.cs          |  1 +
 .../CollaborationEvidenceWebhooksController.cs     |  1 +
 src/Dip.Api/Controllers/CollectorsController.cs    |  1 +
 src/Dip.Api/Controllers/ConnectionsController.cs   |  1 +
 src/Dip.Api/Controllers/DashboardController.cs     |  1 +
 .../Controllers/DefectIntelligenceController.cs    |  1 +
 .../Controllers/EnterpriseFleetController.cs       |  1 +
 .../Controllers/EnterpriseGovernanceController.cs  |  1 +
 .../Controllers/EvidenceIngestionController.cs     |  1 +
 .../Controllers/ExternalEventsController.cs        |  1 +
 src/Dip.Api/Controllers/GameReplayController.cs    |  1 +
 .../Controllers/GitHubRepositoriesController.cs    |  1 +
 .../Controllers/HardwareBacktestsController.cs     |  1 +
 src/Dip.Api/Controllers/HardwareController.cs      |  1 +
 src/Dip.Api/Controllers/IncidentsController.cs     |  1 +
 src/Dip.Api/Controllers/IntelligenceController.cs  |  1 +
 .../Controllers/IntelligenceDashboardController.cs |  1 +
 src/Dip.Api/Controllers/JobsController.cs          |  1 +
 .../Controllers/LocalCollectorController.cs        |  1 +
 .../ManagedConnectionProjectsController.cs         |  1 +
 .../Controllers/ManagementOperationsController.cs  |  1 +
 src/Dip.Api/Controllers/MetricsController.cs       |  1 +
 src/Dip.Api/Controllers/NotificationsController.cs |  1 +
 .../Controllers/P2IntelligenceController.cs        |  1 +
 src/Dip.Api/Controllers/P2JobsController.cs        |  1 +
 .../Controllers/PerformanceBudgetsController.cs    |  1 +
 src/Dip.Api/Controllers/PipelineRunsController.cs  |  1 +
 .../Controllers/PolicyProfilesController.cs        |  1 +
 .../Controllers/PortfolioManagementController.cs   |  1 +
 .../Controllers/ProducerIntelligenceController.cs  |  1 +
 .../Controllers/ProductionToolsController.cs       |  1 +
 src/Dip.Api/Controllers/ProjectClaimsController.cs |  1 +
 .../Controllers/ProjectControlController.cs        |  1 +
 .../ProjectIntelligenceSourcesController.cs        |  1 +
 .../ProjectManagementCommandCenterController.cs    |  1 +
 .../Controllers/ProjectMembershipsController.cs    |  1 +
 .../Controllers/ProjectSourceBindingsController.cs |  1 +
 src/Dip.Api/Controllers/ProjectsController.cs      |  1 +
 src/Dip.Api/Controllers/QaArtifactsController.cs   |  1 +
 src/Dip.Api/Controllers/QaController.cs            |  1 +
 src/Dip.Api/Controllers/QaGovernanceController.cs  |  1 +
 .../Controllers/QualityAutomationController.cs     |  1 +
 .../Controllers/RegressionTriageController.cs      |  1 +
 .../Controllers/ReleaseEvidenceController.cs       |  1 +
 .../Controllers/ReleaseReadinessController.cs      |  1 +
 .../Controllers/RepositoryGraphController.cs       |  1 +
 src/Dip.Api/Controllers/SystemStatusController.cs  |  1 +
 .../Controllers/TrackedArtifactsController.cs      |  1 +
 src/Dip.Api/Controllers/WebhooksController.cs      |  1 +
 src/Dip.Api/Controllers/WorkItemsController.cs     |  1 +
 .../Extensions/ApplicationBuilderExtensions.cs     |  1 +
 .../Extensions/ServiceCollectionExtensions.cs      |  1 +
 src/Dip.Api/Extensions/SwaggerExtensions.cs        |  1 +
 src/Dip.Api/Filters/ValidationFilter.cs            |  1 +
 src/Dip.Api/GlobalUsings.cs                        |  1 +
 src/Dip.Api/Hubs/ProjectEventsHub.cs               |  1 +
 src/Dip.Api/Middleware/CorrelationIdMiddleware.cs  |  1 +
 .../Middleware/ExceptionHandlingMiddleware.cs      |  1 +
 src/Dip.Api/Middleware/RateLimitMiddleware.cs      |  1 +
 .../Middleware/RawBodyBufferingExtensions.cs       |  1 +
 src/Dip.Api/Middleware/RequestLoggingMiddleware.cs |  1 +
 src/Dip.Api/Program.cs                             |  1 +
 .../Abstractions/Artifacts/IArtifactStore.cs       |  1 +
 .../Calendars/IExternalCalendarConnector.cs        |  1 +
 .../Abstractions/External/IAnalysisClient.cs       |  1 +
 .../Abstractions/External/IGitHubClient.cs         |  1 +
 .../Abstractions/External/INotificationClient.cs   |  1 +
 .../Abstractions/External/IObjectStorage.cs        |  1 +
 .../Abstractions/External/IQueueClient.cs          |  1 +
 .../GitHub/IGitHubPullRequestCommentService.cs     |  1 +
 .../GitHub/IGitHubRepositoryCatalog.cs             |  1 +
 .../Abstractions/GitHub/IGitHubWebhookVerifier.cs  |  1 +
 .../Integrations/ProjectControlIntegrations.cs     |  1 +
 .../Abstractions/Messaging/IQueueClient.cs         |  1 +
 .../Observability/IDipOperationalMetrics.cs        |  1 +
 .../Persistence/IEnterpriseFleetRepositories.cs    |  1 +
 .../IEvidenceIntegrationRepositories.cs            |  1 +
 .../Abstractions/Persistence/IMetricRepository.cs  |  1 +
 .../Abstractions/Persistence/IOutboxRepository.cs  |  1 +
 .../Persistence/IPipelineRunRepository.cs          |  1 +
 .../Persistence/IProcessedWebhookRepository.cs     |  1 +
 .../Abstractions/Persistence/IProjectRepository.cs |  1 +
 .../Persistence/IQualityAutomationRepositories.cs  |  1 +
 .../Persistence/IQualityIssueRepository.cs         |  1 +
 .../Persistence/IQualityRuleRepository.cs          |  1 +
 .../Persistence/ITrackedArtifactRepository.cs      |  1 +
 .../Abstractions/Persistence/IUnitOfWork.cs        |  1 +
 .../Abstractions/Persistence/Repositories.cs       |  1 +
 .../Provenance/IReleaseProvenanceCollector.cs      |  1 +
 .../Abstractions/Replay/IGameReplayExecutor.cs     |  1 +
 .../Security/IIntegrationCredentialStore.cs        |  1 +
 .../Spreadsheets/IGoogleSheetsConnector.cs         |  1 +
 .../Abstractions/Time/ISystemClock.cs              |  1 +
 .../WorkItems/IExternalWorkItemConnector.cs        |  1 +
 src/Dip.Application/DependencyInjection.cs         |  1 +
 .../DependencyInjection.GitHub.cs                  |  1 +
 .../Commands/RequestAnomalyDetectionCommand.cs     |  1 +
 .../Commands/RequestDailySummaryCommand.cs         |  1 +
 .../Commands/RequestReleasePredictionCommand.cs    |  1 +
 .../Features/Analysis/Dtos/AnomalyResultDto.cs     |  1 +
 .../Features/Analysis/Dtos/DailySummaryDto.cs      |  1 +
 .../Features/Analysis/Dtos/ReleasePredictionDto.cs |  1 +
 .../Features/Artifacts/ArtifactService.cs          |  1 +
 .../Features/Artifacts/TrackedArtifactContracts.cs |  1 +
 .../Features/Artifacts/TrackedArtifactService.cs   |  1 +
 .../Features/Assets/AssetIntelligenceService.cs    |  1 +
 .../Authorization/IProjectMutationCoordinator.cs   |  1 +
 .../Features/Authorization/ProjectAccessService.cs |  1 +
 .../Authorization/ProjectResourceAuthorization.cs  |  1 +
 .../Features/Benchmarks/BenchmarkContracts.cs      |  1 +
 .../Benchmarks/BenchmarkOrchestrationService.cs    |  1 +
 .../Features/Capabilities/CapabilityRegistry.cs    |  1 +
 .../ChangeImpact/ChangeImpactAnalysisService.cs    |  1 +
 .../ChangeRisk/ChangeRiskIntelligenceService.cs    |  1 +
 .../Features/Collectors/CollectorReportService.cs  |  1 +
 .../Collectors/CollectorReportValidator.cs         |  1 +
 .../Features/Dashboard/DashboardQueryService.cs    |  1 +
 .../Dashboard/IntelligenceDashboardService.cs      |  1 +
 .../Dashboard/ManagementDashboardService.cs        |  1 +
 .../Dashboard/ProjectControlDashboardService.cs    |  1 +
 .../Features/Decisions/DecisionLogService.cs       |  1 +
 .../DevelopmentGraph/DevelopmentGraphContracts.cs  |  1 +
 .../DevelopmentGraph/DevelopmentGraphService.cs    |  1 +
 .../ProductionGraphProjectionService.cs            |  1 +
 .../ProjectControlTestImpactService.cs             |  1 +
 .../Enterprise/AdaptiveQaRoutingService.cs         |  1 +
 .../Enterprise/AgentPolicyDistributionService.cs   |  1 +
 .../Enterprise/DistributedIngestionService.cs      |  1 +
 .../Features/Enterprise/EnterpriseTenantService.cs |  1 +
 .../Enterprise/EvidenceIntegrityService.cs         |  1 +
 .../Enterprise/IdentityResolutionService.cs        |  1 +
 .../Enterprise/RetentionGovernanceService.cs       |  1 +
 .../Evidence/EvidenceIngestionContracts.cs         |  1 +
 .../Features/Evidence/EvidenceIngestionService.cs  |  1 +
 .../Features/Evidence/EvidenceValidator.cs         |  1 +
 .../ExternalEvents/ExternalIncidentIngestion.cs    |  1 +
 .../FeatureControl/FeatureReadinessService.cs      |  1 +
 .../Features/Fleet/AgentFleetService.cs            |  1 +
 .../GitHub/GitHubDevelopmentGraphIngestor.cs       |  1 +
 .../GitHub/GitHubQualityReportFormatter.cs         |  1 +
 .../Features/GitHub/GitHubWebhookProcessor.cs      |  1 +
 .../GitHubIntegration/GitHubRepositoryModels.cs    |  1 +
 .../Hardware/HardwareBacktestingService.cs         |  1 +
 .../Features/Hardware/HardwareContracts.cs         |  1 +
 .../Hardware/HardwareOrchestrationService.cs       |  1 +
 .../Commands/RecalculateProjectHealthCommand.cs    |  1 +
 .../Health/Queries/GetHealthHistoryQuery.cs        |  1 +
 .../Health/Queries/GetProjectHealthQuery.cs        |  1 +
 .../Health/Services/ProjectHealthCalculator.cs     |  1 +
 .../Health/Services/RiskScoreCalculator.cs         |  1 +
 .../Incidents/IncidentAggregationService.cs        |  1 +
 .../Integrations/ExternalCiIntegrationService.cs   |  1 +
 .../Features/Integrations/IIntegrationConnector.cs |  1 +
 .../Integrations/IntegrationConnectionService.cs   |  1 +
 .../Integrations/IntegrationConnectorRegistry.cs   |  1 +
 .../Features/Integrations/IntegrationContracts.cs  |  1 +
 .../Integrations/VersionControlSyncService.cs      |  1 +
 .../Features/Intelligence/AdaptiveQaPlanner.cs     |  1 +
 .../Intelligence/CommunityHardwareService.cs       |  1 +
 .../Intelligence/ForecastAndDecisionService.cs     |  1 +
 .../Features/Intelligence/P2Abstractions.cs        |  1 +
 .../Features/Intelligence/P2AnalysisService.cs     |  1 +
 .../Features/Intelligence/P2Contracts.cs           |  1 +
 .../Intelligence/P2OrchestrationService.cs         |  1 +
 .../Intelligence/RootCauseIntelligenceService.cs   |  1 +
 .../Features/Intelligence/TestStrengthContracts.cs |  1 +
 src/Dip.Application/Features/Jobs/DurableJobs.cs   |  1 +
 .../Features/Jobs/P2JobEnqueueService.cs           |  1 +
 .../Management/ManagementAnalyticsService.cs       |  1 +
 .../Features/Management/ManagementContracts.cs     |  1 +
 .../Management/ManagementOperationsContracts.cs    |  1 +
 .../Management/ManagementOperationsService.cs      |  1 +
 .../Management/ManagementSpreadsheetService.cs     |  1 +
 .../Features/Management/ManagementSyncService.cs   |  1 +
 .../Features/Management/ManagementXlsxCodec.cs     |  1 +
 .../Management/PortfolioManagementService.cs       |  1 +
 .../Management/ProducerIntelligenceService.cs      |  1 +
 .../ProjectManagementCommandCenterService.cs       |  1 +
 .../Management/SpreadsheetSchemaInference.cs       |  1 +
 .../Management/WorkCalendarIntegrationService.cs   |  1 +
 .../Features/Management/WorkManagementService.cs   |  1 +
 .../Milestones/MilestoneIntelligenceService.cs     |  1 +
 .../Commands/RetryNotificationCommand.cs           |  1 +
 .../Commands/SendNotificationCommand.cs            |  1 +
 .../Features/Notifications/Dtos/NotificationDto.cs |  1 +
 .../Features/Operations/Services/OutboxWriter.cs   |  1 +
 .../Features/P2Intelligence/AdaptiveQaPlanner.cs   |  1 +
 .../EvidenceGroundedRootCauseService.cs            |  1 +
 .../HardwareCommunityIntelligenceService.cs        |  1 +
 .../Features/P2Intelligence/P2Contracts.cs         |  1 +
 .../Features/P2Intelligence/P2EvidencePolicies.cs  |  1 +
 .../P2TechnicalIntelligenceService.cs              |  1 +
 .../ProjectDecisionIntelligenceService.cs          |  1 +
 .../Performance/PerformanceBudgetService.cs        |  1 +
 .../PipelineRuns/Commands/AddMetricsCommand.cs     |  1 +
 .../Commands/AddQualityIssuesCommand.cs            |  1 +
 .../Commands/CompletePipelineRunCommand.cs         |  1 +
 .../Commands/CreatePipelineRunCommand.cs           |  1 +
 .../Features/PipelineRuns/Dtos/PipelineRunDto.cs   |  1 +
 .../PipelineRuns/Queries/GetPipelineRunQuery.cs    |  1 +
 .../PipelineRuns/Queries/GetPipelineRunsQuery.cs   |  1 +
 .../Features/Policies/PolicyContracts.cs           |  1 +
 .../Features/Policies/PolicyProfileService.cs      |  1 +
 .../Features/Policies/PolicyResolver.cs            |  1 +
 .../ProductionTools/ProductionEventQueryService.cs |  1 +
 .../ProductionTools/ProductionMediaService.cs      |  1 +
 .../ProductionTools/ProductionToolContracts.cs     |  1 +
 .../ProductionTools/ProductionToolEventMapping.cs  |  1 +
 .../ProductionTools/ProductionToolEventService.cs  |  1 +
 .../ProjectEntityResolutionService.cs              |  1 +
 .../ProjectIntelligenceContracts.cs                |  1 +
 .../ProjectPlanIngestionService.cs                 |  1 +
 .../ProjectPlanUnderstandingEngine.cs              |  1 +
 .../ProjectProgressIntelligenceService.cs          |  1 +
 .../ProjectSourceIngestionService.cs               |  1 +
 .../ProjectTruth/AdaptiveProjectRiskEvaluator.cs   |  1 +
 .../ProjectTruth/ClaimEvidenceAssessmentService.cs |  1 +
 .../ProjectTruth/ClaimVerificationEvaluator.cs     |  1 +
 .../ProjectTruth/EvidenceConfidenceEvaluator.cs    |  1 +
 .../Features/ProjectTruth/PmEvidenceTranslator.cs  |  1 +
 .../Features/ProjectTruth/ProgressEvaluator.cs     |  1 +
 .../ProjectTruth/ProjectBlockerEvaluator.cs        |  1 +
 .../Features/ProjectTruth/ProjectClaimService.cs   |  1 +
 .../Features/ProjectTruth/ProjectRiskEvaluator.cs  |  1 +
 .../Features/ProjectTruth/ProjectTruthEvaluator.cs |  1 +
 .../Features/ProjectTruth/ProjectTruthModels.cs    |  1 +
 .../Features/ProjectTruth/ProjectTruthPolicy.cs    |  1 +
 .../Features/ProjectTruth/ProjectTruthService.cs   |  1 +
 .../Projects/Commands/CreateProjectCommand.cs      |  1 +
 .../Projects/Commands/DeleteProjectCommand.cs      |  1 +
 .../Projects/Commands/UpdateProjectCommand.cs      |  1 +
 .../Features/Projects/Dtos/ProjectDashboardDto.cs  |  1 +
 .../Features/Projects/Dtos/ProjectDto.cs           |  1 +
 .../Features/Projects/ProjectService.cs            |  1 +
 .../Projects/Queries/GetProjectDashboardQuery.cs   |  1 +
 .../Features/Projects/Queries/GetProjectQuery.cs   |  1 +
 .../Features/Projects/Queries/GetProjectsQuery.cs  |  1 +
 .../Commands/RecalculatePullRequestRiskCommand.cs  |  1 +
 .../Commands/SyncPullRequestCommand.cs             |  1 +
 .../PullRequests/Dtos/PullRequestRiskDto.cs        |  1 +
 .../PullRequests/Queries/GetPullRequestQuery.cs    |  1 +
 .../Queries/GetPullRequestRiskQuery.cs             |  1 +
 .../Quality/Commands/EvaluateQualityGateCommand.cs |  1 +
 .../Quality/Commands/ResolveQualityIssueCommand.cs |  1 +
 .../Features/Quality/Dtos/QualityIssueDto.cs       |  1 +
 .../Features/Quality/Dtos/QualityTrendDto.cs       |  1 +
 .../Features/Quality/QualityGateEvaluator.cs       |  1 +
 .../Features/Quality/QualityRuleEvaluator.cs       |  1 +
 .../Quality/Queries/GetQualityIssuesQuery.cs       |  1 +
 .../Quality/Queries/GetQualityTrendsQuery.cs       |  1 +
 .../QualityAutomation/AssetQa/AssetQaContracts.cs  |  1 +
 .../QualityAutomation/AssetQa/AssetQaRuleEngine.cs |  1 +
 .../QualityAutomation/AssetQa/AssetQaService.cs    |  1 +
 .../QualityAutomation/AutonomousDebugService.cs    |  1 +
 .../BranchQualityOperationsService.cs              |  1 +
 .../CommercialQualityOperationsService.cs          |  1 +
 .../GitBranchQaWorkspaceManager.cs                 |  1 +
 .../ProductionCapabilityQaPack.cs                  |  1 +
 .../QualityAutomation/ProductionQaIntelligence.cs  |  1 +
 .../Features/QualityAutomation/QaCapabilities.cs   |  1 +
 .../Features/QualityAutomation/QaContracts.cs      |  1 +
 .../Features/QualityAutomation/QaFingerprint.cs    |  1 +
 .../QualityAutomation/QaOrchestrationService.cs    |  1 +
 .../RuntimeDefectIntelligenceService.cs            |  1 +
 .../QualityAutomation/VisualQa/PortablePngCodec.cs |  1 +
 .../QualityAutomation/VisualQa/VisualComparator.cs |  1 +
 .../VisualQa/VisualQaContracts.cs                  |  1 +
 .../QualityAutomation/VisualQa/VisualQaService.cs  |  1 +
 .../QaGovernanceAdministrationService.cs           |  1 +
 .../QualityGovernance/QaGovernanceContracts.cs     |  1 +
 .../QualityGovernance/QaGovernanceCoreEngine.cs    |  1 +
 .../QualityGovernance/QaGovernanceQueryService.cs  |  1 +
 .../QualityGovernance/QaGovernanceRunProcessor.cs  |  1 +
 .../Features/Realtime/ProjectRealtimeEventDto.cs   |  1 +
 .../Features/Release/ReleaseEvidenceService.cs     |  1 +
 .../Features/Release/ReleaseReadinessService.cs    |  1 +
 .../Features/Replay/GameReplayService.cs           |  1 +
 .../Repositories/MultiRepositoryGraphService.cs    |  1 +
 .../Features/Review/ReviewIntelligenceService.cs   |  1 +
 .../Features/Scenarios/WhatIfSimulationService.cs  |  1 +
 .../Features/SystemStatus/SystemStatusDtos.cs      |  1 +
 .../Features/TeamLoad/Dtos/TeamLoadDto.cs          |  1 +
 .../TeamLoad/Queries/GetMemberWorkloadQuery.cs     |  1 +
 .../Features/TeamLoad/Queries/GetTeamLoadQuery.cs  |  1 +
 .../Features/Triage/RegressionTriageService.cs     |  1 +
 .../Features/WorkItems/WorkItemService.cs          |  1 +
 src/Dip.Client/DipPlatformClient.cs                |  1 +
 src/Dip.Desktop/App.xaml.cs                        |  1 +
 src/Dip.Desktop/MainWindow.xaml.cs                 |  1 +
 src/Dip.Desktop/ViewModels/MainWindowViewModel.cs  |  1 +
 src/Dip.Domain/Exceptions/DomainException.cs       |  1 +
 .../Exceptions/InvalidMetricException.cs           |  1 +
 .../Exceptions/InvalidProjectStateException.cs     |  1 +
 .../Features/Artifacts/Entities/ArtifactBlob.cs    |  1 +
 .../Artifacts/Entities/ArtifactDependency.cs       |  1 +
 .../Features/Artifacts/Entities/ArtifactPreview.cs |  1 +
 .../Features/Artifacts/Entities/ArtifactRecord.cs  |  1 +
 .../Artifacts/Entities/AssetQaFindingRecord.cs     |  1 +
 .../Artifacts/Entities/GoldenMasterProfile.cs      |  1 +
 .../Features/Artifacts/Entities/QaRun.cs           |  1 +
 .../Features/Artifacts/Entities/TrackedArtifact.cs |  1 +
 .../Artifacts/Entities/TrackedArtifactVersion.cs   |  1 +
 .../Artifacts/Entities/VisualComparisonRecord.cs   |  1 +
 .../Artifacts/Enums/ArtifactTrackingEnums.cs       |  1 +
 src/Dip.Domain/Features/Artifacts/Enums/P2Enums.cs |  1 +
 .../Features/Common/Enums/P2IntelligenceStatus.cs  |  1 +
 src/Dip.Domain/Features/Common/Enums/QaLanguage.cs |  1 +
 src/Dip.Domain/Features/Common/Enums/Severity.cs   |  1 +
 .../Common/Events/CriticalIssueDetectedEvent.cs    |  1 +
 .../Features/Common/Rules/TeamLoadRule.cs          |  1 +
 .../Features/Common/ValueObjects/HealthScore.cs    |  1 +
 .../Features/Common/ValueObjects/Percentage.cs     |  1 +
 .../Entities/ClaimEvidenceAssessmentRecord.cs      |  1 +
 .../Evidence/Entities/ClaimEvidenceLink.cs         |  1 +
 .../Entities/EnterpriseEvidenceEntities.cs         |  1 +
 .../Features/Evidence/Entities/EvidenceRecord.cs   |  1 +
 .../Evidence/Entities/P2IntelligenceRecord.cs      |  1 +
 .../Features/Evidence/Entities/ProjectClaim.cs     |  1 +
 .../Evidence/Entities/ProjectTruthSnapshot.cs      |  1 +
 .../Evidence/Entities/QaGovernanceEntities.cs      |  1 +
 .../Evidence/Entities/ReleaseEvidenceBundle.cs     |  1 +
 .../Evidence/Enums/ClaimVerificationStatus.cs      |  1 +
 .../Evidence/Enums/DevelopmentIntelligenceEnums.cs |  1 +
 .../Features/Evidence/Enums/EvidenceConfidence.cs  |  1 +
 .../Evidence/Enums/EvidenceIntegrationEnums.cs     |  1 +
 .../Features/Evidence/Enums/EvidenceOutcome.cs     |  1 +
 .../Features/Evidence/Enums/EvidenceRelation.cs    |  1 +
 .../Features/Evidence/Enums/EvidenceSourceType.cs  |  1 +
 .../Features/Evidence/Enums/P2IntelligenceKind.cs  |  1 +
 .../Evidence/Enums/PlatformIntelligenceEnums.cs    |  1 +
 .../Features/Evidence/Enums/ProjectTruthStatus.cs  |  1 +
 .../Entities/IdentityGovernanceEntities.cs         |  1 +
 .../Identity/Entities/OrganizationTenant.cs        |  1 +
 .../Features/Identity/Entities/PolicyProfile.cs    |  1 +
 .../Identity/Entities/ProjectMembership.cs         |  1 +
 .../Entities/QaGovernanceHardeningEntities.cs      |  1 +
 .../Features/Identity/Enums/ProjectControlEnums.cs |  1 +
 .../Features/Identity/Enums/ProjectRole.cs         |  1 +
 .../Identity/Enums/QaGovernanceSecurityEnums.cs    |  1 +
 .../Integrations/Entities/IntegrationConnection.cs |  1 +
 .../Integrations/Entities/ProcessedWebhook.cs      |  1 +
 .../Operations/Entities/AgentFleetEntities.cs      |  1 +
 .../Features/Operations/Entities/AuditLog.cs       |  1 +
 .../Features/Operations/Entities/DailySummary.cs   |  1 +
 .../Features/Operations/Entities/DurableJob.cs     |  1 +
 .../Operations/Entities/ExternalWorkItemLink.cs    |  1 +
 .../Features/Operations/Entities/Incident.cs       |  1 +
 .../Features/Operations/Entities/IncidentEvent.cs  |  1 +
 .../Operations/Entities/IncidentOccurrence.cs      |  1 +
 .../Operations/Entities/IntelligenceRun.cs         |  1 +
 .../Features/Operations/Entities/Notification.cs   |  1 +
 .../Features/Operations/Entities/OutboxMessage.cs  |  1 +
 .../Features/Operations/Entities/PipelineRun.cs    |  1 +
 .../Entities/ReleaseReadinessSnapshot.cs           |  1 +
 .../Operations/Enums/EnterpriseFleetEnums.cs       |  1 +
 .../Features/Operations/Enums/NotificationType.cs  |  1 +
 .../Features/Operations/Enums/PipelineStatus.cs    |  1 +
 .../Features/Operations/Enums/QaRunStatus.cs       |  1 +
 .../Operations/Events/PipelineCompletedEvent.cs    |  1 +
 .../Features/Production/Entities/QaFinding.cs      |  1 +
 .../Production/Entities/ReproductionAttempt.cs     |  1 +
 .../Production/Enums/ReproductionStatus.cs         |  1 +
 .../Features/Projects/Entities/Commit.cs           |  1 +
 .../Projects/Entities/CrossRepositoryImpact.cs     |  1 +
 .../Features/Projects/Entities/DecisionTrace.cs    |  1 +
 .../Projects/Entities/DevelopmentGraphEdge.cs      |  1 +
 .../Projects/Entities/DevelopmentGraphNode.cs      |  1 +
 .../Features/Projects/Entities/Project.cs          |  1 +
 .../Features/Projects/Entities/ProjectForecast.cs  |  1 +
 .../Features/Projects/Entities/ProjectMember.cs    |  1 +
 .../Projects/Entities/ProjectSourceBinding.cs      |  1 +
 .../Features/Projects/Entities/PullRequest.cs      |  1 +
 .../Features/Projects/Entities/Repository.cs       |  1 +
 .../Projects/Entities/RepositoryDependency.cs      |  1 +
 .../Features/Projects/Entities/RepositoryNode.cs   |  1 +
 .../Features/Projects/Entities/TaskItem.cs         |  1 +
 .../Features/Projects/Enums/IssueStatus.cs         |  1 +
 .../Features/Projects/ValueObjects/CommitSha.cs    |  1 +
 .../Projects/ValueObjects/RepositoryUrl.cs         |  1 +
 .../Quality/Entities/AdaptiveQaPlanSnapshot.cs     |  1 +
 .../Quality/Entities/BenchmarkComparison.cs        |  1 +
 .../Quality/Entities/BenchmarkDefinition.cs        |  1 +
 .../Features/Quality/Entities/BenchmarkMetric.cs   |  1 +
 .../Features/Quality/Entities/BenchmarkRun.cs      |  1 +
 .../Quality/Entities/ChangeRiskAssessment.cs       |  1 +
 .../Quality/Entities/CommunityHardwareSample.cs    |  1 +
 .../Features/Quality/Entities/GameReplayMetric.cs  |  1 +
 .../Features/Quality/Entities/GameReplayRun.cs     |  1 +
 .../Features/Quality/Entities/GameReplaySession.cs |  1 +
 .../Features/Quality/Entities/HardwareBacktest.cs  |  1 +
 .../Quality/Entities/HardwareCalibration.cs        |  1 +
 .../Quality/Entities/HardwareCommunitySample.cs    |  1 +
 .../Features/Quality/Entities/HardwareProfile.cs   |  1 +
 src/Dip.Domain/Features/Quality/Entities/Metric.cs |  1 +
 .../Entities/PerformanceBudgetEvaluation.cs        |  1 +
 .../Quality/Entities/PerformanceBudgetProfile.cs   |  1 +
 .../Quality/Entities/PerformanceSimulationRun.cs   |  1 +
 .../Quality/Entities/QualityAutomationProfile.cs   |  1 +
 .../Features/Quality/Entities/QualityGateResult.cs |  1 +
 .../Features/Quality/Entities/QualityIssue.cs      |  1 +
 .../Features/Quality/Entities/QualityRule.cs       |  1 +
 .../Features/Quality/Entities/RegressionTriage.cs  |  1 +
 .../Quality/Enums/HardwareProfileSource.cs         |  1 +
 .../Quality/Enums/MetricComparisonStatus.cs        |  1 +
 .../Features/Quality/Enums/ProjectRiskLevel.cs     |  1 +
 .../Features/Quality/Enums/QaGovernanceEnums.cs    |  1 +
 .../Quality/Enums/QualityAutomationEnums.cs        |  1 +
 .../Features/Quality/Enums/QualityGateStatus.cs    |  1 +
 src/Dip.Domain/Features/Quality/Enums/RiskLevel.cs |  1 +
 .../Features/Quality/Enums/SimulationStatus.cs     |  1 +
 .../Quality/Events/PullRequestRiskChangedEvent.cs  |  1 +
 .../Quality/Events/QualityGateFailedEvent.cs       |  1 +
 .../Features/Quality/Rules/MetricThresholdRule.cs  |  1 +
 .../Features/Quality/Rules/QualityGateRule.cs      |  1 +
 .../Features/Quality/Rules/RegressionRule.cs       |  1 +
 .../Quality/Services/QualityIssueFingerprint.cs    |  1 +
 .../Features/Quality/ValueObjects/LanguageId.cs    |  1 +
 .../Features/Quality/ValueObjects/MetricValue.cs   |  1 +
 .../Features/Quality/ValueObjects/RiskScore.cs     |  1 +
 src/Dip.Infrastructure/Analysis/AnalysisClient.cs  |  1 +
 src/Dip.Infrastructure/Analysis/AnalysisOptions.cs |  1 +
 .../Analysis/Models/AnalysisRequest.cs             |  1 +
 .../Analysis/Models/AnalysisResponse.cs            |  1 +
 .../Artifacts/ArtifactStoreOptions.cs              |  1 +
 .../Artifacts/LocalArtifactStore.cs                |  1 +
 .../Artifacts/MinioArtifactStore.cs                |  1 +
 .../Authentication/AuthenticationOptions.cs        |  1 +
 .../Authentication/GitHubOAuthService.cs           |  1 +
 .../Authentication/JwtTokenService.cs              |  1 +
 .../Benchmarks/BenchmarkExecutor.cs                |  1 +
 .../Calendars/GoogleCalendarRestConnector.cs       |  1 +
 src/Dip.Infrastructure/DependencyInjection.cs      |  1 +
 .../DependencyInjection.ArtifactTracking.cs        |  7 +-
 .../DependencyInjection.EnterpriseFleet.cs         |  1 +
 .../DependencyInjection.EvidenceIntegration.cs     |  7 +-
 .../DependencyInjection.GitHub.cs                  |  3 +-
 .../DependencyInjection.ProductionTools.cs         |  7 +-
 .../DependencyInjection.QualityAutomation.cs       |  5 +-
 .../ExternalEventIngestionOptions.cs               |  1 +
 .../OpenTelemetryIncidentIngestionAdapter.cs       |  1 +
 .../SentryIncidentIngestionAdapter.cs              |  1 +
 src/Dip.Infrastructure/GitHub/GitHubClient.cs      |  1 +
 .../GitHub/GitHubClientFactory.cs                  |  1 +
 src/Dip.Infrastructure/GitHub/GitHubOptions.cs     |  1 +
 .../GitHub/GitHubPullRequestCommentService.cs      |  1 +
 .../GitHub/GitHubRepositoryCatalog.cs              |  1 +
 .../GitHub/GitHubWebhookParser.cs                  |  1 +
 .../GitHub/GitHubWebhookVerifier.cs                |  1 +
 .../GitHub/Models/GitHubIssueModel.cs              |  1 +
 .../GitHub/Models/GitHubPullRequestModel.cs        |  1 +
 .../GitHub/Models/GitHubWorkflowRunModel.cs        |  1 +
 .../Hardware/HardwareCalibrationService.cs         |  1 +
 .../Hardware/HardwareConstraintRunner.cs           |  1 +
 .../Hardware/PerformanceSimulationService.cs       |  1 +
 .../Integrations/ExternalIntegrationOptions.cs     |  1 +
 .../Integrations/GitHub/GitHubConnector.cs         |  3 +-
 .../Integrations/GitHubVersionControlConnector.cs  |  1 +
 .../Integrations/JenkinsCiConnector.cs             |  1 +
 .../PerforceVersionControlConnector.cs             |  1 +
 src/Dip.Infrastructure/Intelligence/FuzzRunner.cs  |  1 +
 .../Intelligence/IntelligenceExecutionSafety.cs    |  1 +
 .../Intelligence/MediaRegressionRunner.cs          |  1 +
 .../Intelligence/MinimumHardwareRunner.cs          |  1 +
 .../Intelligence/MutationRunner.cs                 |  1 +
 .../Intelligence/RegressionRunner.cs               |  1 +
 .../Intelligence/ReliabilityRunner.cs              |  1 +
 .../EmailManagementNotificationChannel.cs          |  1 +
 .../Management/GitHubManagementSyncAdapter.cs      |  1 +
 .../Management/JiraManagementSyncAdapter.cs        |  1 +
 .../Management/LinearManagementSyncAdapter.cs      |  1 +
 .../Management/ManagementNotificationOptions.cs    |  1 +
 .../SlackManagementNotificationChannel.cs          |  1 +
 .../TeamsManagementNotificationChannel.cs          |  1 +
 .../Messaging/PostgresQueueClient.cs               |  1 +
 src/Dip.Infrastructure/Messaging/QueueOptions.cs   |  1 +
 .../Notifications/EmailNotificationClient.cs       |  1 +
 .../GitHubCommentNotificationClient.cs             |  1 +
 .../Notifications/NotificationOptions.cs           |  1 +
 .../Notifications/SlackNotificationClient.cs       |  1 +
 .../Observability/DipOperationalMetrics.cs         |  1 +
 .../Observability/LoggingExtensions.cs             |  1 +
 .../Observability/MetricsRegistry.cs               |  1 +
 .../Observability/OpenTelemetryExtensions.cs       |  1 +
 .../P2Intelligence/DeterministicLocalDebugAgent.cs |  1 +
 .../P2Intelligence/MediaRegressionRunner.cs        |  1 +
 .../P2Intelligence/NativeSymbolicator.cs           |  1 +
 .../OpenAiCompatibleFixProposalAgent.cs            |  1 +
 .../P2Intelligence/P2ExperimentRunner.cs           |  1 +
 .../P2Intelligence/TestImpactAnalyzer.cs           |  1 +
 .../AdaptiveQaPlanSnapshotConfiguration.cs         |  1 +
 .../ArtifactTrackingConfigurations.cs              |  1 +
 .../Configurations/AuditLogConfiguration.cs        |  1 +
 .../ClaimEvidenceLinkConfiguration.cs              |  1 +
 .../Persistence/Configurations/Configurations.cs   |  1 +
 .../Configurations/DecisionTraceConfiguration.cs   |  1 +
 .../DevelopmentGraphConfigurations.cs              |  1 +
 .../DevelopmentIntelligenceConfigurations.cs       |  1 +
 .../EnterpriseFleetConfigurations.cs               |  1 +
 .../EvidenceIntegrationConfigurations.cs           |  1 +
 .../Configurations/EvidenceRecordConfiguration.cs  |  1 +
 .../HardwareCalibrationConfiguration.cs            |  1 +
 .../HardwareCommunitySampleConfiguration.cs        |  1 +
 .../Configurations/HardwareProfileConfiguration.cs |  1 +
 .../Configurations/LegacyCoreConfigurations.cs     |  1 +
 .../Configurations/MetricConfiguration.cs          |  1 +
 .../Configurations/NotificationConfiguration.cs    |  1 +
 .../Configurations/OutboxMessageConfiguration.cs   |  1 +
 .../Persistence/Configurations/P2Configurations.cs |  1 +
 .../P2IntelligenceRecordConfiguration.cs           |  1 +
 .../PerformanceSimulationRunConfiguration.cs       |  1 +
 .../Configurations/PipelineRunConfiguration.cs     |  1 +
 .../PlatformIntelligenceConfigurations.cs          |  1 +
 .../ProcessedWebhookConfiguration.cs               |  1 +
 .../Configurations/ProjectClaimConfiguration.cs    |  1 +
 .../Configurations/ProjectConfiguration.cs         |  1 +
 .../ProjectTruthSnapshotConfiguration.cs           |  1 +
 .../Configurations/PullRequestConfiguration.cs     |  1 +
 .../Configurations/QaFindingConfiguration.cs       |  1 +
 .../Configurations/QaGovernanceConfigurations.cs   |  1 +
 .../QaGovernanceHardeningConfigurations.cs         |  1 +
 .../Configurations/QaRunConfiguration.cs           |  1 +
 .../QualityAutomationConfigurations.cs             |  1 +
 .../QualityGateResultConfiguration.cs              |  1 +
 .../Configurations/QualityIssueConfiguration.cs    |  1 +
 .../ReproductionAttemptConfiguration.cs            |  1 +
 .../Persistence/DbInitializer.cs                   |  1 +
 src/Dip.Infrastructure/Persistence/DipDbContext.cs |  1 +
 .../Persistence/DipDbContextFactory.cs             |  1 +
 .../Persistence/LegacyMigrationAdopter.cs          |  1 +
 .../Persistence/P0SchemaBootstrapper.cs            |  1 +
 .../Persistence/ProjectMutationCoordinator.cs      |  1 +
 .../Persistence/ProjectResourceProjectResolver.cs  |  1 +
 .../QaGovernanceExecutionCoordinator.cs            |  1 +
 .../Persistence/Repositories/AuditLogRepository.cs |  1 +
 .../Repositories/ClaimEvidenceLinkRepository.cs    |  1 +
 .../Repositories/DashboardReadRepository.cs        |  1 +
 .../Repositories/DevelopmentGraphRepository.cs     |  1 +
 .../DevelopmentIntelligenceRepositories.cs         |  1 +
 .../Repositories/EnterpriseFleetRepositories.cs    |  1 +
 .../EvidenceIntegrationRepositories.cs             |  1 +
 .../Persistence/Repositories/EvidenceRepository.cs |  1 +
 .../Persistence/Repositories/MetricRepository.cs   |  1 +
 .../Persistence/Repositories/OutboxRepository.cs   |  1 +
 .../Persistence/Repositories/P1Repositories.cs     |  1 +
 .../Persistence/Repositories/P2LegacyStores.cs     |  1 +
 .../Persistence/Repositories/P2Repositories.cs     |  1 +
 .../Repositories/PipelineRunRepository.cs          |  1 +
 .../PlatformIntelligenceRepositories.cs            |  1 +
 .../Repositories/ProcessedWebhookRepository.cs     |  1 +
 .../Repositories/ProjectClaimRepository.cs         |  1 +
 .../Repositories/ProjectMembershipRepository.cs    |  1 +
 .../Persistence/Repositories/ProjectRepository.cs  |  1 +
 .../Repositories/ProjectTruthRepository.cs         |  1 +
 .../Repositories/QaGovernanceRepository.cs         |  1 +
 .../Repositories/QualityAutomationRepositories.cs  |  1 +
 .../Repositories/QualityIssueRepository.cs         |  1 +
 .../Persistence/Repositories/Repositories.cs       |  1 +
 .../Repositories/TrackedArtifactRepository.cs      |  1 +
 .../CollaborationWebhookVerifier.cs                |  1 +
 .../ProjectIntelligenceSourceOptions.cs            |  1 +
 .../RedmineProjectSourceAdapter.cs                 |  1 +
 .../SlackProjectSourceAdapter.cs                   |  1 +
 .../Provenance/ReleaseProvenanceCollector.cs       |  1 +
 .../Provenance/ReleaseProvenanceOptions.cs         |  1 +
 .../QualityAutomation/CSharpQaEngine.cs            |  1 +
 .../CodeOwnersOwnershipResolver.cs                 |  1 +
 .../QualityAutomation/CppQaEngine.cs               |  1 +
 .../QualityAutomation/DeepLanguagePackQaAdapter.cs |  1 +
 .../EnvironmentSnapshotService.cs                  |  1 +
 .../QualityAutomation/GameProjectQaPack.cs         |  1 +
 .../QualityAutomation/GenericCommandQaAdapter.cs   |  1 +
 .../QualityAutomation/IdeDiagnosticNormalizer.cs   |  1 +
 .../QualityAutomation/ManifestQaAdapter.cs         |  1 +
 .../QualityAutomation/ProcessExecutor.cs           |  1 +
 .../QualityAutomation/PythonQaEngine.cs            |  1 +
 .../QualityAutomation/QaAdapterRegistry.cs         |  1 +
 .../QualityAutomation/QaBugMinimizer.cs            |  1 +
 .../QualityAutomation/QaCapabilityPlanner.cs       |  1 +
 .../QualityAutomation/QaExecutionOptions.cs        |  1 +
 .../QualityAutomation/QaParsing.cs                 |  1 +
 .../QualityAutomation/QaToolSupport.cs             |  1 +
 .../QualityAutomation/QaWorkspaceResolver.cs       |  1 +
 .../RepositoryTechnologyDetector.cs                |  1 +
 .../QualityAutomation/ReproductionCoordinator.cs   |  1 +
 .../QualityAutomation/SystemIntegrationQaPack.cs   |  1 +
 .../QualityAutomation/UnityProjectQaAdapter.cs     |  1 +
 .../UniversalDiagnosticNormalizer.cs               |  1 +
 .../QualityAutomation/UniversalSemanticAnalyzer.cs |  1 +
 .../QualityAutomation/UniversalSyntaxTree.cs       |  1 +
 .../QualityAutomation/VerifiedFixService.cs        |  1 +
 .../Queue/QueueMessageSerializer.cs                |  1 +
 src/Dip.Infrastructure/Queue/QueueOptions.cs       |  1 +
 src/Dip.Infrastructure/Queue/RedisQueueClient.cs   |  1 +
 .../Replay/GameReplayExecutor.cs                   |  1 +
 src/Dip.Infrastructure/Replay/GameReplayOptions.cs |  1 +
 .../Resilience/CircuitBreakerPolicies.cs           |  1 +
 src/Dip.Infrastructure/Resilience/RetryPolicies.cs |  1 +
 .../EnterpriseIntegrationCredentialStore.cs        |  1 +
 .../Security/LocalIntegrationCredentialStore.cs    |  1 +
 .../Spreadsheets/GoogleSheetsRestConnector.cs      |  1 +
 .../Storage/LocalObjectStorage.cs                  |  1 +
 .../Storage/MinioObjectStorage.cs                  |  1 +
 src/Dip.Infrastructure/Storage/StorageOptions.cs   |  1 +
 src/Dip.Infrastructure/Time/SystemClock.cs         |  1 +
 .../WorkItems/GitHubWorkItemConnector.cs           |  1 +
 .../WorkItems/JiraWorkItemConnector.cs             |  1 +
 .../WorkItems/LinearWorkItemConnector.cs           |  1 +
 .../WorkItems/WorkItemIntegrationOptions.cs        |  1 +
 src/Dip.Shared/Constants/HeaderNames.cs            |  1 +
 src/Dip.Shared/Constants/MetricNames.cs            |  1 +
 src/Dip.Shared/Constants/QueueNames.cs             |  1 +
 src/Dip.Shared/Contracts/Api/ApiError.cs           |  1 +
 src/Dip.Shared/Contracts/Api/HealthResponse.cs     |  1 +
 src/Dip.Shared/Contracts/Api/PagedResponse.cs      |  1 +
 .../Contracts/Collectors/CollectorArtifact.cs      |  1 +
 .../Contracts/Collectors/CollectorIssue.cs         |  1 +
 .../Contracts/Collectors/CollectorMetric.cs        |  1 +
 .../Contracts/Collectors/CollectorReport.cs        |  1 +
 .../Contracts/Collectors/CollectorRunStatus.cs     |  1 +
 .../Contracts/Events/AnalysisRequested.cs          |  1 +
 .../Contracts/Events/GitHubWebhookReceived.cs      |  1 +
 .../Contracts/Events/NotificationRequested.cs      |  1 +
 .../Contracts/Events/PipelineCompletedMessage.cs   |  1 +
 .../Contracts/Events/QualityGateFailedMessage.cs   |  1 +
 src/Dip.Shared/Contracts/Events/QueueMessage.cs    |  1 +
 src/Dip.Shared/Serialization/SharedJsonOptions.cs  |  1 +
 src/Dip.Web/GlobalUsings.cs                        |  1 +
 .../Pages/Dashboard/ManagementOperations.razor.cs  |  1 +
 src/Dip.Web/Program.cs                             |  1 +
 src/Dip.Web/Services/ApiAuthenticationHandler.cs   |  1 +
 src/Dip.Web/Services/ApiClient.cs                  |  1 +
 .../Services/ApiClientAdditionalOperations.cs      |  1 +
 src/Dip.Web/Services/AuthenticationService.cs      |  1 +
 src/Dip.Web/Services/OnboardingStateService.cs     |  1 +
 src/Dip.Web/Services/ProjectDashboardService.cs    |  1 +
 .../Services/ProjectOperationFlowResolver.cs       |  1 +
 src/Dip.Web/Services/RealtimeUpdateService.cs      |  1 +
 src/Dip.Web/State/ProjectState.cs                  |  1 +
 src/Dip.Web/State/UserState.cs                     |  1 +
 src/Dip.Web/wwwroot/js/charts.js                   |  1 +
 src/Dip.Web/wwwroot/js/onboarding.js               |  1 +
 src/Dip.Worker/Handlers/CollectorReportHandler.cs  |  1 +
 .../Handlers/PullRequestWebhookHandler.cs          |  1 +
 .../Handlers/QualityGateResultHandler.cs           |  1 +
 .../Handlers/WorkflowRunWebhookHandler.cs          |  1 +
 src/Dip.Worker/Options/WorkerOptions.cs            |  1 +
 src/Dip.Worker/Program.cs                          |  1 +
 src/Dip.Worker/Scheduling/DailySummaryScheduler.cs |  1 +
 .../Scheduling/HealthRecalculationScheduler.cs     |  1 +
 src/Dip.Worker/Workers/AnalysisRequestWorker.cs    |  1 +
 src/Dip.Worker/Workers/ArtifactRetentionWorker.cs  |  1 +
 .../Workers/AutomaticRegressionTriageWorker.cs     |  1 +
 .../Workers/CollectorReportProcessedWorker.cs      |  1 +
 src/Dip.Worker/Workers/DailySummaryWorker.cs       |  1 +
 src/Dip.Worker/Workers/DeadLetterWorker.cs         |  1 +
 src/Dip.Worker/Workers/DurableJobHandlers.cs       |  1 +
 src/Dip.Worker/Workers/DurableJobRecoveryWorker.cs |  1 +
 src/Dip.Worker/Workers/DurableJobWorker.cs         |  1 +
 src/Dip.Worker/Workers/GitHubChangeRiskWorker.cs   |  1 +
 src/Dip.Worker/Workers/GitHubWebhookWorker.cs      |  1 +
 .../Workers/HealthRecalculationWorker.cs           |  1 +
 .../Workers/ManagementNotificationWorker.cs        |  1 +
 src/Dip.Worker/Workers/NotificationWorker.cs       |  1 +
 src/Dip.Worker/Workers/OutboxWorker.cs             |  1 +
 src/Dip.Worker/Workers/PipelineProcessingWorker.cs |  1 +
 src/Dip.Worker/Workers/QualityEvaluationWorker.cs  |  1 +
 src/Dip.Worker/Workers/WorkerMetricsEndpoint.cs    |  1 +
 tests/Dip.Agent.Core.Tests/MediaSpoolTests.cs      |  1 +
 .../ProjectFileScannerTests.cs                     |  1 +
 .../SqliteAgentStateStoreTests.cs                  |  1 +
 tests/Dip.Agent.Core.Tests/ToolEventOutboxTests.cs |  1 +
 .../CollectorReportValidatorTests.cs               |  1 +
 tests/Dip.Application.Tests/DashboardTests.cs      |  1 +
 .../DifficultyFiveFoundationTests.cs               |  1 +
 .../DifficultyOne/EvidenceRecordV2Tests.cs         | 63 ---------------
 .../DifficultyOne/IntegrationConnectionTests.cs    | 34 --------
 .../DifficultyOne/PolicyResolverTests.cs           | 94 ----------------------
 .../DifficultyTwo/TrackedArtifactTests.cs          | 26 ------
 .../GitHubQualityReportFormatterTests.cs           |  1 +
 .../Intelligence/P2PureLogicTests.cs               |  1 +
 .../ManagementOperationsContractTests.cs           |  1 +
 tests/Dip.Application.Tests/OutboxWriterTests.cs   |  1 +
 .../P2Intelligence/AdaptiveQaPlannerTests.cs       |  1 +
 .../P2Intelligence/EvidencePolicyTests.cs          |  1 +
 .../P2Intelligence/GameRegressionEvaluatorTests.cs |  1 +
 .../ProductionToolEventMappingTests.cs             |  3 +-
 .../ProjectIntelligenceContractsTests.cs           |  1 +
 .../ProjectPlanUnderstandingEngineTests.cs         |  1 +
 .../AdaptiveProjectRiskEvaluatorTests.cs           |  1 +
 .../ClaimVerificationEvaluatorTests.cs             |  1 +
 .../ProjectTruth/PmDecisionTranslatorTests.cs      |  1 +
 .../ProjectTruth/PmEvidenceTranslatorTests.cs      |  1 +
 .../ProjectTruth/ProgressEvaluatorTests.cs         |  1 +
 .../ProjectTruth/ProjectTruthEvaluatorTests.cs     |  1 +
 .../ProjectTruth/ProjectTruthPolicyTests.cs        |  1 +
 .../Projects/ProjectServiceTests.cs                |  1 +
 .../Quality/QualityGateEvaluatorTests.cs           |  1 +
 .../Quality/QualityRuleEvaluatorTests.cs           |  1 +
 .../QualityAutomation/AssetQaRuleEngineTests.cs    |  3 +-
 .../QualityAutomation/BranchQaPolicyTests.cs       |  1 +
 .../ClaimEvidenceAssessmentScoringTests.cs         |  3 +-
 .../QualityAutomation/CommercialQaPolicyTests.cs   |  1 +
 .../GitBranchQaWorkspaceManagerTests.cs            |  1 +
 .../ProductionQaIntelligenceTests.cs               |  1 +
 .../QualityAutomation/QaFingerprintTests.cs        |  1 +
 .../QualityAutomation/VisualComparatorTests.cs     |  3 +-
 tests/Dip.Domain.Tests/DevelopmentGraphTests.cs    |  1 +
 .../DevelopmentIntelligenceEntitiesTests.cs        |  1 +
 tests/Dip.Domain.Tests/Entities/MetricTests.cs     |  1 +
 .../Dip.Domain.Tests/Entities/PipelineRunTests.cs  |  1 +
 tests/Dip.Domain.Tests/EvidenceRecordTests.cs      |  1 +
 tests/Dip.Domain.Tests/GlobalUsings.cs             |  1 +
 tests/Dip.Domain.Tests/OutboxMessageTests.cs       |  1 +
 tests/Dip.Domain.Tests/P1EntitiesTests.cs          |  1 +
 tests/Dip.Domain.Tests/P2EntitiesTests.cs          |  1 +
 tests/Dip.Domain.Tests/PipelineRunTests.cs         |  1 +
 .../PlatformIntelligence/DurableJobTests.cs        |  1 +
 .../PlatformIntelligence/IncidentTests.cs          |  1 +
 tests/Dip.Domain.Tests/ProcessedWebhookTests.cs    |  1 +
 tests/Dip.Domain.Tests/ProjectClaimTests.cs        |  1 +
 .../QualityAutomation/LanguageIdTests.cs           |  1 +
 .../QualityGovernance/QaGovernanceEntitiesTests.cs |  1 +
 tests/Dip.Domain.Tests/QualityIssueTests.cs        |  1 +
 .../ValueObjects/CommitShaTests.cs                 |  1 +
 .../ValueObjects/HealthScoreTests.cs               |  1 +
 .../Scenarios/CollectorPipelineScenario.cs         |  1 +
 .../Scenarios/DailySummaryScenario.cs              |  1 +
 .../Scenarios/GameDevelopmentEvidenceScenario.cs   |  1 +
 .../Scenarios/P1QaEvidenceScenario.cs              |  1 +
 .../Scenarios/P2DecisionIntelligenceScenario.cs    |  1 +
 .../Scenarios/P2IntelligenceScenario.cs            |  1 +
 .../Scenarios/ProjectTruthScenario.cs              |  1 +
 .../Scenarios/PullRequestPipelineScenario.cs       |  1 +
 .../Scenarios/QualityGateFailureScenario.cs        |  1 +
 .../Dip.EndToEndTests/Support/DipScenarioClient.cs |  1 +
 .../Artifacts/LocalArtifactStoreTests.cs           |  1 +
 .../ReplayAndProvenanceSafetyTests.cs              |  1 +
 .../GitHubWebhookVerifierTests.cs                  |  1 +
 .../IntelligenceExecutionSafetyTests.cs            |  1 +
 .../DeterministicLocalDebugAgentTests.cs           |  1 +
 .../P2Intelligence/TestImpactAnalyzerTests.cs      |  1 +
 .../Persistence/GovernanceHardeningTests.cs        |  1 +
 .../Persistence/MigrationMetadataTests.cs          |  1 +
 .../Persistence/ProjectRepositoryTests.cs          |  1 +
 .../IdeDiagnosticNormalizerTests.cs                |  1 +
 .../ProcessExecutorSandboxTests.cs                 |  1 +
 .../QualityAutomation/QaAdapterRegistryTests.cs    |  1 +
 .../QualityAutomation/QaCapabilityPlannerTests.cs  |  1 +
 .../RepositoryTechnologyDetectorTests.cs           |  1 +
 .../UniversalDiagnosticNormalizerTests.cs          |  1 +
 .../UniversalSemanticAnalyzerTests.cs              |  1 +
 .../QualityAutomation/UniversalSyntaxTreeTests.cs  |  1 +
 .../Api/CustomWebApplicationFactory.cs             |  1 +
 tests/Dip.IntegrationTests/Api/ProjectsApiTests.cs |  1 +
 .../Database/PostgreSqlFixture.cs                  |  1 +
 .../GovernanceAuthorizationSurfaceTests.cs         |  1 +
 ...anagementOperationsAuthorizationSurfaceTests.cs |  1 +
 ...ProjectIntelligenceAuthorizationSurfaceTests.cs |  1 +
 tests/load/k6/dashboard-load.js                    |  1 +
 tests/load/k6/pipeline-load.js                     |  1 +
 tests/load/k6/webhook-load.js                      |  1 +
 842 files changed, 920 insertions(+), 255 deletions(-)
```

## Git status

```text
 M .editorconfig
 M scripts/Backup-Dip.ps1
 M scripts/Bootstrap-ProjectMemberships.ps1
 M scripts/Build-NativeTools.ps1
 M scripts/Complete-PortfolioRelease.ps1
RM scripts/Generate-Difficulty2-Migration.ps1 -> scripts/Generate-ArtifactTracking-Migration.ps1
RM scripts/Generate-Difficulty5-Migration.ps1 -> scripts/Generate-EnterpriseFleet-Migration.ps1
RM scripts/Generate-Difficulty1-Migration.ps1 -> scripts/Generate-EvidenceIntegration-Migration.ps1
RM scripts/Generate-Difficulty3-Migration.ps1 -> scripts/Generate-ProductionTools-Migration.ps1
RM scripts/Generate-Difficulty4-Migration.ps1 -> scripts/Generate-QualityAutomation-Migration.ps1
 M scripts/Initialize-SelfHostedPostgres.ps1
 M scripts/Invoke-ManualCI.ps1
 M scripts/Invoke-QaBugZoo.ps1
 M scripts/New-SelfHostedFallbackEvidence.ps1
 M scripts/Publish-Portfolio.ps1
 M scripts/Publish-PortfolioRelease.ps1
 M scripts/Request-FinalVerification.ps1
 M scripts/Restore-Dip.ps1
 M scripts/Run-DIP-Agent.ps1
 M scripts/Run-GovernanceLoadTest.ps1
 M scripts/Run-PortfolioBenchmarks.ps1
 M scripts/Run-UniversalQaBenchmark.ps1
 M scripts/Set-DIP-ProductionBridgeEnvironment.ps1
 M scripts/Start-DIP-Desktop.ps1
 M scripts/Start-DIP.ps1
 M scripts/Stop-DIP.ps1
 M scripts/Test-P1-Capture.ps1
 M scripts/Verify-ActionsPolicy.ps1
RM scripts/Verify-Difficulty2-Local.ps1 -> scripts/Verify-ArtifactTracking-Local.ps1
 M scripts/Verify-DatabaseMigrations.ps1
 M scripts/Verify-DisasterRecovery.ps1
RM scripts/Verify-Difficulty5-Local.ps1 -> scripts/Verify-EnterpriseFleet-Local.ps1
RM scripts/Verify-Difficulty1-Local.ps1 -> scripts/Verify-EvidenceIntegration-Local.ps1
 M scripts/Verify-GitHubCiPreflight.ps1
 M scripts/Verify-GuiPhase1.ps1
 M scripts/Verify-GuiPhase1Static.ps1
 M scripts/Verify-GuiRuntime.ps1
 M scripts/Verify-P1.ps1
 M scripts/Verify-P2.ps1
 M scripts/Verify-PortfolioFormal.ps1
 M scripts/Verify-PortfolioReadiness.ps1
RM scripts/Verify-Difficulty3-Local.ps1 -> scripts/Verify-ProductionTools-Local.ps1
 M scripts/Verify-ProjectControl.ps1
 M scripts/Verify-QaGovernance.ps1
RM scripts/Verify-Difficulty4-Local.ps1 -> scripts/Verify-QualityAutomation-Local.ps1
 M scripts/Verify-SelfHostedWindowsPreflight.ps1
 M scripts/Verify-SelfHostedWindowsRuntime.ps1
 M scripts/Verify-Solution.ps1
 M scripts/Verify-UnityCollector.ps1
 M scripts/Verify-UniversalQa.ps1
 M scripts/build-all.ps1
 M scripts/format-all.ps1
 M scripts/generate-sample-data.ps1
 M scripts/package-release.ps1
 M scripts/setup-development.ps1
 M scripts/test-all.ps1
 M scripts/validate-schemas.ps1
 M scripts/verify-environment.ps1
 M src/Dip.Agent.Core/AgentApiClient.cs
 M src/Dip.Agent.Core/AgentModels.cs
 M src/Dip.Agent.Core/AgentOptions.cs
 M src/Dip.Agent.Core/AgentRuntime.cs
 M src/Dip.Agent.Core/EnterpriseAgentClient.cs
 M src/Dip.Agent.Core/MediaSpool.cs
 M src/Dip.Agent.Core/ProjectFileScanner.cs
 M src/Dip.Agent.Core/SqliteAgentStateStore.cs
 M src/Dip.Agent.Core/ToolEventModels.cs
 M src/Dip.Agent.Windows/AgentConfiguration.cs
 M src/Dip.Agent.Windows/CollectorWorker.cs
 M src/Dip.Agent.Windows/FleetWorker.cs
 M src/Dip.Agent.Windows/Program.cs
 M src/Dip.Agent.Windows/ToolBridgeWorker.cs
 M src/Dip.Api/Authorization/Policies.cs
 M src/Dip.Api/Authorization/ProductionSecurityGuard.cs
 M src/Dip.Api/Authorization/ProjectScopeAuthorization.cs
 M src/Dip.Api/Controllers/AgentPolicyDistributionController.cs
 M src/Dip.Api/Controllers/ArtifactsController.cs
 M src/Dip.Api/Controllers/AuthController.cs
 M src/Dip.Api/Controllers/BenchmarksController.cs
 M src/Dip.Api/Controllers/CapabilitiesController.cs
 M src/Dip.Api/Controllers/ChangeRiskController.cs
 M src/Dip.Api/Controllers/ClaimEvidenceAssessmentsController.cs
 M src/Dip.Api/Controllers/CollaborationEvidenceWebhooksController.cs
 M src/Dip.Api/Controllers/CollectorsController.cs
 M src/Dip.Api/Controllers/ConnectionsController.cs
 M src/Dip.Api/Controllers/DashboardController.cs
 M src/Dip.Api/Controllers/DefectIntelligenceController.cs
 M src/Dip.Api/Controllers/EnterpriseFleetController.cs
 M src/Dip.Api/Controllers/EnterpriseGovernanceController.cs
 M src/Dip.Api/Controllers/EvidenceIngestionController.cs
 M src/Dip.Api/Controllers/ExternalEventsController.cs
 M src/Dip.Api/Controllers/GameReplayController.cs
 M src/Dip.Api/Controllers/GitHubRepositoriesController.cs
 M src/Dip.Api/Controllers/HardwareBacktestsController.cs
 M src/Dip.Api/Controllers/HardwareController.cs
 M src/Dip.Api/Controllers/IncidentsController.cs
 M src/Dip.Api/Controllers/IntelligenceController.cs
 M src/Dip.Api/Controllers/IntelligenceDashboardController.cs
 M src/Dip.Api/Controllers/JobsController.cs
 M src/Dip.Api/Controllers/LocalCollectorController.cs
 M src/Dip.Api/Controllers/ManagedConnectionProjectsController.cs
 M src/Dip.Api/Controllers/ManagementOperationsController.cs
 M src/Dip.Api/Controllers/MetricsController.cs
 M src/Dip.Api/Controllers/NotificationsController.cs
 M src/Dip.Api/Controllers/P2IntelligenceController.cs
 M src/Dip.Api/Controllers/P2JobsController.cs
 M src/Dip.Api/Controllers/PerformanceBudgetsController.cs
 M src/Dip.Api/Controllers/PipelineRunsController.cs
 M src/Dip.Api/Controllers/PolicyProfilesController.cs
 M src/Dip.Api/Controllers/PortfolioManagementController.cs
 M src/Dip.Api/Controllers/ProducerIntelligenceController.cs
 M src/Dip.Api/Controllers/ProductionToolsController.cs
 M src/Dip.Api/Controllers/ProjectClaimsController.cs
 M src/Dip.Api/Controllers/ProjectControlController.cs
 M src/Dip.Api/Controllers/ProjectIntelligenceSourcesController.cs
 M src/Dip.Api/Controllers/ProjectManagementCommandCenterController.cs
 M src/Dip.Api/Controllers/ProjectMembershipsController.cs
 M src/Dip.Api/Controllers/ProjectSourceBindingsController.cs
 M src/Dip.Api/Controllers/ProjectsController.cs
 M src/Dip.Api/Controllers/QaArtifactsController.cs
 M src/Dip.Api/Controllers/QaController.cs
 M src/Dip.Api/Controllers/QaGovernanceController.cs
 M src/Dip.Api/Controllers/QualityAutomationController.cs
 M src/Dip.Api/Controllers/RegressionTriageController.cs
 M src/Dip.Api/Controllers/ReleaseEvidenceController.cs
 M src/Dip.Api/Controllers/ReleaseReadinessController.cs
 M src/Dip.Api/Controllers/RepositoryGraphController.cs
 M src/Dip.Api/Controllers/SystemStatusController.cs
 M src/Dip.Api/Controllers/TrackedArtifactsController.cs
 M src/Dip.Api/Controllers/WebhooksController.cs
 M src/Dip.Api/Controllers/WorkItemsController.cs
 M src/Dip.Api/Extensions/ApplicationBuilderExtensions.cs
 M src/Dip.Api/Extensions/ServiceCollectionExtensions.cs
 M src/Dip.Api/Extensions/SwaggerExtensions.cs
 M src/Dip.Api/Filters/ValidationFilter.cs
 M src/Dip.Api/GlobalUsings.cs
 M src/Dip.Api/Hubs/ProjectEventsHub.cs
 M src/Dip.Api/Middleware/CorrelationIdMiddleware.cs
 M src/Dip.Api/Middleware/ExceptionHandlingMiddleware.cs
 M src/Dip.Api/Middleware/RateLimitMiddleware.cs
 M src/Dip.Api/Middleware/RawBodyBufferingExtensions.cs
 M src/Dip.Api/Middleware/RequestLoggingMiddleware.cs
 M src/Dip.Api/Program.cs
 M src/Dip.Application/Abstractions/Artifacts/IArtifactStore.cs
 M src/Dip.Application/Abstractions/Calendars/IExternalCalendarConnector.cs
 M src/Dip.Application/Abstractions/External/IAnalysisClient.cs
 M src/Dip.Application/Abstractions/External/IGitHubClient.cs
 M src/Dip.Application/Abstractions/External/INotificationClient.cs
 M src/Dip.Application/Abstractions/External/IObjectStorage.cs
 M src/Dip.Application/Abstractions/External/IQueueClient.cs
 M src/Dip.Application/Abstractions/GitHub/IGitHubPullRequestCommentService.cs
 M src/Dip.Application/Abstractions/GitHub/IGitHubRepositoryCatalog.cs
 M src/Dip.Application/Abstractions/GitHub/IGitHubWebhookVerifier.cs
 M src/Dip.Application/Abstractions/Integrations/ProjectControlIntegrations.cs
 M src/Dip.Application/Abstractions/Messaging/IQueueClient.cs
 M src/Dip.Application/Abstractions/Observability/IDipOperationalMetrics.cs
RM src/Dip.Application/Abstractions/Persistence/IDifficultyFiveRepositories.cs -> src/Dip.Application/Abstractions/Persistence/IEnterpriseFleetRepositories.cs
RM src/Dip.Application/Abstractions/Persistence/IDifficultyOneRepositories.cs -> src/Dip.Application/Abstractions/Persistence/IEvidenceIntegrationRepositories.cs
 M src/Dip.Application/Abstractions/Persistence/IMetricRepository.cs
 M src/Dip.Application/Abstractions/Persistence/IOutboxRepository.cs
 M src/Dip.Application/Abstractions/Persistence/IPipelineRunRepository.cs
 M src/Dip.Application/Abstractions/Persistence/IProcessedWebhookRepository.cs
 M src/Dip.Application/Abstractions/Persistence/IProjectRepository.cs
RM src/Dip.Application/Abstractions/Persistence/IDifficultyFourRepositories.cs -> src/Dip.Application/Abstractions/Persistence/IQualityAutomationRepositories.cs
 M src/Dip.Application/Abstractions/Persistence/IQualityIssueRepository.cs
 M src/Dip.Application/Abstractions/Persistence/IQualityRuleRepository.cs
RM src/Dip.Application/Abstractions/Persistence/IDifficultyTwoRepositories.cs -> src/Dip.Application/Abstractions/Persistence/ITrackedArtifactRepository.cs
 M src/Dip.Application/Abstractions/Persistence/IUnitOfWork.cs
 M src/Dip.Application/Abstractions/Persistence/Repositories.cs
 M src/Dip.Application/Abstractions/Provenance/IReleaseProvenanceCollector.cs
 M src/Dip.Application/Abstractions/Replay/IGameReplayExecutor.cs
 M src/Dip.Application/Abstractions/Security/IIntegrationCredentialStore.cs
 M src/Dip.Application/Abstractions/Spreadsheets/IGoogleSheetsConnector.cs
 M src/Dip.Application/Abstractions/Time/ISystemClock.cs
 M src/Dip.Application/Abstractions/WorkItems/IExternalWorkItemConnector.cs
 M src/Dip.Application/DependencyInjection.cs
RM src/Dip.Application/DependencyInjection.GitHub.cs -> src/Dip.Application/DependencyInjection/DependencyInjection.GitHub.cs
 M src/Dip.Application/Features/Analysis/Commands/RequestAnomalyDetectionCommand.cs
 M src/Dip.Application/Features/Analysis/Commands/RequestDailySummaryCommand.cs
 M src/Dip.Application/Features/Analysis/Commands/RequestReleasePredictionCommand.cs
 M src/Dip.Application/Features/Analysis/Dtos/AnomalyResultDto.cs
 M src/Dip.Application/Features/Analysis/Dtos/DailySummaryDto.cs
 M src/Dip.Application/Features/Analysis/Dtos/ReleasePredictionDto.cs
 M src/Dip.Application/Features/Artifacts/ArtifactService.cs
 M src/Dip.Application/Features/Artifacts/TrackedArtifactContracts.cs
 M src/Dip.Application/Features/Artifacts/TrackedArtifactService.cs
 M src/Dip.Application/Features/Assets/AssetIntelligenceService.cs
 M src/Dip.Application/Features/Authorization/IProjectMutationCoordinator.cs
 M src/Dip.Application/Features/Authorization/ProjectAccessService.cs
 M src/Dip.Application/Features/Authorization/ProjectResourceAuthorization.cs
 M src/Dip.Application/Features/Benchmarks/BenchmarkContracts.cs
 M src/Dip.Application/Features/Benchmarks/BenchmarkOrchestrationService.cs
 M src/Dip.Application/Features/Capabilities/CapabilityRegistry.cs
 M src/Dip.Application/Features/ChangeImpact/ChangeImpactAnalysisService.cs
 M src/Dip.Application/Features/ChangeRisk/ChangeRiskIntelligenceService.cs
 M src/Dip.Application/Features/Collectors/CollectorReportService.cs
 M src/Dip.Application/Features/Collectors/CollectorReportValidator.cs
 M src/Dip.Application/Features/Dashboard/DashboardQueryService.cs
 M src/Dip.Application/Features/Dashboard/IntelligenceDashboardService.cs
 M src/Dip.Application/Features/Dashboard/ManagementDashboardService.cs
 M src/Dip.Application/Features/Dashboard/ProjectControlDashboardService.cs
 M src/Dip.Application/Features/Decisions/DecisionLogService.cs
 M src/Dip.Application/Features/DevelopmentGraph/DevelopmentGraphContracts.cs
 M src/Dip.Application/Features/DevelopmentGraph/DevelopmentGraphService.cs
 M src/Dip.Application/Features/DevelopmentGraph/ProductionGraphProjectionService.cs
 M src/Dip.Application/Features/DevelopmentGraph/ProjectControlTestImpactService.cs
 M src/Dip.Application/Features/Enterprise/AdaptiveQaRoutingService.cs
 M src/Dip.Application/Features/Enterprise/AgentPolicyDistributionService.cs
 M src/Dip.Application/Features/Enterprise/DistributedIngestionService.cs
 M src/Dip.Application/Features/Enterprise/EnterpriseTenantService.cs
 M src/Dip.Application/Features/Enterprise/EvidenceIntegrityService.cs
 M src/Dip.Application/Features/Enterprise/IdentityResolutionService.cs
 M src/Dip.Application/Features/Enterprise/RetentionGovernanceService.cs
 M src/Dip.Application/Features/Evidence/EvidenceIngestionContracts.cs
 M src/Dip.Application/Features/Evidence/EvidenceIngestionService.cs
 M src/Dip.Application/Features/Evidence/EvidenceValidator.cs
 M src/Dip.Application/Features/ExternalEvents/ExternalIncidentIngestion.cs
 M src/Dip.Application/Features/FeatureControl/FeatureReadinessService.cs
 M src/Dip.Application/Features/Fleet/AgentFleetService.cs
 M src/Dip.Application/Features/GitHub/GitHubDevelopmentGraphIngestor.cs
 M src/Dip.Application/Features/GitHub/GitHubQualityReportFormatter.cs
 M src/Dip.Application/Features/GitHub/GitHubWebhookProcessor.cs
 M src/Dip.Application/Features/GitHubIntegration/GitHubRepositoryModels.cs
 M src/Dip.Application/Features/Hardware/HardwareBacktestingService.cs
 M src/Dip.Application/Features/Hardware/HardwareContracts.cs
 M src/Dip.Application/Features/Hardware/HardwareOrchestrationService.cs
 M src/Dip.Application/Features/Health/Commands/RecalculateProjectHealthCommand.cs
 M src/Dip.Application/Features/Health/Queries/GetHealthHistoryQuery.cs
 M src/Dip.Application/Features/Health/Queries/GetProjectHealthQuery.cs
 M src/Dip.Application/Features/Health/Services/ProjectHealthCalculator.cs
 M src/Dip.Application/Features/Health/Services/RiskScoreCalculator.cs
 M src/Dip.Application/Features/Incidents/IncidentAggregationService.cs
 M src/Dip.Application/Features/Integrations/ExternalCiIntegrationService.cs
 M src/Dip.Application/Features/Integrations/IIntegrationConnector.cs
 M src/Dip.Application/Features/Integrations/IntegrationConnectionService.cs
 M src/Dip.Application/Features/Integrations/IntegrationConnectorRegistry.cs
 M src/Dip.Application/Features/Integrations/IntegrationContracts.cs
 M src/Dip.Application/Features/Integrations/VersionControlSyncService.cs
 M src/Dip.Application/Features/Intelligence/AdaptiveQaPlanner.cs
 M src/Dip.Application/Features/Intelligence/CommunityHardwareService.cs
 M src/Dip.Application/Features/Intelligence/ForecastAndDecisionService.cs
 M src/Dip.Application/Features/Intelligence/P2Abstractions.cs
 M src/Dip.Application/Features/Intelligence/P2AnalysisService.cs
 M src/Dip.Application/Features/Intelligence/P2Contracts.cs
 M src/Dip.Application/Features/Intelligence/P2OrchestrationService.cs
 M src/Dip.Application/Features/Intelligence/RootCauseIntelligenceService.cs
 M src/Dip.Application/Features/Intelligence/TestStrengthContracts.cs
 M src/Dip.Application/Features/Jobs/DurableJobs.cs
 M src/Dip.Application/Features/Jobs/P2JobEnqueueService.cs
 M src/Dip.Application/Features/Management/ManagementAnalyticsService.cs
 M src/Dip.Application/Features/Management/ManagementContracts.cs
 M src/Dip.Application/Features/Management/ManagementOperationsContracts.cs
 M src/Dip.Application/Features/Management/ManagementOperationsService.cs
 M src/Dip.Application/Features/Management/ManagementSpreadsheetService.cs
 M src/Dip.Application/Features/Management/ManagementSyncService.cs
 M src/Dip.Application/Features/Management/ManagementXlsxCodec.cs
 M src/Dip.Application/Features/Management/PortfolioManagementService.cs
 M src/Dip.Application/Features/Management/ProducerIntelligenceService.cs
 M src/Dip.Application/Features/Management/ProjectManagementCommandCenterService.cs
 M src/Dip.Application/Features/Management/SpreadsheetSchemaInference.cs
 M src/Dip.Application/Features/Management/WorkCalendarIntegrationService.cs
 M src/Dip.Application/Features/Management/WorkManagementService.cs
 M src/Dip.Application/Features/Milestones/MilestoneIntelligenceService.cs
 M src/Dip.Application/Features/Notifications/Commands/RetryNotificationCommand.cs
 M src/Dip.Application/Features/Notifications/Commands/SendNotificationCommand.cs
 M src/Dip.Application/Features/Notifications/Dtos/NotificationDto.cs
RM src/Dip.Application/Services/OutboxWriter.cs -> src/Dip.Application/Features/Operations/Services/OutboxWriter.cs
 M src/Dip.Application/Features/P2Intelligence/AdaptiveQaPlanner.cs
 M src/Dip.Application/Features/P2Intelligence/EvidenceGroundedRootCauseService.cs
 M src/Dip.Application/Features/P2Intelligence/HardwareCommunityIntelligenceService.cs
 M src/Dip.Application/Features/P2Intelligence/P2Contracts.cs
 M src/Dip.Application/Features/P2Intelligence/P2EvidencePolicies.cs
 M src/Dip.Application/Features/P2Intelligence/P2TechnicalIntelligenceService.cs
 M src/Dip.Application/Features/P2Intelligence/ProjectDecisionIntelligenceService.cs
 M src/Dip.Application/Features/Performance/PerformanceBudgetService.cs
 M src/Dip.Application/Features/PipelineRuns/Commands/AddMetricsCommand.cs
 M src/Dip.Application/Features/PipelineRuns/Commands/AddQualityIssuesCommand.cs
 M src/Dip.Application/Features/PipelineRuns/Commands/CompletePipelineRunCommand.cs
 M src/Dip.Application/Features/PipelineRuns/Commands/CreatePipelineRunCommand.cs
 M src/Dip.Application/Features/PipelineRuns/Dtos/PipelineRunDto.cs
 M src/Dip.Application/Features/PipelineRuns/Queries/GetPipelineRunQuery.cs
 M src/Dip.Application/Features/PipelineRuns/Queries/GetPipelineRunsQuery.cs
 M src/Dip.Application/Features/Policies/PolicyContracts.cs
 M src/Dip.Application/Features/Policies/PolicyProfileService.cs
 M src/Dip.Application/Features/Policies/PolicyResolver.cs
 M src/Dip.Application/Features/ProductionTools/ProductionEventQueryService.cs
 M src/Dip.Application/Features/ProductionTools/ProductionMediaService.cs
 M src/Dip.Application/Features/ProductionTools/ProductionToolContracts.cs
 M src/Dip.Application/Features/ProductionTools/ProductionToolEventMapping.cs
 M src/Dip.Application/Features/ProductionTools/ProductionToolEventService.cs
 M src/Dip.Application/Features/ProjectIntelligence/ProjectEntityResolutionService.cs
 M src/Dip.Application/Features/ProjectIntelligence/ProjectIntelligenceContracts.cs
 M src/Dip.Application/Features/ProjectIntelligence/ProjectPlanIngestionService.cs
 M src/Dip.Application/Features/ProjectIntelligence/ProjectPlanUnderstandingEngine.cs
 M src/Dip.Application/Features/ProjectIntelligence/ProjectProgressIntelligenceService.cs
 M src/Dip.Application/Features/ProjectIntelligence/ProjectSourceIngestionService.cs
 M src/Dip.Application/Features/ProjectTruth/AdaptiveProjectRiskEvaluator.cs
 M src/Dip.Application/Features/ProjectTruth/ClaimEvidenceAssessmentService.cs
 M src/Dip.Application/Features/ProjectTruth/ClaimVerificationEvaluator.cs
 M src/Dip.Application/Features/ProjectTruth/EvidenceConfidenceEvaluator.cs
 M src/Dip.Application/Features/ProjectTruth/PmEvidenceTranslator.cs
 M src/Dip.Application/Features/ProjectTruth/ProgressEvaluator.cs
 M src/Dip.Application/Features/ProjectTruth/ProjectBlockerEvaluator.cs
 M src/Dip.Application/Features/ProjectTruth/ProjectClaimService.cs
 M src/Dip.Application/Features/ProjectTruth/ProjectRiskEvaluator.cs
 M src/Dip.Application/Features/ProjectTruth/ProjectTruthEvaluator.cs
 M src/Dip.Application/Features/ProjectTruth/ProjectTruthModels.cs
 M src/Dip.Application/Features/ProjectTruth/ProjectTruthPolicy.cs
 M src/Dip.Application/Features/ProjectTruth/ProjectTruthService.cs
 M src/Dip.Application/Features/Projects/Commands/CreateProjectCommand.cs
 M src/Dip.Application/Features/Projects/Commands/DeleteProjectCommand.cs
 M src/Dip.Application/Features/Projects/Commands/UpdateProjectCommand.cs
 M src/Dip.Application/Features/Projects/Dtos/ProjectDashboardDto.cs
 M src/Dip.Application/Features/Projects/Dtos/ProjectDto.cs
 M src/Dip.Application/Features/Projects/ProjectService.cs
 M src/Dip.Application/Features/Projects/Queries/GetProjectDashboardQuery.cs
 M src/Dip.Application/Features/Projects/Queries/GetProjectQuery.cs
 M src/Dip.Application/Features/Projects/Queries/GetProjectsQuery.cs
 M src/Dip.Application/Features/PullRequests/Commands/RecalculatePullRequestRiskCommand.cs
 M src/Dip.Application/Features/PullRequests/Commands/SyncPullRequestCommand.cs
 M src/Dip.Application/Features/PullRequests/Dtos/PullRequestRiskDto.cs
 M src/Dip.Application/Features/PullRequests/Queries/GetPullRequestQuery.cs
 M src/Dip.Application/Features/PullRequests/Queries/GetPullRequestRiskQuery.cs
 M src/Dip.Application/Features/Quality/Commands/EvaluateQualityGateCommand.cs
 M src/Dip.Application/Features/Quality/Commands/ResolveQualityIssueCommand.cs
 M src/Dip.Application/Features/Quality/Dtos/QualityIssueDto.cs
 M src/Dip.Application/Features/Quality/Dtos/QualityTrendDto.cs
 M src/Dip.Application/Features/Quality/QualityGateEvaluator.cs
 M src/Dip.Application/Features/Quality/QualityRuleEvaluator.cs
 M src/Dip.Application/Features/Quality/Queries/GetQualityIssuesQuery.cs
 M src/Dip.Application/Features/Quality/Queries/GetQualityTrendsQuery.cs
 M src/Dip.Application/Features/QualityAutomation/AssetQa/AssetQaContracts.cs
 M src/Dip.Application/Features/QualityAutomation/AssetQa/AssetQaRuleEngine.cs
 M src/Dip.Application/Features/QualityAutomation/AssetQa/AssetQaService.cs
 M src/Dip.Application/Features/QualityAutomation/AutonomousDebugService.cs
 M src/Dip.Application/Features/QualityAutomation/BranchQualityOperationsService.cs
 M src/Dip.Application/Features/QualityAutomation/CommercialQualityOperationsService.cs
 M src/Dip.Application/Features/QualityAutomation/GitBranchQaWorkspaceManager.cs
 M src/Dip.Application/Features/QualityAutomation/ProductionCapabilityQaPack.cs
 M src/Dip.Application/Features/QualityAutomation/ProductionQaIntelligence.cs
 M src/Dip.Application/Features/QualityAutomation/QaCapabilities.cs
 M src/Dip.Application/Features/QualityAutomation/QaContracts.cs
 M src/Dip.Application/Features/QualityAutomation/QaFingerprint.cs
 M src/Dip.Application/Features/QualityAutomation/QaOrchestrationService.cs
 M src/Dip.Application/Features/QualityAutomation/RuntimeDefectIntelligenceService.cs
 M src/Dip.Application/Features/QualityAutomation/VisualQa/PortablePngCodec.cs
 M src/Dip.Application/Features/QualityAutomation/VisualQa/VisualComparator.cs
 M src/Dip.Application/Features/QualityAutomation/VisualQa/VisualQaContracts.cs
 M src/Dip.Application/Features/QualityAutomation/VisualQa/VisualQaService.cs
 M src/Dip.Application/Features/QualityGovernance/QaGovernanceAdministrationService.cs
 M src/Dip.Application/Features/QualityGovernance/QaGovernanceContracts.cs
 M src/Dip.Application/Features/QualityGovernance/QaGovernanceCoreEngine.cs
 M src/Dip.Application/Features/QualityGovernance/QaGovernanceQueryService.cs
 M src/Dip.Application/Features/QualityGovernance/QaGovernanceRunProcessor.cs
 M src/Dip.Application/Features/Realtime/ProjectRealtimeEventDto.cs
 M src/Dip.Application/Features/Release/ReleaseEvidenceService.cs
 M src/Dip.Application/Features/Release/ReleaseReadinessService.cs
 M src/Dip.Application/Features/Replay/GameReplayService.cs
 M src/Dip.Application/Features/Repositories/MultiRepositoryGraphService.cs
 M src/Dip.Application/Features/Review/ReviewIntelligenceService.cs
 M src/Dip.Application/Features/Scenarios/WhatIfSimulationService.cs
 M src/Dip.Application/Features/SystemStatus/SystemStatusDtos.cs
 M src/Dip.Application/Features/TeamLoad/Dtos/TeamLoadDto.cs
 M src/Dip.Application/Features/TeamLoad/Queries/GetMemberWorkloadQuery.cs
 M src/Dip.Application/Features/TeamLoad/Queries/GetTeamLoadQuery.cs
 M src/Dip.Application/Features/Triage/RegressionTriageService.cs
 M src/Dip.Application/Features/WorkItems/WorkItemService.cs
 M src/Dip.Client/DipPlatformClient.cs
 M src/Dip.Desktop/App.xaml.cs
 M src/Dip.Desktop/MainWindow.xaml.cs
 M src/Dip.Desktop/ViewModels/MainWindowViewModel.cs
 M src/Dip.Domain/Exceptions/DomainException.cs
 M src/Dip.Domain/Exceptions/InvalidMetricException.cs
 M src/Dip.Domain/Exceptions/InvalidProjectStateException.cs
RM src/Dip.Domain/Entities/ArtifactBlob.cs -> src/Dip.Domain/Features/Artifacts/Entities/ArtifactBlob.cs
RM src/Dip.Domain/Entities/ArtifactDependency.cs -> src/Dip.Domain/Features/Artifacts/Entities/ArtifactDependency.cs
RM src/Dip.Domain/Entities/ArtifactPreview.cs -> src/Dip.Domain/Features/Artifacts/Entities/ArtifactPreview.cs
RM src/Dip.Domain/Entities/ArtifactRecord.cs -> src/Dip.Domain/Features/Artifacts/Entities/ArtifactRecord.cs
RM src/Dip.Domain/Entities/AssetQaFindingRecord.cs -> src/Dip.Domain/Features/Artifacts/Entities/AssetQaFindingRecord.cs
RM src/Dip.Domain/Entities/GoldenMasterProfile.cs -> src/Dip.Domain/Features/Artifacts/Entities/GoldenMasterProfile.cs
RM src/Dip.Domain/Entities/QaRun.cs -> src/Dip.Domain/Features/Artifacts/Entities/QaRun.cs
RM src/Dip.Domain/Entities/TrackedArtifact.cs -> src/Dip.Domain/Features/Artifacts/Entities/TrackedArtifact.cs
RM src/Dip.Domain/Entities/TrackedArtifactVersion.cs -> src/Dip.Domain/Features/Artifacts/Entities/TrackedArtifactVersion.cs
RM src/Dip.Domain/Entities/VisualComparisonRecord.cs -> src/Dip.Domain/Features/Artifacts/Entities/VisualComparisonRecord.cs
RM src/Dip.Domain/Enums/DifficultyTwoEnums.cs -> src/Dip.Domain/Features/Artifacts/Enums/ArtifactTrackingEnums.cs
RM src/Dip.Domain/Enums/P2Enums.cs -> src/Dip.Domain/Features/Artifacts/Enums/P2Enums.cs
RM src/Dip.Domain/Enums/P2IntelligenceStatus.cs -> src/Dip.Domain/Features/Common/Enums/P2IntelligenceStatus.cs
RM src/Dip.Domain/Enums/QaLanguage.cs -> src/Dip.Domain/Features/Common/Enums/QaLanguage.cs
RM src/Dip.Domain/Enums/Severity.cs -> src/Dip.Domain/Features/Common/Enums/Severity.cs
RM src/Dip.Domain/Events/CriticalIssueDetectedEvent.cs -> src/Dip.Domain/Features/Common/Events/CriticalIssueDetectedEvent.cs
RM src/Dip.Domain/Rules/TeamLoadRule.cs -> src/Dip.Domain/Features/Common/Rules/TeamLoadRule.cs
RM src/Dip.Domain/ValueObjects/HealthScore.cs -> src/Dip.Domain/Features/Common/ValueObjects/HealthScore.cs
RM src/Dip.Domain/ValueObjects/Percentage.cs -> src/Dip.Domain/Features/Common/ValueObjects/Percentage.cs
RM src/Dip.Domain/Entities/ClaimEvidenceAssessmentRecord.cs -> src/Dip.Domain/Features/Evidence/Entities/ClaimEvidenceAssessmentRecord.cs
RM src/Dip.Domain/Entities/ClaimEvidenceLink.cs -> src/Dip.Domain/Features/Evidence/Entities/ClaimEvidenceLink.cs
RM src/Dip.Domain/Entities/EnterpriseEvidenceEntities.cs -> src/Dip.Domain/Features/Evidence/Entities/EnterpriseEvidenceEntities.cs
RM src/Dip.Domain/Entities/EvidenceRecord.cs -> src/Dip.Domain/Features/Evidence/Entities/EvidenceRecord.cs
RM src/Dip.Domain/Entities/P2IntelligenceRecord.cs -> src/Dip.Domain/Features/Evidence/Entities/P2IntelligenceRecord.cs
RM src/Dip.Domain/Entities/ProjectClaim.cs -> src/Dip.Domain/Features/Evidence/Entities/ProjectClaim.cs
RM src/Dip.Domain/Entities/ProjectTruthSnapshot.cs -> src/Dip.Domain/Features/Evidence/Entities/ProjectTruthSnapshot.cs
RM src/Dip.Domain/Entities/QaGovernanceEntities.cs -> src/Dip.Domain/Features/Evidence/Entities/QaGovernanceEntities.cs
RM src/Dip.Domain/Entities/ReleaseEvidenceBundle.cs -> src/Dip.Domain/Features/Evidence/Entities/ReleaseEvidenceBundle.cs
RM src/Dip.Domain/Enums/ClaimVerificationStatus.cs -> src/Dip.Domain/Features/Evidence/Enums/ClaimVerificationStatus.cs
RM src/Dip.Domain/Enums/DevelopmentIntelligenceEnums.cs -> src/Dip.Domain/Features/Evidence/Enums/DevelopmentIntelligenceEnums.cs
RM src/Dip.Domain/Enums/EvidenceConfidence.cs -> src/Dip.Domain/Features/Evidence/Enums/EvidenceConfidence.cs
RM src/Dip.Domain/Enums/DifficultyOneEnums.cs -> src/Dip.Domain/Features/Evidence/Enums/EvidenceIntegrationEnums.cs
RM src/Dip.Domain/Enums/EvidenceOutcome.cs -> src/Dip.Domain/Features/Evidence/Enums/EvidenceOutcome.cs
RM src/Dip.Domain/Enums/EvidenceRelation.cs -> src/Dip.Domain/Features/Evidence/Enums/EvidenceRelation.cs
RM src/Dip.Domain/Enums/EvidenceSourceType.cs -> src/Dip.Domain/Features/Evidence/Enums/EvidenceSourceType.cs
RM src/Dip.Domain/Enums/P2IntelligenceKind.cs -> src/Dip.Domain/Features/Evidence/Enums/P2IntelligenceKind.cs
RM src/Dip.Domain/Enums/PlatformIntelligenceEnums.cs -> src/Dip.Domain/Features/Evidence/Enums/PlatformIntelligenceEnums.cs
RM src/Dip.Domain/Enums/ProjectTruthStatus.cs -> src/Dip.Domain/Features/Evidence/Enums/ProjectTruthStatus.cs
RM src/Dip.Domain/Entities/IdentityGovernanceEntities.cs -> src/Dip.Domain/Features/Identity/Entities/IdentityGovernanceEntities.cs
RM src/Dip.Domain/Entities/OrganizationTenant.cs -> src/Dip.Domain/Features/Identity/Entities/OrganizationTenant.cs
RM src/Dip.Domain/Entities/PolicyProfile.cs -> src/Dip.Domain/Features/Identity/Entities/PolicyProfile.cs
RM src/Dip.Domain/Entities/ProjectMembership.cs -> src/Dip.Domain/Features/Identity/Entities/ProjectMembership.cs
RM src/Dip.Domain/Entities/QaGovernanceHardeningEntities.cs -> src/Dip.Domain/Features/Identity/Entities/QaGovernanceHardeningEntities.cs
RM src/Dip.Domain/Enums/ProjectControlEnums.cs -> src/Dip.Domain/Features/Identity/Enums/ProjectControlEnums.cs
RM src/Dip.Domain/Enums/ProjectRole.cs -> src/Dip.Domain/Features/Identity/Enums/ProjectRole.cs
RM src/Dip.Domain/Enums/QaGovernanceSecurityEnums.cs -> src/Dip.Domain/Features/Identity/Enums/QaGovernanceSecurityEnums.cs
RM src/Dip.Domain/Entities/IntegrationConnection.cs -> src/Dip.Domain/Features/Integrations/Entities/IntegrationConnection.cs
RM src/Dip.Domain/Entities/ProcessedWebhook.cs -> src/Dip.Domain/Features/Integrations/Entities/ProcessedWebhook.cs
RM src/Dip.Domain/Entities/AgentFleetEntities.cs -> src/Dip.Domain/Features/Operations/Entities/AgentFleetEntities.cs
RM src/Dip.Domain/Entities/AuditLog.cs -> src/Dip.Domain/Features/Operations/Entities/AuditLog.cs
RM src/Dip.Domain/Entities/DailySummary.cs -> src/Dip.Domain/Features/Operations/Entities/DailySummary.cs
RM src/Dip.Domain/Entities/DurableJob.cs -> src/Dip.Domain/Features/Operations/Entities/DurableJob.cs
RM src/Dip.Domain/Entities/ExternalWorkItemLink.cs -> src/Dip.Domain/Features/Operations/Entities/ExternalWorkItemLink.cs
RM src/Dip.Domain/Entities/Incident.cs -> src/Dip.Domain/Features/Operations/Entities/Incident.cs
RM src/Dip.Domain/Entities/IncidentEvent.cs -> src/Dip.Domain/Features/Operations/Entities/IncidentEvent.cs
RM src/Dip.Domain/Entities/IncidentOccurrence.cs -> src/Dip.Domain/Features/Operations/Entities/IncidentOccurrence.cs
RM src/Dip.Domain/Entities/IntelligenceRun.cs -> src/Dip.Domain/Features/Operations/Entities/IntelligenceRun.cs
RM src/Dip.Domain/Entities/Notification.cs -> src/Dip.Domain/Features/Operations/Entities/Notification.cs
RM src/Dip.Domain/Entities/OutboxMessage.cs -> src/Dip.Domain/Features/Operations/Entities/OutboxMessage.cs
RM src/Dip.Domain/Entities/PipelineRun.cs -> src/Dip.Domain/Features/Operations/Entities/PipelineRun.cs
RM src/Dip.Domain/Entities/ReleaseReadinessSnapshot.cs -> src/Dip.Domain/Features/Operations/Entities/ReleaseReadinessSnapshot.cs
RM src/Dip.Domain/Enums/DifficultyFiveEnums.cs -> src/Dip.Domain/Features/Operations/Enums/EnterpriseFleetEnums.cs
RM src/Dip.Domain/Enums/NotificationType.cs -> src/Dip.Domain/Features/Operations/Enums/NotificationType.cs
RM src/Dip.Domain/Enums/PipelineStatus.cs -> src/Dip.Domain/Features/Operations/Enums/PipelineStatus.cs
RM src/Dip.Domain/Enums/QaRunStatus.cs -> src/Dip.Domain/Features/Operations/Enums/QaRunStatus.cs
RM src/Dip.Domain/Events/PipelineCompletedEvent.cs -> src/Dip.Domain/Features/Operations/Events/PipelineCompletedEvent.cs
RM src/Dip.Domain/Entities/QaFinding.cs -> src/Dip.Domain/Features/Production/Entities/QaFinding.cs
RM src/Dip.Domain/Entities/ReproductionAttempt.cs -> src/Dip.Domain/Features/Production/Entities/ReproductionAttempt.cs
RM src/Dip.Domain/Enums/ReproductionStatus.cs -> src/Dip.Domain/Features/Production/Enums/ReproductionStatus.cs
RM src/Dip.Domain/Entities/Commit.cs -> src/Dip.Domain/Features/Projects/Entities/Commit.cs
RM src/Dip.Domain/Entities/CrossRepositoryImpact.cs -> src/Dip.Domain/Features/Projects/Entities/CrossRepositoryImpact.cs
RM src/Dip.Domain/Entities/DecisionTrace.cs -> src/Dip.Domain/Features/Projects/Entities/DecisionTrace.cs
RM src/Dip.Domain/Entities/DevelopmentGraphEdge.cs -> src/Dip.Domain/Features/Projects/Entities/DevelopmentGraphEdge.cs
RM src/Dip.Domain/Entities/DevelopmentGraphNode.cs -> src/Dip.Domain/Features/Projects/Entities/DevelopmentGraphNode.cs
RM src/Dip.Domain/Entities/Project.cs -> src/Dip.Domain/Features/Projects/Entities/Project.cs
RM src/Dip.Domain/Entities/ProjectForecast.cs -> src/Dip.Domain/Features/Projects/Entities/ProjectForecast.cs
RM src/Dip.Domain/Entities/ProjectMember.cs -> src/Dip.Domain/Features/Projects/Entities/ProjectMember.cs
RM src/Dip.Domain/Entities/ProjectSourceBinding.cs -> src/Dip.Domain/Features/Projects/Entities/ProjectSourceBinding.cs
RM src/Dip.Domain/Entities/PullRequest.cs -> src/Dip.Domain/Features/Projects/Entities/PullRequest.cs
RM src/Dip.Domain/Entities/Repository.cs -> src/Dip.Domain/Features/Projects/Entities/Repository.cs
RM src/Dip.Domain/Entities/RepositoryDependency.cs -> src/Dip.Domain/Features/Projects/Entities/RepositoryDependency.cs
RM src/Dip.Domain/Entities/RepositoryNode.cs -> src/Dip.Domain/Features/Projects/Entities/RepositoryNode.cs
RM src/Dip.Domain/Entities/TaskItem.cs -> src/Dip.Domain/Features/Projects/Entities/TaskItem.cs
RM src/Dip.Domain/Enums/IssueStatus.cs -> src/Dip.Domain/Features/Projects/Enums/IssueStatus.cs
RM src/Dip.Domain/ValueObjects/CommitSha.cs -> src/Dip.Domain/Features/Projects/ValueObjects/CommitSha.cs
RM src/Dip.Domain/ValueObjects/RepositoryUrl.cs -> src/Dip.Domain/Features/Projects/ValueObjects/RepositoryUrl.cs
RM src/Dip.Domain/Entities/AdaptiveQaPlanSnapshot.cs -> src/Dip.Domain/Features/Quality/Entities/AdaptiveQaPlanSnapshot.cs
RM src/Dip.Domain/Entities/BenchmarkComparison.cs -> src/Dip.Domain/Features/Quality/Entities/BenchmarkComparison.cs
RM src/Dip.Domain/Entities/BenchmarkDefinition.cs -> src/Dip.Domain/Features/Quality/Entities/BenchmarkDefinition.cs
RM src/Dip.Domain/Entities/BenchmarkMetric.cs -> src/Dip.Domain/Features/Quality/Entities/BenchmarkMetric.cs
RM src/Dip.Domain/Entities/BenchmarkRun.cs -> src/Dip.Domain/Features/Quality/Entities/BenchmarkRun.cs
RM src/Dip.Domain/Entities/ChangeRiskAssessment.cs -> src/Dip.Domain/Features/Quality/Entities/ChangeRiskAssessment.cs
RM src/Dip.Domain/Entities/CommunityHardwareSample.cs -> src/Dip.Domain/Features/Quality/Entities/CommunityHardwareSample.cs
RM src/Dip.Domain/Entities/GameReplayMetric.cs -> src/Dip.Domain/Features/Quality/Entities/GameReplayMetric.cs
RM src/Dip.Domain/Entities/GameReplayRun.cs -> src/Dip.Domain/Features/Quality/Entities/GameReplayRun.cs
RM src/Dip.Domain/Entities/GameReplaySession.cs -> src/Dip.Domain/Features/Quality/Entities/GameReplaySession.cs
RM src/Dip.Domain/Entities/HardwareBacktest.cs -> src/Dip.Domain/Features/Quality/Entities/HardwareBacktest.cs
RM src/Dip.Domain/Entities/HardwareCalibration.cs -> src/Dip.Domain/Features/Quality/Entities/HardwareCalibration.cs
RM src/Dip.Domain/Entities/HardwareCommunitySample.cs -> src/Dip.Domain/Features/Quality/Entities/HardwareCommunitySample.cs
RM src/Dip.Domain/Entities/HardwareProfile.cs -> src/Dip.Domain/Features/Quality/Entities/HardwareProfile.cs
RM src/Dip.Domain/Entities/Metric.cs -> src/Dip.Domain/Features/Quality/Entities/Metric.cs
RM src/Dip.Domain/Entities/PerformanceBudgetEvaluation.cs -> src/Dip.Domain/Features/Quality/Entities/PerformanceBudgetEvaluation.cs
RM src/Dip.Domain/Entities/PerformanceBudgetProfile.cs -> src/Dip.Domain/Features/Quality/Entities/PerformanceBudgetProfile.cs
RM src/Dip.Domain/Entities/PerformanceSimulationRun.cs -> src/Dip.Domain/Features/Quality/Entities/PerformanceSimulationRun.cs
RM src/Dip.Domain/Entities/QualityAutomationProfile.cs -> src/Dip.Domain/Features/Quality/Entities/QualityAutomationProfile.cs
RM src/Dip.Domain/Entities/QualityGateResult.cs -> src/Dip.Domain/Features/Quality/Entities/QualityGateResult.cs
RM src/Dip.Domain/Entities/QualityIssue.cs -> src/Dip.Domain/Features/Quality/Entities/QualityIssue.cs
RM src/Dip.Domain/Entities/QualityRule.cs -> src/Dip.Domain/Features/Quality/Entities/QualityRule.cs
RM src/Dip.Domain/Entities/RegressionTriage.cs -> src/Dip.Domain/Features/Quality/Entities/RegressionTriage.cs
RM src/Dip.Domain/Enums/HardwareProfileSource.cs -> src/Dip.Domain/Features/Quality/Enums/HardwareProfileSource.cs
RM src/Dip.Domain/Enums/MetricComparisonStatus.cs -> src/Dip.Domain/Features/Quality/Enums/MetricComparisonStatus.cs
RM src/Dip.Domain/Enums/ProjectRiskLevel.cs -> src/Dip.Domain/Features/Quality/Enums/ProjectRiskLevel.cs
RM src/Dip.Domain/Enums/QaGovernanceEnums.cs -> src/Dip.Domain/Features/Quality/Enums/QaGovernanceEnums.cs
RM src/Dip.Domain/Enums/DifficultyFourEnums.cs -> src/Dip.Domain/Features/Quality/Enums/QualityAutomationEnums.cs
RM src/Dip.Domain/Enums/QualityGateStatus.cs -> src/Dip.Domain/Features/Quality/Enums/QualityGateStatus.cs
RM src/Dip.Domain/Enums/RiskLevel.cs -> src/Dip.Domain/Features/Quality/Enums/RiskLevel.cs
RM src/Dip.Domain/Enums/SimulationStatus.cs -> src/Dip.Domain/Features/Quality/Enums/SimulationStatus.cs
RM src/Dip.Domain/Events/PullRequestRiskChangedEvent.cs -> src/Dip.Domain/Features/Quality/Events/PullRequestRiskChangedEvent.cs
RM src/Dip.Domain/Events/QualityGateFailedEvent.cs -> src/Dip.Domain/Features/Quality/Events/QualityGateFailedEvent.cs
RM src/Dip.Domain/Rules/MetricThresholdRule.cs -> src/Dip.Domain/Features/Quality/Rules/MetricThresholdRule.cs
RM src/Dip.Domain/Rules/QualityGateRule.cs -> src/Dip.Domain/Features/Quality/Rules/QualityGateRule.cs
RM src/Dip.Domain/Rules/RegressionRule.cs -> src/Dip.Domain/Features/Quality/Rules/RegressionRule.cs
RM src/Dip.Domain/Services/QualityIssueFingerprint.cs -> src/Dip.Domain/Features/Quality/Services/QualityIssueFingerprint.cs
RM src/Dip.Domain/ValueObjects/LanguageId.cs -> src/Dip.Domain/Features/Quality/ValueObjects/LanguageId.cs
RM src/Dip.Domain/ValueObjects/MetricValue.cs -> src/Dip.Domain/Features/Quality/ValueObjects/MetricValue.cs
RM src/Dip.Domain/ValueObjects/RiskScore.cs -> src/Dip.Domain/Features/Quality/ValueObjects/RiskScore.cs
 M src/Dip.Infrastructure/Analysis/AnalysisClient.cs
 M src/Dip.Infrastructure/Analysis/AnalysisOptions.cs
 M src/Dip.Infrastructure/Analysis/Models/AnalysisRequest.cs
 M src/Dip.Infrastructure/Analysis/Models/AnalysisResponse.cs
 M src/Dip.Infrastructure/Artifacts/ArtifactStoreOptions.cs
 M src/Dip.Infrastructure/Artifacts/LocalArtifactStore.cs
 M src/Dip.Infrastructure/Artifacts/MinioArtifactStore.cs
 M src/Dip.Infrastructure/Authentication/AuthenticationOptions.cs
 M src/Dip.Infrastructure/Authentication/GitHubOAuthService.cs
 M src/Dip.Infrastructure/Authentication/JwtTokenService.cs
 M src/Dip.Infrastructure/Benchmarks/BenchmarkExecutor.cs
 M src/Dip.Infrastructure/Calendars/GoogleCalendarRestConnector.cs
 M src/Dip.Infrastructure/DependencyInjection.cs
RM src/Dip.Infrastructure/DependencyInjection.DifficultyTwo.cs -> src/Dip.Infrastructure/DependencyInjection/DependencyInjection.ArtifactTracking.cs
RM src/Dip.Infrastructure/DependencyInjection.DifficultyFive.cs -> src/Dip.Infrastructure/DependencyInjection/DependencyInjection.EnterpriseFleet.cs
RM src/Dip.Infrastructure/DependencyInjection.DifficultyOne.cs -> src/Dip.Infrastructure/DependencyInjection/DependencyInjection.EvidenceIntegration.cs
RM src/Dip.Infrastructure/DependencyInjection.GitHub.cs -> src/Dip.Infrastructure/DependencyInjection/DependencyInjection.GitHub.cs
RM src/Dip.Infrastructure/DependencyInjection.DifficultyThree.cs -> src/Dip.Infrastructure/DependencyInjection/DependencyInjection.ProductionTools.cs
RM src/Dip.Infrastructure/DependencyInjection.DifficultyFour.cs -> src/Dip.Infrastructure/DependencyInjection/DependencyInjection.QualityAutomation.cs
 M src/Dip.Infrastructure/ExternalEvents/ExternalEventIngestionOptions.cs
 M src/Dip.Infrastructure/ExternalEvents/OpenTelemetryIncidentIngestionAdapter.cs
 M src/Dip.Infrastructure/ExternalEvents/SentryIncidentIngestionAdapter.cs
 M src/Dip.Infrastructure/GitHub/GitHubClient.cs
 M src/Dip.Infrastructure/GitHub/GitHubClientFactory.cs
 M src/Dip.Infrastructure/GitHub/GitHubOptions.cs
 M src/Dip.Infrastructure/GitHub/GitHubPullRequestCommentService.cs
 M src/Dip.Infrastructure/GitHub/GitHubRepositoryCatalog.cs
 M src/Dip.Infrastructure/GitHub/GitHubWebhookParser.cs
 M src/Dip.Infrastructure/GitHub/GitHubWebhookVerifier.cs
 M src/Dip.Infrastructure/GitHub/Models/GitHubIssueModel.cs
 M src/Dip.Infrastructure/GitHub/Models/GitHubPullRequestModel.cs
 M src/Dip.Infrastructure/GitHub/Models/GitHubWorkflowRunModel.cs
 M src/Dip.Infrastructure/Hardware/HardwareCalibrationService.cs
 M src/Dip.Infrastructure/Hardware/HardwareConstraintRunner.cs
 M src/Dip.Infrastructure/Hardware/PerformanceSimulationService.cs
 M src/Dip.Infrastructure/Integrations/ExternalIntegrationOptions.cs
 M src/Dip.Infrastructure/Integrations/GitHub/GitHubConnector.cs
 M src/Dip.Infrastructure/Integrations/GitHubVersionControlConnector.cs
 M src/Dip.Infrastructure/Integrations/JenkinsCiConnector.cs
 M src/Dip.Infrastructure/Integrations/PerforceVersionControlConnector.cs
 M src/Dip.Infrastructure/Intelligence/FuzzRunner.cs
 M src/Dip.Infrastructure/Intelligence/IntelligenceExecutionSafety.cs
 M src/Dip.Infrastructure/Intelligence/MediaRegressionRunner.cs
 M src/Dip.Infrastructure/Intelligence/MinimumHardwareRunner.cs
 M src/Dip.Infrastructure/Intelligence/MutationRunner.cs
 M src/Dip.Infrastructure/Intelligence/RegressionRunner.cs
 M src/Dip.Infrastructure/Intelligence/ReliabilityRunner.cs
 M src/Dip.Infrastructure/Management/EmailManagementNotificationChannel.cs
 M src/Dip.Infrastructure/Management/GitHubManagementSyncAdapter.cs
 M src/Dip.Infrastructure/Management/JiraManagementSyncAdapter.cs
 M src/Dip.Infrastructure/Management/LinearManagementSyncAdapter.cs
 M src/Dip.Infrastructure/Management/ManagementNotificationOptions.cs
 M src/Dip.Infrastructure/Management/SlackManagementNotificationChannel.cs
 M src/Dip.Infrastructure/Management/TeamsManagementNotificationChannel.cs
 M src/Dip.Infrastructure/Messaging/PostgresQueueClient.cs
 M src/Dip.Infrastructure/Messaging/QueueOptions.cs
 M src/Dip.Infrastructure/Notifications/EmailNotificationClient.cs
 M src/Dip.Infrastructure/Notifications/GitHubCommentNotificationClient.cs
 M src/Dip.Infrastructure/Notifications/NotificationOptions.cs
 M src/Dip.Infrastructure/Notifications/SlackNotificationClient.cs
 M src/Dip.Infrastructure/Observability/DipOperationalMetrics.cs
 M src/Dip.Infrastructure/Observability/LoggingExtensions.cs
 M src/Dip.Infrastructure/Observability/MetricsRegistry.cs
 M src/Dip.Infrastructure/Observability/OpenTelemetryExtensions.cs
 M src/Dip.Infrastructure/P2Intelligence/DeterministicLocalDebugAgent.cs
 M src/Dip.Infrastructure/P2Intelligence/MediaRegressionRunner.cs
 M src/Dip.Infrastructure/P2Intelligence/NativeSymbolicator.cs
 M src/Dip.Infrastructure/P2Intelligence/OpenAiCompatibleFixProposalAgent.cs
 M src/Dip.Infrastructure/P2Intelligence/P2ExperimentRunner.cs
 M src/Dip.Infrastructure/P2Intelligence/TestImpactAnalyzer.cs
 M src/Dip.Infrastructure/Persistence/Configurations/AdaptiveQaPlanSnapshotConfiguration.cs
RM src/Dip.Infrastructure/Persistence/Configurations/DifficultyTwoConfigurations.cs -> src/Dip.Infrastructure/Persistence/Configurations/ArtifactTrackingConfigurations.cs
 M src/Dip.Infrastructure/Persistence/Configurations/AuditLogConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/ClaimEvidenceLinkConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/Configurations.cs
 M src/Dip.Infrastructure/Persistence/Configurations/DecisionTraceConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/DevelopmentGraphConfigurations.cs
 M src/Dip.Infrastructure/Persistence/Configurations/DevelopmentIntelligenceConfigurations.cs
RM src/Dip.Infrastructure/Persistence/Configurations/DifficultyFiveConfigurations.cs -> src/Dip.Infrastructure/Persistence/Configurations/EnterpriseFleetConfigurations.cs
RM src/Dip.Infrastructure/Persistence/Configurations/DifficultyOneConfigurations.cs -> src/Dip.Infrastructure/Persistence/Configurations/EvidenceIntegrationConfigurations.cs
 M src/Dip.Infrastructure/Persistence/Configurations/EvidenceRecordConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/HardwareCalibrationConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/HardwareCommunitySampleConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/HardwareProfileConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/LegacyCoreConfigurations.cs
 M src/Dip.Infrastructure/Persistence/Configurations/MetricConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/NotificationConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/OutboxMessageConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/P2Configurations.cs
 M src/Dip.Infrastructure/Persistence/Configurations/P2IntelligenceRecordConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/PerformanceSimulationRunConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/PipelineRunConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/PlatformIntelligenceConfigurations.cs
 M src/Dip.Infrastructure/Persistence/Configurations/ProcessedWebhookConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/ProjectClaimConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/ProjectConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/ProjectTruthSnapshotConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/PullRequestConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/QaFindingConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/QaGovernanceConfigurations.cs
 M src/Dip.Infrastructure/Persistence/Configurations/QaGovernanceHardeningConfigurations.cs
 M src/Dip.Infrastructure/Persistence/Configurations/QaRunConfiguration.cs
RM src/Dip.Infrastructure/Persistence/Configurations/DifficultyFourConfigurations.cs -> src/Dip.Infrastructure/Persistence/Configurations/QualityAutomationConfigurations.cs
 M src/Dip.Infrastructure/Persistence/Configurations/QualityGateResultConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/QualityIssueConfiguration.cs
 M src/Dip.Infrastructure/Persistence/Configurations/ReproductionAttemptConfiguration.cs
 M src/Dip.Infrastructure/Persistence/DbInitializer.cs
 M src/Dip.Infrastructure/Persistence/DipDbContext.cs
 M src/Dip.Infrastructure/Persistence/DipDbContextFactory.cs
 M src/Dip.Infrastructure/Persistence/LegacyMigrationAdopter.cs
 M src/Dip.Infrastructure/Persistence/P0SchemaBootstrapper.cs
 M src/Dip.Infrastructure/Persistence/ProjectMutationCoordinator.cs
 M src/Dip.Infrastructure/Persistence/ProjectResourceProjectResolver.cs
 M src/Dip.Infrastructure/Persistence/QaGovernanceExecutionCoordinator.cs
 M src/Dip.Infrastructure/Persistence/Repositories/AuditLogRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/ClaimEvidenceLinkRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/DashboardReadRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/DevelopmentGraphRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/DevelopmentIntelligenceRepositories.cs
RM src/Dip.Infrastructure/Persistence/Repositories/DifficultyFiveRepositories.cs -> src/Dip.Infrastructure/Persistence/Repositories/EnterpriseFleetRepositories.cs
RM src/Dip.Infrastructure/Persistence/Repositories/DifficultyOneRepositories.cs -> src/Dip.Infrastructure/Persistence/Repositories/EvidenceIntegrationRepositories.cs
 M src/Dip.Infrastructure/Persistence/Repositories/EvidenceRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/MetricRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/OutboxRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/P1Repositories.cs
 M src/Dip.Infrastructure/Persistence/Repositories/P2LegacyStores.cs
 M src/Dip.Infrastructure/Persistence/Repositories/P2Repositories.cs
 M src/Dip.Infrastructure/Persistence/Repositories/PipelineRunRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/PlatformIntelligenceRepositories.cs
 M src/Dip.Infrastructure/Persistence/Repositories/ProcessedWebhookRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/ProjectClaimRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/ProjectMembershipRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/ProjectRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/ProjectTruthRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/QaGovernanceRepository.cs
RM src/Dip.Infrastructure/Persistence/Repositories/DifficultyFourRepositories.cs -> src/Dip.Infrastructure/Persistence/Repositories/QualityAutomationRepositories.cs
 M src/Dip.Infrastructure/Persistence/Repositories/QualityIssueRepository.cs
 M src/Dip.Infrastructure/Persistence/Repositories/Repositories.cs
RM src/Dip.Infrastructure/Persistence/Repositories/DifficultyTwoRepositories.cs -> src/Dip.Infrastructure/Persistence/Repositories/TrackedArtifactRepository.cs
 M src/Dip.Infrastructure/ProjectIntelligence/CollaborationWebhookVerifier.cs
 M src/Dip.Infrastructure/ProjectIntelligence/ProjectIntelligenceSourceOptions.cs
 M src/Dip.Infrastructure/ProjectIntelligence/RedmineProjectSourceAdapter.cs
 M src/Dip.Infrastructure/ProjectIntelligence/SlackProjectSourceAdapter.cs
 M src/Dip.Infrastructure/Provenance/ReleaseProvenanceCollector.cs
 M src/Dip.Infrastructure/Provenance/ReleaseProvenanceOptions.cs
 M src/Dip.Infrastructure/QualityAutomation/CSharpQaEngine.cs
 M src/Dip.Infrastructure/QualityAutomation/CodeOwnersOwnershipResolver.cs
 M src/Dip.Infrastructure/QualityAutomation/CppQaEngine.cs
 M src/Dip.Infrastructure/QualityAutomation/DeepLanguagePackQaAdapter.cs
 M src/Dip.Infrastructure/QualityAutomation/EnvironmentSnapshotService.cs
 M src/Dip.Infrastructure/QualityAutomation/GameProjectQaPack.cs
 M src/Dip.Infrastructure/QualityAutomation/GenericCommandQaAdapter.cs
 M src/Dip.Infrastructure/QualityAutomation/IdeDiagnosticNormalizer.cs
 M src/Dip.Infrastructure/QualityAutomation/ManifestQaAdapter.cs
 M src/Dip.Infrastructure/QualityAutomation/ProcessExecutor.cs
 M src/Dip.Infrastructure/QualityAutomation/PythonQaEngine.cs
 M src/Dip.Infrastructure/QualityAutomation/QaAdapterRegistry.cs
 M src/Dip.Infrastructure/QualityAutomation/QaBugMinimizer.cs
 M src/Dip.Infrastructure/QualityAutomation/QaCapabilityPlanner.cs
 M src/Dip.Infrastructure/QualityAutomation/QaExecutionOptions.cs
 M src/Dip.Infrastructure/QualityAutomation/QaParsing.cs
 M src/Dip.Infrastructure/QualityAutomation/QaToolSupport.cs
 M src/Dip.Infrastructure/QualityAutomation/QaWorkspaceResolver.cs
 M src/Dip.Infrastructure/QualityAutomation/RepositoryTechnologyDetector.cs
 M src/Dip.Infrastructure/QualityAutomation/ReproductionCoordinator.cs
 M src/Dip.Infrastructure/QualityAutomation/SystemIntegrationQaPack.cs
 M src/Dip.Infrastructure/QualityAutomation/UnityProjectQaAdapter.cs
 M src/Dip.Infrastructure/QualityAutomation/UniversalDiagnosticNormalizer.cs
 M src/Dip.Infrastructure/QualityAutomation/UniversalSemanticAnalyzer.cs
 M src/Dip.Infrastructure/QualityAutomation/UniversalSyntaxTree.cs
 M src/Dip.Infrastructure/QualityAutomation/VerifiedFixService.cs
 M src/Dip.Infrastructure/Queue/QueueMessageSerializer.cs
 M src/Dip.Infrastructure/Queue/QueueOptions.cs
 M src/Dip.Infrastructure/Queue/RedisQueueClient.cs
 M src/Dip.Infrastructure/Replay/GameReplayExecutor.cs
 M src/Dip.Infrastructure/Replay/GameReplayOptions.cs
 M src/Dip.Infrastructure/Resilience/CircuitBreakerPolicies.cs
 M src/Dip.Infrastructure/Resilience/RetryPolicies.cs
 M src/Dip.Infrastructure/Security/EnterpriseIntegrationCredentialStore.cs
 M src/Dip.Infrastructure/Security/LocalIntegrationCredentialStore.cs
 M src/Dip.Infrastructure/Spreadsheets/GoogleSheetsRestConnector.cs
 M src/Dip.Infrastructure/Storage/LocalObjectStorage.cs
 M src/Dip.Infrastructure/Storage/MinioObjectStorage.cs
 M src/Dip.Infrastructure/Storage/StorageOptions.cs
 M src/Dip.Infrastructure/Time/SystemClock.cs
 M src/Dip.Infrastructure/WorkItems/GitHubWorkItemConnector.cs
 M src/Dip.Infrastructure/WorkItems/JiraWorkItemConnector.cs
 M src/Dip.Infrastructure/WorkItems/LinearWorkItemConnector.cs
 M src/Dip.Infrastructure/WorkItems/WorkItemIntegrationOptions.cs
 M src/Dip.Shared/Constants/HeaderNames.cs
 M src/Dip.Shared/Constants/MetricNames.cs
 M src/Dip.Shared/Constants/QueueNames.cs
 M src/Dip.Shared/Contracts/Api/ApiError.cs
 M src/Dip.Shared/Contracts/Api/HealthResponse.cs
 M src/Dip.Shared/Contracts/Api/PagedResponse.cs
 M src/Dip.Shared/Contracts/Collectors/CollectorArtifact.cs
 M src/Dip.Shared/Contracts/Collectors/CollectorIssue.cs
 M src/Dip.Shared/Contracts/Collectors/CollectorMetric.cs
 M src/Dip.Shared/Contracts/Collectors/CollectorReport.cs
 M src/Dip.Shared/Contracts/Collectors/CollectorRunStatus.cs
 M src/Dip.Shared/Contracts/Events/AnalysisRequested.cs
 M src/Dip.Shared/Contracts/Events/GitHubWebhookReceived.cs
 M src/Dip.Shared/Contracts/Events/NotificationRequested.cs
 M src/Dip.Shared/Contracts/Events/PipelineCompletedMessage.cs
 M src/Dip.Shared/Contracts/Events/QualityGateFailedMessage.cs
 M src/Dip.Shared/Contracts/Events/QueueMessage.cs
 M src/Dip.Shared/Serialization/SharedJsonOptions.cs
 M src/Dip.Web/GlobalUsings.cs
 M src/Dip.Web/Pages/Dashboard/ManagementOperations.razor.cs
 M src/Dip.Web/Program.cs
 M src/Dip.Web/Services/ApiAuthenticationHandler.cs
 M src/Dip.Web/Services/ApiClient.cs
 M src/Dip.Web/Services/ApiClientAdditionalOperations.cs
 M src/Dip.Web/Services/AuthenticationService.cs
 M src/Dip.Web/Services/OnboardingStateService.cs
 M src/Dip.Web/Services/ProjectDashboardService.cs
 M src/Dip.Web/Services/ProjectOperationFlowResolver.cs
 M src/Dip.Web/Services/RealtimeUpdateService.cs
 M src/Dip.Web/State/ProjectState.cs
 M src/Dip.Web/State/UserState.cs
 M src/Dip.Web/wwwroot/js/charts.js
 M src/Dip.Web/wwwroot/js/onboarding.js
 M src/Dip.Worker/Handlers/CollectorReportHandler.cs
 M src/Dip.Worker/Handlers/PullRequestWebhookHandler.cs
 M src/Dip.Worker/Handlers/QualityGateResultHandler.cs
 M src/Dip.Worker/Handlers/WorkflowRunWebhookHandler.cs
 M src/Dip.Worker/Options/WorkerOptions.cs
 M src/Dip.Worker/Program.cs
 M src/Dip.Worker/Scheduling/DailySummaryScheduler.cs
 M src/Dip.Worker/Scheduling/HealthRecalculationScheduler.cs
 M src/Dip.Worker/Workers/AnalysisRequestWorker.cs
 M src/Dip.Worker/Workers/ArtifactRetentionWorker.cs
 M src/Dip.Worker/Workers/AutomaticRegressionTriageWorker.cs
 M src/Dip.Worker/Workers/CollectorReportProcessedWorker.cs
 M src/Dip.Worker/Workers/DailySummaryWorker.cs
 M src/Dip.Worker/Workers/DeadLetterWorker.cs
 M src/Dip.Worker/Workers/DurableJobHandlers.cs
 M src/Dip.Worker/Workers/DurableJobRecoveryWorker.cs
 M src/Dip.Worker/Workers/DurableJobWorker.cs
 M src/Dip.Worker/Workers/GitHubChangeRiskWorker.cs
 M src/Dip.Worker/Workers/GitHubWebhookWorker.cs
 M src/Dip.Worker/Workers/HealthRecalculationWorker.cs
 M src/Dip.Worker/Workers/ManagementNotificationWorker.cs
 M src/Dip.Worker/Workers/NotificationWorker.cs
 M src/Dip.Worker/Workers/OutboxWorker.cs
 M src/Dip.Worker/Workers/PipelineProcessingWorker.cs
 M src/Dip.Worker/Workers/QualityEvaluationWorker.cs
 M src/Dip.Worker/Workers/WorkerMetricsEndpoint.cs
 M tests/Dip.Agent.Core.Tests/MediaSpoolTests.cs
 M tests/Dip.Agent.Core.Tests/ProjectFileScannerTests.cs
 M tests/Dip.Agent.Core.Tests/SqliteAgentStateStoreTests.cs
 M tests/Dip.Agent.Core.Tests/ToolEventOutboxTests.cs
 M tests/Dip.Application.Tests/CollectorReportValidatorTests.cs
 M tests/Dip.Application.Tests/DashboardTests.cs
 M tests/Dip.Application.Tests/DifficultyFiveFoundationTests.cs
 D tests/Dip.Application.Tests/DifficultyOne/EvidenceRecordV2Tests.cs
 D tests/Dip.Application.Tests/DifficultyOne/IntegrationConnectionTests.cs
 D tests/Dip.Application.Tests/DifficultyOne/PolicyResolverTests.cs
 D tests/Dip.Application.Tests/DifficultyTwo/TrackedArtifactTests.cs
 M tests/Dip.Application.Tests/GitHubQualityReportFormatterTests.cs
 M tests/Dip.Application.Tests/Intelligence/P2PureLogicTests.cs
 M tests/Dip.Application.Tests/ManagementOperationsContractTests.cs
 M tests/Dip.Application.Tests/OutboxWriterTests.cs
 M tests/Dip.Application.Tests/P2Intelligence/AdaptiveQaPlannerTests.cs
 M tests/Dip.Application.Tests/P2Intelligence/EvidencePolicyTests.cs
 M tests/Dip.Application.Tests/P2Intelligence/GameRegressionEvaluatorTests.cs
RM tests/Dip.Application.Tests/DifficultyThree/ProductionToolEventMappingTests.cs -> tests/Dip.Application.Tests/ProductionTools/ProductionToolEventMappingTests.cs
 M tests/Dip.Application.Tests/ProjectIntelligence/ProjectIntelligenceContractsTests.cs
 M tests/Dip.Application.Tests/ProjectIntelligence/ProjectPlanUnderstandingEngineTests.cs
 M tests/Dip.Application.Tests/ProjectTruth/AdaptiveProjectRiskEvaluatorTests.cs
 M tests/Dip.Application.Tests/ProjectTruth/ClaimVerificationEvaluatorTests.cs
 M tests/Dip.Application.Tests/ProjectTruth/PmDecisionTranslatorTests.cs
 M tests/Dip.Application.Tests/ProjectTruth/PmEvidenceTranslatorTests.cs
 M tests/Dip.Application.Tests/ProjectTruth/ProgressEvaluatorTests.cs
 M tests/Dip.Application.Tests/ProjectTruth/ProjectTruthEvaluatorTests.cs
 M tests/Dip.Application.Tests/ProjectTruth/ProjectTruthPolicyTests.cs
 M tests/Dip.Application.Tests/Projects/ProjectServiceTests.cs
 M tests/Dip.Application.Tests/Quality/QualityGateEvaluatorTests.cs
 M tests/Dip.Application.Tests/Quality/QualityRuleEvaluatorTests.cs
RM tests/Dip.Application.Tests/DifficultyFour/AssetQaRuleEngineTests.cs -> tests/Dip.Application.Tests/QualityAutomation/AssetQaRuleEngineTests.cs
 M tests/Dip.Application.Tests/QualityAutomation/BranchQaPolicyTests.cs
RM tests/Dip.Application.Tests/DifficultyFour/ClaimEvidenceAssessmentScoringTests.cs -> tests/Dip.Application.Tests/QualityAutomation/ClaimEvidenceAssessmentScoringTests.cs
 M tests/Dip.Application.Tests/QualityAutomation/CommercialQaPolicyTests.cs
 M tests/Dip.Application.Tests/QualityAutomation/GitBranchQaWorkspaceManagerTests.cs
 M tests/Dip.Application.Tests/QualityAutomation/ProductionQaIntelligenceTests.cs
 M tests/Dip.Application.Tests/QualityAutomation/QaFingerprintTests.cs
RM tests/Dip.Application.Tests/DifficultyFour/VisualComparatorTests.cs -> tests/Dip.Application.Tests/QualityAutomation/VisualComparatorTests.cs
 M tests/Dip.Domain.Tests/DevelopmentGraphTests.cs
 M tests/Dip.Domain.Tests/DevelopmentIntelligence/DevelopmentIntelligenceEntitiesTests.cs
 M tests/Dip.Domain.Tests/Entities/MetricTests.cs
 M tests/Dip.Domain.Tests/Entities/PipelineRunTests.cs
 M tests/Dip.Domain.Tests/EvidenceRecordTests.cs
 M tests/Dip.Domain.Tests/GlobalUsings.cs
 M tests/Dip.Domain.Tests/OutboxMessageTests.cs
 M tests/Dip.Domain.Tests/P1EntitiesTests.cs
 M tests/Dip.Domain.Tests/P2EntitiesTests.cs
 M tests/Dip.Domain.Tests/PipelineRunTests.cs
 M tests/Dip.Domain.Tests/PlatformIntelligence/DurableJobTests.cs
 M tests/Dip.Domain.Tests/PlatformIntelligence/IncidentTests.cs
 M tests/Dip.Domain.Tests/ProcessedWebhookTests.cs
 M tests/Dip.Domain.Tests/ProjectClaimTests.cs
 M tests/Dip.Domain.Tests/QualityAutomation/LanguageIdTests.cs
 M tests/Dip.Domain.Tests/QualityGovernance/QaGovernanceEntitiesTests.cs
 M tests/Dip.Domain.Tests/QualityIssueTests.cs
 M tests/Dip.Domain.Tests/ValueObjects/CommitShaTests.cs
 M tests/Dip.Domain.Tests/ValueObjects/HealthScoreTests.cs
 M tests/Dip.EndToEndTests/Scenarios/CollectorPipelineScenario.cs
 M tests/Dip.EndToEndTests/Scenarios/DailySummaryScenario.cs
 M tests/Dip.EndToEndTests/Scenarios/GameDevelopmentEvidenceScenario.cs
 M tests/Dip.EndToEndTests/Scenarios/P1QaEvidenceScenario.cs
 M tests/Dip.EndToEndTests/Scenarios/P2DecisionIntelligenceScenario.cs
 M tests/Dip.EndToEndTests/Scenarios/P2IntelligenceScenario.cs
 M tests/Dip.EndToEndTests/Scenarios/ProjectTruthScenario.cs
 M tests/Dip.EndToEndTests/Scenarios/PullRequestPipelineScenario.cs
 M tests/Dip.EndToEndTests/Scenarios/QualityGateFailureScenario.cs
 M tests/Dip.EndToEndTests/Support/DipScenarioClient.cs
 M tests/Dip.Infrastructure.Tests/Artifacts/LocalArtifactStoreTests.cs
 M tests/Dip.Infrastructure.Tests/DevelopmentIntelligence/ReplayAndProvenanceSafetyTests.cs
 M tests/Dip.Infrastructure.Tests/GitHubWebhookVerifierTests.cs
 M tests/Dip.Infrastructure.Tests/Intelligence/IntelligenceExecutionSafetyTests.cs
 M tests/Dip.Infrastructure.Tests/P2Intelligence/DeterministicLocalDebugAgentTests.cs
 M tests/Dip.Infrastructure.Tests/P2Intelligence/TestImpactAnalyzerTests.cs
 M tests/Dip.Infrastructure.Tests/Persistence/GovernanceHardeningTests.cs
 M tests/Dip.Infrastructure.Tests/Persistence/MigrationMetadataTests.cs
 M tests/Dip.Infrastructure.Tests/Persistence/ProjectRepositoryTests.cs
 M tests/Dip.Infrastructure.Tests/QualityAutomation/IdeDiagnosticNormalizerTests.cs
 M tests/Dip.Infrastructure.Tests/QualityAutomation/ProcessExecutorSandboxTests.cs
 M tests/Dip.Infrastructure.Tests/QualityAutomation/QaAdapterRegistryTests.cs
 M tests/Dip.Infrastructure.Tests/QualityAutomation/QaCapabilityPlannerTests.cs
 M tests/Dip.Infrastructure.Tests/QualityAutomation/RepositoryTechnologyDetectorTests.cs
 M tests/Dip.Infrastructure.Tests/QualityAutomation/UniversalDiagnosticNormalizerTests.cs
 M tests/Dip.Infrastructure.Tests/QualityAutomation/UniversalSemanticAnalyzerTests.cs
 M tests/Dip.Infrastructure.Tests/QualityAutomation/UniversalSyntaxTreeTests.cs
 M tests/Dip.IntegrationTests/Api/CustomWebApplicationFactory.cs
 M tests/Dip.IntegrationTests/Api/ProjectsApiTests.cs
 M tests/Dip.IntegrationTests/Database/PostgreSqlFixture.cs
 M tests/Dip.IntegrationTests/GovernanceAuthorizationSurfaceTests.cs
 M tests/Dip.IntegrationTests/ManagementOperationsAuthorizationSurfaceTests.cs
 M tests/Dip.IntegrationTests/ProjectIntelligenceAuthorizationSurfaceTests.cs
 M tests/load/k6/dashboard-load.js
 M tests/load/k6/pipeline-load.js
 M tests/load/k6/webhook-load.js
?? docs/code-guide/
?? tests/Dip.Application.Tests/ArtifactTracking/
?? tests/Dip.Application.Tests/EvidenceIntegration/
```
