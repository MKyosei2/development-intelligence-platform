using Dip.Application.Features.P2Intelligence;
using Dip.Infrastructure.P2Intelligence;
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Tests.P2Intelligence;

public sealed class TestImpactAnalyzerTests
{
    [Fact]
    public async Task AnalyzeAsync_PrioritizesTestProjectReferencingChangedProject()
    {
        var temp=Path.Combine(Path.GetTempPath(),"dip-test-impact-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            var src=Path.Combine(temp,"src","Game");
            var tests=Path.Combine(temp,"tests","Game.Tests");
            Directory.CreateDirectory(src);Directory.CreateDirectory(tests);
            await File.WriteAllTextAsync(Path.Combine(src,"Game.csproj"),"<Project Sdk=\"Microsoft.NET.Sdk\" />");
            await File.WriteAllTextAsync(Path.Combine(src,"PlayerController.cs"),"public sealed class PlayerController { }");
            await File.WriteAllTextAsync(Path.Combine(tests,"Game.Tests.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"..\\..\\src\\Game\\Game.csproj\" /></ItemGroup></Project>");
            await File.WriteAllTextAsync(Path.Combine(tests,"PlayerControllerTests.cs"),"public sealed class PlayerControllerTests { }");

            var resolver=new QaWorkspaceResolver(Options.Create(new QaExecutionOptions{WorkspaceRoot=temp,ArtifactRoot=Path.Combine(temp,"artifacts")}));
            var sut=new TestImpactAnalyzer(resolver);
            var result=await sut.AnalyzeAsync(new TestImpactCommand(
                Guid.NewGuid(),"player",".",["src/Game/PlayerController.cs"]),CancellationToken.None);

            result.Status.Should().Be("Selected");
            result.SelectedTests.Should().BeGreaterThan(0);
            result.Tests[0].TestPath.Should().Contain("PlayerControllerTests.cs");
            result.Tests[0].Score.Should().BeGreaterThanOrEqualTo(70m);
        }
        finally
        {
            try{Directory.Delete(temp,true);}catch{}
        }
    }
}
