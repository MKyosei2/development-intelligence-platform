// Purpose: Verifies P ro gr es sE va lu at or Te st s behavior.
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

        var result = new ProgressEvaluator().Evaluate([confirmed, contradicted], assessments);

        result.ReportedProgress.Should().Be(100m);
        result.VerifiedProgress.Should().Be(50m);
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("検査済")]
    [InlineData("レビュー済")]
    [InlineData("Rendered")]
    public void Evaluate_RecognizesCrossDomainCompletionVocabulary(string status)
    {
        var claim = new ProjectClaim(
            Guid.NewGuid(), Guid.NewGuid(), "item", status, null,
            "reporter", null, DateTimeOffset.UtcNow);
        var assessments = new Dictionary<Guid, ClaimVerificationAssessment>
        {
            [claim.Id] = new(claim.Id, ClaimVerificationStatus.Confirmed, "ok", [])
        };

        var result = new ProgressEvaluator().Evaluate([claim], assessments);

        result.ReportedProgress.Should().Be(100m);
        result.VerifiedProgress.Should().Be(100m);
    }

    [Fact]
    public void Evaluate_CustomPartialFactor_IsApplied()
    {
        var claim = new ProjectClaim(
            Guid.NewGuid(), Guid.NewGuid(), "item", "Complete", 100m,
            "reporter", null, DateTimeOffset.UtcNow);
        var assessments = new Dictionary<Guid, ClaimVerificationAssessment>
        {
            [claim.Id] = new(claim.Id, ClaimVerificationStatus.PartiallyConfirmed, "partial", [])
        };
        var evaluator = new ProgressEvaluator(ProjectTruthPolicy.Default with
        {
            PartiallyConfirmedProgressFactor = 0.7m
        });

        evaluator.Evaluate([claim], assessments).VerifiedProgress.Should().Be(70m);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Rejected")]
    [InlineData("Blocked")]
    [InlineData("On Hold")]
    [InlineData("失敗")]
    [InlineData("保留")]
    public void Evaluate_FailedOrBlockedStatus_DoesNotReceiveArbitraryMidpointCredit(string status)
    {
        var claim = new ProjectClaim(
            Guid.NewGuid(), Guid.NewGuid(), "item", status, null,
            "reporter", null, DateTimeOffset.UtcNow);
        var assessments = new Dictionary<Guid, ClaimVerificationAssessment>
        {
            [claim.Id] = new(claim.Id, ClaimVerificationStatus.Confirmed, "status observed", [])
        };

        var result = new ProgressEvaluator().Evaluate([claim], assessments);

        result.ReportedProgress.Should().Be(0m);
        result.VerifiedProgress.Should().Be(0m);
    }

    [Fact]
    public void Evaluate_UnknownStatus_RemainsUnknownInsteadOfZeroPercent()
    {
        var claim = new ProjectClaim(
            Guid.NewGuid(), Guid.NewGuid(), "item", "custom-vendor-status", null,
            "reporter", null, DateTimeOffset.UtcNow);
        var assessments = new Dictionary<Guid, ClaimVerificationAssessment>
        {
            [claim.Id] = new(claim.Id, ClaimVerificationStatus.Confirmed, "status observed", [])
        };

        var result = new ProgressEvaluator().Evaluate([claim], assessments);

        result.ReportedProgress.Should().BeNull();
        result.VerifiedProgress.Should().Be(0m);
    }

    [Fact]
    public void Evaluate_InProgressFallback_IsPolicyControlled()
    {
        var claim = new ProjectClaim(
            Guid.NewGuid(), Guid.NewGuid(), "item", "Working", null,
            "reporter", null, DateTimeOffset.UtcNow);
        var assessments = new Dictionary<Guid, ClaimVerificationAssessment>
        {
            [claim.Id] = new(claim.Id, ClaimVerificationStatus.Confirmed, "ok", [])
        };
        var evaluator = new ProgressEvaluator(ProjectTruthPolicy.Default with
        {
            InferredInProgressReportedProgress = 35m
        });

        var result = evaluator.Evaluate([claim], assessments);

        result.ReportedProgress.Should().Be(35m);
        result.VerifiedProgress.Should().Be(35m);
    }

    [Fact]
    public void Evaluate_ExplicitProgress_AlwaysWinsOverStatusFallback()
    {
        var claim = new ProjectClaim(
            Guid.NewGuid(), Guid.NewGuid(), "item", "Blocked", 82m,
            "reporter", null, DateTimeOffset.UtcNow);
        var assessments = new Dictionary<Guid, ClaimVerificationAssessment>
        {
            [claim.Id] = new(claim.Id, ClaimVerificationStatus.Confirmed, "ok", [])
        };

        var result = new ProgressEvaluator().Evaluate([claim], assessments);

        result.ReportedProgress.Should().Be(82m);
        result.VerifiedProgress.Should().Be(82m);
    }

    [Fact]
    public void Policy_RejectsInvalidProgressFallbacks()
    {
        var policy = ProjectTruthPolicy.Default with
        {
            InferredUnknownReportedProgress = 101m
        };

        var act = () => policy.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
