// Purpose: Implements Media Regression Runner infrastructure responsibilities for intelligence.
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Dip.Application.Features.Intelligence;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Infrastructure.Intelligence;

public sealed class MediaRegressionRunner : IMediaRegressionRunner
{
    private readonly IQaWorkspaceResolver _workspace;
    public MediaRegressionRunner(IQaWorkspaceResolver workspace) => _workspace = workspace;

    public async Task<MediaRegressionResult> CompareAsync(VisualRegressionRequest request, string artifactRoot, string kind, CancellationToken ct)
    {
        var workspace = _workspace.Resolve(request.WorkspaceRelativePath);
        var baseline = Resolve(workspace, request.BaselinePath);
        var current = Resolve(workspace, request.CurrentPath);
        if (!File.Exists(baseline) || !File.Exists(current)) throw new FileNotFoundException("Baseline/current media is missing.");

        var output = Path.Combine(artifactRoot, $"{kind}-diff.json");
        var helper = Path.GetFullPath(Path.Combine("tools", "p2_media_compare.py"));
        var python = FindPython();
        if (python is not null && File.Exists(helper))
        {
            var psi = new ProcessStartInfo(python)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add(helper);
            psi.ArgumentList.Add("--kind"); psi.ArgumentList.Add(NormalizeKind(kind));
            psi.ArgumentList.Add("--baseline"); psi.ArgumentList.Add(baseline);
            psi.ArgumentList.Add("--current"); psi.ArgumentList.Add(current);
            psi.ArgumentList.Add("--output"); psi.ArgumentList.Add(output);
            psi.ArgumentList.Add("--threshold"); psi.ArgumentList.Add(request.ThresholdPercent.ToString(System.Globalization.CultureInfo.InvariantCulture));
            using var p = Process.Start(psi) ?? throw new InvalidOperationException("Could not launch media comparison helper.");
            var stdout = await p.StandardOutput.ReadToEndAsync(ct);
            var stderr = await p.StandardError.ReadToEndAsync(ct);
            await p.WaitForExitAsync(ct);
            if (p.ExitCode == 0 && File.Exists(output))
            {
                var json = await File.ReadAllTextAsync(output, ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var passed = root.GetProperty("passed").GetBoolean();
                var difference = root.GetProperty("differencePercent").GetDecimal();
                var diffArtifact = root.TryGetProperty("diffArtifact", out var da) && da.ValueKind == JsonValueKind.String ? da.GetString() ?? output : output;
                return new MediaRegressionResult(passed, difference, NormalizeKind(kind), diffArtifact, json);
            }
            await File.WriteAllTextAsync(Path.Combine(artifactRoot, "media-helper-error.txt"), stdout + Environment.NewLine + stderr, ct);
        }

        var a = await File.ReadAllBytesAsync(baseline, ct);
        var b = await File.ReadAllBytesAsync(current, ct);
        var max = Math.Max(a.Length, b.Length);
        long diff = Math.Abs(a.Length - b.Length);
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++) if (a[i] != b[i]) diff++;
        var pct = max == 0 ? 0 : decimal.Round(100m * diff / max, 4);
        var passedFallback = pct <= Math.Max(0, request.ThresholdPercent);
        var payload = JsonSerializer.Serialize(new
        {
            kind = NormalizeKind(kind),
            differencePercent = pct,
            thresholdPercent = request.ThresholdPercent,
            passed = passedFallback,
            comparison = new { method = "binary-fallback", precision = "low", reason = "Python media helper unavailable or failed" },
            baselineHash = Convert.ToHexString(SHA256.HashData(a)),
            currentHash = Convert.ToHexString(SHA256.HashData(b))
        });
        await File.WriteAllTextAsync(output, payload, ct);
        return new MediaRegressionResult(passedFallback, pct, NormalizeKind(kind), output, payload);
    }

    private static string Resolve(string workspace, string value)
    {
        if (Path.IsPathRooted(value)) throw new InvalidOperationException("Media paths must be workspace-relative.");
        var x = Path.GetFullPath(Path.Combine(workspace, value));
        var prefix = workspace.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Media path escapes workspace.");
        return x;
    }

    private static string NormalizeKind(string kind) => kind.ToLowerInvariant() switch { "video" => "video", "audio" => "audio", _ => "visual" };
    private static string? FindPython()
    {
        foreach (var name in OperatingSystem.IsWindows() ? new[] { "python", "py" } : new[] { "python3", "python" })
        {
            try { using var p = Process.Start(new ProcessStartInfo(name, "--version") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
            if (p is not null) { p.WaitForExit(1500);
            if (p.ExitCode == 0) return name;
             } }
            catch {
                // Intentionally ignored to preserve the existing best-effort or fallback behavior.
            }
        }
        return null;
    }
}
