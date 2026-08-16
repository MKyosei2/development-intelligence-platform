using Dip.Application.Features.QualityAutomation;using Dip.Domain.ValueObjects;using Dip.Infrastructure.QualityAutomation;
namespace Dip.Infrastructure.Tests.QualityAutomation;
public sealed class QaCapabilityPlannerTests
{
    [Fact] public void Go_pack_includes_race_fuzz_and_data_flow(){var profile=new QaTechnologyProfile(".",[LanguageId.Go],["Go Modules"],[],[],[]);var p=new QaCapabilityPlanner().Describe(LanguageId.Go,profile,".");Assert.Contains(p.Capabilities,x=>x.Capability==QaCapability.RaceDetection);Assert.Contains(p.Capabilities,x=>x.Capability==QaCapability.Fuzz);Assert.Contains(p.Capabilities,x=>x.Capability==QaCapability.DataFlowAnalysis);}
    [Fact] public void Unknown_language_keeps_manifest_escape_hatch(){var l=LanguageId.Parse("fortran");var p=new QaCapabilityPlanner().Describe(l,new(".",[l],[],[],[],[]),".");Assert.Contains(p.Capabilities,x=>x.Tool==".dip/qa.json"&&x.Capability==QaCapability.Fuzz);}
}
