// Purpose: Verifies Adaptive QA Planner behavior, edge cases, and repository invariants.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.P2Intelligence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.P2Intelligence;

public sealed class AdaptiveQaPlannerTests
{
    [Fact]
    public async Task RenderingChange_SelectsVisualAndPerformanceChecks()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var evidence = new FakeEvidence([
            new EvidenceRecord(Guid.NewGuid(),projectId,null,"rendering",EvidenceSourceType.Performance,
                "performance.gpu",EvidenceOutcome.Failed,"GPU regression","historical",EvidenceConfidence.High,
                "Rendering/Shaders/hero.hlsl",null,now.AddDays(-2),now.AddDays(-2))
        ]);
        var planner = new AdaptiveQaPlanner(evidence, new FakeIntelligence(), new FakePlans(), new FakeUow(), new FakeClock(now));

        var plan = await planner.PlanAsync(new(projectId, "rendering",
            ["Rendering/Shaders/hero.hlsl"], null, 60), CancellationToken.None);

        plan.Steps.Should().Contain(x => x.Id == "visual-regression");
        plan.Steps.Should().Contain(x => x.Id == "gpu-performance");
        plan.Steps.Should().Contain(x => x.Id == "cpp-qa");
        plan.RiskScore.Should().BeGreaterThan(60m);
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
    private sealed class FakeUow : IUnitOfWork { public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1); }
    private sealed class FakeClock(DateTimeOffset now) : ISystemClock { public DateTimeOffset UtcNow => now; }
}
