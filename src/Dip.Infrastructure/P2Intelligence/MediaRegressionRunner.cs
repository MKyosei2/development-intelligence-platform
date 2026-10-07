// Purpose: Runs unified visual/video/audio regression analysis using the durable Visual QA comparator and media-specific temporal alignment.
using System.Globalization;
using System.Text.Json;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Application.Features.QualityAutomation.VisualQa;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

public sealed class MediaRegressionRunner(
    ProcessExecutor executor,
    IQaWorkspaceResolver workspaceResolver) : IMediaRegressionRunner
{
    public async Task<MediaRegressionResult> AnalyzeAsync(MediaRegressionCommand command, CancellationToken ct)
    {
        var workspace = workspaceResolver.Resolve(command.WorkspaceRelativePath);
        var baseline = SafePath(workspace, command.BaselinePath);
        var current = SafePath(workspace, command.CurrentPath);
        if (!File.Exists(baseline) || !File.Exists(current))
            return Failure(command.Type, "One or both media inputs do not exist.", "");

        var ffmpeg = ProcessExecutor.FindExecutable("ffmpeg");
        var ffprobe = ProcessExecutor.FindExecutable("ffprobe");
        if (ffmpeg is null || ffprobe is null)
            return Failure(command.Type, "ffmpeg and ffprobe are required for P2 media regression.", "");

        var root = ArtifactRoot(command.ProjectId, command.Type.ToString().ToLowerInvariant());
        return command.Type switch
        {
            MediaRegressionType.Visual => await AnalyzeVisualAsync(command, ffmpeg, ffprobe, baseline, current, root, ct),
            MediaRegressionType.Video => await AnalyzeVideoAsync(command, ffmpeg, ffprobe, baseline, current, root, ct),
            MediaRegressionType.Audio => await AnalyzeAudioAsync(command, ffmpeg, baseline, current, root, ct),
            _ => Failure(command.Type, "Unsupported media regression type.", RelativeArtifact(root))
        };
    }

    private async Task<MediaRegressionResult> AnalyzeVisualAsync(
        MediaRegressionCommand command,
        string ffmpeg,
        string ffprobe,
        string baseline,
        string current,
        string root,
        CancellationToken ct)
    {
        var sizeA = await ProbeSizeAsync(ffprobe, baseline, root, ct);
        var sizeB = await ProbeSizeAsync(ffprobe, current, root, ct);
        if (sizeA is null || sizeB is null)
            return Failure(command.Type, "Could not read image dimensions.", RelativeArtifact(root));
        if (sizeA != sizeB)
        {
            var detail = JsonSerializer.Serialize(new
            {
                baseline = sizeA,
                current = sizeB,
                comparator = "visual-qa-portable-png-v2",
                reason = "dimension-mismatch"
            });
            return Build(command, 100m, 99m, "Image dimensions differ.", RelativeArtifact(root), detail, "0s");
        }

        var normalizedBaseline = Path.Combine(root, "baseline.normalized.png");
        var normalizedCurrent = Path.Combine(root, "current.normalized.png");
        if (!await DecodeFramePngAsync(ffmpeg, baseline, normalizedBaseline, null, root, ct)
            || !await DecodeFramePngAsync(ffmpeg, current, normalizedCurrent, null, root, ct))
        {
            return Failure(command.Type, "Could not normalize one or both images to PNG.", RelativeArtifact(root));
        }

        var computation = await VisualQaService.CompareAutonomousPngAsync(
            normalizedBaseline,
            normalizedCurrent,
            Array.Empty<VisualMaskRegion>(),
            ct);
        if (!computation.Compatible)
            return Build(command, 100m, 99m, computation.IncompatibilityReason ?? "Visual inputs are incompatible.", RelativeArtifact(root),
                JsonSerializer.Serialize(new { comparator = "visual-qa-portable-png-v2", computation }), "0s");

        string? diffArtifact = null;
        if (computation.DiffPng is { Length: > 0 })
        {
            var diffPath = Path.Combine(root, "difference.png");
            await File.WriteAllBytesAsync(diffPath, computation.DiffPng, ct);
            diffArtifact = RelativeArtifact(diffPath);
        }

        var difference = VisualQaService.AutonomousDifferencePercent(computation);
        var comparisonDetail = JsonSerializer.Serialize(new
        {
            comparator = "visual-qa-portable-png-tiled-ssim-v2",
            computation.Width,
            computation.Height,
            computation.ChangedPixelCount,
            computation.EvaluatedPixelCount,
            computation.ChangedPixelPercent,
            computation.Ssim,
            differencePercent = difference,
            diffArtifact
        });
        return Build(
            command,
            difference,
            99m,
            $"{computation.ChangedPixelPercent:0.####}% of pixels changed; tiled SSIM {computation.Ssim:0.######}.",
            RelativeArtifact(root),
            comparisonDetail,
            difference >= command.WarningThresholdPercent ? "0s" : null);
    }

    private async Task<MediaRegressionResult> AnalyzeVideoAsync(
        MediaRegressionCommand command,
        string ffmpeg,
        string ffprobe,
        string baseline,
        string current,
        string root,
        CancellationToken ct)
    {
        var durationA = await ProbeDurationAsync(ffprobe, baseline, root, ct);
        var durationB = await ProbeDurationAsync(ffprobe, current, root, ct);
        var sizeA = await ProbeSizeAsync(ffprobe, baseline, root, ct);
        var sizeB = await ProbeSizeAsync(ffprobe, current, root, ct);
        if (durationA is null || durationB is null || sizeA is null || sizeB is null)
            return Failure(command.Type, "Could not probe video metadata.", RelativeArtifact(root));
        if (sizeA != sizeB)
        {
            var detail = JsonSerializer.Serialize(new
            {
                baseline = sizeA,
                current = sizeB,
                durationA,
                durationB,
                reason = "dimension-mismatch"
            });
            return Build(command, 100m, 96m, "Video dimensions differ.", RelativeArtifact(root), detail, "0s");
        }

        var sampleFps = Math.Clamp(command.VideoSampleFps, 1, 10);
        var interval = 1d / sampleFps;
        var duration = Math.Min(durationA.Value, durationB.Value);
        var frameCount = Math.Clamp((int)Math.Ceiling(duration * sampleFps), 1, 1200);
        var temporalRadius = Math.Min(0.250d, interval / 2d);
        var offsets = temporalRadius <= .001d
            ? new[] { 0d }
            : new[] { -temporalRadius, 0d, temporalRadius };
        var frameDir = Path.Combine(root, "frames");
        Directory.CreateDirectory(frameDir);
        var samples = new List<VideoSampleMetric>(frameCount);
        string? first = null;
        byte[]? worstDiff = null;
        decimal worstDifference = -1m;

        for (var i = 0; i < frameCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var at = Math.Min(duration, i * interval);
            var baselinePng = Path.Combine(frameDir, $"baseline-{i:D5}.png");
            if (!await DecodeFramePngAsync(ffmpeg, baseline, baselinePng, at, root, ct))
                continue;

            VisualComparisonComputation? best = null;
            double bestOffset = 0d;
            decimal bestDifference = 100m;
            foreach (var offset in offsets)
            {
                var candidateAt = Math.Clamp(at + offset, 0d, durationB.Value);
                var currentPng = Path.Combine(frameDir, $"current-{i:D5}-{OffsetName(offset)}.png");
                try
                {
                    if (!await DecodeFramePngAsync(ffmpeg, current, currentPng, candidateAt, root, ct))
                        continue;
                    var computation = await VisualQaService.CompareAutonomousPngAsync(
                        baselinePng,
                        currentPng,
                        Array.Empty<VisualMaskRegion>(),
                        ct);
                    if (!computation.Compatible)
                        continue;
                    var difference = VisualQaService.AutonomousDifferencePercent(computation);
                    if (best is null || difference < bestDifference)
                    {
                        best = computation;
                        bestDifference = difference;
                        bestOffset = offset;
                    }
                }
                finally
                {
                    DeleteBestEffort(currentPng);
                }
            }

            DeleteBestEffort(baselinePng);
            if (best is null)
                continue;

            samples.Add(new(
                at,
                bestOffset,
                bestDifference,
                best.ChangedPixelPercent,
                best.Ssim));
            if (first is null && bestDifference >= command.WarningThresholdPercent)
                first = $"{at:0.00}s";
            if (bestDifference > worstDifference && best.DiffPng is { Length: > 0 })
            {
                worstDifference = bestDifference;
                worstDiff = best.DiffPng;
            }
        }

        if (samples.Count == 0)
            return Failure(command.Type, "No video frames could be compared.", RelativeArtifact(root));

        var ordered = samples.Select(x => x.DifferencePercent).OrderBy(x => x).ToArray();
        var average = decimal.Round(ordered.Average(), 3);
        var p95 = Percentile(ordered, .95d);
        var maximum = ordered[^1];
        var averageShiftMs = decimal.Round((decimal)samples.Average(x => Math.Abs(x.TemporalOffsetSeconds)) * 1000m, 2);
        var durationDelta = Math.Abs(durationA.Value - durationB.Value);
        var durationPenalty = (decimal)Math.Min(20d, durationDelta * 2d);
        var combined = Math.Min(100m, Math.Max(average, p95) + durationPenalty);

        string? worstDiffArtifact = null;
        if (worstDiff is { Length: > 0 })
        {
            var path = Path.Combine(root, "worst-frame-difference.png");
            await File.WriteAllBytesAsync(path, worstDiff, ct);
            worstDiffArtifact = RelativeArtifact(path);
        }

        var comparisonDetail = JsonSerializer.Serialize(new
        {
            comparator = "visual-qa-portable-png-tiled-ssim-v2",
            temporalAlignment = new { radiusSeconds = temporalRadius, averageAbsoluteShiftMs = averageShiftMs },
            samples = samples.Count,
            averageDifferencePercent = average,
            p95DifferencePercent = p95,
            maxDifferencePercent = maximum,
            durationA,
            durationB,
            durationDeltaSeconds = durationDelta,
            durationPenaltyPercent = durationPenalty,
            firstDifference = first,
            worstDiffArtifact,
            sampleMetrics = samples.Take(500).ToArray()
        });
        return Build(
            command,
            combined,
            Math.Min(99m, 80m + samples.Count / 20m),
            $"Compared {samples.Count} temporally aligned frame(s); p95 {p95:0.###}%, max {maximum:0.###}%, average shift {averageShiftMs:0.##} ms.",
            RelativeArtifact(root),
            comparisonDetail,
            first);
    }

    private async Task<MediaRegressionResult> AnalyzeAudioAsync(
        MediaRegressionCommand command,
        string ffmpeg,
        string baseline,
        string current,
        string root,
        CancellationToken ct)
    {
        var a = Path.Combine(root, "baseline.s16le");
        var b = Path.Combine(root, "current.s16le");
        var okA = await DecodeAudioAsync(ffmpeg, baseline, a, root, ct);
        var okB = await DecodeAudioAsync(ffmpeg, current, b, root, ct);
        if (!okA || !okB)
            return Failure(command.Type, "Could not decode one or both audio inputs.", RelativeArtifact(root));

        var aa = ToSamples(await File.ReadAllBytesAsync(a, ct));
        var bb = ToSamples(await File.ReadAllBytesAsync(b, ct));
        var lag = FindBestLag(aa, bb, 4000, 8, 160000);
        var metrics = CompareAudioAligned(aa, bb, lag);
        var detail = JsonSerializer.Serialize(new
        {
            sampleRate = 16000,
            alignmentLagSamples = lag,
            alignmentLagMs = decimal.Round(lag / 16m, 3),
            metrics
        });
        return Build(
            command,
            metrics.DifferencePercent,
            98m,
            $"Aligned audio by {lag / 16m:0.###} ms; correlation {metrics.Correlation:0.####}, RMS {metrics.BaselineRms:0.####}->{metrics.CurrentRms:0.####}, silence delta {metrics.SilenceDeltaPercent:0.##}%.",
            RelativeArtifact(root),
            detail,
            null);
    }

    private MediaRegressionResult Build(
        MediaRegressionCommand command,
        decimal difference,
        decimal confidence,
        string summary,
        string artifact,
        string detail,
        string? first)
    {
        var status = difference >= command.FailureThresholdPercent
            ? "Failed"
            : difference >= command.WarningThresholdPercent
                ? "Warning"
                : "Passed";
        return new(status, command.Type, decimal.Round(Math.Clamp(difference, 0m, 100m), 3), confidence, first, summary, artifact, detail);
    }

    private static MediaRegressionResult Failure(MediaRegressionType type, string summary, string artifact)
        => new("InfrastructureFailure", type, 0m, 0m, null, summary, artifact, JsonSerializer.Serialize(new { error = summary }));

    private async Task<(int Width, int Height)?> ProbeSizeAsync(string ffprobe, string file, string root, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var r = await executor.RunAsync(
            ffprobe,
            ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "csv=s=x:p=0", file],
            root,
            null,
            TimeSpan.FromSeconds(20),
            Path.Combine(root, $"probe-{id}.out"),
            Path.Combine(root, $"probe-{id}.err"),
            ct);
        var parts = r.StandardOutput.Trim().Split('x');
        return r.ExitCode == 0 && parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h)
            ? (w, h)
            : null;
    }

    private async Task<double?> ProbeDurationAsync(string ffprobe, string file, string root, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var r = await executor.RunAsync(
            ffprobe,
            ["-v", "error", "-show_entries", "format=duration", "-of", "default=noprint_wrappers=1:nokey=1", file],
            root,
            null,
            TimeSpan.FromSeconds(20),
            Path.Combine(root, $"duration-{id}.out"),
            Path.Combine(root, $"duration-{id}.err"),
            ct);
        return r.ExitCode == 0
               && double.TryParse(r.StandardOutput.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : null;
    }

    private async Task<bool> DecodeFramePngAsync(
        string ffmpeg,
        string input,
        string output,
        double? at,
        string root,
        CancellationToken ct)
    {
        var args = new List<string> { "-y", "-v", "error" };
        if (at is not null)
        {
            args.Add("-ss");
            args.Add(at.Value.ToString("0.###", CultureInfo.InvariantCulture));
        }
        args.AddRange(["-i", input, "-frames:v", "1", "-pix_fmt", "rgba", output]);
        var id = Guid.NewGuid().ToString("N");
        var r = await executor.RunAsync(
            ffmpeg,
            args,
            root,
            null,
            TimeSpan.FromSeconds(30),
            Path.Combine(root, $"ffmpeg-{id}.out"),
            Path.Combine(root, $"ffmpeg-{id}.err"),
            ct);
        return r.ExitCode == 0 && File.Exists(output) && new FileInfo(output).Length > 0;
    }

    private async Task<bool> DecodeAudioAsync(string ffmpeg, string input, string output, string root, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var r = await executor.RunAsync(
            ffmpeg,
            ["-y", "-v", "error", "-i", input, "-vn", "-ac", "1", "-ar", "16000", "-f", "s16le", output],
            root,
            null,
            TimeSpan.FromMinutes(2),
            Path.Combine(root, $"audio-{id}.out"),
            Path.Combine(root, $"audio-{id}.err"),
            ct);
        return r.ExitCode == 0 && File.Exists(output) && new FileInfo(output).Length > 0;
    }

    private sealed record VideoSampleMetric(
        double TimeSeconds,
        double TemporalOffsetSeconds,
        decimal DifferencePercent,
        decimal ChangedPixelPercent,
        decimal Ssim);

    private sealed record AudioMetrics(
        decimal DifferencePercent,
        decimal BaselineRms,
        decimal CurrentRms,
        decimal SilenceDeltaPercent,
        decimal ClipDeltaPercent,
        decimal PeakDeltaPercent,
        decimal ZeroCrossingDeltaPercent,
        decimal Correlation);

    private static AudioMetrics CompareAudioAligned(short[] a, short[] b, int lag)
    {
        var startA = Math.Max(0, -lag);
        var startB = Math.Max(0, lag);
        var n = Math.Min(a.Length - startA, b.Length - startB);
        if (n <= 0)
            return new(100m, 0m, 0m, 100m, 0m, 100m, 100m, 0m);

        double sqA = 0d, sqB = 0d, absDiff = 0d, peakA = 0d, peakB = 0d;
        double dot = 0d, normA = 0d, normB = 0d;
        var silenceA = 0;
        var silenceB = 0;
        var clipA = 0;
        var clipB = 0;
        var zcA = 0;
        var zcB = 0;
        double previousA = 0d, previousB = 0d;
        for (var i = 0; i < n; i++)
        {
            var aa = a[startA + i] / 32768d;
            var bb = b[startB + i] / 32768d;
            sqA += aa * aa;
            sqB += bb * bb;
            absDiff += Math.Abs(aa - bb);
            peakA = Math.Max(peakA, Math.Abs(aa));
            peakB = Math.Max(peakB, Math.Abs(bb));
            dot += aa * bb;
            normA += aa * aa;
            normB += bb * bb;
            if (Math.Abs(aa) < .005d) silenceA++;
            if (Math.Abs(bb) < .005d) silenceB++;
            if (Math.Abs(aa) > .99d) clipA++;
            if (Math.Abs(bb) > .99d) clipB++;
            if (i > 0 && Math.Sign(aa) != Math.Sign(previousA)) zcA++;
            if (i > 0 && Math.Sign(bb) != Math.Sign(previousB)) zcB++;
            previousA = aa;
            previousB = bb;
        }

        var rmsA = Math.Sqrt(sqA / n);
        var rmsB = Math.Sqrt(sqB / n);
        var correlation = normA <= double.Epsilon || normB <= double.Epsilon
            ? (rmsA <= .000001d && rmsB <= .000001d ? 1d : 0d)
            : Math.Clamp(dot / Math.Sqrt(normA * normB), -1d, 1d);
        var maePercent = (decimal)Math.Min(1d, absDiff / n) * 100m;
        var rmsDelta = (decimal)(Math.Abs(rmsA - rmsB) / Math.Max(.0001d, Math.Max(rmsA, rmsB))) * 100m;
        var silence = Math.Abs((decimal)silenceA / n * 100m - (decimal)silenceB / n * 100m);
        var clip = Math.Abs((decimal)clipA / n * 100m - (decimal)clipB / n * 100m);
        var peak = (decimal)Math.Abs(peakA - peakB) * 100m;
        var zc = Math.Abs((decimal)zcA / Math.Max(1, n - 1) * 100m - (decimal)zcB / Math.Max(1, n - 1) * 100m);
        var correlationPenalty = (decimal)Math.Clamp((1d - correlation) * 50d, 0d, 100d);
        var lengthPenalty = Math.Abs(a.Length - b.Length) > n * .02 ? 10m : 0m;
        var difference = Math.Min(100m,
            maePercent * .40m
            + rmsDelta * .15m
            + silence * .10m
            + clip * .10m
            + peak * .05m
            + zc * .05m
            + correlationPenalty * .15m
            + lengthPenalty);

        return new(
            decimal.Round(difference, 3),
            decimal.Round((decimal)rmsA, 5),
            decimal.Round((decimal)rmsB, 5),
            decimal.Round(silence, 3),
            decimal.Round(clip, 3),
            decimal.Round(peak, 3),
            decimal.Round(zc, 3),
            decimal.Round((decimal)correlation, 5));
    }

    private static int FindBestLag(short[] a, short[] b, int maxLagSamples, int stride, int maxWindowSamples)
    {
        if (a.Length == 0 || b.Length == 0)
            return 0;
        var maxLag = Math.Min(maxLagSamples, Math.Max(0, Math.Min(a.Length, b.Length) / 4));
        var bestLag = 0;
        var best = double.NegativeInfinity;
        for (var lag = -maxLag; lag <= maxLag; lag += Math.Max(1, stride))
        {
            var startA = Math.Max(0, -lag);
            var startB = Math.Max(0, lag);
            var n = Math.Min(Math.Min(a.Length - startA, b.Length - startB), maxWindowSamples);
            if (n < 512)
                continue;
            double dot = 0d, normA = 0d, normB = 0d;
            for (var i = 0; i < n; i += Math.Max(1, stride))
            {
                var aa = a[startA + i] / 32768d;
                var bb = b[startB + i] / 32768d;
                dot += aa * bb;
                normA += aa * aa;
                normB += bb * bb;
            }
            if (normA <= double.Epsilon || normB <= double.Epsilon)
                continue;
            var score = dot / Math.Sqrt(normA * normB);
            if (score > best)
            {
                best = score;
                bestLag = lag;
            }
        }
        return bestLag;
    }

    private static short[] ToSamples(byte[] bytes)
    {
        var samples = new short[bytes.Length / 2];
        for (var i = 0; i < samples.Length; i++)
            samples[i] = BitConverter.ToInt16(bytes, i * 2);
        return samples;
    }

    private static decimal Percentile(decimal[] sorted, double percentile)
    {
        if (sorted.Length == 0) return 0m;
        if (sorted.Length == 1) return sorted[0];
        var position = Math.Clamp(percentile, 0d, 1d) * (sorted.Length - 1);
        var lo = (int)Math.Floor(position);
        var hi = (int)Math.Ceiling(position);
        if (lo == hi) return sorted[lo];
        var fraction = (decimal)(position - lo);
        return decimal.Round(sorted[lo] + (sorted[hi] - sorted[lo]) * fraction, 3);
    }

    private static string OffsetName(double value)
        => value.ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture).Replace('.', '_').Replace('+', 'p').Replace('-', 'm');

    private static void DeleteBestEffort(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            System.Diagnostics.Trace.TraceWarning(
                "Media regression temporary file cleanup failed for '{0}': {1}: {2}",
                path,
                ex.GetType().Name,
                ex.Message);
        }
    }

    private static string SafePath(string root, string relative)
    {
        if (Path.IsPathRooted(relative))
            throw new InvalidOperationException("Media path must be workspace-relative.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var resolvedRoot = Path.GetFullPath(root);
        var prefix = resolvedRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.Equals(resolvedRoot, comparison) && !full.StartsWith(prefix, comparison))
            throw new InvalidOperationException("Media path escapes the configured workspace.");
        return full;
    }

    private static string ArtifactRoot(Guid projectId, string category)
    {
        var root = Path.GetFullPath(Path.Combine(
            "artifacts",
            "p2",
            projectId.ToString("N"),
            "media",
            category,
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string RelativeArtifact(string full)
        => Path.GetRelativePath(Path.GetFullPath("."), full).Replace('\\', '/');
}
