// Purpose: Verifies finding lifecycle processing and the composed QA governance pipeline with stateful repository doubles.
using Dip.Application.Abstractions.Observability;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityGovernance;

public sealed class QaFindingLifecycleProcessorTests
{
    private const string CommitSha = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task ProcessCompletedRunAsync_ExercisesGovernancePipelineAndReadModels()
    {
        var now = new DateTimeOffset(2026, 9, 23, 5, 0, 0, TimeSpan.Zero);
        var projectId = Guid.NewGuid();
        var artifactRoot = Directory.CreateTempSubdirectory("dip-qa-governance-").FullName;
        var repository = new InMemoryQaGovernanceRepository();
        var unitOfWork = new TrackingUnitOfWork();
        var clock = new FixedClock(now);
        var metrics = new RecordingMetrics();

        var lifecycle = new QaFindingLifecycleProcessor(
            repository,
            unitOfWork,
            new NullOwnershipResolver(),
            new EmptyFindingRepository());
        var reliability = new QaReliabilityProcessor(repository, unitOfWork);
        var core = new QaGovernanceCoreEngine(
            repository,
            unitOfWork,
            clock,
            metrics,
            lifecycle,
            reliability,
            new QaBaselineProcessor(repository),
            new QaPolicyEvaluator(),
            new QaProvenanceBuilder(),
            new QaHealthEvaluator(),
            new QaCostEvaluator(repository),
            new QaBenchmarkComparisonService(null!, null!));

        try
        {
            var healthyRun = CompletedRun(
                projectId,
                artifactRoot,
                QaRunStatus.Succeeded,
                now.AddMinutes(-2),
                0,
                0,
                0);
            await core.ProcessCompletedRunAsync(
                healthyRun,
                Array.Empty<QaFinding>(),
                CancellationToken.None);

            var failingRun = CompletedRun(
                projectId,
                artifactRoot,
                QaRunStatus.Failed,
                now.AddMinutes(-1),
                1,
                0,
                1);
            var finding = new QaFinding(
                Guid.NewGuid(),
                projectId,
                failingRun.Id,
                "unit-test",
                new LanguageId("csharp"),
                "test.failure",
                Severity.Warning,
                "dotnet-test",
                "Example test",
                "Deterministic assertion failure.",
                "fp-test",
                "src/Example.cs",
                42,
                3,
                "Example",
                "XunitException",
                "stack",
                "TEST001",
                now);
            finding.ApplyReproduction(ReproductionStatus.Reproduced, 1, 3);

            await core.ProcessCompletedRunAsync(
                failingRun,
                [finding],
                CancellationToken.None);

            await repository.AddTestImpactRuleAsync(
                new QaTestImpactRule(
                    Guid.NewGuid(),
                    projectId,
                    "src/**",
                    "targeted-tests",
                    "{\"command\":\"dotnet test\"}",
                    100,
                    true,
                    now),
                CancellationToken.None);

            var overview = await core.GetOverviewAsync(projectId, CancellationToken.None);
            var selected = await core.SelectTestsAsync(
                new SmartTestSelectionRequest(projectId, ["src/Example.cs"], 20),
                CancellationToken.None);
            var recommendations = await core.RecommendTestsAsync(
                projectId,
                ["src/Example.cs"],
                CancellationToken.None);
            var freshness = await core.GetFreshnessAsync(
                projectId,
                14,
                CancellationToken.None);
            var correlation = await core.CorrelateAsync(
                projectId,
                finding.Fingerprint,
                CancellationToken.None);
            var cost = await core.GetCostSummaryAsync(
                projectId,
                20,
                CancellationToken.None);

            (await core.ListFindingStatesAsync(projectId, 20, CancellationToken.None))
                .Should().ContainSingle(x => x.Fingerprint == finding.Fingerprint);
            (await core.ListReliabilityAsync(projectId, 20, CancellationToken.None))
                .Should().Contain(x => x.TestKey == "test:Example test");
            (await core.ListBaselinesAsync(projectId, 20, CancellationToken.None))
                .Should().Contain(x => x.Status == QaRegressionStatus.Regressed);
            (await core.ListSystemHealthAsync(projectId, 20, CancellationToken.None))
                .Should().NotBeEmpty();
            (await core.ListTrendsAsync(projectId, 20, CancellationToken.None))
                .Should().HaveCount(2);
            (await core.ListOwnershipRulesAsync(projectId, CancellationToken.None))
                .Should().BeEmpty();
            (await core.ListWaiversAsync(projectId, CancellationToken.None))
                .Should().BeEmpty();
            (await core.ListMinimizationsAsync(projectId, 20, CancellationToken.None))
                .Should().BeEmpty();
            (await core.GetActivePolicyAsync(projectId, CancellationToken.None))
                .Should().BeNull();

            overview.Should().NotBeNull();
            selected.Should().ContainSingle(x => x.TestKey == "targeted-tests");
            recommendations.Should().NotBeEmpty();
            freshness.Status.Should().Be(QaFreshnessStatus.Fresh.ToString());
            correlation.Fingerprint.Should().Be(finding.Fingerprint);
            cost.RunCount.Should().Be(2);
            QaGovernanceCoreEngine.GetDiagnosticSchema().SchemaVersion
                .Should().Be("dip.finding.v2");

            repository.PolicyEvaluations.Should().HaveCount(2);
            repository.Provenance.Should().HaveCount(2);
            repository.Costs.Should().HaveCount(2);
            metrics.CompletedRuns.Should().Be(2);
            unitOfWork.SaveCount.Should().BeGreaterThan(2);
        }
        finally
        {
            Directory.Delete(artifactRoot, true);
        }
    }

