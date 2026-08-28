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
        var atBoundary = evaluator.Evaluate(new ProgressAssessment(80m, 100m),EvidenceConfidence.High,Array.Empty<ProjectBlocker>(),0,0);
        var belowBoundary = evaluator.Evaluate(new ProgressAssessment(81m, 100m),EvidenceConfidence.High,Array.Empty<ProjectBlocker>(),0,0);
        atBoundary.Should().Be(ProjectRiskLevel.Medium);belowBoundary.Should().Be(ProjectRiskLevel.Low);
    }

    [Fact]
    public void CustomProgressGapThreshold_IsAppliedWithoutChangingEvaluatorCode()
    {
        var evaluator = new ProjectRiskEvaluator(ProjectTruthPolicy.Default with{MediumRiskProgressGapThreshold = 30m});
        evaluator.Evaluate(new ProgressAssessment(75m,100m),EvidenceConfidence.High,Array.Empty<ProjectBlocker>(),0,0).Should().Be(ProjectRiskLevel.Low);
        evaluator.Evaluate(new ProgressAssessment(70m,100m),EvidenceConfidence.High,Array.Empty<ProjectBlocker>(),0,0).Should().Be(ProjectRiskLevel.Medium);
    }

    [Fact]
    public void CustomContradictionThreshold_IsApplied()
    {
        var evaluator = new ProjectRiskEvaluator(ProjectTruthPolicy.Default with{HighRiskContradictionThreshold = 3});
        evaluator.Evaluate(new ProgressAssessment(100m,100m),EvidenceConfidence.High,Array.Empty<ProjectBlocker>(),2,0).Should().Be(ProjectRiskLevel.Medium);
        evaluator.Evaluate(new ProgressAssessment(100m,100m),EvidenceConfidence.High,Array.Empty<ProjectBlocker>(),3,0).Should().Be(ProjectRiskLevel.High);
    }

    [Fact]
    public void InvalidPercentageThreshold_FailsFast()
    {
        var invalid = ProjectTruthPolicy.Default with{MediumRiskProgressGapThreshold = 101m};
        Action action = () => invalid.Validate();
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void InvalidContradictionThreshold_FailsFast()
    {
        var invalid = ProjectTruthPolicy.Default with{HighRiskContradictionThreshold = 0};
        Action action = () =>
        {
            _ = new ProjectRiskEvaluator(invalid);
        };
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void InvalidCrossDomainProgressFactor_FailsFast()
    {
        var invalid=ProjectTruthPolicy.Default with{PartiallyConfirmedProgressFactor=1.1m};
        Action action = () => invalid.Validate();
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void DefaultCriticalEvidenceKinds_ContainNonSoftwareVerificationGates()
    {
        var policy=ProjectTruthPolicy.Default;
        policy.CriticalEvidenceKindTokens.Should().Contain(x=>x.Contains("inspection",StringComparison.OrdinalIgnoreCase));
        policy.CriticalEvidenceKindTokens.Should().Contain(x=>x.Contains("approval",StringComparison.OrdinalIgnoreCase));
        policy.CriticalEvidenceKindTokens.Should().Contain(x=>x.Contains("render",StringComparison.OrdinalIgnoreCase));
    }
}
