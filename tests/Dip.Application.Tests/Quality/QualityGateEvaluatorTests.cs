// Purpose: Defines Quality Gate Evaluator Tests responsibilities for this repository area.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.Quality;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.Quality;

public sealed class QualityGateEvaluatorTests
{
    private readonly QualityGateEvaluator _evaluator =
        new(new QualityRuleEvaluator());

    [Fact]
    public void Evaluate_ReturnsFailed_WhenFpsDropsTwentyPercent()
    {
        var metric = new Metric(
            Guid.NewGuid(), Guid.NewGuid(), "average_fps", 48m, "fps", 60m);

        var result = _evaluator.Evaluate([metric], []);

        result.Status.Should().Be("Failed");
        result.CriticalIssueCount.Should().Be(1);
        result.Score.Should().BeLessThan(100m);
    }

    [Fact]
    public void Evaluate_ReturnsWarning_WhenWarningIssueExists()
    {
        var issue = CreateIssue(Severity.Warning, "Large texture");

        var result = _evaluator.Evaluate([], [issue]);

        result.Status.Should().Be("Warning");
        result.WarningIssueCount.Should().Be(1);
        result.Reasons.Should().Contain(x => x.Contains("Large texture"));
    }

    [Fact]
    public void Evaluate_ReturnsPassed_WhenThereAreNoViolations()
    {
        var metric = new Metric(
            Guid.NewGuid(), Guid.NewGuid(), "average_fps", 61m, "fps", 60m);

        var result = _evaluator.Evaluate([metric], []);

        result.Status.Should().Be("Passed");
        result.Score.Should().Be(100m);
        result.CriticalIssueCount.Should().Be(0);
        result.WarningIssueCount.Should().Be(0);
    }

    [Fact]
    public void AutonomousDebugBudget_DefaultsStayInsideBoundedSafetyEnvelope()
    {
        var budget = new AutonomousDebugBudget().Validate();

        budget.MaxHypotheses.Should().BeInRange(1, 10);
        budget.MaxExperiments.Should().BeInRange(1, 10);
        budget.MaxAiCalls.Should().BeInRange(2, 20);
        budget.TimeoutSecondsPerExperiment.Should().BeInRange(30, 1800);
        budget.MaxWallClockMinutes.Should().BeInRange(1, 120);
    }

    [Theory]
    [InlineData(0, 3, 5, 900, 30)]
    [InlineData(4, 0, 5, 900, 30)]
    [InlineData(4, 3, 1, 900, 30)]
    [InlineData(4, 3, 5, 5, 30)]
    [InlineData(4, 3, 5, 900, 0)]
    public void AutonomousDebugBudget_RejectsUnboundedOrInvalidValues(
        int hypotheses,
        int experiments,
        int aiCalls,
        int timeoutSeconds,
        int wallClockMinutes)
    {
        var budget = new AutonomousDebugBudget(
            hypotheses,
            experiments,
            aiCalls,
            timeoutSeconds,
            wallClockMinutes);

        Action action = () => budget.Validate();

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static QualityIssue CreateIssue(Severity severity, string title) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            "asset",
            severity,
            title,
            "Quality rule violation.",
            "Assets/A.png",
            "asset.rule",
            null,
            DateTimeOffset.UtcNow);
}


public sealed class AutonomousDebugServiceTests
{
    [Fact]
    public async Task RunAsync_PersistsNoGroundedHypothesis_AndHistoryRestores()
    {
        var fixture = CreateFixture(
            hypothesis: _ => null,
            fix: _ => null,
            verify: _ => RejectedVerification());
        var projectId = Guid.NewGuid();

        var result = await fixture.Service.RunAsync(
            Request(projectId),
            CancellationToken.None);

        result.Status.Should().Be("NoGroundedHypothesis");
        fixture.Verifier.Requests.Should().BeEmpty();

        var restored = await fixture.Service.GetAsync(projectId, result.SessionId, CancellationToken.None);
        restored.Should().NotBeNull();
        restored!.Status.Should().Be("NoGroundedHypothesis");

        var history = await fixture.Service.ListAsync(projectId, 20, CancellationToken.None);
        history.Should().ContainSingle(x => x.SessionId == result.SessionId);
        history.Single().Status.Should().Be("NoGroundedHypothesis");
    }

