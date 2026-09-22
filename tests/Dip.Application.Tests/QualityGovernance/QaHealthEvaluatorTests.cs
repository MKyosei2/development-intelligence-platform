// Purpose: Verifies malformed or incomplete QA summaries degrade self-health instead of reporting healthy.
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityGovernance;

public sealed class QaHealthEvaluatorTests
{
    [Fact]
    public void Evaluate_WithMalformedSummary_ReturnsDegraded()
    {
        var run = new QaRun(Guid.NewGuid(), Guid.NewGuid(), "unit-test",
            new LanguageId("csharp"), ".", null, ".", DateTimeOffset.UtcNow);
        run.Start(DateTimeOffset.UtcNow);
        run.Complete(QaRunStatus.Succeeded, 0, 0, 0, "{broken", DateTimeOffset.UtcNow);

        var result = new QaHealthEvaluator().Evaluate(run, DateTimeOffset.UtcNow);

        result.Status.Should().Be(QaSystemHealthStatus.Degraded);
    }
}
