// Purpose: Provides safe Git process execution and shared branch-workspace validation helpers for QA isolation.
using System.Diagnostics;
using System.Text.Json;
using Dip.Application.Features.GitHubIntegration;

namespace Dip.Application.Features.QualityAutomation;

internal sealed record BranchQaGitResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

internal static class BranchQaGitSupport
{
    internal static StringComparison PathComparison =>
        OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    internal static void NeedWorkspace(string workspace)
    {
        if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(workspace))
            throw new DirectoryNotFoundException(
                "The synchronized QA workspace does not exist.");
    }

    internal static string NormalizeBranch(string? value)
    {
        var branch = (value ?? string.Empty).Trim();
        if (branch.Length is < 1 or > 200)
            throw new ArgumentException(
                "Branch must be 1-200 characters.",
                nameof(value));

        if (branch.StartsWith('-')
            || branch.Equals("HEAD", StringComparison.OrdinalIgnoreCase)
            || branch.Any(char.IsControl))
        {
            throw new ArgumentException("Unsafe Git branch name.", nameof(value));
        }

        return branch;
    }

    internal static string NormalizeCommit(string value)
    {
        var commit = value.Trim();
        if (commit.Length is < 7 or > 64 || commit.Any(x => !Uri.IsHexDigit(x)))
        {
            throw new ArgumentException(
                "CommitSha must be 7-64 hexadecimal characters.",
                nameof(value));
        }

        return commit;
    }

    internal static string NormalizeRelativeWorkspace(string value)
    {
        var text = (value ?? string.Empty)
            .Trim()
            .Replace('\\', '/')
            .TrimEnd('/');

        return string.IsNullOrWhiteSpace(text) ? "." : text;
    }

    internal static async Task<bool> IsGitRepositoryAsync(
        string workspace,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            workspace,
            ["rev-parse", "--is-inside-work-tree"],
            TimeSpan.FromSeconds(10),
            cancellationToken,
            throwOnStartFailure: false);

        return result.ExitCode == 0
               && result.StandardOutput.Trim().Equals(
                   "true",
                   StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<(string Status, string? Warning)> RefreshAsync(
        string workspace,
        string? branch,
        bool requested,
        CancellationToken cancellationToken)
    {
        if (!requested)
            return ("Cached", null);

        var args = string.IsNullOrWhiteSpace(branch)
            ? new[] { "fetch", "--prune", "--no-tags", "origin" }
            : new[]
            {
                "fetch",
                "--no-tags",
                "origin",
                $"refs/heads/{branch}:refs/remotes/origin/{branch}"
            };

        var result = await RunGitAsync(
            workspace,
            args,
            TimeSpan.FromMinutes(2),
            cancellationToken,
            throwOnStartFailure: false);

        return result.ExitCode == 0
            ? ("Fetched", null)
            : (
                "Cached",
                "Remote refresh failed; branch QA is using the last locally "
                + $"cached Git ref. {OneLine(result.StandardError)}"
            );
    }

    internal static async Task<(string Ref, string Sha, bool Remote)?>
        ResolveBranchRefAsync(
            string workspace,
            string branch,
            CancellationToken cancellationToken)
    {
        foreach (var candidate in new[]
                 {
                     ($"refs/remotes/origin/{branch}", true),
                     ($"refs/heads/{branch}", false)
                 })
        {
            var result = await RunGitAsync(
                workspace,
                ["rev-parse", "--verify", candidate.Item1],
                TimeSpan.FromSeconds(15),
                cancellationToken,
                throwOnStartFailure: false);

            if (result.ExitCode != 0)
                continue;

            var sha = result.StandardOutput
                .Trim()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()
                ?.Trim();

            if (!string.IsNullOrWhiteSpace(sha))
                return (candidate.Item1, sha, candidate.Item2);
        }

        return null;
    }

    internal static async Task ValidateBranchAsync(
        string workspace,
        string branch,
        CancellationToken cancellationToken)
    {
        var result = await RunGitAsync(
            workspace,
            ["check-ref-format", "--branch", branch],
            TimeSpan.FromSeconds(10),
            cancellationToken,
            throwOnStartFailure: false);

        if (result.ExitCode != 0)
            throw new ArgumentException(
                $"'{branch}' is not a valid Git branch name.",
                nameof(branch));
    }

    internal static void ValidatePortableBranch(string branch)
    {
        if (branch.StartsWith('/')
            || branch.EndsWith('/')
            || branch.EndsWith('.')
            || branch.Contains("..", StringComparison.Ordinal)
            || branch.Contains("@{", StringComparison.Ordinal)
            || branch.Contains("//", StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid Git branch name.", nameof(branch));
        }

        if (branch.IndexOfAny([' ', '~', '^', ':', '?', '*', '[', '\\']) >= 0
            || branch.Any(char.IsControl))
        {
            throw new ArgumentException("Invalid Git branch name.", nameof(branch));
        }

        foreach (var part in branch.Split('/'))
        {
            if (part.Length == 0
                || part.StartsWith('.')
                || part.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Invalid Git branch name.",
                    nameof(branch));
            }
        }
    }

    internal static async Task<GitHubWorkspaceSyncDto?> ReadSyncMarkerAsync(
        string sourceWorkspace,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(sourceWorkspace, ".dip-sync.json");
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<GitHubWorkspaceSyncDto>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static (string Owner, string Repository) ParseRepository(string value)
    {
        var parts = (value ?? string.Empty)
            .Trim()
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);

        if (parts.Length != 2
            || parts.Any(
                x => x.Length == 0
                     || x.Contains("..", StringComparison.Ordinal)
                     || x.Any(char.IsControl)))
        {
            throw new InvalidOperationException(
                "GitHub synchronization provenance does not contain a valid "
                + "owner/repository pair.");
        }

        return (parts[0], parts[1]);
    }

    internal static string ResolveWorkspaceRoot(
        string sourceWorkspace,
        string sourceRelative)
    {
        var root = Path.GetFullPath(sourceWorkspace);
        var parts = (sourceRelative ?? string.Empty)
            .Replace('\\', '/')
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);

        for (var index = 0; index < parts.Length; index++)
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new InvalidOperationException(
                    "Could not resolve QA workspace root from synchronization provenance.");
        }

        return Path.GetFullPath(root);
    }

    internal static void EnsureInside(string root, string path)
    {
        var prefix = Path.GetFullPath(root)
                         .TrimEnd(
                             Path.DirectorySeparatorChar,
                             Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;

        var full = Path.GetFullPath(path);
        if (!full.StartsWith(prefix, PathComparison))
        {
            throw new InvalidOperationException(
                "Branch snapshot escaped the configured QA workspace root.");
        }
    }

    internal static async Task DeleteArchiveSnapshotAsync(
        string sourceWorkspace,
        GitHubWorkspaceSyncDto marker,
        GitHubWorkspaceSyncDto snapshot)
    {
        try
        {
            var root = ResolveWorkspaceRoot(
                sourceWorkspace,
                marker.WorkspaceRelativePath);
            var target = Path.GetFullPath(
                Path.Combine(
                    root,
                    snapshot.WorkspaceRelativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            EnsureInside(root, target);
            await DeleteDirectoryAsync(target);
        }
        catch
        {
            // Cleanup is best-effort; the shared workspace must remain untouched.
        }
    }

    internal static Task DeleteDirectoryAsync(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, true);
        }
        catch
        {
            // Cleanup is best-effort; stale isolated directories are safe to inspect.
        }

        return Task.CompletedTask;
    }

    internal static void EnsureSuccess(
        BranchQaGitResult result,
        string message)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{message} {OneLine(result.StandardError)}".Trim());
        }
    }

    internal static string OneLine(string? value) =>
        string.Join(
                " ",
                (value ?? string.Empty).Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries))
            .Trim();

    internal static IEnumerable<string> Lines(string value) =>
        value.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries
            | StringSplitOptions.TrimEntries);

    internal static async Task<BranchQaGitResult> RunGitAsync(
        string workspace,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool throwOnStartFailure = true)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workspace,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        process.StartInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";

        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        try
        {
            if (!process.Start())
                return new BranchQaGitResult(127, "", "git process did not start");
        }
        catch (Exception exception) when (!throwOnStartFailure)
        {
            return new BranchQaGitResult(127, "", exception.Message);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Git is required for branch QA but could not be started.",
                exception);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        linked.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Process may already have exited while cancellation was observed.
            }

            if (cancellationToken.IsCancellationRequested)
                throw;

            return new BranchQaGitResult(
                124,
                await stdoutTask,
                $"Git command timed out after {timeout.TotalSeconds:0} seconds. "
                + await stderrTask);
        }

        return new BranchQaGitResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }
}
