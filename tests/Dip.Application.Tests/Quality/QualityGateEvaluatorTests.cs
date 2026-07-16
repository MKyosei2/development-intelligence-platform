using Dip.Application.Features.Quality.Services;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.Quality;

public sealed class QualityGateEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsFail_WhenFpsDropsTenPercent()
    {
        var metric = new Metric(Guid.NewGuid(), Guid.NewGuid(), "average_fps", 54, "fps", 60);

        var result = new QualityGateEvaluator().Evaluate([metric], []);

        result.Status.Should().Be("Fail");
        result.CriticalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Evaluate_ReturnsWarning_WhenWarningIssueExists()
    {
        var issue = new QualityIssue(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "asset",
            Severity.Warning,
            "Large texture",
            "Texture is oversized.",
            "Assets/A.png",
            DateTimeOffset.UtcNow);

        var result = new QualityGateEvaluator().Evaluate([], [issue]);

        result.Status.Should().Be("Warning");
        result.WarningCount.Should().Be(1);
    }
}
