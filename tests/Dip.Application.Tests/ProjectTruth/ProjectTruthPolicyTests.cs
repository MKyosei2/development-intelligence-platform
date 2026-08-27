using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.ProjectTruth;

public sealed class ProjectTruthPolicyTests
{
    [Fact]
    public void DefaultPolicy_PreservesHistoricalProgressGapBoundary()
    {
        var evaluator = new ProjectRiskEvaluator();

        var atBoundary = evaluator.Evaluate(
            new ProgressAssessment(80m, 100m),
            EvidenceConfidence.High,
            Array.Empty<ProjectBlocker>(),
            contradictionCount: 0,
            unverifiedClaimCount: 0);

        var belowBoundary = evaluator.Evaluate(
            new ProgressAssessment(81m, 100m),
            EvidenceConfidence.High,
            Array.Empty<ProjectBlocker>(),
            contradictionCount: 0,
            unverifiedClaimCount: 0);

        atBoundary.Should().Be(ProjectRiskLevel.Medium);
        belowBoundary.Should().Be(ProjectRiskLevel.Low);
    }

    [Fact]
    public void CustomProgressGapThreshold_IsAppliedWithoutChangingEvaluatorCode()
    {
        var policy = ProjectTruthPolicy.Default with
        {
            MediumRiskProgressGapThreshold = 30m
        };
        var evaluator = new ProjectRiskEvaluator(policy);

        var twentyFivePointGap = evaluator.Evaluate(
            new ProgressAssessment(75m, 100m),
            EvidenceConfidence.High,
            Array.Empty<ProjectBlocker>(),
            contradictionCount: 0,
            unverifiedClaimCount: 0);

        var thirtyPointGap = evaluator.Evaluate(
            new ProgressAssessment(70m, 100m),
            EvidenceConfidence.High,
            Array.Empty<ProjectBlocker>(),
            contradictionCount: 0,
            unverifiedClaimCount: 0);

        twentyFivePointGap.Should().Be(ProjectRiskLevel.Low);
        thirtyPointGap.Should().Be(ProjectRiskLevel.Medium);
    }

    [Fact]
    public void CustomContradictionThreshold_IsApplied()
    {
        var policy = ProjectTruthPolicy.Default with
        {
            HighRiskContradictionThreshold = 3
        };
        var evaluator = new ProjectRiskEvaluator(policy);

        var twoContradictions = evaluator.Evaluate(
            new ProgressAssessment(100m, 100m),
            EvidenceConfidence.High,
            Array.Empty<ProjectBlocker>(),
            contradictionCount: 2,
            unverifiedClaimCount: 0);

        var threeContradictions = evaluator.Evaluate(
            new ProgressAssessment(100m, 100m),
            EvidenceConfidence.High,
            Array.Empty<ProjectBlocker>(),
            contradictionCount: 3,
            unverifiedClaimCount: 0);

        twoContradictions.Should().Be(ProjectRiskLevel.Medium);
        threeContradictions.Should().Be(ProjectRiskLevel.High);
    }

    [Fact]
    public void InvalidPercentageThreshold_FailsFast()
    {
        var invalid = ProjectTruthPolicy.Default with
        {
            MediumRiskProgressGapThreshold = 101m
        };

        var action = () => invalid.Validate();

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void InvalidContradictionThreshold_FailsFast()
    {
        var invalid = ProjectTruthPolicy.Default with
        {
            HighRiskContradictionThreshold = 0
        };

        var action = () => new ProjectRiskEvaluator(invalid);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
