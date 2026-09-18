// Purpose: Verifies behavior and invariants covered by Q ua li ty Ru le Ev al ua to rT es ts.
using Dip.Application.Features.Quality;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.Quality;

public sealed class QualityRuleEvaluatorTests
{
    private readonly QualityRuleEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_DetectsHigherIsBetterRegression()
    {
        var metric = new Metric(
            Guid.NewGuid(), Guid.NewGuid(), "average_fps", 48m, "fps", 60m);

        var result = _evaluator.Evaluate([metric], []);

        result.Should().ContainSingle();
        result[0].Severity.Should().Be(Severity.Critical);
        result[0].Reason.Should().Contain("decreased by 20");
    }

    [Fact]
    public void Evaluate_DetectsLowerIsBetterRegression()
    {
        var metric = new Metric(
            Guid.NewGuid(), Guid.NewGuid(), "memory_mb", 120m, "MB", 100m);

        var result = _evaluator.Evaluate([metric], []);

        result.Should().ContainSingle();
        result[0].Severity.Should().Be(Severity.Critical);
        result[0].Reason.Should().Contain("increased by 20");
    }

    [Fact]
    public void Evaluate_IgnoresResolvedIssue()
    {
        var issue = CreateIssue(Severity.Critical);
        issue.Resolve(DateTimeOffset.UtcNow.AddMinutes(1));

        _evaluator.Evaluate([], [issue]).Should().BeEmpty();
    }

    private static QualityIssue CreateIssue(Severity severity) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            "build",
            severity,
            "Build failure",
            "The build failed.",
            "ci",
            "build.failed",
            null,
            DateTimeOffset.UtcNow);
}
