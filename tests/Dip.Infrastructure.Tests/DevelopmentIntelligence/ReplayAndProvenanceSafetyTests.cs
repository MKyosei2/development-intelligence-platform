// Purpose: Verifies expected behavior and invariants for the surrounding feature.
using Dip.Application.Abstractions.Replay;
using Dip.Infrastructure.Provenance;
using Dip.Infrastructure.QualityAutomation;
using Dip.Infrastructure.Replay;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dip.Infrastructure.Tests.DevelopmentIntelligence;

public sealed class ReplayAndProvenanceSafetyTests
{
    [Fact]
    public async Task ReplayExecutor_RejectsExecutableOutsideConfiguredWorkspace()
    {
        var workspace = Path.Combine(Path.GetTempPath(), "dip-replay-workspace-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(workspace);
        try
        {
            var executor = new GameReplayExecutor(new ProcessExecutor(), Options.Create(new GameReplayOptions { WorkspaceRoot = workspace, AllowPathLookup = false }));
            var outside = Path.GetFullPath(Path.Combine(workspace, "..", "outside.exe"));
            var request = new ReplayExecutionRequest(outside, "", workspace, "scene", "input", 1, 1, "abc", "build", "hardware");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => executor.ExecuteAsync(request, CancellationToken.None));
        }
        finally { Directory.Delete(workspace, true); }
    }

    [Fact]
    public async Task ProvenanceCollector_RejectsRepositoryOutsideAllowedRoot()
    {
        var allowed = Path.Combine(Path.GetTempPath(), "dip-provenance-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(allowed);
        try
        {
            var collector = new ReleaseProvenanceCollector(Options.Create(new ReleaseProvenanceOptions { AllowedRoot = allowed }));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => collector.CollectAsync(Path.GetTempPath(), CancellationToken.None));
        }
        finally { Directory.Delete(allowed, true); }
    }
}
