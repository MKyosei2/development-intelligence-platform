// Purpose: Verifies that missing QA coverage cannot be represented as a passing quality gate.
using Dip.Application.Features.Quality;
using Dip.Domain.Entities;
using FluentAssertions;

namespace Dip.Application.Tests.Quality;

public sealed class QualityGateFailClosedTests
{
    private readonly QualityGateEvaluator _evaluator = new(new QualityRuleEvaluator());

    [Fact]
    public void EmptyObservations_AreWarning_NotPassed()
    {
        var result = _evaluator.Evaluate([], []);

        result.Status.Should().Be("Warning");
        result.Score.Should().Be(0m);
        result.Reasons.Should().ContainSingle(reason =>
            reason.Contains("not PASS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CollectorSummaryOnly_AreWarning_NotPassed()
    {
        var runId = Guid.NewGuid();
        var metrics = new[]
        {
            new Metric(Guid.NewGuid(), runId, "collector_issue_critical_count", 0m, "count", null),
            new Metric(Guid.NewGuid(), runId, "collector_issue_warning_count", 0m, "count", null)
        };

        var result = _evaluator.Evaluate(metrics, []);

        result.Status.Should().Be("Warning");
        result.Score.Should().Be(0m);
    }

    [Fact]
    public void MeaningfulHealthyMetric_CanPass()
    {
        var metric = new Metric(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "average_fps",
            61m,
            "fps",
            60m);

        var result = _evaluator.Evaluate([metric], []);

        result.Status.Should().Be("Passed");
        result.Score.Should().Be(100m);
    }
}
