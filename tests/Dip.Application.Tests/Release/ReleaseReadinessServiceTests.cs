// Purpose: Verifies ReleaseReadinessService fail-closed behavior with explicit repository fakes for exact-revision release scenarios.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Release;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Tests.Release;

public sealed class ReleaseReadinessServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 0, 0, 0, TimeSpan.Zero);
    private static readonly string Sha = new('a', 40);

    [Fact]
    public async Task EvaluateAsync_RejectsEmptyProjectId()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.EvaluateAsync(new(Guid.Empty, Sha), CancellationToken.None));
    }

    [Fact]
    public async Task EvaluateAsync_RejectsMissingCommitSha()
    {
        var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Service.EvaluateAsync(new(Guid.NewGuid(), " "), CancellationToken.None));
    }

    [Fact]
    public async Task EvaluateAsync_BlocksWrongShaQaProvenance()
    {
        var fixture = Fixture.Valid();
        fixture.Qa.Provenance = Provenance(fixture.ProjectId, new string('b', 40), Now);

        var result = await fixture.Service.EvaluateAsync(new(fixture.ProjectId, Sha), CancellationToken.None);

        Assert.Equal("NoGo", result.Decision);
        Assert.Contains(result.Blockers, x => x.Contains("different commit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_BlocksStaleQaProvenance()
    {
        var fixture = Fixture.Valid();
        fixture.Qa.Provenance = Provenance(fixture.ProjectId, Sha, Now.AddDays(-31));

        var result = await fixture.Service.EvaluateAsync(new(fixture.ProjectId, Sha, EvidenceFreshnessDays: 14), CancellationToken.None);

        Assert.Equal("NoGo", result.Decision);
        Assert.Contains(result.Blockers, x => x.Contains("stale", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_BlocksQaNoGoWithoutOverride()
    {
        var fixture = Fixture.Valid();
        fixture.Qa.PolicyEvaluation = PolicyEvaluation(fixture.ProjectId, QaGateVerdict.NoGo, "blocking test failure");

        var result = await fixture.Service.EvaluateAsync(new(fixture.ProjectId, Sha), CancellationToken.None);

        Assert.Equal("NoGo", result.Decision);
        Assert.Contains(result.Blockers, x => x.Contains("QA policy gate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_BlocksMalformedDevelopmentGraphMetadata()
    {
        var fixture = Fixture.Valid();
        fixture.Graph.Nodes =
        [
            new DevelopmentGraphNode(
                Guid.NewGuid(), fixture.ProjectId, DevelopmentGraphNodeType.Requirement,
                "req:bad", "Malformed requirement", "Completed", "test", null, "{not-json", Now)
        ];

        var result = await fixture.Service.EvaluateAsync(new(fixture.ProjectId, Sha), CancellationToken.None);

        Assert.Equal("NoGo", result.Decision);
        Assert.Contains(result.Blockers, x => x.Contains("malformed metadata", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_BlocksProjectTruthBlocker()
    {
        var fixture = Fixture.Valid();
        fixture.Truth.Latest = Truth(fixture.ProjectId, blockerCount: 1);

        var result = await fixture.Service.EvaluateAsync(new(fixture.ProjectId, Sha), CancellationToken.None);

        Assert.Equal("NoGo", result.Decision);
        Assert.Contains(result.Blockers, x => x.Contains("Project Truth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_BlocksFailedPerformanceBudget()
    {
        var fixture = Fixture.Valid();
        fixture.Budgets.Evaluations =
        [
            new PerformanceBudgetEvaluation(
                Guid.NewGuid(), fixture.ProjectId, Guid.NewGuid(), null, Guid.NewGuid(),
                PerformanceBudgetVerdict.Failed, "p95 budget failed", "{}", Now)
        ];

        var result = await fixture.Service.EvaluateAsync(new(fixture.ProjectId, Sha), CancellationToken.None);

        Assert.Equal("NoGo", result.Decision);
        Assert.Contains(result.Blockers, x => x.Contains("performance budget", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsGoWhenCurrentEvidenceIsCompleteAndPassing()
    {
        var fixture = Fixture.Valid();

        var result = await fixture.Service.EvaluateAsync(new(fixture.ProjectId, Sha), CancellationToken.None);

        Assert.Equal("Go", result.Decision);
        Assert.Empty(result.Blockers);
        Assert.Empty(result.Warnings);
        Assert.Equal(Sha, result.CommitSha);
        Assert.Single(fixture.Readiness.Added);
    }

    private static QaPolicyEvaluation PolicyEvaluation(Guid projectId, QaGateVerdict verdict, string summary) =>
        new(Guid.NewGuid(), projectId, Guid.NewGuid(), null, "test-policy", verdict, "[]", "[]", summary, Now);

    private static QaEvidenceProvenance Provenance(Guid projectId, string sha, DateTimeOffset capturedAt) =>
        new(Guid.NewGuid(), projectId, Guid.NewGuid(), sha, "env", "{}", "2.0", "test-policy", new string('1', 64), new string('2', 64), capturedAt);

    private static ProjectTruthSnapshot Truth(Guid projectId, int blockerCount = 0) =>
        new(
            Guid.NewGuid(), projectId, ProjectTruthStatus.Healthy, ProjectRiskLevel.Low,
            100m, 100m, 100m, EvidenceConfidence.High, blockerCount, 0, 0,
            "Project truth is healthy.", "[]", "[]", Now);

    private sealed class Fixture
    {
        public Guid ProjectId { get; } = Guid.NewGuid();
        public FakeIncidentRepository Incidents { get; } = new();
        public FakeDurableJobRepository Jobs { get; } = new();
        public FakePerformanceBudgetRepository Budgets { get; } = new();
        public FakeProjectTruthRepository Truth { get; } = new();
        public FakeEvidenceRepository Evidence { get; } = new();
        public FakeChangeRiskRepository Risk { get; } = new();
        public FakeReleaseEvidenceRepository ReleaseEvidence { get; } = new();
        public FakeReleaseReadinessRepository Readiness { get; } = new();
        public FakeDevelopmentGraphRepository Graph { get; } = new();
        public FakeQaGovernanceRepository Qa { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeClock Clock { get; } = new(Now);

        public ReleaseReadinessService Service => new(
            Incidents, Jobs, Budgets, Truth, Evidence, Risk, ReleaseEvidence,
            Readiness, Graph, Qa, UnitOfWork, Clock);

        public static Fixture Valid()
        {
            var fixture = new Fixture();
            fixture.Budgets.Evaluations =
            [
                new PerformanceBudgetEvaluation(
                    Guid.NewGuid(), fixture.ProjectId, Guid.NewGuid(), null, Guid.NewGuid(),
                    PerformanceBudgetVerdict.Passed, "performance budget passed", "{}", Now)
            ];
            fixture.Evidence.Items =
            [
                new EvidenceRecord(
                    Guid.NewGuid(), fixture.ProjectId, null, "release", EvidenceSourceType.System,
                    "release-check", EvidenceOutcome.Succeeded, "Release check", "Current release evidence.",
                    EvidenceConfidence.High, null, "{}", Now, Now)
            ];
            fixture.Truth.Latest = Truth(fixture.ProjectId);
            fixture.Qa.PolicyEvaluation = PolicyEvaluation(fixture.ProjectId, QaGateVerdict.Go, "GO");
            fixture.Qa.Provenance = Provenance(fixture.ProjectId, Sha, Now);
            fixture.ReleaseEvidence.Latest = new ReleaseEvidenceBundle(
                Guid.NewGuid(), fixture.ProjectId, Sha, "build-1", ReleaseEvidenceBundleStatus.Succeeded,
                new string('1', 32), new string('2', 32), new string('3', 32), new string('a', 64), Now);
            return fixture;
        }
    }

    private sealed class FakeClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow => utcNow;
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            Saves++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeIncidentRepository : IIncidentRepository
    {
        public IReadOnlyList<Incident> Recent { get; set; } = [];
        public Task<Incident?> FindByFingerprintAsync(Guid projectId,string fingerprint,CancellationToken ct)=>Task.FromResult<Incident?>(null);
        public Task<Incident?> GetAsync(Guid id,CancellationToken ct)=>Task.FromResult<Incident?>(null);
        public Task<Incident?> FindLatestOpenForSubjectAsync(Guid projectId,string subjectKey,CancellationToken ct)=>Task.FromResult<Incident?>(null);
        public Task AddAsync(Incident incident,CancellationToken ct)=>Task.CompletedTask;
        public Task AddOccurrenceAsync(IncidentOccurrence occurrence,CancellationToken ct)=>Task.CompletedTask;
        public Task AddEventAsync(IncidentEvent incidentEvent,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<Incident>> ListRecentAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult(Recent);
        public Task<IReadOnlyList<IncidentEvent>> ListEventsAsync(Guid incidentId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<IncidentEvent>>([]);
        public Task<IReadOnlyList<IncidentOccurrence>> ListOccurrencesAsync(Guid incidentId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<IncidentOccurrence>>([]);
    }

    private sealed class FakeDurableJobRepository : IDurableJobRepository
    {
        public IReadOnlyList<DurableJob> Recent { get; set; } = [];
        public Task AddAsync(DurableJob job,CancellationToken ct)=>Task.CompletedTask;
        public Task<DurableJob?> GetAsync(Guid id,CancellationToken ct)=>Task.FromResult<DurableJob?>(null);
        public Task<IReadOnlyList<DurableJob>> ListRecentAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult(Recent);
        public Task<DurableJob?> TryClaimAsync(Guid id,string owner,DateTimeOffset now,TimeSpan lease,CancellationToken ct)=>Task.FromResult<DurableJob?>(null);
        public Task<IReadOnlyList<DurableJob>> ListExpiredLeasesAsync(DateTimeOffset now,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<DurableJob>>([]);
        public Task<long> CountQueuedAsync(CancellationToken ct)=>Task.FromResult(0L);
    }

    private sealed class FakePerformanceBudgetRepository : IPerformanceBudgetRepository
    {
        public IReadOnlyList<PerformanceBudgetEvaluation> Evaluations { get; set; } = [];
        public Task AddProfileAsync(PerformanceBudgetProfile profile,CancellationToken ct)=>Task.CompletedTask;
        public Task<PerformanceBudgetProfile?> GetProfileAsync(Guid id,CancellationToken ct)=>Task.FromResult<PerformanceBudgetProfile?>(null);
        public Task<PerformanceBudgetProfile?> GetProfileByKeyAsync(Guid? projectId,string key,CancellationToken ct)=>Task.FromResult<PerformanceBudgetProfile?>(null);
        public Task<IReadOnlyList<PerformanceBudgetProfile>> ListProfilesAsync(Guid? projectId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<PerformanceBudgetProfile>>([]);
        public Task AddEvaluationAsync(PerformanceBudgetEvaluation evaluation,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<PerformanceBudgetEvaluation>> ListEvaluationsAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult(Evaluations);
    }

    private sealed class FakeProjectTruthRepository : IProjectTruthRepository
    {
        public ProjectTruthSnapshot? Latest { get; set; }
        public Task<ProjectTruthSnapshot?> GetLatestAsync(Guid projectId,CancellationToken ct)=>Task.FromResult(Latest);
        public Task<IReadOnlyList<ProjectTruthSnapshot>> ListRecentAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<ProjectTruthSnapshot>>(Latest is null?[]:[Latest]);
        public Task AddAsync(ProjectTruthSnapshot snapshot,CancellationToken ct){Latest=snapshot;return Task.CompletedTask;}
    }

    private sealed class FakeEvidenceRepository : IEvidenceRepository
    {
        public IReadOnlyList<EvidenceRecord> Items { get; set; } = [];
        public Task AddAsync(EvidenceRecord evidence,CancellationToken ct)=>Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<EvidenceRecord> evidence,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(Guid projectId,DateTimeOffset? observedSince,CancellationToken ct)=>Task.FromResult(Items);
    }

    private sealed class FakeChangeRiskRepository : IChangeRiskRepository
    {
        public ChangeRiskAssessment? Latest { get; set; }
        public Task AddAsync(ChangeRiskAssessment assessment,CancellationToken ct){Latest=assessment;return Task.CompletedTask;}
        public Task<ChangeRiskAssessment?> GetLatestAsync(Guid projectId,string? commitSha,CancellationToken ct)=>Task.FromResult(Latest);
        public Task<IReadOnlyList<ChangeRiskAssessment>> ListAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<ChangeRiskAssessment>>(Latest is null?[]:[Latest]);
    }

    private sealed class FakeReleaseEvidenceRepository : IReleaseEvidenceRepository
    {
        public ReleaseEvidenceBundle? Latest { get; set; }
        public Task AddAsync(ReleaseEvidenceBundle bundle,CancellationToken ct){Latest=bundle;return Task.CompletedTask;}
        public Task<ReleaseEvidenceBundle?> GetLatestAsync(Guid projectId,CancellationToken ct)=>Task.FromResult(Latest);
        public Task<IReadOnlyList<ReleaseEvidenceBundle>> ListAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<ReleaseEvidenceBundle>>(Latest is null?[]:[Latest]);
    }

    private sealed class FakeReleaseReadinessRepository : IReleaseReadinessRepository
    {
        public List<ReleaseReadinessSnapshot> Added { get; } = [];
        public Task AddAsync(ReleaseReadinessSnapshot snapshot,CancellationToken ct){Added.Add(snapshot);return Task.CompletedTask;}
        public Task<ReleaseReadinessSnapshot?> GetLatestAsync(Guid projectId,CancellationToken ct)=>Task.FromResult(Added.LastOrDefault());
        public Task<IReadOnlyList<ReleaseReadinessSnapshot>> ListAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<ReleaseReadinessSnapshot>>(Added.TakeLast(take).ToArray());
    }

    private sealed class FakeDevelopmentGraphRepository : IDevelopmentGraphRepository
    {
        public IReadOnlyList<DevelopmentGraphNode> Nodes { get; set; } = [];
        public Task<DevelopmentGraphNode?> GetNodeAsync(Guid id,CancellationToken ct)=>Task.FromResult(Nodes.FirstOrDefault(x=>x.Id==id));
        public Task<DevelopmentGraphNode?> FindNodeAsync(Guid projectId,DevelopmentGraphNodeType type,string key,CancellationToken ct)=>Task.FromResult(Nodes.FirstOrDefault(x=>x.ProjectId==projectId&&x.Type==type&&x.Key==key));
        public Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(Guid projectId,DevelopmentGraphNodeType? type,CancellationToken ct)=>Task.FromResult<IReadOnlyList<DevelopmentGraphNode>>(Nodes.Where(x=>x.ProjectId==projectId&&(!type.HasValue||x.Type==type.Value)).ToArray());
        public Task AddNodeAsync(DevelopmentGraphNode node,CancellationToken ct)=>Task.CompletedTask;
        public Task<DevelopmentGraphEdge?> FindEdgeAsync(Guid projectId,Guid sourceNodeId,Guid targetNodeId,DevelopmentGraphEdgeType type,CancellationToken ct)=>Task.FromResult<DevelopmentGraphEdge?>(null);
        public Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(Guid projectId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<DevelopmentGraphEdge>>([]);
        public Task AddEdgeAsync(DevelopmentGraphEdge edge,CancellationToken ct)=>Task.CompletedTask;
    }

    private sealed class FakeQaGovernanceRepository : IQaGovernanceRepository
    {
        public QaPolicy? ActivePolicy { get; set; }
        public QaPolicyEvaluation? PolicyEvaluation { get; set; }
        public QaEvidenceProvenance? Provenance { get; set; }
        public IReadOnlyList<QaReleaseGateOverride> Overrides { get; set; } = [];
        public Task<QaFindingState?> FindFindingStateAsync(Guid projectId,string fingerprint,CancellationToken ct)=>Task.FromResult<QaFindingState?>(null);
        public Task AddFindingStateAsync(QaFindingState state,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaFindingState>> ListFindingStatesAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaFindingState>>([]);
        public Task<IReadOnlyList<QaFindingState>> ListFindingStatesPageAsync(Guid projectId,DateTimeOffset? before,string? beforeFingerprint,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaFindingState>>([]);
        public Task<QaWaiver?> GetWaiverAsync(Guid id,CancellationToken ct)=>Task.FromResult<QaWaiver?>(null);
        public Task AddWaiverAsync(QaWaiver waiver,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaWaiver>> ListWaiversAsync(Guid projectId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaWaiver>>([]);
        public Task<QaTestReliability?> FindReliabilityAsync(Guid projectId,string testKey,CancellationToken ct)=>Task.FromResult<QaTestReliability?>(null);
        public Task AddReliabilityAsync(QaTestReliability reliability,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaTestReliability>> ListReliabilityAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaTestReliability>>([]);
        public Task<QaRegressionBaseline?> FindBaselineAsync(Guid projectId,string subjectKey,string metricKey,CancellationToken ct)=>Task.FromResult<QaRegressionBaseline?>(null);
        public Task AddBaselineAsync(QaRegressionBaseline baseline,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaRegressionBaseline>> ListBaselinesAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaRegressionBaseline>>([]);
        public Task AddOwnershipRuleAsync(QaOwnershipRule rule,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaOwnershipRule>> ListOwnershipRulesAsync(Guid projectId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaOwnershipRule>>([]);
        public Task AddPolicyAsync(QaPolicy policy,CancellationToken ct){ActivePolicy=policy;return Task.CompletedTask;}
        public Task<QaPolicy?> GetActivePolicyAsync(Guid projectId,CancellationToken ct)=>Task.FromResult(ActivePolicy);
        public Task AddPolicyEvaluationAsync(QaPolicyEvaluation evaluation,CancellationToken ct){PolicyEvaluation=evaluation;return Task.CompletedTask;}
        public Task<QaPolicyEvaluation?> GetLatestPolicyEvaluationAsync(Guid projectId,CancellationToken ct)=>Task.FromResult(PolicyEvaluation);
        public Task AddProvenanceAsync(QaEvidenceProvenance provenance,CancellationToken ct){Provenance=provenance;return Task.CompletedTask;}
        public Task<QaEvidenceProvenance?> GetLatestProvenanceAsync(Guid projectId,CancellationToken ct)=>Task.FromResult(Provenance);
        public Task AddSystemHealthAsync(QaSystemHealthSnapshot snapshot,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaSystemHealthSnapshot>> ListSystemHealthAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaSystemHealthSnapshot>>([]);
        public Task AddTrendAsync(QaQualityTrendSnapshot snapshot,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaQualityTrendSnapshot>> ListTrendsAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaQualityTrendSnapshot>>([]);
        public Task AddTestImpactRuleAsync(QaTestImpactRule rule,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaTestImpactRule>> ListTestImpactRulesAsync(Guid projectId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaTestImpactRule>>([]);
        public Task AddRetentionRuleAsync(QaArtifactRetentionRule rule,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaArtifactRetentionRule>> ListRetentionRulesAsync(Guid projectId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaArtifactRetentionRule>>([]);
        public Task AddCostAsync(QaRunCostRecord cost,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaRunCostRecord>> ListCostsAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaRunCostRecord>>([]);
        public Task AddMinimizationAsync(QaBugMinimizationRecord record,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaBugMinimizationRecord>> ListMinimizationsAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaBugMinimizationRecord>>([]);
        public Task<QaGovernanceProcessedRun?> GetProcessedRunAsync(Guid qaRunId,CancellationToken ct)=>Task.FromResult<QaGovernanceProcessedRun?>(null);
        public Task AddProcessedRunAsync(QaGovernanceProcessedRun record,CancellationToken ct)=>Task.CompletedTask;
        public Task<QaPolicy?> GetPolicyAsync(Guid id,CancellationToken ct)=>Task.FromResult(ActivePolicy?.Id==id?ActivePolicy:null);
        public Task<IReadOnlyList<QaPolicy>> ListPoliciesAsync(Guid projectId,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaPolicy>>(ActivePolicy is null?[]:[ActivePolicy]);
        public Task<QaReleaseGateOverride?> GetReleaseOverrideAsync(Guid id,CancellationToken ct)=>Task.FromResult(Overrides.FirstOrDefault(x=>x.Id==id));
        public Task AddReleaseOverrideAsync(QaReleaseGateOverride record,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaReleaseGateOverride>> ListReleaseOverridesAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult(Overrides);
        public Task AddBackupVerificationAsync(QaBackupVerificationRecord record,CancellationToken ct)=>Task.CompletedTask;
        public Task<IReadOnlyList<QaBackupVerificationRecord>> ListBackupVerificationsAsync(Guid projectId,int take,CancellationToken ct)=>Task.FromResult<IReadOnlyList<QaBackupVerificationRecord>>([]);
    }
}
