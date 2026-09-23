// Purpose: Provides shared workspace, process, artifact, cleanup, and diagnostic helpers for P2 experiment runners.
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

internal abstract class ExperimentExecutionSupport
{
    protected readonly ProcessExecutor executor;
    protected readonly IQaWorkspaceResolver workspaceResolver;

    protected ExperimentExecutionSupport(ProcessExecutor executor, IQaWorkspaceResolver workspaceResolver)
    {
        this.executor = executor;
        this.workspaceResolver = workspaceResolver;
    }

    protected async Task<byte[]> MinimizeFailureAsync(
            string executable,
            string workspace,
            FuzzCommand command,
            byte[] original,
            string expectedFingerprint,
            bool expectCrash,
            CancellationToken ct)
    {
        var current = original.ToArray();
        var maxRuns = Math.Clamp(command.MaxMinimizationRunsPerFailure, 1, 100);
        var runs = 0;
        for (var chunk = Math.Max(1, current.Length / 2); chunk >= 1 && runs < maxRuns; chunk = Math.Max(0, chunk / 2))
        {
            var changed = true;
            while (changed && runs < maxRuns)
            {
                changed = false;
                for (var offset = 0; offset < current.Length && runs < maxRuns; offset += Math.Max(1, chunk))
                {
                    var length = Math.Min(chunk, current.Length - offset);
                    if (length <= 0 || current.Length - length <= 0)
                        continue;
                    var candidate = current[..offset].Concat(current[(offset + length)..]).ToArray();
                    var input = Path.Combine(Path.GetTempPath(), $"dip-fuzz-min-{Guid.NewGuid():N}.bin");
                    var stdout = Path.Combine(Path.GetTempPath(), $"dip-fuzz-min-{Guid.NewGuid():N}.stdout.log");
                    var stderr = Path.Combine(Path.GetTempPath(), $"dip-fuzz-min-{Guid.NewGuid():N}.stderr.log");
                    try
                    {
                        await File.WriteAllBytesAsync(input, candidate, ct);
                        var arguments = (command.Target.Arguments ?? Array.Empty<string>()).Select(a => a.Replace(command.ArgumentPlaceholder, input, StringComparison.Ordinal)).ToArray();
                        var result = await executor.RunAsync(
                            executable,
                            arguments,
                            workspace,
                            new Dictionary<string, string> { { "DIP_FUZZ", "1" }, { "DIP_FUZZ_MINIMIZE", "1" } },
                            TimeSpan.FromSeconds(Math.Clamp(command.Target.TimeoutSeconds, 1, 600)),
                            stdout,
                            stderr,
                            ct);
                        runs++;
                        var sameKind = expectCrash
                            ? !result.TimedOut && result.ExitCode is not null && result.ExitCode != 0
                            : result.TimedOut;
                        var sameFingerprint = Fingerprint(result.StandardOutput + "\n" + result.StandardError) == expectedFingerprint;
                        if (sameKind && sameFingerprint)
                        {
                            current = candidate;
                            changed = true;
                            break;
                        }
                    }
                    finally
                    {
                        DeleteBestEffort(input);
                        DeleteBestEffort(stdout);
                        DeleteBestEffort(stderr);
                    }
                }
            }
            if (chunk == 1)
                break;
        }
        return current;
    }

    protected static byte[] BuildFuzzInput(int index, int max, Random random)
    {
        var boundary = new[] { 0, 1, 2, 7, 8, 15, 16, 31, 32, 63, 64, 127, 128, 255, 256, 511, 512, 1023, 1024, 4095, 4096 };
        var size = index < boundary.Length ? Math.Min(boundary[index], max) : random.Next(0, max + 1);
        var bytes = new byte[size];
        switch (index % 8)
        {
            case 1:
                Array.Fill(bytes, (byte)0x00);
                break;
            case 2:
                Array.Fill(bytes, (byte)0xFF);
                break;
            case 3:
                Array.Fill(bytes, (byte)'A');
                break;
            case 4:
                for (var i = 0; i < bytes.Length; i++)
                    bytes[i] = (byte)(i & 0xFF);
                break;
            default:
                random.NextBytes(bytes);
                break;
        }
        return bytes;
    }

