// Purpose: Provides focused fixtures for direct unit tests of split QA orchestration responsibilities.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Tests.QualityAutomation;

internal static class QaSplitTestFixtures
{
    public static QaExecutionPlan CreatePlan(
        string workspace,
        Guid? projectId = null,
        string? commitSha = "0123456789abcdef0123456789abcdef01234567",
        IReadOnlyList<QaAdapterSelection>? selections = null)
    {
        var id = projectId ?? Guid.NewGuid();
        var command = new QaExecutionCommand(
            id,
            "unit-test",
            new LanguageId("csharp"),
            ".",
            commitSha,
            null,
            null,
            AutomaticallyReproduce: false);

        var lease = new BranchQaWorkspaceLease(
            workspace,
            ".",
            "main",
            commitSha,
            "main",
            "not-requested",
            null,
            null);

        return new QaExecutionPlan(
            command,
            ".",
            workspace,
            workspace,
            new LanguageId("csharp"),
            new LanguageId("csharp"),
            new QaTechnologyProfile(workspace, [new LanguageId("csharp")], [], [], [], []),
            new ProductionQaProfile("software", [], [], []),
            selections ?? [new QaAdapterSelection(new LanguageId("csharp"), new NoopAdapter())],
            [],
            lease);
    }

    public static QaRun CreateRun(
        string artifactRoot,
        string? commitSha = "0123456789abcdef0123456789abcdef01234567")
    {
        var now = DateTimeOffset.UtcNow;
        return new QaRun(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "unit-test",
            new LanguageId("csharp"),
            ".",
            commitSha,
            artifactRoot,
            now);
    }

    private sealed class NoopAdapter : IQaAdapter
    {
        public string AdapterId => "unit-noop";
        public int Priority => 0;
        public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace) => true;
        public Task<QaAdapterResult> RunAsync(
            QaExecutionCommand command,
            string workspace,
            string artifactRoot,
            CancellationToken cancellationToken) =>
            Task.FromResult(new QaAdapterResult(command.Language, [], [], "{}"));
    }
}
