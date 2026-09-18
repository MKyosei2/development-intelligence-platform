// Purpose: Verifies behavior and invariants covered by I de Di ag no st ic No rm al iz er Te st s.
using Dip.Application.Features.QualityAutomation;using Dip.Domain.ValueObjects;using Dip.Infrastructure.QualityAutomation;
namespace Dip.Infrastructure.Tests.QualityAutomation;
public sealed class IdeDiagnosticNormalizerTests
{
    [Fact] public void Normalizes_lsp_error_to_critical_finding(){var batch=new IdeDiagnosticBatch(Guid.NewGuid(),"subject",LanguageId.TypeScript,".",null,[new("typescript","TS2322","Error","bad assignment","src/a.ts",4,2)]);var f=new IdeDiagnosticNormalizer().Normalize(batch).Single();Assert.Equal("Critical",f.Severity.ToString());Assert.Equal("TS2322",f.RuleId);Assert.Equal("lsp",f.SourceFormat);}
}