    protected static List<MutationCaseSpec> BuildAutomaticMutations(string workspace, IReadOnlyList<string>? sourceFiles, int remaining)
    {
        var result = new List<MutationCaseSpec>();
        if (remaining <= 0 || sourceFiles is null)
            return result;
        var operators = new (string Old, string New, string Name)[]
        {
                (">=", ">", "boundary-gte-to-gt"), ("<=", "<", "boundary-lte-to-lt"),
                ("==", "!=", "equality-invert"), ("!=", "==", "inequality-invert"),
                ("true", "false", "bool-true-to-false"), ("false", "true", "bool-false-to-true"),
                (" + ", " - ", "arithmetic-plus-to-minus"), (" - ", " + ", "arithmetic-minus-to-plus")
        };
        foreach (var relative in sourceFiles.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (result.Count >= remaining)
                break;
            var file = SafePath(workspace, relative);
            if (!File.Exists(file))
                continue;
            var text = File.ReadAllText(file);
            foreach (var op in operators)
            {
                var search = 0;
                while (result.Count < remaining)
                {
                    var index = text.IndexOf(op.Old, search, StringComparison.Ordinal);
                    if (index < 0)
                        break;
                    var contextStart = Math.Max(0, index - 24);
                    var contextLength = Math.Min(text.Length - contextStart, op.Old.Length + 48);
                    var context = text.Substring(contextStart, contextLength);
                    if (FindAll(text, context, 2).Count == 1)
                    {
                        var local = context.IndexOf(op.Old, StringComparison.Ordinal);
                        var replacement = context[..local] + op.New + context[(local + op.Old.Length)..];
                        result.Add(new($"auto-{op.Name}-{result.Count + 1:D3}", relative, context, replacement, op.Name));
                    }
                    search = index + op.Old.Length;
                }
            }
        }
        return result;
    }

    protected static List<int> FindAll(string text, string value, int limit)
    {
        var result = new List<int>();
        if (string.IsNullOrEmpty(value))
            return result;
        var start = 0;
        while (start <= text.Length - value.Length && result.Count < limit)
        {
            var index = text.IndexOf(value, start, StringComparison.Ordinal);
            if (index < 0)
                break;
            result.Add(index);
            start = index + Math.Max(1, value.Length);
        }
        return result;
    }

    protected static (int Line, int Column) LineColumn(string text, int offset)
    {
        var line = 1;
        var column = 1;
        for (var i = 0; i < Math.Min(offset, text.Length); i++)
        {
            if (text[i] == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
        }
        return (line, column);
    }

    protected async Task<(bool? Result, long DurationMs)> EvaluateCommitAsync(
            string git,
            string workspace,
            string root,
            string commit,
            ExperimentTarget target,
            CancellationToken ct)
    {
        var worktree = Path.Combine(root, "worktrees", SafeName(commit));
        if (Directory.Exists(worktree))
            Directory.Delete(worktree, true);
        Directory.CreateDirectory(Path.GetDirectoryName(worktree)!);
        var add = await RunGitAsync(git, workspace, root, ["worktree", "add", "--force", "--detach", worktree, commit], ct);
        if (add.ExitCode != 0)
            return (null, add.DurationMs);
        try
        {
            var executable = ResolveTargetExecutable(worktree, target.Executable);
            var directory = Path.Combine(root, "evaluations", SafeName(commit));
            Directory.CreateDirectory(directory);
            var result = await executor.RunAsync(
                executable,
                target.Arguments ?? Array.Empty<string>(),
                worktree,
                null,
                TimeSpan.FromSeconds(Math.Clamp(target.TimeoutSeconds, 1, 3600)),
                Path.Combine(directory, "stdout.log"),
                Path.Combine(directory, "stderr.log"),
                ct);
            if (!result.TimedOut && result.ExitCode is null)
                return (null, result.DurationMs);
            return (!result.TimedOut && result.ExitCode == 0, result.DurationMs);
        }
        finally
        {
            var remove = await RunGitAsync(git, workspace, root, ["worktree", "remove", "--force", worktree], CancellationToken.None);
            if (remove.ExitCode != 0)
                await File.AppendAllTextAsync(Path.Combine(root, "worktree-cleanup-warning.log"), remove.StandardError + Environment.NewLine, CancellationToken.None);
        }
    }

    protected async Task<ProcessExecutionResult> RunGitAsync(
            string git,
            string workspace,
            string root,
            IEnumerable<string> args,
            CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(root, "git");
        Directory.CreateDirectory(directory);
        return await executor.RunAsync(
            git,
            args,
            workspace,
            null,
            TimeSpan.FromMinutes(5),
            Path.Combine(directory, $"{id}.out.log"),
            Path.Combine(directory, $"{id}.err.log"),
            ct);
    }

    protected async Task PrepareIsolatedWorkspaceAsync(string source, string target, CancellationToken ct)
    {
        if (Directory.Exists(target))
            Directory.Delete(target, true);
        var git = ProcessExecutor.FindExecutable("git");
        if (git is not null && Directory.Exists(Path.Combine(source, ".git")))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var root = Path.GetDirectoryName(target)!;
            var result = await RunGitAsync(git, source, root, ["worktree", "add", "--force", "--detach", target, "HEAD"], ct);
            if (result.ExitCode == 0)
                return;
        }
        CopyDirectory(source, target);
    }

