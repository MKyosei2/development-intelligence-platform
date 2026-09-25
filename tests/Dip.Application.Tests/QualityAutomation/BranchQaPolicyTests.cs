// Purpose: Defines Branch QA Policy Tests responsibilities for this repository area.
using Dip.Application.Features.QualityAutomation;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class BranchQaPolicyTests
{
    [Fact]
    public void UnverifiedBranch_CannotBePromoted()
    {
        var gate = BranchQualityOperationsService.EvaluateGate(null, null, 0, 0);
        gate.Status.Should().Be("Unknown");
        gate.CanPromote.Should().BeFalse();
    }

    [Fact]
    public void CriticalIssue_BlocksBranchEvenAfterSuccessfulRun()
    {
        var gate = BranchQualityOperationsService.EvaluateGate("Succeeded", 1m, 1, 0);
        gate.Status.Should().Be("Blocked");
        gate.CanPromote.Should().BeFalse();
    }

    [Fact]
    public void StaleSuccessfulEvidence_IsHeld()
    {
        var gate = BranchQualityOperationsService.EvaluateGate("Succeeded", 25m, 0, 0, 24m);
        gate.Status.Should().Be("Hold");
        gate.Label.Should().Be("Stale");
    }

    [Fact]
    public void FreshCleanSuccessfulBranch_IsReady()
    {
        var gate = BranchQualityOperationsService.EvaluateGate("Succeeded", 2m, 0, 0, 24m);
        gate.Status.Should().Be("Ready");
        gate.CanPromote.Should().BeTrue();
    }
}
