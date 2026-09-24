// Purpose: Owns the lifetime and cleanup callback for an isolated branch QA workspace.
namespace Dip.Application.Features.QualityAutomation;

/// <summary>
/// Disposable branch QA workspace. Branch verification never mutates the shared
/// synchronized repository. An isolated worktree or archive snapshot is removed
/// when the QA run finishes.
/// </summary>
public sealed class BranchQaWorkspaceLease : IAsyncDisposable
{
    private readonly Func<ValueTask>? _cleanup;
    private int _disposed;

    internal BranchQaWorkspaceLease(
        string workspacePath,
        string workspaceRelativePath,
        string? branchName,
        string? commitSha,
        string sourceRef,
        string refreshStatus,
        string? warning,
        Func<ValueTask>? cleanup)
    {
        WorkspacePath = workspacePath;
        WorkspaceRelativePath = workspaceRelativePath;
        BranchName = branchName;
        CommitSha = commitSha;
        SourceRef = sourceRef;
        RefreshStatus = refreshStatus;
        Warning = warning;
        _cleanup = cleanup;
    }

    public string WorkspacePath { get; }
    public string WorkspaceRelativePath { get; }
    public string? BranchName { get; }
    public string? CommitSha { get; }
    public string SourceRef { get; }
    public string RefreshStatus { get; }
    public string? Warning { get; }
    public bool IsIsolated => _cleanup is not null;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0 || _cleanup is null)
            return;

        await _cleanup();
    }
}
