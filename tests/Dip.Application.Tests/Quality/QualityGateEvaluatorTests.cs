using Dip.Application.Features.Quality;
using Dip.Application.Features.QualityAutomation;
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