    protected static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (name is ".git" or "bin" or "obj" or ".dipqa" or "artifacts" or "node_modules" or ".vs")
                continue;
            CopyDirectory(directory, Path.Combine(target, name));
        }
    }

    protected static async Task ReadLinesAsync(
            StreamReader reader,
            StringBuilder sink,
            ConcurrentQueue<(int Second, decimal Fps, decimal? Frame)>? metrics,
            Stopwatch stopwatch,
            CancellationToken ct)
    {
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
                break;
            sink.AppendLine(line);
            if (metrics is not null && TryMetric(line, out var fps, out var frame))
                metrics.Enqueue(((int)stopwatch.Elapsed.TotalSeconds, fps, frame));
        }
    }

    protected static bool TryMetric(string line, out decimal fps, out decimal? frame)
    {
        fps = 0m;
        frame = null;
        if (!line.Contains("DIP_METRIC", StringComparison.OrdinalIgnoreCase))
            return false;
        foreach (var token in line.Split([' ', ';'], StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = token.Split('=', 2);
            if (parts.Length != 2)
                continue;
            if (parts[0].Equals("fps", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(parts[1], out var parsedFps))
                fps = parsedFps;
            if (parts[0].Equals("frame_time_ms", StringComparison.OrdinalIgnoreCase) && decimal.TryParse(parts[1], out var parsedFrame))
                frame = parsedFrame;
        }
        return fps > 0m || frame is not null;
    }

    protected static async Task TerminateProcessAsync(Process process)
    {
        if (process.HasExited)
            return;
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
            return;
        }
        catch (Win32Exception) when (process.HasExited)
        {
            return;
        }
        try
        {
            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (InvalidOperationException) when (process.HasExited)
        {
        }
    }

    protected static void DeleteBestEffort(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort cleanup: the experiment result must not be reclassified by a stale temp file.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup: the verification artifact already captures the run outcome.
        }
    }

    protected static string ResolveTargetExecutable(string workspace, string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new ArgumentException("Executable is required.");
        if (Path.IsPathRooted(executable))
            throw new InvalidOperationException("P2 target executable must be workspace-relative.");
        var resolved = SafePath(workspace, executable);
        if (!File.Exists(resolved))
            throw new FileNotFoundException("P2 target executable was not found inside the configured workspace.", resolved);
        return resolved;
    }

    protected static string SafePath(string root, string relative)
    {
        if (Path.IsPathRooted(relative))
            throw new InvalidOperationException("Path must be relative.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && !full.Equals(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path escapes the configured workspace.");
        }
        return full;
    }

    protected static string ArtifactRoot(Guid projectId, string category)
    {
        var root = Path.GetFullPath(Path.Combine(
            "artifacts",
            "p2",
            projectId.ToString("N"),
            category,
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(root);
        return root;
    }

    protected static string RelativeArtifact(string full) => Path.GetRelativePath(Path.GetFullPath("."), full).Replace('\\', '/');

    protected static string Fingerprint(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(text)))).ToLowerInvariant()[..16];

    protected static string SafeName(string value) => string.Concat(value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')).Substring(0, Math.Min(80, value.Length));
}
