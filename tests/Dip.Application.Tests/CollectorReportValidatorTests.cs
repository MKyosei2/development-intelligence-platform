// Purpose: Defines Collector Report Validator Tests responsibilities for this repository area.
using Dip.Application.Features.Collectors;
using Dip.Shared.Contracts.Collectors;
using Xunit;
namespace Dip.Application.Tests;
public sealed class CollectorReportValidatorTests
{
    private readonly CollectorReportValidator _v = new();
    [Fact] public void ValidReport_Passes() => _v.ValidateAndThrow(Create());
    [Fact]
    public void ReverseTime_Fails()
    {
        var r = Create(); Assert.Throws<ArgumentException>(() => _v.ValidateAndThrow(r with { FinishedAt = r.StartedAt.AddMinutes(-1) }));
    }
    [Fact]
    public void DuplicateMetrics_Fails()
    {
        var r = Create(); var m = new CollectorMetric("average_fps", 60, "fps", 59);
        Assert.Throws<ArgumentException>(() => _v.ValidateAndThrow(r with { Metrics = new[] { m, m } }));
    }
    private static CollectorReport Create()
    {
        var n = DateTimeOffset.UtcNow; return new("1.0", "report-1", "run-1", 1, "generic-json", "1.0.0",
            Guid.NewGuid(), "MKyosei2/development-intelligence-platform",
            "0123456789abcdef0123456789abcdef01234567", "main", n, n.AddMinutes(1),
            CollectorRunStatus.Succeeded, Array.Empty<CollectorMetric>(), Array.Empty<CollectorIssue>());
    }
}
