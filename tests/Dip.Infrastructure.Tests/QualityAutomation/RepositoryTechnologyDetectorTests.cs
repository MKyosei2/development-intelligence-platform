// Purpose: Verifies behavior and invariants covered by R ep os it or yT ec hn ol og yD et ec to rT es ts.
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class RepositoryTechnologyDetectorTests
{
    [Fact]
    public async Task DetectAsync_FindsLanguagesBuildSystemsAndUnity()
    {
        var root=Path.Combine(Path.GetTempPath(),"dip-tech-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try
        {
            Directory.CreateDirectory(Path.Combine(root,"Assets"));Directory.CreateDirectory(Path.Combine(root,"ProjectSettings"));
            await File.WriteAllTextAsync(Path.Combine(root,"ProjectSettings","ProjectVersion.txt"),"m_EditorVersion: 6000.0");
            await File.WriteAllTextAsync(Path.Combine(root,"Assets","Game.cs"),"class Game{}");
                await File.WriteAllTextAsync(Path.Combine(root,"tool.py"),"print('ok')");
                await File.WriteAllTextAsync(Path.Combine(root,"package.json"),"{\"dependencies\":{\"react\":\"1\"}}");
            var result=await new RepositoryTechnologyDetector().DetectAsync(root,CancellationToken.None);
            result.Languages.Should().Contain(LanguageId.CSharp).And.Contain(LanguageId.Python);
                result.BuildSystems.Should().Contain("npm");
                result.GameEngines.Should().Contain("Unity");
                result.Frameworks.Should().Contain("react");
        }
        finally{try{Directory.Delete(root,true);}catch{}}
    }
}
