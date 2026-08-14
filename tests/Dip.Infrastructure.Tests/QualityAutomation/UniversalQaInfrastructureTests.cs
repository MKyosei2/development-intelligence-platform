using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class UniversalQaInfrastructureTests : IDisposable
{
    private readonly string _root=Path.Combine(Path.GetTempPath(),"dip-universal-qa-tests",Guid.NewGuid().ToString("N"));

    public UniversalQaInfrastructureTests()=>Directory.CreateDirectory(_root);

    [Fact]
    public async Task Detector_FindsUnityCSharpAndMsBuild()
    {
        Directory.CreateDirectory(Path.Combine(_root,"Assets"));
        Directory.CreateDirectory(Path.Combine(_root,"ProjectSettings"));
        await File.WriteAllTextAsync(Path.Combine(_root,"Game.csproj"),"<Project />");
        await File.WriteAllTextAsync(Path.Combine(_root,"Assets","Player.cs"),"public sealed class Player {}");

        var profile=await new WorkspaceTechnologyDetector().DetectAsync(_root,CancellationToken.None);

        profile.PrimaryLanguage.Should().Be(LanguageId.CSharp);
        profile.GameEngines.Should().Contain("unity");
        profile.BuildSystems.Should().Contain("msbuild");
        profile.Frameworks.Should().Contain("unity");
    }

    [Fact]
    public async Task Detector_PrioritizesCppForUnreal()
    {
        await File.WriteAllTextAsync(Path.Combine(_root,"Sample.uproject"),"{}");
        await File.WriteAllTextAsync(Path.Combine(_root,"Sample.cpp"),"int main(){return 0;}");
        await File.WriteAllTextAsync(Path.Combine(_root,"Tool.py"),"print('tool')");

        var profile=await new WorkspaceTechnologyDetector().DetectAsync(_root,CancellationToken.None);

        profile.PrimaryLanguage.Should().Be(LanguageId.Cpp);
        profile.GameEngines.Should().Contain("unreal");
    }

    [Fact]
    public void Sarif_IsNormalizedToCommonFinding()
    {
        const string sarif="""
        {"runs":[{"tool":{"driver":{"name":"ExampleAnalyzer"}},"results":[{"ruleId":"RULE001","level":"error","message":{"text":"unsafe call"},"locations":[{"physicalLocation":{"artifactLocation":{"uri":"src/app.ts"},"region":{"startLine":12,"startColumn":3}}}]}]}]}
        """;

        var findings=new DiagnosticNormalizer().Normalize(LanguageId.TypeScript,new DiagnosticDocument("fallback","sarif",sarif));

        findings.Should().ContainSingle();
        var finding=findings[0];
        finding.Tool.Should().Be("ExampleAnalyzer");
        finding.RuleId.Should().Be("RULE001");
        finding.Severity.Should().Be(Severity.Critical);
        finding.FilePath.Should().Be("src/app.ts");
        finding.Line.Should().Be(12);
    }

    [Fact]
    public void JUnitFailure_IsNormalizedWithoutLanguageSpecificParser()
    {
        const string junit="""
        <testsuite tests="1" failures="1"><testcase classname="Game.SaveTests" name="RoundTrip" file="tests/save.cs" line="44"><failure type="AssertionError" message="save mismatch">expected A but got B</failure></testcase></testsuite>
        """;

        var findings=new DiagnosticNormalizer().Normalize(new LanguageId("custom-language"),new DiagnosticDocument("custom-test","junit",junit));

        findings.Should().ContainSingle();
        findings[0].Category.Should().Be("test");
        findings[0].RuleId.Should().Be("junit");
        findings[0].FilePath.Should().Be("tests/save.cs");
        findings[0].Line.Should().Be(44);
    }

    [Fact]
    public void Registry_UsesUniversalAdapterForUnknownLanguage()
    {
        var normalizer=new DiagnosticNormalizer();
        var executor=new ProcessExecutor();
        var fallback=new UniversalFallbackQaAdapter(executor,normalizer);
        var registry=new QaAdapterRegistry(new IQaAdapter[]{fallback});

        registry.TryResolve(new LanguageId("zig"),out var adapter).Should().BeTrue();
        adapter.Should().BeSameAs(fallback);
    }

    public void Dispose()
    {
        try{if(Directory.Exists(_root))Directory.Delete(_root,true);}catch{}
    }
}
