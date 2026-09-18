// Purpose: Verifies behavior and invariants covered by A da pt iv eP ro je ct Ri sk Ev al ua to rT es ts.
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.ProjectTruth;

public sealed class AdaptiveProjectRiskEvaluatorTests
{
    [Fact]
    public void Evaluate_RaisesRiskForRepeatedRecentObjectiveFailures()
    {
        var now=DateTimeOffset.UtcNow;
        var projectId=Guid.NewGuid();
        var evidence=Enumerable.Range(0,6).Select(i=>Evidence(projectId,i<3?EvidenceOutcome.Failed:EvidenceOutcome.Succeeded,now.AddHours(-i))).ToArray();
        var result=new AdaptiveProjectRiskEvaluator().Evaluate(ProjectRiskLevel.Low,evidence,[],now);
        result.RiskLevel.Should().Be(ProjectRiskLevel.Critical);
        result.HealthPenalty.Should().BeGreaterThan(0m);
        result.Reasons.Should().NotBeEmpty();
    }

    [Fact]
    public void Evaluate_NeverLowersDeterministicRisk()
    {
        var now=DateTimeOffset.UtcNow;
        var projectId=Guid.NewGuid();
        var evidence=new[]{Evidence(projectId,EvidenceOutcome.Succeeded,now)};
        var result=new AdaptiveProjectRiskEvaluator().Evaluate(ProjectRiskLevel.High,evidence,[],now);
        result.RiskLevel.Should().Be(ProjectRiskLevel.High);
        result.HealthPenalty.Should().Be(0m);
    }

    private static EvidenceRecord Evidence(Guid projectId,EvidenceOutcome outcome,DateTimeOffset at)
        =>new(Guid.NewGuid(),projectId,null,"project",EvidenceSourceType.Quality,"qa.test",outcome,"QA evidence",outcome.ToString(),EvidenceConfidence.High,null,null,at,at);
}