    [Fact]
    public async Task RunAsync_ReturnsReadyForHumanReview_OnlyAfterIsolatedVerifierAcceptsCandidate()
    {
        var evidenceId = Guid.NewGuid();
        var hypothesis = new DebugHypothesis(
            "H1",
            "Null path is unguarded",
            "The supplied evidence points to a missing guard.",
            0.92m,
            [evidenceId],
            [DebugExperimentKind.VerifyPatch, DebugExperimentKind.RegressionTest]);
        var fix = new FixProposalResponse(
            "test-agent",
            "deterministic",
            "Add the smallest guard and a regression test.",
            "diff --git a/a.cs b/a.cs\n--- a/a.cs\n+++ b/a.cs\n@@ -1 +1 @@\n-old\n+new\n",
            [new RegressionTestDraft("tests/regression.cs", "// regression", "Reproduce the defect.")],
            [evidenceId]);
        var accepted = new VerifiedFixResult(
            true,
            true,
            "Verified",
            "abc123",
            "artifacts/verified-fix",
            ["apply patch", "run regression", "run QA"],
            [],
            "Candidate passed isolated verification.");
        var fixture = CreateFixture(
            _ => new DebugHypothesisResponse("test-agent", "deterministic", [hypothesis]),
            _ => fix,
            _ => accepted);
        var projectId = Guid.NewGuid();

        var result = await fixture.Service.RunAsync(Request(projectId), CancellationToken.None);

        result.Status.Should().Be("ReadyForHumanReview");
        result.Attempts.Should().ContainSingle();
        result.Attempts[0].Status.Should().Be("VerifiedCandidate");
        result.Attempts[0].Verified.Should().BeTrue();
        fixture.Verifier.Requests.Should().ContainSingle();
        fixture.Verifier.Requests[0].WorkspaceRelativePath.Should().Be("workspace-under-test");
        fixture.Verifier.Requests[0].RegressionTests.Should().ContainSingle();

        var history = await fixture.Service.ListAsync(projectId, 20, CancellationToken.None);
        history.Should().ContainSingle(x => x.Status == "ReadyForHumanReview");
    }

    [Fact]
    public async Task RunAsync_StopsAtExperimentBudget_AndDoesNotVerifyAdditionalHypotheses()
    {
        var evidenceId = Guid.NewGuid();
        var hypotheses = new[]
        {
            new DebugHypothesis("H1", "First", "First grounded hypothesis.", 0.9m, [evidenceId], [DebugExperimentKind.VerifyPatch]),
            new DebugHypothesis("H2", "Second", "Second grounded hypothesis.", 0.8m, [evidenceId], [DebugExperimentKind.VerifyPatch])
        };
        var fix = new FixProposalResponse(
            "test-agent",
            "deterministic",
            "Candidate",
            "diff --git a/a b/a\n--- a/a\n+++ b/a\n@@ -1 +1 @@\n-a\n+b\n",
            [new RegressionTestDraft("tests/r.cs", "// regression", "Regression")],
            [evidenceId]);
        var fixture = CreateFixture(
            _ => new DebugHypothesisResponse("test-agent", "deterministic", hypotheses),
            _ => fix,
            _ => RejectedVerification());
        var projectId = Guid.NewGuid();
        var budget = new AutonomousDebugBudget(
            MaxHypotheses: 4,
            MaxExperiments: 1,
            MaxAiCalls: 5,
            TimeoutSecondsPerExperiment: 900,
            MaxWallClockMinutes: 30);

        var result = await fixture.Service.RunAsync(Request(projectId, budget), CancellationToken.None);

        result.Status.Should().Be("BudgetExhausted");
        result.Attempts.Should().ContainSingle();
        fixture.FixAgent.Requests.Should().ContainSingle();
        fixture.Verifier.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task RunAsync_PersistsFailedSnapshot_WhenProposalStageThrows()
    {
        var evidenceId = Guid.NewGuid();
        var hypothesis = new DebugHypothesis(
            "H1", "Failure path", "Grounded.", 0.8m, [evidenceId], [DebugExperimentKind.VerifyPatch]);
        var fixture = CreateFixture(
            _ => new DebugHypothesisResponse("test-agent", "deterministic", [hypothesis]),
            _ => throw new InvalidOperationException("proposal failed"),
            _ => RejectedVerification());
        var projectId = Guid.NewGuid();

        Func<Task> action = () => fixture.Service.RunAsync(Request(projectId), CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>();

        var history = await fixture.Service.ListAsync(projectId, 20, CancellationToken.None);
        history.Should().ContainSingle();
        history[0].Status.Should().Be("Failed");
        history[0].Summary.Should().Contain(nameof(InvalidOperationException));
    }

    private static AutonomousDebugRequest Request(Guid projectId, AutonomousDebugBudget? budget = null) =>
        new(
            projectId,
            "subject-under-test",
            "Explain the failure using supplied evidence.",
            "{}",
            "workspace-under-test",
            null,
            "abc123",
            LanguageId.Auto,
            "software",
            budget);

    private static VerifiedFixResult RejectedVerification() =>
        new(
            true,
            false,
            "Rejected",
            "abc123",
            "artifacts/rejected-fix",
            ["apply patch", "run regression"],
            [],
            "Candidate did not pass verification.");

    private static Fixture CreateFixture(
        Func<DebugHypothesisRequest, DebugHypothesisResponse?> hypothesis,
        Func<FixProposalRequest, FixProposalResponse?> fix,
        Func<VerifiedFixRequest, VerifiedFixResult> verify)
    {
        var repo = new InMemoryDevelopmentGraphRepository();
        var uow = new NoOpUnitOfWork();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero));
        var graph = new DevelopmentGraphService(repo, uow, clock);
        var hypothesisAgent = new StubHypothesisAgent(hypothesis);
        var fixAgent = new StubFixProposalAgent(fix);
        var verifier = new StubVerifiedFixService(verify);
        return new(
            new AutonomousDebugService(hypothesisAgent, fixAgent, verifier, graph, clock),
            fixAgent,
            verifier);
    }

