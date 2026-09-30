// Purpose: Verifies post-run pipeline, issue, and evidence materialization across valid/invalid revision and QA outcome branches.
using System.Reflection;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class QaPostRunProcessorTests
{
    [Fact]
    public async Task MaterializePipelineAndIssuesAsync_InvalidCommitSha_ReturnsNullBeforePersistence()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-postrun-invalid-").FullName;
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace, commitSha: "not-a-commit-sha");
            var run = CompletedRun(plan.Command.ProjectId, workspace, "not-a-commit-sha", QaRunStatus.Succeeded);
            var fixture = new Fixture();

            var pipelineRunId = await fixture.InvokeMaterializeAsync(plan.Command, run, []);

            pipelineRunId.Should().BeNull();
            fixture.Pipelines.Items.Should().BeEmpty();
            fixture.Issues.Items.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Theory]
    [InlineData(QaRunStatus.Succeeded, PipelineStatus.Succeeded)]
    [InlineData(QaRunStatus.Partial, PipelineStatus.PartiallySucceeded)]
    [InlineData(QaRunStatus.Cancelled, PipelineStatus.Cancelled)]
    [InlineData(QaRunStatus.InfrastructureFailure, PipelineStatus.Failed)]
    [InlineData(QaRunStatus.Failed, PipelineStatus.Failed)]
    public async Task MaterializePipelineAndIssuesAsync_MapsRunStatusToPipelineStatus(
        QaRunStatus qaStatus,
        PipelineStatus expected)
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-postrun-status-").FullName;
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            var run = CompletedRun(plan.Command.ProjectId, workspace, plan.Command.CommitSha, qaStatus);
            var fixture = new Fixture();

            var id = await fixture.InvokeMaterializeAsync(plan.Command, run, []);

            id.Should().NotBeNull();
            fixture.Pipelines.Items.Should().ContainSingle();
            fixture.Pipelines.Items[0].Status.Should().Be(expected);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task MaterializePipelineAndIssuesAsync_AddsOnlyCriticalWarningAndDeduplicatesFingerprint()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-postrun-issues-").FullName;
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            var run = CompletedRun(plan.Command.ProjectId, workspace, plan.Command.CommitSha, QaRunStatus.Failed);
            var findings = new[]
            {
                Finding(plan.Command.ProjectId, run.Id, "same", Severity.Warning, "test.failure"),
                Finding(plan.Command.ProjectId, run.Id, "same", Severity.Critical, "runtime-crash"),
                Finding(plan.Command.ProjectId, run.Id, "other", Severity.Warning, "static-analysis"),
                Finding(plan.Command.ProjectId, run.Id, "info", Severity.Info, "style")
            };
            var fixture = new Fixture();

            await fixture.InvokeMaterializeAsync(plan.Command, run, findings);

            fixture.Issues.Items.Should().HaveCount(2);
            fixture.Issues.Items.Should().Contain(x => x.Fingerprint == "same" && x.Severity == Severity.Critical);
            fixture.Issues.Items.Should().Contain(x => x.Fingerprint == "other");
            fixture.Issues.Items.Should().NotContain(x => x.Fingerprint == "info");
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task MaterializePipelineAndIssuesAsync_ReoccursExistingIssueInsteadOfAddingDuplicate()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-postrun-reoccur-").FullName;
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            var run = CompletedRun(plan.Command.ProjectId, workspace, plan.Command.CommitSha, QaRunStatus.Failed);
            var existing = new QualityIssue(
                Guid.NewGuid(),
                plan.Command.ProjectId,
                Guid.NewGuid(),
                "existing",
                "test.failure",
                Severity.Warning,
                "Existing issue",
                "Existing description",
                "src/A.cs",
                "TEST",
                "{}",
                DateTimeOffset.UtcNow.AddHours(-1));
            var fixture = new Fixture();
            fixture.Issues.Items.Add(existing);
            var finding = Finding(plan.Command.ProjectId, run.Id, "existing", Severity.Critical, "runtime-crash");

            var pipelineId = await fixture.InvokeMaterializeAsync(plan.Command, run, [finding]);

            fixture.Issues.Items.Should().ContainSingle();
            existing.OccurrenceCount.Should().Be(2);
            existing.Severity.Should().Be(Severity.Critical);
            existing.LastSeenPipelineRunId.Should().Be(pipelineId);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Theory]
    [InlineData(QaRunStatus.Succeeded, Severity.Info, EvidenceOutcome.Succeeded)]
    [InlineData(QaRunStatus.Partial, Severity.Info, EvidenceOutcome.Warning)]
    [InlineData(QaRunStatus.InfrastructureFailure, Severity.Info, EvidenceOutcome.Unknown)]
    [InlineData(QaRunStatus.Failed, Severity.Info, EvidenceOutcome.Failed)]
    [InlineData(QaRunStatus.Succeeded, Severity.Critical, EvidenceOutcome.Failed)]
    [InlineData(QaRunStatus.Succeeded, Severity.Warning, EvidenceOutcome.Warning)]
    public async Task BuildEvidence_MapsOutcomeAndIncludesFindingEvidence(
        QaRunStatus runStatus,
        Severity findingSeverity,
        EvidenceOutcome expected)
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-postrun-evidence-").FullName;
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            var run = CompletedRun(plan.Command.ProjectId, workspace, plan.Command.CommitSha, runStatus);
            var findings = findingSeverity == Severity.Info
                ? new[] { Finding(plan.Command.ProjectId, run.Id, "info-evidence", findingSeverity, "style") }
                : new[] { Finding(plan.Command.ProjectId, run.Id, "finding-evidence", findingSeverity, "test.failure") };
            var tools = new[]
            {
                new QaToolExecution("unit", true, true, 0, 10, "stdout", "stderr", "ok")
            };
            var fixture = new Fixture();

            var evidence = fixture.InvokeBuildEvidence(plan, run, findings, tools, new string('x', 20000), Guid.NewGuid()).ToArray();

            evidence.Should().NotBeEmpty();
            evidence[0].Outcome.Should().Be(expected);
            evidence[0].Confidence.Should().Be(EvidenceConfidence.Verified);
            evidence[0].PayloadJson!.Length.Should().BeLessThanOrEqualTo(15900);
            if (findingSeverity is Severity.Critical or Severity.Warning)
                evidence.Should().HaveCount(2);
            else
                evidence.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task BuildEvidence_UnavailableToolLowersConfidenceAndReproducedFindingIsVerified()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-postrun-confidence-").FullName;
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            var run = CompletedRun(plan.Command.ProjectId, workspace, plan.Command.CommitSha, QaRunStatus.Failed);
            var finding = Finding(plan.Command.ProjectId, run.Id, "reproduced", Severity.Warning, "test.failure");
            finding.ApplyReproduction(ReproductionStatus.Reproduced, 2, 2);
            var tools = new[]
            {
                new QaToolExecution("unit", false, false, null, 10, "", "", "unavailable")
            };
            var fixture = new Fixture();

            var evidence = fixture.InvokeBuildEvidence(plan, run, [finding], tools, "{}", Guid.NewGuid()).ToArray();

            evidence[0].Confidence.Should().Be(EvidenceConfidence.High);
            evidence[1].Confidence.Should().Be(EvidenceConfidence.Verified);
            evidence[1].Summary.Should().Contain("automatically reproduced");
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    private static QaRun CompletedRun(
        Guid projectId,
        string artifactRoot,
        string? commitSha,
        QaRunStatus status)
    {
        var now = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var run = new QaRun(
            Guid.NewGuid(),
            projectId,
            "unit-test",
            new LanguageId("csharp"),
            ".",
            commitSha,
            artifactRoot,
            now.AddSeconds(-5));
        run.Start(now);
        run.Complete(
            status,
            status == QaRunStatus.Succeeded ? 0 : 1,
            status == QaRunStatus.Failed ? 1 : 0,
            status == QaRunStatus.Partial ? 1 : 0,
            "{}",
            now.AddSeconds(1));
        return run;
    }

    private static QaFinding Finding(
        Guid projectId,
        Guid runId,
        string fingerprint,
        Severity severity,
        string category) =>
        new(
            Guid.NewGuid(),
            projectId,
            runId,
            "unit-test",
            new LanguageId("csharp"),
            category,
            severity,
            "unit-tool",
            new string('T', 20),
            new string('M', 200),
            fingerprint,
            "src/A.cs",
            1,
            2,
            "Method",
            "UnitException",
            new string('S', 5000),
            "UNIT001",
            DateTimeOffset.UtcNow);

    private sealed class Fixture
    {
        private readonly QaPostRunProcessor _sut;
        public FakePipelines Pipelines { get; } = new();
        public FakeIssues Issues { get; } = new();
        public FakeEvidence Evidence { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeClock Clock { get; } = new();

        public Fixture()
        {
            _sut = new QaPostRunProcessor(
                Pipelines,
                Issues,
                Evidence,
                UnitOfWork,
                Clock,
                new PmEvidenceTranslator(),
                null!,
                null!);
        }

        public async Task<Guid?> InvokeMaterializeAsync(
            QaExecutionCommand command,
            QaRun run,
            IReadOnlyList<QaFinding> findings)
        {
            var method = typeof(QaPostRunProcessor).GetMethod(
                "MaterializePipelineAndIssuesAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            var task = (Task<Guid?>)method!.Invoke(
                _sut,
                [command, run, findings, CancellationToken.None])!;
            return await task;
        }

        public IEnumerable<EvidenceRecord> InvokeBuildEvidence(
            QaExecutionPlan plan,
            QaRun run,
            IReadOnlyList<QaFinding> findings,
            IReadOnlyList<QaToolExecution> tools,
            string summary,
            Guid? pipelineRunId)
        {
            var method = typeof(QaPostRunProcessor).GetMethod(
                "BuildEvidence",
                BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();
            return (IEnumerable<EvidenceRecord>)method!.Invoke(
                _sut,
                [plan, run, findings, tools, summary, pipelineRunId])!;
        }
    }

    private sealed class FakePipelines : IPipelineRunRepository
    {
        public List<PipelineRun> Items { get; } = [];
        public Task<PipelineRun?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<PipelineRun?> FindByExternalRunAsync(Guid projectId, string externalRunId, string collectorType, int attemptNumber, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.ProjectId == projectId && x.ExternalRunId == externalRunId));
        public Task<IReadOnlyList<PipelineRun>> ListByProjectAsync(Guid projectId, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<PipelineRun>>(Items.Where(x => x.ProjectId == projectId).Take(take).ToArray());
        public Task AddAsync(PipelineRun run, CancellationToken ct) { Items.Add(run); return Task.CompletedTask; }
    }

    private sealed class FakeIssues : IQualityIssueRepository
    {
        public List<QualityIssue> Items { get; } = [];
        public Task<QualityIssue?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<QualityIssue?> FindOpenByFingerprintAsync(Guid projectId, string fingerprint, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.ProjectId == projectId && x.Fingerprint.Equals(fingerprint, StringComparison.OrdinalIgnoreCase) && x.Status != IssueStatus.Resolved));
        public Task<IReadOnlyList<QualityIssue>> ListByProjectAsync(Guid projectId, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<QualityIssue>>(Items.Where(x => x.ProjectId == projectId).Take(take).ToArray());
        public Task AddAsync(QualityIssue issue, CancellationToken ct) { Items.Add(issue); return Task.CompletedTask; }
    }

    private sealed class FakeEvidence : IEvidenceRepository
    {
        public List<EvidenceRecord> Items { get; } = [];
        public Task AddAsync(EvidenceRecord evidence, CancellationToken ct) { Items.Add(evidence); return Task.CompletedTask; }
        public Task AddRangeAsync(IEnumerable<EvidenceRecord> evidence, CancellationToken ct) { Items.AddRange(evidence); return Task.CompletedTask; }
        public Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(Guid projectId, DateTimeOffset? observedSince, CancellationToken ct) => Task.FromResult<IReadOnlyList<EvidenceRecord>>(Items.Where(x => x.ProjectId == projectId).ToArray());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { Saves++; return Task.FromResult(1); }
    }

    private sealed class FakeClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 10, 1, 0, 0, 5, TimeSpan.Zero);
    }
}
