// Purpose: Verifies C la im Ve ri fi ca ti on Ev al ua to rT es ts behavior.
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.ProjectTruth;

public sealed class ClaimVerificationEvaluatorTests
{
    private readonly ClaimVerificationEvaluator _evaluator = new();

    [Fact]
    public void CompleteClaim_WithTwoStrongIndependentSuccesses_IsConfirmed()
    {
        var now = DateTimeOffset.UtcNow;var projectId = Guid.NewGuid();var claim = Claim(projectId, now, "Complete");
        var evidence = new[]{Evidence(projectId, "build", EvidenceOutcome.Succeeded, now),Evidence(projectId, "ci-check", EvidenceOutcome.Succeeded, now)};
        var result = _evaluator.Evaluate(claim, evidence, now);
        result.Status.Should().Be(ClaimVerificationStatus.Confirmed);
        result.EvidenceLinks.Count(item => item.Relation == EvidenceRelation.Supports).Should().Be(2);
    }

    [Fact]
    public void Claim_WithStrongFailure_IsContradicted()
    {
        var now = DateTimeOffset.UtcNow;var projectId = Guid.NewGuid();var claim = Claim(projectId, now, "Complete");
        var evidence = new[]{Evidence(projectId, "build", EvidenceOutcome.Succeeded, now),Evidence(projectId, "runtime", EvidenceOutcome.Failed, now)};
        var result = _evaluator.Evaluate(claim, evidence, now);
        result.Status.Should().Be(ClaimVerificationStatus.Contradicted);
        result.EvidenceLinks.Should().Contain(item => item.Relation == EvidenceRelation.Contradicts);
    }

    [Fact]
    public void OldClaim_WithoutNewEvidence_IsStale()
    {
        var now = DateTimeOffset.UtcNow;var projectId = Guid.NewGuid();var claim = Claim(projectId, now.AddDays(-10), "In progress");
        var result = _evaluator.Evaluate(claim, Array.Empty<EvidenceRecord>(), now);
        result.Status.Should().Be(ClaimVerificationStatus.Stale);
    }

    [Theory]
    [InlineData("承認済")]
    [InlineData("検査済")]
    [InlineData("レビュー済")]
    [InlineData("納品済")]
    public void CrossDomainCompletionClaim_WithIndependentEvidence_CanBeConfirmed(string status)
    {
        var now=DateTimeOffset.UtcNow;var projectId=Guid.NewGuid();var claim=Claim(projectId,now,status);
        var evidence=new[]
        {
            Evidence(projectId,"inspection",EvidenceOutcome.Succeeded,now,EvidenceSourceType.Spreadsheet),
            Evidence(projectId,"approval",EvidenceOutcome.Succeeded,now,EvidenceSourceType.Planning)
        };
        _evaluator.Evaluate(claim,evidence,now).Status.Should().Be(ClaimVerificationStatus.Confirmed);
    }

    private static ProjectClaim Claim(Guid projectId, DateTimeOffset at, string status) => new(
        Guid.NewGuid(), projectId, "project", status, 100m, "reporter", null, at);

    private static EvidenceRecord Evidence(Guid projectId,string kind,EvidenceOutcome outcome,DateTimeOffset at,EvidenceSourceType source=EvidenceSourceType.Build) => new(
        Guid.NewGuid(), projectId, null, "project", source,kind, outcome, kind, $"{kind} result", EvidenceConfidence.High,null, null, at, at);
}
