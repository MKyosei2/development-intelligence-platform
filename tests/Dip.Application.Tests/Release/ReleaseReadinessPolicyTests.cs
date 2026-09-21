// Purpose: Verifies deterministic ReleaseReadinessPolicy scoring boundaries and contribution caps.
using Dip.Application.Features.Release;

namespace Dip.Application.Tests.Release;

public sealed class ReleaseReadinessPolicyTests
{
    [Fact]
    public void CalculateConfidence_UsesBaseScoreWhenNoEvidenceExists()
        => Assert.Equal(45m, ReleaseReadinessPolicy.CalculateConfidence(0, false, false));

    [Fact]
    public void CalculateConfidence_ClampsNegativeEvidenceToZero()
        => Assert.Equal(45m, ReleaseReadinessPolicy.CalculateConfidence(-10, false, false));

    [Fact]
    public void CalculateConfidence_CapsEvidenceContribution()
        => Assert.Equal(80m, ReleaseReadinessPolicy.CalculateConfidence(10_000, false, false));

    [Fact]
    public void CalculateConfidence_AddsProjectTruthContribution()
        => Assert.Equal(55m, ReleaseReadinessPolicy.CalculateConfidence(0, true, false));

    [Fact]
    public void CalculateConfidence_AddsPerformanceBudgetContribution()
        => Assert.Equal(55m, ReleaseReadinessPolicy.CalculateConfidence(0, false, true));

    [Fact]
    public void CalculateConfidence_CannotExceedOneHundred()
        => Assert.Equal(100m, ReleaseReadinessPolicy.CalculateConfidence(10_000, true, true));
}
