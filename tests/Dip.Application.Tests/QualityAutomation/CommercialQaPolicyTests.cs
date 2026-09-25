// Purpose: Verifies Commercial QA Policy behavior, edge cases, and repository invariants.
using Dip.Application.Features.QualityAutomation;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class CommercialQaPolicyTests
{
    [Fact]
    public void ReproducedP0_BlocksPromotionWithHighConfidence()
    {
        var score = CommercialQaPolicy.QualityScore(1, 0, 0, 0);
        var gate = CommercialQaPolicy.ReleaseGate(1, 0, 0, "Succeeded", 1m, 24m, true, score);
        gate.Status.Should().Be("Blocked");
        gate.Label.Should().Contain("DO NOT PROMOTE");
        gate.DecisionConfidencePercent.Should().BeGreaterThanOrEqualTo(95m);
    }

    [Fact]
    public void FreshSuccessfulRunWithoutP0_IsReadyForHumanApproval()
    {
        var score = CommercialQaPolicy.QualityScore(0, 2, 0, 0);
        var gate = CommercialQaPolicy.ReleaseGate(0, 2, 0, "Succeeded", 2m, 24m, false, score);
        gate.Status.Should().Be("Ready");
        gate.Label.Should().Contain("HUMAN APPROVAL");
    }

    [Fact]
    public void StaleEvidence_NeverBecomesReady()
    {
        var gate = CommercialQaPolicy.ReleaseGate(0, 0, 0, "Succeeded", 30m, 24m, false, 100m);
        gate.Status.Should().Be("Hold");
        gate.Reason.Should().Contain("stale");
    }

    [Fact]
    public void OperationalQualityScore_IsBounded()
    {
        CommercialQaPolicy.QualityScore(100, 100, 100, 100).Should().Be(0m);
        CommercialQaPolicy.QualityScore(0, 0, 0, 0).Should().Be(100m);
    }
}
