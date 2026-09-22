// Purpose: Verifies expected behavior and invariants for the surrounding feature.
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class ClaimEvidenceAssessmentScoringTests
{
    [Fact]
    public void Contradicted_high_progress_claim_scores_as_high_discrepancy()
    {
        var score = ClaimEvidenceAssessmentService.ComputeDiscrepancyScore(
            ClaimEvidenceAssessmentStatus.Contradicted, 0, 2, 95m);
        score.Should().BeGreaterThanOrEqualTo(95m);
    }

    [Fact]
    public void Supported_claim_remains_low_discrepancy()
    {
        var score = ClaimEvidenceAssessmentService.ComputeDiscrepancyScore(
            ClaimEvidenceAssessmentStatus.Supported, 3, 0, 100m);
        score.Should().BeLessThanOrEqualTo(5m);
    }
}
