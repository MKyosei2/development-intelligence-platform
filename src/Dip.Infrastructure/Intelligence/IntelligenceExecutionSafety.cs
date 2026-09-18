// Purpose: Provides the infrastructure capability represented by I nt el li ge nc eE xe cu ti on Sa fe ty.
using System.Diagnostics;

namespace Dip.Infrastructure.Intelligence;

/// <summary>
/// Centralizes the safety rules used by P2 intelligence runners that execute
/// repository-local verification commands. The original checkout is never used
/// as a mutation/bisect workspace; callers can request a detached temporary
/// worktree and dispose it when finished.
/// </summary>
public sealed class IntelligenceExecutionSafety
{
    private static readonly HashSet<string> AllowedToolNames = new(
        new[]
        {
            "dotnet", "python", "python3", "py", "pytest",
            "cmake", "ctest", "ninja", "msbuild",
            "pwsh", "powershell",
            "node", "npm", "pnpm", "yarn"
        },
        StringComparer.OrdinalIgnoreCase);

    public string ResolveWorkspaceRelativePath(string workspace, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(workspace))
            throw new ArgumentException("Workspace is required.", nameof(workspace));
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path is required.", nameof(relativePath));

        if (Path.IsPathRooted(relativePath))
            throw new InvalidOperationException("Path must be workspace-relative.");

        var root = Path.GetFullPath(workspace)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        var prefix = root + Path.DirectorySeparatorChar;

        if (!candidate.Equals(root, StringComparison.OrdinalIgnoreCase)
            && !candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Path escapes the configured workspace.");
        }

        return candidate;
    }

    public string ResolveAllowedCommand(string workspace, string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            throw new ArgumentException("Command is required.", nameof(command));

        if (Path.IsPathRooted(command))
            throw new InvalidOperationException("Absolute verification commands are not allowed.");

        var hasPathSeparator = command.Contains(Path.DirectorySeparatorChar)
            || command.Contains(Path.AltDirectorySeparatorChar)
            || command.StartsWith(".", StringComparison.Ordinal);

        if (hasPathSeparator)
        {
            var path = ResolveWorkspaceRelativePath(workspace, command);
            if (!File.Exists(path))
                throw new FileNotFoundException("Workspace-local verification command was not found.", path);
            return path;
        }

        if (!AllowedToolNames.Contains(command))
        {
            throw new InvalidOperationException(
                $"Verification tool '{command}' is not allowlisted. " +
                "Use a workspace-relative executable/script or one of the documented development tools.");
        }

        return command;
    }

    public async Task<SafeProcessResult> RunAllowedCommandAsync(
        string workspace,
        string command,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var resolved = ResolveAllowedCommand(workspace, command);
        return await RunProcessAsync(
            resolved,
            arguments,
            workspace,
            timeout,
            cancellationToken);
    }

    public Task<SafeProcessResult> RunGitAsync(
        string workspace,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        RunProcessAsync("git", arguments, workspace, timeout, cancellationToken);

    public async Task<GitWorktreeSandbox> CreateDetachedWorktreeAsync(
        string workspace,
        string startRef,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(startRef))
            throw new ArgumentException("Start ref is required.", nameof(startRef));

        var rootResult = await RunGitAsync(
            workspace,
            new[] { "rev-parse", "--show-toplevel" },
            TimeSpan.FromSeconds(30),
            cancellationToken);

        if (rootResult.ExitCode != 0 || string.IsNullOrWhiteSpace(rootResult.StandardOutput))
        {
            throw new InvalidOperationException(
                "P2 isolated execution requires a Git repository. " +
                rootResult.StandardError.Trim());
        }

        var repositoryRoot = Path.GetFullPath(rootResult.StandardOutput.Trim());
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "dip-intelligence-worktrees",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(temporaryRoot)!);

        var addResult = await RunGitAsync(
            repositoryRoot,
            new[] { "worktree", "add", "--force", "--detach", temporaryRoot, startRef },
            TimeSpan.FromMinutes(2),
            cancellationToken);

        if (addResult.ExitCode != 0)
        {
            try { Directory.Delete(temporaryRoot, true); } catch { }
            throw new InvalidOperationException(
                "Could not create the isolated Git worktree: " + addResult.StandardError.Trim());
        }

        return new GitWorktreeSandbox(this, repositoryRoot, temporaryRoot);
    }

    private static async Task<SafeProcessResult> RunProcessAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = psi };
        if (!process.Start())
            throw new InvalidOperationException($"Could not start '{fileName}'.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            try { process.Kill(true); } catch { }
            try { await process.WaitForExitAsync(CancellationToken.None); } catch { }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            try { process.Kill(true); } catch { }
            try { await process.WaitForExitAsync(CancellationToken.None); } catch { }
            throw;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return new SafeProcessResult(
            timedOut ? -1 : process.ExitCode,
            timedOut,
            stdout,
            stderr);
    }

    public sealed class GitWorktreeSandbox : IAsyncDisposable
    {
        private readonly IntelligenceExecutionSafety _owner;
        private int _disposed;

        internal GitWorktreeSandbox(
            IntelligenceExecutionSafety owner,
            string repositoryRoot,
            string path)
        {
            _owner = owner;
            RepositoryRoot = repositoryRoot;
            Path = path;
        }

        public string RepositoryRoot { get; }
        public string Path { get; }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try
            {
                await _owner.RunGitAsync(
                    RepositoryRoot,
                    new[] { "worktree", "remove", "--force", Path },
                    TimeSpan.FromMinutes(2),
                    CancellationToken.None);
            }
            catch
            {
                // Best-effort cleanup continues below.
            }

            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, true);
            }
            catch { }

            try
            {
                await _owner.RunGitAsync(
                    RepositoryRoot,
                    new[] { "worktree", "prune" },
                    TimeSpan.FromSeconds(30),
                    CancellationToken.None);
            }
            catch { }
        }
    }
}

public sealed record SafeProcessResult(
    int ExitCode,
    bool TimedOut,
    string StandardOutput,
    string StandardError);
