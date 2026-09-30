// Purpose: Verifies QA execution planning validates requests, resolves isolated revisions, selects adapters, and disposes leases on failure.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class QaExecutionPlannerTests
{
    [Fact]
    public async Task CreateAsync_RejectsEmptyProjectIdBeforeResolvingWorkspace()
    {
        var sut = new QaExecutionPlanner(null!, null!, null!, null!, null!);
        var command = Command(Guid.Empty, new LanguageId("csharp"));

        Func<Task> action = async () =>
            await sut.CreateAsync(command, Guid.NewGuid(), CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ProjectId*");
    }

    [Fact]
    public async Task CreateAsync_RejectsUnknownProject()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-planner-missing-").FullName;
        try
        {
            var projectId = Guid.NewGuid();
            var sut = CreatePlanner(
                project: null,
                workspace,
                new QaTechnologyProfile(workspace, [new LanguageId("csharp")], [], [], [], []),
                [Selection("csharp")]);

            Func<Task> act = async () =>
                await sut.CreateAsync(Command(projectId, new LanguageId("csharp")), Guid.NewGuid(), CancellationToken.None);

            await act.Should().ThrowAsync<KeyNotFoundException>();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task CreateAsync_RejectsAbsoluteRuntimeExecutableBeforeLeaseCreation()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-planner-absolute-").FullName;
        try
        {
            var project = Project(Guid.NewGuid());
            var branch = new FakeBranchManager(workspace);
            var sut = CreatePlanner(
                project,
                workspace,
                new QaTechnologyProfile(workspace, [new LanguageId("csharp")], [], [], [], []),
                [Selection("csharp")],
                branch);
            var command = Command(project.Id, new LanguageId("csharp")) with
            {
                RuntimeExecutable = Path.GetFullPath(Path.Combine(workspace, "runner.exe"))
            };

            Func<Task> act = async () =>
                await sut.CreateAsync(command, Guid.NewGuid(), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*relative*");
            branch.PrepareCalls.Should().Be(0);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task CreateAsync_SingleLanguageBuildsExactRevisionPlan()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-planner-success-").FullName;
        try
        {
            var project = Project(Guid.NewGuid());
            var branch = new FakeBranchManager(
                workspace,
                leaseCommit: new string('b', 40),
                leaseBranch: "fix/qa",
                sourceRef: "refs/heads/fix/qa",
                refreshStatus: "refreshed");
            var profile = new QaTechnologyProfile(
                workspace,
                [new LanguageId("csharp")],
                ["dotnet"],
                [],
                [],
                []);
            var sut = CreatePlanner(project, workspace, profile, [Selection("csharp")], branch);
            var command = Command(project.Id, new LanguageId("csharp")) with
            {
                BranchName = "fix/qa",
                CommitSha = new string('a', 40),
                RefreshRemoteBranch = true
            };

            await using var plan = await sut.CreateAsync(command, Guid.NewGuid(), CancellationToken.None);

            plan.Command.CommitSha.Should().Be(new string('b', 40));
            plan.Command.BranchName.Should().Be("fix/qa");
            plan.RequestedLanguage.Should().Be(new LanguageId("csharp"));
            plan.RunLanguage.Should().Be(new LanguageId("csharp"));
            plan.Selections.Should().ContainSingle();
            plan.SourceRef.Should().Be("refs/heads/fix/qa");
            plan.RefreshStatus.Should().Be("refreshed");
            plan.IsIsolated.Should().BeTrue();
            Directory.Exists(plan.ArtifactRoot).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task CreateAsync_AutoWithMultipleSelections_UsesMultiAndReportsUnsupportedDetectedLanguage()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-planner-auto-").FullName;
        try
        {
            var project = Project(Guid.NewGuid());
            var profile = new QaTechnologyProfile(
                workspace,
                [new LanguageId("csharp"), new LanguageId("python"), new LanguageId("rust")],
                [],
                [],
                [],
                []);
            var registry = new FakeRegistry([
                Selection("csharp"),
                Selection("python")
            ]);
            var sut = CreatePlanner(project, workspace, profile, registry.Selections, registry: registry);

            await using var plan = await sut.CreateAsync(
                Command(project.Id, LanguageId.Auto),
                Guid.NewGuid(),
                CancellationToken.None);

            plan.RequestedLanguage.Should().Be(LanguageId.Auto);
            plan.RunLanguage.Should().Be(LanguageId.Multi);
            plan.Selections.Should().HaveCount(2);
            plan.UnsupportedDetectedLanguages.Should().ContainSingle("rust");
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task CreateAsync_NoAdapter_ThrowsUsefulErrorAndDisposesLease()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-planner-no-adapter-").FullName;
        try
        {
            var project = Project(Guid.NewGuid());
            var branch = new FakeBranchManager(workspace);
            var profile = new QaTechnologyProfile(workspace, [new LanguageId("csharp")], [], [], [], []);
            var sut = CreatePlanner(project, workspace, profile, [], branch);

            Func<Task> act = async () =>
                await sut.CreateAsync(Command(project.Id, new LanguageId("csharp")), Guid.NewGuid(), CancellationToken.None);

            await act.Should().ThrowAsync<NotSupportedException>()
                .WithMessage("*No QA adapter*");
            branch.CleanupCalls.Should().Be(1);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task CreateAsync_DetectorFailure_DisposesPreparedLease()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-planner-detector-fail-").FullName;
        try
        {
            var project = Project(Guid.NewGuid());
            var branch = new FakeBranchManager(workspace);
            var detector = new ThrowingDetector();
            var sut = new QaExecutionPlanner(
                new FakeProjects(project),
                new FakeWorkspaceResolver(workspace),
                branch,
                detector,
                new FakeRegistry([Selection("csharp")]));

            Func<Task> act = async () =>
                await sut.CreateAsync(Command(project.Id, new LanguageId("csharp")), Guid.NewGuid(), CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("detector boom");
            branch.CleanupCalls.Should().Be(1);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    private static QaExecutionPlanner CreatePlanner(
        Project? project,
        string workspace,
        QaTechnologyProfile profile,
        IReadOnlyList<QaAdapterSelection> selections,
        FakeBranchManager? branch = null,
        FakeRegistry? registry = null) =>
        new(
            new FakeProjects(project),
            new FakeWorkspaceResolver(workspace),
            branch ?? new FakeBranchManager(workspace),
            new FakeDetector(profile),
            registry ?? new FakeRegistry(selections));

    private static QaExecutionCommand Command(Guid projectId, LanguageId language) =>
        new(
            projectId,
            "unit-test",
            language,
            ".",
            new string('a', 40),
            null,
            null,
            AutomaticallyReproduce: false);

    private static Project Project(Guid id) =>
        new(id, "QA Project", "https://example.invalid/repo.git", "main", DateTimeOffset.UtcNow);

    private static QaAdapterSelection Selection(string language) =>
        new(new LanguageId(language), new StubAdapter(language));

    private sealed class FakeProjects(Project? project) : IProjectRepository
    {
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(project?.Id == id ? project : null);

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Project>>(project is null ? [] : [project]);

        public Task AddAsync(Project value, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeWorkspaceResolver(string workspace) : IQaWorkspaceResolver
    {
        public string Resolve(string workspaceRelativePath) => workspace;

        public string QaArtifactRoot(Guid projectId, Guid qaRunId) =>
            Path.Combine(workspace, "artifacts", projectId.ToString("N"), qaRunId.ToString("N"));
    }

    private sealed class FakeBranchManager(
        string workspace,
        string? leaseCommit = null,
        string? leaseBranch = "main",
        string sourceRef = "refs/heads/main",
        string refreshStatus = "not-requested") : IBranchQaWorkspaceManager
    {
        public int PrepareCalls { get; private set; }
        public int CleanupCalls { get; private set; }

        public Task<BranchRepositoryCatalogDto> ListBranchesAsync(
            string sourceWorkspace,
            string defaultBranch,
            bool refreshRemote,
            CancellationToken cancellationToken) =>
            Task.FromResult(new BranchRepositoryCatalogDto(defaultBranch, "ok", [], null, DateTimeOffset.UtcNow));

        public Task<BranchQaWorkspaceLease> PrepareAsync(
            string sourceWorkspace,
            string sourceWorkspaceRelativePath,
            string? branchName,
            string? requestedCommitSha,
            bool refreshRemote,
            Guid qaRunId,
            CancellationToken cancellationToken)
        {
            PrepareCalls++;
            return Task.FromResult(new BranchQaWorkspaceLease(
                workspace,
                "isolated",
                leaseBranch ?? branchName,
                leaseCommit ?? requestedCommitSha,
                sourceRef,
                refreshStatus,
                null,
                () =>
                {
                    CleanupCalls++;
                    return ValueTask.CompletedTask;
                }));
        }
    }

    private sealed class FakeDetector(QaTechnologyProfile profile) : IRepositoryTechnologyDetector
    {
        public Task<QaTechnologyProfile> DetectAsync(string workspace, CancellationToken cancellationToken) =>
            Task.FromResult(profile);
    }

    private sealed class ThrowingDetector : IRepositoryTechnologyDetector
    {
        public Task<QaTechnologyProfile> DetectAsync(string workspace, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("detector boom");
    }

    private sealed class FakeRegistry(IReadOnlyList<QaAdapterSelection> selections) : IQaAdapterRegistry
    {
        public IReadOnlyList<QaAdapterSelection> Selections => selections;
        public IReadOnlyList<string> AdapterIds => selections.Select(x => x.Adapter.AdapterId).ToArray();

        public IReadOnlyList<QaAdapterSelection> Resolve(
            LanguageId requested,
            QaTechnologyProfile profile,
            string workspace) => selections;
    }

    private sealed class StubAdapter(string language) : IQaAdapter
    {
        public string AdapterId => "stub-" + language;
        public int Priority => 0;
        public bool Supports(LanguageId value, QaTechnologyProfile profile, string workspace) => true;

        public Task<QaAdapterResult> RunAsync(
            QaExecutionCommand command,
            string workspace,
            string artifactRoot,
            CancellationToken cancellationToken) =>
            Task.FromResult(new QaAdapterResult(command.Language, [], [], "{}"));
    }
}
