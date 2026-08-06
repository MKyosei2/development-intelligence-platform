using Dip.Application.Features.Quality;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
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
