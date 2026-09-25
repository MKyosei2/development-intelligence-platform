// Purpose: Implements F uz zR un ne r infrastructure behavior for i nt el li ge nc e.
using System.Diagnostics;
using System.Text.Json;
using Dip.Application.Features.Intelligence;
using Dip.Application.Features.QualityAutomation;
namespace Dip.Infrastructure.Intelligence;
public sealed class FuzzRunner : IFuzzRunner
{
    readonly IQaWorkspaceResolver _workspace; public FuzzRunner(IQaWorkspaceResolver workspace) { _workspace = workspace; }
    public async Task<FuzzResult> RunAsync(FuzzRequest r, string artifactRoot, CancellationToken ct)
    {
        var ws = _workspace.Resolve(r.WorkspaceRelativePath);
        if (Path.IsPathRooted(r.Executable)) throw new InvalidOperationException("Executable must be workspace-relative.");
        var exe = Path.GetFullPath(Path.Combine(ws, r.Executable));
        var prefix = ws.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!exe.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Executable escapes workspace.");
        var rng = new Random(7919);
        int crashes = 0, timeouts = 0, nonzero = 0;
        var cases = Math.Clamp(r.Cases, 1, 500);
        for (int i = 0; i < cases; i++)
        {
            var payload = Convert.ToBase64String(Enumerable.Range(0, rng.Next(0, 256)).Select(_ => (byte)rng.Next(0, 256)).ToArray());
            var psi = new ProcessStartInfo(exe) { WorkingDirectory = ws, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var a in r.BaseArguments) psi.ArgumentList.Add(a);
            psi.ArgumentList.Add(payload);
            psi.Environment["DIP_FUZZ_CASE"] = i.ToString();
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Target unavailable");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(r.TimeoutSeconds, 1, 300)));
            try
            {
                await p.WaitForExitAsync(timeout.Token);
                if (p.ExitCode != 0)
                {
                    nonzero++;
                    if (p.ExitCode < 0 || p.ExitCode >= 128) crashes++;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                timeouts++;
                try
                {
                    p.Kill(true);
                }
                catch {
                    // Intentionally ignored to preserve the existing best-effort or fallback behavior.
                }
            }
        }
        var json = JsonSerializer.Serialize(new { cases, crashes, timeouts, nonzero, note = "Fuzz input is appended only to the explicitly configured local target; no network target discovery is performed." });
        Directory.CreateDirectory(artifactRoot);
        var path = Path.Combine(artifactRoot, "fuzz.json");
        await File.WriteAllTextAsync(path, json, ct);
        return new(cases, crashes, timeouts, nonzero, path, json);
    }
}
