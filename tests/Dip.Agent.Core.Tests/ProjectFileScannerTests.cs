// Purpose: Verifies P ro je ct Fi le Sc an ne rT es ts behavior.
using Dip.Agent.Core;
using FluentAssertions;

namespace Dip.Agent.Core.Tests;

public sealed class ProjectFileScannerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dip-agent-test-" + Guid.NewGuid().ToString("N"));

    public ProjectFileScannerTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Scan_detects_create_modify_rename_and_delete_without_git()
    {
        var scanner = new ProjectFileScanner(Options());
        var file = Path.Combine(_root, "character.ma");
        await File.WriteAllTextAsync(file, "v1");

        var first = await scanner.ScanAsync(new Dictionary<string, FileSnapshot>(), CancellationToken.None);
        first.Observations.Should().ContainSingle(x => x.Kind == FileObservationKind.Created && x.RelativePath == "character.ma");

        await Task.Delay(20);
        await File.WriteAllTextAsync(file, "v2-longer");
        var second = await scanner.ScanAsync(first.Snapshot, CancellationToken.None);
        second.Observations.Should().ContainSingle(x => x.Kind == FileObservationKind.Modified);

        var renamed = Path.Combine(_root, "hero.ma");
        File.Move(file, renamed);
        var third = await scanner.ScanAsync(second.Snapshot, CancellationToken.None);
        third.Observations.Should().ContainSingle(x => x.Kind == FileObservationKind.Renamed && x.PreviousRelativePath == "character.ma");

        File.Delete(renamed);
        var fourth = await scanner.ScanAsync(third.Snapshot, CancellationToken.None);
        fourth.Observations.Should().ContainSingle(x => x.Kind == FileObservationKind.Deleted);
    }

    [Fact]
    public async Task Scan_excludes_git_and_build_directories()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        Directory.CreateDirectory(Path.Combine(_root, "bin"));
        await File.WriteAllTextAsync(Path.Combine(_root, ".git", "secret"), "x");
        await File.WriteAllTextAsync(Path.Combine(_root, "bin", "generated.dll"), "x");
        await File.WriteAllTextAsync(Path.Combine(_root, "scene.mb"), "x");

        var scan = await new ProjectFileScanner(Options()).ScanAsync(
            new Dictionary<string, FileSnapshot>(), CancellationToken.None);
        scan.Observations.Should().ContainSingle(x => x.RelativePath == "scene.mb");
    }

    private AgentOptions Options() => new(
        new Uri("http://127.0.0.1:5080/"), Guid.NewGuid(), "agent-test", "root", _root,
        Path.Combine(_root, ".state", "agent.db"), null, TimeSpan.FromSeconds(5),
        AgentOptions.DefaultExcludedDirectories);

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }
}
