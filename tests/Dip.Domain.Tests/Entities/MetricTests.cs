using Dip.Domain.Entities;
using FluentAssertions;

namespace Dip.Domain.Tests.Entities;

public sealed class MetricTests
{
    [Fact]
    public void Constructor_CalculatesDifferencePercent()
    {
        var metric = new Metric(Guid.NewGuid(), Guid.NewGuid(), "average_fps", 54, "fps", 60);

        metric.Difference.Should().Be(-6);
        metric.DifferencePercent.Should().Be(-10);
    }

    [Fact]
    public void Constructor_LeavesDifferencePercentNull_WhenBaselineIsZero()
    {
        var metric = new Metric(Guid.NewGuid(), Guid.NewGuid(), "count", 10, "count", 0);
        metric.DifferencePercent.Should().BeNull();
    }
}
