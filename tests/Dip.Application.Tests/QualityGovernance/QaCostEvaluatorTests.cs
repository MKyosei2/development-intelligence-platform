// Purpose: Verifies QA cost measurement respects configured duration budgets.
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityGovernance;

public sealed class QaCostEvaluatorTests
{
    [Fact]
    public void Measure_WhenWallDurationExceedsBudget_ReturnsOverBudget()
    {
        var root = Directory.CreateTempSubdirectory("dip-qa-cost-").FullName;
        var start = DateTimeOffset.UtcNow;
        var run = new QaRun(Guid.NewGuid(), Guid.NewGuid(), "unit-test",
            new LanguageId("csharp"), ".", null, root, start);
        run.Start(start);
        run.Complete(QaRunStatus.Succeeded, 0, 0, 0, "{\"tools\":[]}", start.AddSeconds(2));

        var result = new QaCostEvaluator(null!).Measure(
            run, new QaPolicyRules(MaxRunDurationMs: 1000));

        result.WallDurationMs.Should().BeGreaterThanOrEqualTo(2000);
        result.BudgetVerdict.Should().Be("OverBudget");
    }
}