    private sealed record Fixture(
        AutonomousDebugService Service,
        StubFixProposalAgent FixAgent,
        StubVerifiedFixService Verifier);

    private sealed class StubHypothesisAgent(Func<DebugHypothesisRequest, DebugHypothesisResponse?> handler)
        : IAutonomousDebugAgent
    {
        public Task<DebugHypothesisResponse?> GenerateHypothesesAsync(DebugHypothesisRequest request, CancellationToken ct)
            => Task.FromResult(handler(request));
    }

    private sealed class StubFixProposalAgent(Func<FixProposalRequest, FixProposalResponse?> handler)
        : IFixProposalAgent
    {
        public List<FixProposalRequest> Requests { get; } = [];

        public Task<FixProposalResponse?> ProposeAsync(FixProposalRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(handler(request));
        }
    }

    private sealed class StubVerifiedFixService(Func<VerifiedFixRequest, VerifiedFixResult> handler)
        : IVerifiedFixService
    {
        public List<VerifiedFixRequest> Requests { get; } = [];

        public Task<VerifiedFixResult> VerifyAsync(VerifiedFixRequest request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(handler(request));
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
    }

    private sealed class InMemoryDevelopmentGraphRepository : IDevelopmentGraphRepository
    {
        private readonly List<DevelopmentGraphNode> _nodes = [];
        private readonly List<DevelopmentGraphEdge> _edges = [];

        public Task<DevelopmentGraphNode?> GetNodeAsync(Guid id, CancellationToken ct)
            => Task.FromResult(_nodes.FirstOrDefault(x => x.Id == id));

        public Task<DevelopmentGraphNode?> FindNodeAsync(
            Guid projectId,
            DevelopmentGraphNodeType type,
            string key,
            CancellationToken ct)
            => Task.FromResult(_nodes.FirstOrDefault(x =>
                x.ProjectId == projectId && x.Type == type && x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(
            Guid projectId,
            DevelopmentGraphNodeType? type,
            CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DevelopmentGraphNode>>(
                _nodes.Where(x => x.ProjectId == projectId && (!type.HasValue || x.Type == type.Value)).ToArray());

        public Task AddNodeAsync(DevelopmentGraphNode node, CancellationToken ct)
        {
            _nodes.Add(node);
            return Task.CompletedTask;
        }

        public Task<DevelopmentGraphEdge?> FindEdgeAsync(
            Guid projectId,
            Guid sourceNodeId,
            Guid targetNodeId,
            DevelopmentGraphEdgeType type,
            CancellationToken ct)
            => Task.FromResult(_edges.FirstOrDefault(x =>
                x.ProjectId == projectId && x.SourceNodeId == sourceNodeId && x.TargetNodeId == targetNodeId && x.Type == type));

        public Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(Guid projectId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DevelopmentGraphEdge>>(_edges.Where(x => x.ProjectId == projectId).ToArray());

        public Task AddEdgeAsync(DevelopmentGraphEdge edge, CancellationToken ct)
        {
            _edges.Add(edge);
            return Task.CompletedTask;
        }
    }
}
