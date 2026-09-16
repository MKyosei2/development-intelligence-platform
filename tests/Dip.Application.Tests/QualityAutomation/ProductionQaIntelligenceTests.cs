// Purpose: Verifies P ro du ct io nQ aI nt el li ge nc eT es ts behavior.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class ProductionQaIntelligenceTests
{
    [Fact]
    public void DetectProfile_UsesManifestGenresWithoutPretendingAllGamesShareOneQaShape()
    {
        var root=Path.Combine(Path.GetTempPath(),"dip-production-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root,".dip"));
        try
        {
            File.WriteAllText(Path.Combine(root,".dip","production.json"),"{\"domain\":\"game\",\"genres\":[\"fps\",\"racing\"],\"capabilities\":[\"custom-replay-check\"]}");
            var technology=new QaTechnologyProfile(root,[LanguageId.Cpp],["CMake"],[],["Unreal Engine"],["Unreal Engine: .uproject"]);
            var result=ProductionQaIntelligence.DetectProfile(technology,root);
            result.Domain.Should().Be("game");
            result.Genres.Should().Contain("fps");
            result.Genres.Should().Contain("racing");
            result.Capabilities.Should().Contain("hit-registration");
            result.Capabilities.Should().Contain("vehicle-physics");
            result.Capabilities.Should().Contain("custom-replay-check");
        }
        finally { Directory.Delete(root,true); }
    }

    [Fact]
    public void ReadMediaRegressionSpecs_ParsesReferenceComparisonContract()
    {
        var root=Path.Combine(Path.GetTempPath(),"dip-media-profile-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root,".dip"));
        try
        {
            File.WriteAllText(Path.Combine(root,".dip","production.json"),"{\"mediaRegression\":[{\"type\":\"Visual\",\"baselinePath\":\"refs/a.png\",\"currentPath\":\"out/a.png\",\"warningThresholdPercent\":1.5,\"failureThresholdPercent\":8}]}");
            var specs=ProductionQaIntelligence.ReadMediaRegressionSpecs(root);
            specs.Should().ContainSingle();
            specs[0].Type.Should().Be("Visual");
            specs[0].WarningThresholdPercent.Should().Be(1.5m);
        }
        finally { Directory.Delete(root,true); }
    }

    [Fact]
    public void Classify_ReproducedMemorySafety_IsReleaseBlocker()
    {
        var finding=new QaFindingCandidate("memory-safety",Severity.Critical,"asan","heap use after free","AddressSanitizer use-after-free");
        var result=ProductionQaIntelligence.Classify(finding,ReproductionStatus.Reproduced);
        result.DefectClass.Should().Be("memory-safety");
        result.ReleaseBlocker.Should().BeTrue();
        result.RecommendedAttempts.Should().Be(5);
        result.RootCauseSignals.Should().Contain(x=>x.Contains("reproduced",StringComparison.OrdinalIgnoreCase));
    }
}
