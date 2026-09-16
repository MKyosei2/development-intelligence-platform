// Purpose: Verifies I nt el li ge nc eE xe cu ti on Sa fe ty Te st s behavior.
using Dip.Infrastructure.Intelligence;

namespace Dip.Infrastructure.Tests.Intelligence;

public sealed class IntelligenceExecutionSafetyTests
{
    [Fact]
    public void ResolveWorkspaceRelativePath_RejectsTraversal()
    {
        var safety = new IntelligenceExecutionSafety();
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                safety.ResolveWorkspaceRelativePath(root, Path.Combine("..", "outside.txt")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ResolveAllowedCommand_AllowsKnownDevelopmentTool()
    {
        var safety = new IntelligenceExecutionSafety();
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Equal("dotnet", safety.ResolveAllowedCommand(root, "dotnet"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ResolveAllowedCommand_RejectsUnlistedHostCommand()
    {
        var safety = new IntelligenceExecutionSafety();
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                safety.ResolveAllowedCommand(root, "cmd"));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
