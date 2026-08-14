using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Domain.Tests.QualityGovernance;

public sealed class QaGovernanceEntitiesTests
{
    [Fact]
    public void FindingLifecycle_TracksObservationSeparatelyFromWorkflowUpdate()
    {
        var t0=DateTimeOffset.UtcNow;var finding=new QaFindingState(Guid.NewGuid(),Guid.NewGuid(),new string('a',64),Guid.NewGuid(),"build",new string('b',40),t0);
        finding.Transition(FindingLifecycleStatus.InProgress,"Owner started investigation.","lead",t0.AddHours(1));
        finding.LastSeenAtUtc.Should().Be(t0);
        finding.UpdatedAtUtc.Should().Be(t0.AddHours(1));
        finding.Observe(Guid.NewGuid(),new string('c',40),t0.AddHours(2));
        finding.LastSeenAtUtc.Should().Be(t0.AddHours(2));
        finding.Occurrences.Should().Be(2);
    }


    [Fact]
    public void FindingLifecycle_RejectsInvalidJumpThatWouldBypassVerification()
    {
        var t0=DateTimeOffset.UtcNow;var finding=new QaFindingState(Guid.NewGuid(),Guid.NewGuid(),new string('d',64),Guid.NewGuid(),"build",null,t0);
        var act=()=>finding.Transition(FindingLifecycleStatus.Verified,"Pretend verified","operator",t0.AddMinutes(1));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Waiver_ExpiresAndCannotRemainActivePastDeadline()
    {
        var t0=DateTimeOffset.UtcNow;var waiver=new QaWaiver(Guid.NewGuid(),Guid.NewGuid(),"Known vendor issue","Waiting for upstream fix","qa-lead",null,"RULE-1",null,t0,t0.AddDays(7));
        waiver.IsActive(t0.AddDays(6)).Should().BeTrue();
        waiver.Expire(t0.AddDays(8));
        waiver.Status.Should().Be(WaiverStatus.Expired);
        waiver.IsActive(t0.AddDays(8)).Should().BeFalse();
    }

    [Fact]
    public void Reliability_MixedRepeatedResultsBecomeFlaky()
    {
        var t0=DateTimeOffset.UtcNow;var reliability=new QaTestReliability(Guid.NewGuid(),Guid.NewGuid(),"test:checkout",t0);
        reliability.Observe(false,false,"a",t0.AddMinutes(1));
        reliability.Observe(true,false,"a",t0.AddMinutes(2));
        reliability.Observe(true,false,"a",t0.AddMinutes(3));
        reliability.Classification.Should().Be(TestReliabilityClassification.Flaky);
        reliability.FailureRatePercent.Should().BeApproximately(33.33m,0.01m);
    }

    [Fact]
    public void Reliability_InfrastructureFailureIsNotProductFailure()
    {
        var t0=DateTimeOffset.UtcNow;var reliability=new QaTestReliability(Guid.NewGuid(),Guid.NewGuid(),"suite:main",t0);
        reliability.Observe(false,true,"a",t0.AddMinutes(1));
        reliability.Classification.Should().Be(TestReliabilityClassification.EnvironmentFailure);
        reliability.FailCount.Should().Be(0);
    }

    [Fact]
    public void RegressionBaseline_RemembersLastGoodAndFirstBadCommit()
    {
        var t0=DateTimeOffset.UtcNow;var baseline=new QaRegressionBaseline(Guid.NewGuid(),Guid.NewGuid(),"release","qa.finding_count","good0",0m,t0);
        baseline.Observe("good1",0m,true,t0.AddMinutes(1));
        baseline.Observe("bad1",3m,false,t0.AddMinutes(2));
        baseline.LastKnownGoodCommit.Should().Be("good1");
        baseline.FirstBadCommit.Should().Be("bad1");
        baseline.Status.Should().Be(QaRegressionStatus.Regressed);
        baseline.Observe("good2",0m,true,t0.AddMinutes(3));
        baseline.FirstBadCommit.Should().BeNull();
        baseline.Observe("bad2",2m,false,t0.AddMinutes(4));
        baseline.LastKnownGoodCommit.Should().Be("good2");
        baseline.FirstBadCommit.Should().Be("bad2");
    }
}
