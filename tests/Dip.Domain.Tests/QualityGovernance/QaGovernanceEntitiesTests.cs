// Purpose: Verifies QA Governance Entities behavior, edge cases, and repository invariants.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Domain.Tests.QualityGovernance;

public sealed class QaGovernanceEntitiesTests
{
    [Fact]
    public void FindingLifecycle_TracksObservationSeparatelyFromWorkflowUpdate()
    {
        var t0 = DateTimeOffset.UtcNow; var finding = new QaFindingState(Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), Guid.NewGuid(), "build", new string('b', 40), t0);
        finding.Transition(FindingLifecycleStatus.InProgress, "Owner started investigation.", "lead", t0.AddHours(1));
        finding.LastSeenAtUtc.Should().Be(t0);
        finding.UpdatedAtUtc.Should().Be(t0.AddHours(1));
        finding.Observe(Guid.NewGuid(), new string('c', 40), t0.AddHours(2));
        finding.LastSeenAtUtc.Should().Be(t0.AddHours(2));
        finding.Occurrences.Should().Be(2);
    }


    [Fact]
    public void FindingLifecycle_RejectsInvalidJumpThatWouldBypassVerification()
    {
        var t0 = DateTimeOffset.UtcNow; var finding = new QaFindingState(Guid.NewGuid(), Guid.NewGuid(), new string('d', 64), Guid.NewGuid(), "build", null, t0);
        var act = () => finding.Transition(FindingLifecycleStatus.Verified, "Pretend verified", "operator", t0.AddMinutes(1));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Waiver_ExpiresAndCannotRemainActivePastDeadline()
    {
        var t0 = DateTimeOffset.UtcNow; var waiver = new QaWaiver(Guid.NewGuid(), Guid.NewGuid(), "Known vendor issue", "Waiting for upstream fix", "requester", null, "RULE-1", null, t0, t0.AddDays(7));
        waiver.Approve("qa-lead", "reviewed", t0.AddMinutes(1));
        waiver.IsActive(t0.AddDays(6)).Should().BeTrue();
        waiver.Expire(t0.AddDays(8));
        waiver.Status.Should().Be(WaiverStatus.Expired);
        waiver.IsActive(t0.AddDays(8)).Should().BeFalse();
    }

    [Fact]
    public void Reliability_MixedRepeatedResultsBecomeFlaky()
    {
        var t0 = DateTimeOffset.UtcNow; var reliability = new QaTestReliability(Guid.NewGuid(), Guid.NewGuid(), "test:checkout", t0);
        reliability.Observe(false, false, "a", t0.AddMinutes(1));
        reliability.Observe(true, false, "a", t0.AddMinutes(2));
        reliability.Observe(true, false, "a", t0.AddMinutes(3));
        reliability.Classification.Should().Be(TestReliabilityClassification.Flaky);
        reliability.FailureRatePercent.Should().BeApproximately(33.33m, 0.01m);
    }

    [Fact]
    public void Reliability_InfrastructureFailureIsNotProductFailure()
    {
        var t0 = DateTimeOffset.UtcNow; var reliability = new QaTestReliability(Guid.NewGuid(), Guid.NewGuid(), "suite:main", t0);
        reliability.Observe(false, true, "a", t0.AddMinutes(1));
        reliability.Classification.Should().Be(TestReliabilityClassification.EnvironmentFailure);
        reliability.FailCount.Should().Be(0);
    }

    [Fact]
    public void RegressionBaseline_RemembersLastGoodAndFirstBadCommit()
    {
        var t0 = DateTimeOffset.UtcNow; var baseline = new QaRegressionBaseline(Guid.NewGuid(), Guid.NewGuid(), "release", "qa.finding_count", "good0", 0m, t0);
        baseline.Observe("good1", 0m, true, t0.AddMinutes(1));
        baseline.Observe("bad1", 3m, false, t0.AddMinutes(2));
        baseline.LastKnownGoodCommit.Should().Be("good1");
        baseline.FirstBadCommit.Should().Be("bad1");
        baseline.Status.Should().Be(QaRegressionStatus.Regressed);
        baseline.Observe("good2", 0m, true, t0.AddMinutes(3));
        baseline.FirstBadCommit.Should().BeNull();
        baseline.Observe("bad2", 2m, false, t0.AddMinutes(4));
        baseline.LastKnownGoodCommit.Should().Be("good2");
        baseline.FirstBadCommit.Should().Be("bad2");
    }

    [Fact]
    public void RequestedWaiver_RequiresDifferentApprover()
    {
        var now = DateTimeOffset.UtcNow; var waiver = new QaWaiver(Guid.NewGuid(), Guid.NewGuid(), "Temporary exception", "Known issue", "requester", null, "RULE-1", null, now, now.AddDays(1));
        var self = () => waiver.Approve("requester", "self approval", now.AddMinutes(1)); self.Should().Throw<InvalidOperationException>();
        waiver.Approve("owner", "reviewed", now.AddMinutes(2)); waiver.Status.Should().Be(WaiverStatus.Active); waiver.ApprovedBy.Should().Be("owner");
    }

    [Fact]
    public void Policy_FollowsDraftApproveActivateLifecycle()
    {
        var now = DateTimeOffset.UtcNow; var policy = new QaPolicy(Guid.NewGuid(), Guid.NewGuid(), "Default", "2.0", "{}", false, now);
        policy.LifecycleStatus.Should().Be(QaPolicyLifecycleStatus.Draft);
        policy.Approve("maintainer", now.AddMinutes(1));
        policy.LifecycleStatus.Should().Be(QaPolicyLifecycleStatus.Approved);
        policy.Activate("owner", null, 2, now.AddMinutes(2));
        policy.LifecycleStatus.Should().Be(QaPolicyLifecycleStatus.Active);
        policy.Revision.Should().Be(2);
        policy.Enabled.Should().BeTrue();
    }

    [Fact]
    public void ReleaseOverride_IsCommitBoundExpiringAndTwoPerson()
    {
        var now = DateTimeOffset.UtcNow; var item = new QaReleaseGateOverride(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 40), "Accepted release risk", "requester", now, now.AddHours(2));
        var self = () => item.Approve("requester", "bad", now.AddMinutes(1)); self.Should().Throw<InvalidOperationException>();
        item.Approve("owner", "approved", now.AddMinutes(2));
        item.IsEffective(now.AddHours(1)).Should().BeTrue();
        item.Expire(now.AddHours(3));
        item.IsEffective(now.AddHours(3)).Should().BeFalse();
        item.Status.Should().Be(QaReleaseOverrideStatus.Expired);
    }

    [Fact]
    public void ProcessedRun_RecordsExactInputAndResultHashes()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new QaGovernanceProcessedRun(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2.0.0", new string('b', 64), now);
        item.Status.Should().Be(QaProcessedRunStatus.Processing);
        item.Complete(new string('c', 64), now.AddSeconds(1));
        item.Status.Should().Be(QaProcessedRunStatus.Completed);
        item.ResultHash.Should().Be(new string('c', 64));
    }
}
