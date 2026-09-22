// Purpose: Verifies QA policy boundary conditions and fail-closed infrastructure decisions.
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityGovernance;

public sealed class QaPolicyEvaluatorTests
{
    [Fact]
    public void Evaluate_WhenInfrastructureUnavailableAndBlockingEnabled_ReturnsNoGo()
    {
        var run = NewRun();
        run.Start(DateTimeOffset.UtcNow);
        run.Complete(QaRunStatus.InfrastructureFailure, 0, 0, 0, "{}", DateTimeOffset.UtcNow);
        var sut = new QaPolicyEvaluator();

        var evaluation = sut.Evaluate(
            run, [], [], [], [], new QaPolicyRules(BlockOnUnavailableQa: true),
            null, 0, 0, 0, 0, DateTimeOffset.UtcNow);

        evaluation.Verdict.Should().Be(QaGateVerdict.NoGo);
        evaluation.BlockersJson.Should().Contain("infrastructure");
    }

    [Fact]
    public void ReadRules_WithNoPolicy_ReturnsConservativeDefaults()
    {
        var rules = new QaPolicyEvaluator().ReadRules(null);

        rules.BlockOnUnavailableQa.Should().BeTrue();
        rules.MaxCriticalFindings.Should().Be(0);
    }

    private static QaRun NewRun() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "unit-test", new LanguageId("csharp"),
            ".", "0123456789abcdef0123456789abcdef01234567", ".", DateTimeOffset.UtcNow);
}
