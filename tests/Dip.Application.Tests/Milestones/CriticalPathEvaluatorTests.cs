// Purpose: Verifies deterministic critical-path calculation independently from persistence and clock infrastructure.
using Dip.Application.Features.Milestones;

namespace Dip.Application.Tests.Milestones;

public sealed class CriticalPathEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Evaluate_ReturnsExpectedCriticalPathForSimpleChain()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var result = CriticalPathEvaluator.Evaluate(
            [
                new(first, "a", "A", 3, Now.AddDays(10), 100m, "Completed"),
                new(second, "b", "B", 2, Now.AddDays(12), 0m, "Planned")
            ],
            [new(first, second)],
            Now);

        Assert.Equal("OnTrack", result.Status);
        Assert.Equal(5, result.TotalDurationDays);
        Assert.Equal(new[] { "a", "b" }, result.CriticalPath);
        Assert.Empty(result.Blockers);
    }

    [Fact]
    public void Evaluate_RejectsCyclicDependencyGraph()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var result = CriticalPathEvaluator.Evaluate(
            [
                new(first, "a", "A", 1, Now.AddDays(5), 0m, "Planned"),
                new(second, "b", "B", 1, Now.AddDays(6), 0m, "Planned")
            ],
            [new(first, second), new(second, first)],
            Now);

        Assert.Equal("CycleDetected", result.Status);
        Assert.Single(result.Blockers);
    }
}
