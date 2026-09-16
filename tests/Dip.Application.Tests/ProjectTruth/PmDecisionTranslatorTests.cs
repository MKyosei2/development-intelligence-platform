// Purpose: Verifies P mD ec is io nT ra ns la to rT es ts behavior.
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.ProjectTruth;

public sealed class PmDecisionTranslatorTests
{
    [Fact]
    public void CriticalReproducedCrash_BecomesP0ReleaseBlock()
    {
        var finding=new QaFindingCandidate("native-crash",Severity.Critical,"asan","Access violation","0xC0000005");
        var decision=new PmEvidenceTranslator().Decide(finding,true);
        decision.Priority.Should().Be("P0");
        decision.BlocksRelease.Should().BeTrue();
        decision.OwnerRole.Should().Contain("Runtime");
        decision.ConfidencePercent.Should().BeGreaterThan(90m);
    }

    [Fact]
    public void Warning_StaticFinding_DoesNotAutomaticallyBlockRelease()
    {
        var finding=new QaFindingCandidate("static-analysis",Severity.Warning,"analyzer","Potential issue","Review this code path");
        var decision=new PmEvidenceTranslator().Decide(finding,false);
        decision.Priority.Should().Be("P1");
        decision.BlocksRelease.Should().BeFalse();
        decision.Alternative.Should().NotBeNullOrWhiteSpace();
    }
}
