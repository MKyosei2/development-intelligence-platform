using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.ProjectTruth;

public sealed class ProgressEvaluatorTests
{
    [Fact]
    public void Evaluate_SeparatesReportedAndEvidenceBackedProgress()
    {
        var confirmed = new ProjectClaim(
            Guid.NewGuid(), Guid.NewGuid(), "feature-a", "Complete", 100m,
            "a", null, DateTimeOffset.UtcNow);
        var contradicted = new ProjectClaim(
            Guid.NewGuid(), confirmed.ProjectId, "feature-b", "Complete", 100m,
            "b", null, DateTimeOffset.UtcNow);

        var assessments = new Dictionary<Guid, ClaimVerificationAssessment>
        {
            [confirmed.Id] = new(confirmed.Id, ClaimVerificationStatus.Confirmed, "ok", []),
            [contradicted.Id] = new(contradicted.Id, ClaimVerificationStatus.Contradicted, "bad", [])
        };

        var result = new ProgressEvaluator().Evaluate(
            [confirmed, contradicted],
            assessments);

        result.ReportedProgress.Should().Be(100m);
        result.VerifiedProgress.Should().Be(50m);
    }
}
