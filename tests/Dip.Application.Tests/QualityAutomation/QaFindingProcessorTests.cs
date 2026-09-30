// Purpose: Verifies finding processing applies incident, reproduction, PM-decision, and runtime-path rules without weakening failure semantics.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Incidents;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class QaFindingProcessorTests
{
    [Fact]
    public async Task ProcessAsync_WithNoFindings_WritesIntelligenceArtifactAndReturnsZeroDecisions()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-findings-").FullName;
        try
        {
            await using var plan = CreatePlan(workspace, automaticallyReproduce: false);
            var fixture = new Fixture();
            var sut = fixture.CreateProcessor();

            var result = await sut.ProcessAsync(plan, Guid.NewGuid(), [], CancellationToken.None);

            result.ReleaseBlockers.Should().Be(0);
            result.P0Decisions.Should().Be(0);
            File.Exists(Path.Combine(plan.ArtifactRoot, "qa-intelligence.json")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ProcessAsync_CriticalAndWarningFindings_AreObservedAndClassified()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-findings-incidents-").FullName;
        try
        {
            await using var plan = CreatePlan(workspace, automaticallyReproduce: false);
            var fixture = new Fixture();
            var runId = Guid.NewGuid();
            var findings = new[]
            {
                Finding(plan.Command.ProjectId, runId, Severity.Critical, "memory-safety", "fp-critical"),
                Finding(plan.Command.ProjectId, runId, Severity.Warning, "test.failure", "fp-warning"),
                Finding(plan.Command.ProjectId, runId, Severity.Info, "style", "fp-info")
            };

            var result = await fixture.CreateProcessor().ProcessAsync(plan, runId, findings, CancellationToken.None);

            fixture.Incidents.Incidents.Should().HaveCount(2);
            fixture.Incidents.Occurrences.Should().HaveCount(2);
            result.ReleaseBlockers.Should().BeGreaterThanOrEqualTo(1);
            result.P0Decisions.Should().BeGreaterThanOrEqualTo(1);
            var intelligence = await File.ReadAllTextAsync(Path.Combine(plan.ArtifactRoot, "qa-intelligence.json"));
            intelligence.Should().Contain("fp-critical");
            intelligence.Should().Contain("fp-warning");
            intelligence.Should().Contain("fp-info");
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ProcessAsync_AutomaticReproduction_PersistsAttemptsAndAttachesReproduction()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-findings-repro-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(workspace, "bin"));
            var fixture = new Fixture();
            fixture.Reproduction.Result = new ReproductionResult(
                ReproductionStatus.Reproduced,
                1,
                2,
                [
                    new ReproductionAttemptResult(
                        1,
                        ReproductionStatus.Reproduced,
                        true,
                        true,
                        false,
                        134,
                        120,
                        Path.Combine(workspace, "attempt-1"),
                        "video.mp4",
                        null,
                        "dump.dmp",
                        "{\"reproduced\":true}"),
                    new ReproductionAttemptResult(
                        2,
                        ReproductionStatus.NotReproduced,
                        false,
                        false,
                        false,
                        0,
                        80,
                        Path.Combine(workspace, "attempt-2"),
                        null,
                        null,
                        null,
                        "{\"reproduced\":false}")
                ]);

            await using var plan = CreatePlan(
                workspace,
                automaticallyReproduce: true,
                runtimeExecutable: Path.Combine("bin", "game.exe"),
                language: new LanguageId("csharp"));
            var runId = Guid.NewGuid();
            var finding = Finding(plan.Command.ProjectId, runId, Severity.Critical, "runtime-crash", "fp-repro");

            var result = await fixture.CreateProcessor().ProcessAsync(plan, runId, [finding], CancellationToken.None);

            fixture.Reproduction.Scenarios.Should().ContainSingle();
            fixture.Attempts.Items.Should().HaveCount(2);
            finding.ReproductionStatus.Should().Be(ReproductionStatus.Reproduced);
            finding.ReproducedAttempts.Should().Be(1);
            finding.TotalAttempts.Should().Be(2);
            fixture.Incidents.Events.Should().Contain(x => x.Type == IncidentEventType.Reproduced);
            result.ReleaseBlockers.Should().Be(1);
            result.P0Decisions.Should().Be(1);
            fixture.UnitOfWork.Saves.Should().BeGreaterThan(2);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ProcessAsync_PythonReproduction_InjectsDebugEnvironment()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-findings-python-").FullName;
        try
        {
            await using var plan = CreatePlan(
                workspace,
                automaticallyReproduce: true,
                runtimeExecutable: "runner.py",
                language: LanguageId.Python);
            var fixture = new Fixture();
            var runId = Guid.NewGuid();
            var finding = Finding(
                plan.Command.ProjectId,
                runId,
                Severity.Warning,
                "test.failure",
                "fp-python",
                LanguageId.Python);

            await fixture.CreateProcessor().ProcessAsync(plan, runId, [finding], CancellationToken.None);

            var scenario = fixture.Reproduction.Scenarios.Single();
            scenario.EnvironmentVariables.Should().ContainKey("PYTHONFAULTHANDLER");
            scenario.EnvironmentVariables.Should().ContainKey("PYTHONASYNCIODEBUG");
            scenario.Executable.Should().Be(Path.GetFullPath(Path.Combine(workspace, "runner.py")));
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ProcessAsync_AbsoluteRuntimeExecutable_IsRejected()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-findings-absolute-").FullName;
        try
        {
            await using var plan = CreatePlan(
                workspace,
                automaticallyReproduce: true,
                runtimeExecutable: Path.GetFullPath(Path.Combine(workspace, "runner.exe")));
            var fixture = new Fixture();
            var finding = Finding(plan.Command.ProjectId, Guid.NewGuid(), Severity.Critical, "runtime-crash", "fp-abs");

            Func<Task> act = () => fixture.CreateProcessor().ProcessAsync(plan, Guid.NewGuid(), [finding], CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*workspace-relative*");
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ProcessAsync_RuntimeExecutableEscapingWorkspace_IsRejected()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-findings-escape-").FullName;
        try
        {
            await using var plan = CreatePlan(
                workspace,
                automaticallyReproduce: true,
                runtimeExecutable: Path.Combine("..", "outside.exe"));
            var fixture = new Fixture();
            var finding = Finding(plan.Command.ProjectId, Guid.NewGuid(), Severity.Critical, "runtime-crash", "fp-escape");

            Func<Task> act = () => fixture.CreateProcessor().ProcessAsync(plan, Guid.NewGuid(), [finding], CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*escapes the QA workspace*");
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ProcessAsync_NonReproducibleInfoFinding_DoesNotCallReproduction()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-findings-no-repro-").FullName;
        try
        {
            await using var plan = CreatePlan(workspace, automaticallyReproduce: true, runtimeExecutable: "runner.exe");
            var fixture = new Fixture();
            var finding = Finding(plan.Command.ProjectId, Guid.NewGuid(), Severity.Info, "style", "fp-style");

            var result = await fixture.CreateProcessor().ProcessAsync(plan, Guid.NewGuid(), [finding], CancellationToken.None);

            fixture.Reproduction.Scenarios.Should().BeEmpty();
            result.ReleaseBlockers.Should().Be(0);
            result.P0Decisions.Should().Be(0);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    private static QaExecutionPlan CreatePlan(
        string workspace,
        bool automaticallyReproduce,
        string? runtimeExecutable = null,
        LanguageId? language = null)
    {
        var projectId = Guid.NewGuid();
        var selectedLanguage = language ?? new LanguageId("csharp");
        var command = new QaExecutionCommand(
            projectId,
            "unit-test",
            selectedLanguage,
            ".",
            new string('a', 40),
            runtimeExecutable,
            ["--unit"],
            TimeoutSeconds: 30,
            AutomaticallyReproduce: automaticallyReproduce,
            RecordScreen: false,
            RecordSystemAudio: false,
            BranchName: "main");
        var lease = new BranchQaWorkspaceLease(
            workspace,
            ".",
            "main",
            command.CommitSha,
            "refs/heads/main",
            "not-requested",
            null,
            null);
        var profile = new QaTechnologyProfile(workspace, [selectedLanguage], [], [], [], []);

        return new QaExecutionPlan(
            command,
            ".",
            workspace,
            workspace,
            selectedLanguage,
            selectedLanguage,
            profile,
            new ProductionQaProfile("software", [], [], []),
            [],
            [],
            lease);
    }

    private static QaFinding Finding(
        Guid projectId,
        Guid runId,
        Severity severity,
        string category,
        string fingerprint,
        LanguageId? language = null) =>
        new(
            Guid.NewGuid(),
            projectId,
            runId,
            "unit-test",
            language ?? new LanguageId("csharp"),
            category,
            severity,
            "unit-tool",
            "Finding title",
            "Finding message",
            fingerprint,
            "src/File.cs",
            10,
            2,
            "Method",
            "UnitException",
            "stack",
            "UNIT001",
            DateTimeOffset.UtcNow);

    private sealed class Fixture
    {
        public FakeReproduction Reproduction { get; } = new();
        public FakeAttempts Attempts { get; } = new();
        public FakeUnitOfWork UnitOfWork { get; } = new();
        public FakeClock Clock { get; } = new();
        public FakeIncidentRepository Incidents { get; } = new();

        public QaFindingProcessor CreateProcessor()
        {
            var incidentService = new IncidentAggregationService(
                Incidents,
                null!,
                UnitOfWork,
                Clock);
            return new QaFindingProcessor(
                Reproduction,
                Attempts,
                UnitOfWork,
                Clock,
                incidentService,
                new PmEvidenceTranslator());
        }
    }

    private sealed class FakeReproduction : IReproductionCoordinator
    {
        public List<ReproductionScenario> Scenarios { get; } = [];
        public ReproductionResult Result { get; set; } =
            new(ReproductionStatus.NotReproduced, 0, 1, []);

        public Task<ReproductionResult> ReproduceAsync(ReproductionScenario scenario, CancellationToken cancellationToken)
        {
            Scenarios.Add(scenario);
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeAttempts : IReproductionAttemptRepository
    {
        public List<ReproductionAttempt> Items { get; } = [];
        public Task AddAsync(ReproductionAttempt attempt, CancellationToken ct) { Items.Add(attempt); return Task.CompletedTask; }
        public Task<IReadOnlyList<ReproductionAttempt>> ListByFindingAsync(Guid findingId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ReproductionAttempt>>(Items.Where(x => x.QaFindingId == findingId).ToArray());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { Saves++; return Task.FromResult(1); }
    }

    private sealed class FakeClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeIncidentRepository : IIncidentRepository
    {
        public List<Incident> Incidents { get; } = [];
        public List<IncidentOccurrence> Occurrences { get; } = [];
        public List<IncidentEvent> Events { get; } = [];

        public Task<Incident?> FindByFingerprintAsync(Guid projectId, string fingerprint, CancellationToken ct) =>
            Task.FromResult(Incidents.FirstOrDefault(x => x.ProjectId == projectId && x.Fingerprint == fingerprint));
        public Task<Incident?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Incidents.FirstOrDefault(x => x.Id == id));
        public Task<Incident?> FindLatestOpenForSubjectAsync(Guid projectId, string subjectKey, CancellationToken ct) =>
            Task.FromResult(Incidents.LastOrDefault(x => x.ProjectId == projectId && x.SubjectKey == subjectKey));
        public Task AddAsync(Incident incident, CancellationToken ct) { Incidents.Add(incident); return Task.CompletedTask; }
        public Task AddOccurrenceAsync(IncidentOccurrence occurrence, CancellationToken ct) { Occurrences.Add(occurrence); return Task.CompletedTask; }
        public Task AddEventAsync(IncidentEvent incidentEvent, CancellationToken ct) { Events.Add(incidentEvent); return Task.CompletedTask; }
        public Task<IReadOnlyList<Incident>> ListRecentAsync(Guid projectId, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Incident>>(Incidents.Where(x => x.ProjectId == projectId).Take(take).ToArray());
        public Task<IReadOnlyList<IncidentEvent>> ListEventsAsync(Guid incidentId, CancellationToken ct) => Task.FromResult<IReadOnlyList<IncidentEvent>>(Events.Where(x => x.IncidentId == incidentId).ToArray());
        public Task<IReadOnlyList<IncidentOccurrence>> ListOccurrencesAsync(Guid incidentId, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<IncidentOccurrence>>(Occurrences.Where(x => x.IncidentId == incidentId).Take(take).ToArray());
    }
}
