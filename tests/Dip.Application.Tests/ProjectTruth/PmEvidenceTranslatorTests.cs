using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using FluentAssertions;
namespace Dip.Application.Tests.ProjectTruth;
public sealed class PmEvidenceTranslatorTests
{
 [Fact]public void NativeCrash_IsExplainedWithoutRequiringTechnicalKnowledge()
 {
  var result=new PmEvidenceTranslator().Explain(new QaFindingCandidate("native-crash",Severity.Critical,"asan","Access violation","0xC0000005"));
  result.Title.Should().Contain("runtime");result.Summary.Should().Contain("crash");
 }
 [Fact]public void TestFailure_DoesNotAccusePerson()
 {
  var result=new PmEvidenceTranslator().Explain(new QaFindingCandidate("test",Severity.Warning,"pytest","failed","expected 2 got 3"));
  result.Summary.Should().Contain("expected behavior");result.Summary.Should().NotContain("lie");
 }
}