    private static QaRun CompletedRun(
        Guid projectId,
        string artifactRoot,
        QaRunStatus status,
        DateTimeOffset startedAt,
        int findings,
        int critical,
        int warning)
    {
        var summary =
            "{\"tools\":[{\"Tool\":\"dotnet-test\",\"WasAvailable\":true,"
            + "\"SandboxJson\":\"{\\\"Enabled\\\":true}\",\"DurationMs\":120,\"CpuTimeMs\":60}],"
            + "\"adapterFailures\":[],\"unavailableLanguages\":[],"
            + "\"unsupportedDetectedLanguages\":[]}";

        var run = new QaRun(
            Guid.NewGuid(),
            projectId,
            "unit-test",
            new LanguageId("csharp"),
            ".",
            CommitSha,
            artifactRoot,
            startedAt.AddMinutes(-1));
        run.Start(startedAt);
        run.Complete(
            status,
            findings,
            critical,
            warning,
            summary,
            startedAt.AddSeconds(30));
        return run;
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class TrackingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class RecordingMetrics : IDipOperationalMetrics
    {
        public int CompletedRuns { get; private set; }

        public void JobStarted(string type) { }
        public void JobFinished(string type, string status, TimeSpan duration) { }
        public void ArtifactStored(string provider, long bytes) { }
        public void LeaseRecovered() { }
        public void QueueDepthObserved(long depth) { }

        public void GovernanceRunCompleted(
            string runStatus,
            string gateVerdict,
            int openFindings,
            int flakyTests,
            int activeWaivers,
            string selfHealth)
        {
            CompletedRuns++;
        }

        public void GovernanceMutation(string action, string status = "success") { }
        public void GovernanceSandboxDenied(string reason) { }
        public string RenderPrometheus() => string.Empty;
    }

    private sealed class NullOwnershipResolver : IRepositoryOwnershipResolver
    {
        public Task<RepositoryOwnershipResolution?> ResolveAsync(
            string workspaceRelativePath,
            string? filePath,
            CancellationToken ct) =>
            Task.FromResult<RepositoryOwnershipResolution?>(null);
    }

    private sealed class EmptyFindingRepository : IQaFindingRepository
    {
        public Task AddRangeAsync(IEnumerable<QaFinding> findings, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<QaFinding?> GetAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<QaFinding?>(null);

        public Task<IReadOnlyList<QaFinding>> ListByRunAsync(
            Guid runId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaFinding>>([]);

        public Task<IReadOnlyList<QaFinding>> ListRecentByProjectAsync(
            Guid projectId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaFinding>>([]);
    }

    private sealed class InMemoryQaGovernanceRepository : IQaGovernanceRepository
    {
        public List<QaFindingState> FindingStates { get; } = [];
        public List<QaWaiver> Waivers { get; } = [];
        public List<QaTestReliability> Reliability { get; } = [];
        public List<QaRegressionBaseline> Baselines { get; } = [];
        public List<QaOwnershipRule> OwnershipRules { get; } = [];
        public List<QaPolicyEvaluation> PolicyEvaluations { get; } = [];
        public List<QaEvidenceProvenance> Provenance { get; } = [];
        public List<QaSystemHealthSnapshot> Health { get; } = [];
        public List<QaQualityTrendSnapshot> Trends { get; } = [];
        public List<QaTestImpactRule> TestImpactRules { get; } = [];
        public List<QaArtifactRetentionRule> RetentionRules { get; } = [];
        public List<QaRunCostRecord> Costs { get; } = [];
        public List<QaBugMinimizationRecord> Minimizations { get; } = [];
        public List<QaReleaseGateOverride> Overrides { get; } = [];
        public List<QaBackupVerificationRecord> BackupVerifications { get; } = [];
        public List<QaGovernanceProcessedRun> ProcessedRuns { get; } = [];
        public QaPolicy? ActivePolicy { get; set; }

        public Task<QaFindingState?> FindFindingStateAsync(
            Guid projectId,
            string fingerprint,
            CancellationToken ct) =>
            Task.FromResult(FindingStates.FirstOrDefault(
                x => x.ProjectId == projectId
                     && x.Fingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase)));

        public Task AddFindingStateAsync(QaFindingState state, CancellationToken ct)
        {
            FindingStates.Add(state);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaFindingState>> ListFindingStatesAsync(
            Guid projectId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaFindingState>>(
                FindingStates.Where(x => x.ProjectId == projectId).Take(take).ToArray());

        public Task<IReadOnlyList<QaFindingState>> ListFindingStatesPageAsync(
            Guid projectId,
            DateTimeOffset? before,
            string? beforeFingerprint,
            int take,
            CancellationToken ct) =>
            ListFindingStatesAsync(projectId, take, ct);

        public Task<QaWaiver?> GetWaiverAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Waivers.FirstOrDefault(x => x.Id == id));

        public Task AddWaiverAsync(QaWaiver waiver, CancellationToken ct)
        {
            Waivers.Add(waiver);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaWaiver>> ListWaiversAsync(
            Guid projectId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaWaiver>>(
                Waivers.Where(x => x.ProjectId == projectId).ToArray());

        public Task<QaTestReliability?> FindReliabilityAsync(
            Guid projectId,
            string testKey,
            CancellationToken ct) =>
            Task.FromResult(Reliability.FirstOrDefault(
                x => x.ProjectId == projectId
                     && x.TestKey.Equals(testKey, StringComparison.OrdinalIgnoreCase)));

        public Task AddReliabilityAsync(QaTestReliability reliability, CancellationToken ct)
        {
            Reliability.Add(reliability);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaTestReliability>> ListReliabilityAsync(
            Guid projectId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaTestReliability>>(
                Reliability.Where(x => x.ProjectId == projectId).Take(take).ToArray());

        public Task<QaRegressionBaseline?> FindBaselineAsync(
            Guid projectId,
            string subjectKey,
            string metricKey,
            CancellationToken ct) =>
            Task.FromResult(Baselines.FirstOrDefault(
                x => x.ProjectId == projectId
                     && x.SubjectKey.Equals(subjectKey, StringComparison.OrdinalIgnoreCase)
                     && x.MetricKey.Equals(metricKey, StringComparison.OrdinalIgnoreCase)));

        public Task AddBaselineAsync(QaRegressionBaseline baseline, CancellationToken ct)
        {
            Baselines.Add(baseline);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaRegressionBaseline>> ListBaselinesAsync(
            Guid projectId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaRegressionBaseline>>(
                Baselines.Where(x => x.ProjectId == projectId).Take(take).ToArray());

        public Task AddOwnershipRuleAsync(QaOwnershipRule rule, CancellationToken ct)
        {
            OwnershipRules.Add(rule);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaOwnershipRule>> ListOwnershipRulesAsync(
            Guid projectId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaOwnershipRule>>(
                OwnershipRules.Where(x => x.ProjectId == projectId).ToArray());

        public Task AddPolicyAsync(QaPolicy policy, CancellationToken ct)
        {
            ActivePolicy = policy;
            return Task.CompletedTask;
        }

        public Task<QaPolicy?> GetActivePolicyAsync(Guid projectId, CancellationToken ct) =>
            Task.FromResult(ActivePolicy?.ProjectId == projectId ? ActivePolicy : null);

        public Task AddPolicyEvaluationAsync(QaPolicyEvaluation evaluation, CancellationToken ct)
        {
            PolicyEvaluations.Add(evaluation);
            return Task.CompletedTask;
        }

        public Task<QaPolicyEvaluation?> GetLatestPolicyEvaluationAsync(
            Guid projectId,
            CancellationToken ct) =>
            Task.FromResult(PolicyEvaluations.LastOrDefault(x => x.ProjectId == projectId));

        public Task AddProvenanceAsync(QaEvidenceProvenance provenance, CancellationToken ct)
        {
            Provenance.Add(provenance);
            return Task.CompletedTask;
        }

        public Task<QaEvidenceProvenance?> GetLatestProvenanceAsync(
            Guid projectId,
            CancellationToken ct) =>
            Task.FromResult(Provenance.LastOrDefault(x => x.ProjectId == projectId));

        public Task AddSystemHealthAsync(QaSystemHealthSnapshot snapshot, CancellationToken ct)
        {
            Health.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaSystemHealthSnapshot>> ListSystemHealthAsync(
            Guid projectId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaSystemHealthSnapshot>>(
                Health.Where(x => x.ProjectId == projectId)
                    .OrderByDescending(x => x.ObservedAtUtc)
                    .Take(take)
                    .ToArray());

        public Task AddTrendAsync(QaQualityTrendSnapshot snapshot, CancellationToken ct)
        {
            Trends.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaQualityTrendSnapshot>> ListTrendsAsync(
            Guid projectId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaQualityTrendSnapshot>>(
                Trends.Where(x => x.ProjectId == projectId)
                    .OrderByDescending(x => x.CapturedAtUtc)
                    .Take(take)
                    .ToArray());

        public Task AddTestImpactRuleAsync(QaTestImpactRule rule, CancellationToken ct)
        {
            TestImpactRules.Add(rule);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaTestImpactRule>> ListTestImpactRulesAsync(
            Guid projectId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaTestImpactRule>>(
                TestImpactRules.Where(x => x.ProjectId == projectId).ToArray());

        public Task AddRetentionRuleAsync(QaArtifactRetentionRule rule, CancellationToken ct)
        {
            RetentionRules.Add(rule);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaArtifactRetentionRule>> ListRetentionRulesAsync(
            Guid projectId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaArtifactRetentionRule>>(
                RetentionRules.Where(x => x.ProjectId == projectId).ToArray());

        public Task AddCostAsync(QaRunCostRecord cost, CancellationToken ct)
        {
            Costs.Add(cost);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaRunCostRecord>> ListCostsAsync(
            Guid projectId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaRunCostRecord>>(
                Costs.Where(x => x.ProjectId == projectId).Take(take).ToArray());

        public Task AddMinimizationAsync(QaBugMinimizationRecord record, CancellationToken ct)
        {
            Minimizations.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaBugMinimizationRecord>> ListMinimizationsAsync(
            Guid projectId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaBugMinimizationRecord>>(
                Minimizations.Where(x => x.ProjectId == projectId).Take(take).ToArray());

        public Task<QaGovernanceProcessedRun?> GetProcessedRunAsync(
            Guid qaRunId,
            CancellationToken ct) =>
            Task.FromResult(ProcessedRuns.FirstOrDefault(x => x.QaRunId == qaRunId));

        public Task AddProcessedRunAsync(QaGovernanceProcessedRun record, CancellationToken ct)
        {
            ProcessedRuns.Add(record);
            return Task.CompletedTask;
        }

        public Task<QaPolicy?> GetPolicyAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(ActivePolicy?.Id == id ? ActivePolicy : null);

        public Task<IReadOnlyList<QaPolicy>> ListPoliciesAsync(
            Guid projectId,
            CancellationToken ct)
        {
            IReadOnlyList<QaPolicy> result =
                ActivePolicy?.ProjectId == projectId ? [ActivePolicy] : [];
            return Task.FromResult(result);
        }

        public Task<QaReleaseGateOverride?> GetReleaseOverrideAsync(
            Guid id,
            CancellationToken ct) =>
            Task.FromResult(Overrides.FirstOrDefault(x => x.Id == id));

        public Task AddReleaseOverrideAsync(
            QaReleaseGateOverride record,
            CancellationToken ct)
        {
            Overrides.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaReleaseGateOverride>> ListReleaseOverridesAsync(
            Guid projectId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaReleaseGateOverride>>(
                Overrides.Where(x => x.ProjectId == projectId).Take(take).ToArray());

        public Task AddBackupVerificationAsync(
            QaBackupVerificationRecord record,
            CancellationToken ct)
        {
            BackupVerifications.Add(record);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<QaBackupVerificationRecord>> ListBackupVerificationsAsync(
            Guid projectId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<QaBackupVerificationRecord>>(
                BackupVerifications.Where(x => x.ProjectId == projectId).Take(take).ToArray());
    }
}
