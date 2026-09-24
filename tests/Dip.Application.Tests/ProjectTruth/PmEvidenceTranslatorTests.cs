// Purpose: Verifies P mE vi de nc eT ra ns la to r behavior, edge cases, and invariants for p ro je ct tr ut h.
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.ProjectTruth;

public sealed class PmEvidenceTranslatorTests
{
    [Fact]
    public void NativeCrash_IsExplainedWithoutRequiringTechnicalKnowledge()
    {
        var result = new PmEvidenceTranslator().Explain(new QaFindingCandidate("native-crash", Severity.Critical, "asan", "Access violation", "0xC0000005"));
        result.Title.Should().Contain("runtime"); result.Summary.Should().Contain("crash");
    }

    [Fact]
    public void TestFailure_DoesNotAccusePerson()
    {
        var result = new PmEvidenceTranslator().Explain(new QaFindingCandidate("test", Severity.Warning, "pytest", "failed", "expected 2 got 3"));
        result.Summary.Should().Contain("expected behavior"); result.Summary.Should().NotContain("lie");
    }

    [Fact]
    public void ConstructionInspectionFailure_ProducesDomainAppropriateAction()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new EvidenceRecord(Guid.NewGuid(), Guid.NewGuid(), null, "floor-3", EvidenceSourceType.Spreadsheet, "ConstructionInspection", EvidenceOutcome.Failed, "3F piping inspection", "Inspection failed.", EvidenceConfidence.High, null, "{\"domainId\":\"construction\"}", now, now);
        var result = new PmEvidenceTranslator().Explain(evidence);
        result.Title.Should().Contain("Inspection"); result.RecommendedAction.Should().Contain("inspection");
    }

    [Fact]
    public void MediaReviewWarning_IsNotDescribedAsCodeFailure()
    {
        var result = new PmEvidenceTranslator().Explain(new QaFindingCandidate("media-review", Severity.Warning, "review", "Shot 030 retake", "Review rejected"));
        result.Title.Should().Contain("Media"); result.Summary.Should().NotContain("source cannot");
    }
}
