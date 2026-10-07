// Purpose: Verifies Adaptive QA Planner behavior, executable experiment mapping, and repository invariants.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.P2Intelligence;

public sealed class AdaptiveQaPlannerTests
{
    [Fact]
    public async Task RenderingChange_SelectsVisualAndGpuPerformance_AsExecutableRequiredExperiments()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var evidence = new FakeEvidence([
            new EvidenceRecord(Guid.NewGuid(),projectId,null,"rendering",EvidenceSourceType.Performance,
                "performance.gpu",EvidenceOutcome.Failed,"GPU regression","historical",EvidenceConfidence.High,
                "Rendering/Shaders/hero.hlsl",null,now.AddDays(-2),now.AddDays(-2))
        ]);
        var planner = new AdaptiveQaPlanner(
            evidence,
            new FakeIntelligence(),
            new FakePlans(),
            new FakeUow(),
            new FakeClock(now));

        var plan = await planner.PlanAsync(new(projectId, "rendering",
            ["Rendering/Shaders/hero.hlsl"], null, 60), CancellationToken.None);

        plan.Steps.Should().Contain(x => x.Id == "visual-regression");
        plan.Steps.Should().Contain(x => x.Id == "gpu-performance");
        plan.Steps.Should().Contain(x => x.Id == "cpp-qa");
        plan.RiskScore.Should().BeGreaterThan(60m);

        plan.ExecutablePlan.Should().NotBeNull();
        plan.ExecutablePlan!.Steps.Should().Contain(x =>
            x.Kind == DebugExperimentKind.VisualRegression && x.Required);
        plan.ExecutablePlan.Steps.Should().Contain(x =>
            x.Kind == DebugExperimentKind.GpuPerformance && x.Required);
        plan.ExecutablePlan.Steps.Should().Contain(x =>
            x.Kind == DebugExperimentKind.RegressionTest && x.Required);
        plan.ExecutablePlan.Steps.Should().OnlyContain(x =>
            !string.IsNullOrWhiteSpace(x.SuccessCondition)
            && !string.IsNullOrWhiteSpace(x.FalsificationCondition)
            && x.RequiredEvidence.Count > 0);
    }

    [Fact]
    public async Task MultiplayerChange_SelectsDedicatedMultiplayerExperiment()
    {
        var now = DateTimeOffset.UtcNow;
        var planner = new AdaptiveQaPlanner(
            new FakeEvidence([]),
            new FakeIntelligence(),
            new FakePlans(),
            new FakeUow(),
            new FakeClock(now));

        var plan = await planner.PlanAsync(new(
            Guid.NewGuid(),
            "netcode",
            ["Assets/Scripts/Multiplayer/PlayerReplication.cs"],
            null,
            60), CancellationToken.None);

        plan.Steps.Should().Contain(x => x.Id == "multiplayer" && x.Required);
        plan.ExecutablePlan.Should().NotBeNull();
        plan.ExecutablePlan!.Steps.Should().Contain(x =>
            x.Kind == DebugExperimentKind.Multiplayer && x.Required);
    }

    [Fact]
    public async Task DocumentationOnlyChange_DoesNotRequireRuntimeOrMediaExperiments()
    {
        var now = DateTimeOffset.UtcNow;
        var planner = new AdaptiveQaPlanner(
            new FakeEvidence([]),
            new FakeIntelligence(),
            new FakePlans(),
            new FakeUow(),
            new FakeClock(now));

        var plan = await planner.PlanAsync(new(
            Guid.NewGuid(),
            "docs",
            ["docs/README.md"],
            null,
            10), CancellationToken.None);

        plan.ExecutablePlan.Should().NotBeNull();
        plan.ExecutablePlan!.Steps.Should().ContainSingle(x =>
            x.Kind == DebugExperimentKind.StaticAnalysis);
        plan.ExecutablePlan.Steps.Should().NotContain(x =>
            x.Kind == DebugExperimentKind.RuntimeReplay
            || x.Kind == DebugExperimentKind.VisualRegression
            || x.Kind == DebugExperimentKind.GpuPerformance
            || x.Kind == DebugExperimentKind.Benchmark
            || x.Kind == DebugExperimentKind.Multiplayer);
    }

    private sealed class FakeEvidence(IReadOnlyList<EvidenceRecord> values) : IEvidenceRepository
    {
        public Task AddAsync(EvidenceRecord evidence, CancellationToken ct) => Task.CompletedTask;
        public Task AddRangeAsync(IEnumerable<EvidenceRecord> evidence, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(Guid projectId, DateTimeOffset? since, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EvidenceRecord>>(values.Where(x => x.ProjectId == projectId && (since is null || x.ObservedAtUtc >= since)).ToArray());
    }

    private sealed class FakeIntelligence : IP2IntelligenceRepository
    {
        public Task AddAsync(P2IntelligenceRecord record, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<P2IntelligenceRecord>> ListRecentAsync(Guid projectId, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<P2IntelligenceRecord>>([]);
        public Task<IReadOnlyList<P2IntelligenceRecord>> ListByKindAsync(Guid projectId, P2IntelligenceKind kind, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<P2IntelligenceRecord>>([]);
    }

    private sealed class FakePlans : IAdaptiveQaPlanRepository
    {
        public Task AddAsync(AdaptiveQaPlanSnapshot plan, CancellationToken ct) => Task.CompletedTask;
        public Task<AdaptiveQaPlanSnapshot?> GetLatestAsync(Guid projectId, CancellationToken ct) => Task.FromResult<AdaptiveQaPlanSnapshot?>(null);
    }

    private sealed class FakeUow : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
    }

    private sealed class FakeClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow => now;
    }
}
