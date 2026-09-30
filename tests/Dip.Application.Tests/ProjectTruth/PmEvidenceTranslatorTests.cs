// Purpose: Verifies PM Evidence Translator behavior, edge cases, and repository invariants.
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
        result.Title.Should().Contain("runtime");
        result.Summary.Should().Contain("crash");
    }

    [Fact]
    public void TestFailure_DoesNotAccusePerson()
    {
        var result = new PmEvidenceTranslator().Explain(new QaFindingCandidate("test", Severity.Warning, "pytest", "failed", "expected 2 got 3"));
        result.Summary.Should().Contain("expected behavior");
        result.Summary.Should().NotContain("lie");
    }

    [Fact]
    public void NativeQaFailure_ProducesQualityFocusedAction()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new EvidenceRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "project",
            EvidenceSourceType.Quality,
            "QualityGate",
            EvidenceOutcome.Failed,
            "Native QA quality gate failed",
            "A reproducible native memory-safety finding is still current.",
            EvidenceConfidence.High,
            null,
            "{\"ruleId\":\"DIP-NATIVE-MEMORY-001\"}",
            now,
            now);

        var result = new PmEvidenceTranslator().Explain(evidence);

        result.Title.Should().Contain("Development");
        result.RecommendedAction.Should().Contain("failed evidence");
    }

    [Fact]
    public void MediaReviewWarning_IsNotDescribedAsCodeFailure()
    {
        var result = new PmEvidenceTranslator().Explain(new QaFindingCandidate("media-review", Severity.Warning, "review", "Shot 030 retake", "Review rejected"));
        result.Title.Should().Contain("Media");
        result.Summary.Should().NotContain("source cannot");
    }
}
