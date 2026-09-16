// Purpose: Coordinates G it Br an ch Qa Wo rk sp ac eM an ag er application behavior for QualityAutomation.
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Features.GitHubIntegration;

namespace Dip.Application.Features.QualityAutomation;

public sealed record BranchRepositoryRefDto(
    string Branch,
    string CommitSha,
    bool IsDefault,
    bool RemoteTracked,
    int AheadOfDefault,
    int BehindDefault,
    DateTimeOffset? LastCommitAtUtc,
    string Source,
    string RefreshStatus,
    string? Warning);

public sealed record BranchRepositoryCatalogDto(
    string DefaultBranch,
    string RefreshStatus,
    IReadOnlyList<BranchRepositoryRefDto> Branches,
    string? Warning,
    DateTimeOffset GeneratedAtUtc);

public interface IBranchQaWorkspaceManager
{
    Task<BranchRepositoryCatalogDto> ListBranchesAsync(
        string sourceWorkspace,
        string defaultBranch,
        bool refreshRemote,
        CancellationToken cancellationToken);

    Task<BranchQaWorkspaceLease> PrepareAsync(
        string sourceWorkspace,
        string sourceWorkspaceRelativePath,
        string? branchName,
        string? requestedCommitSha,
        bool refreshRemote,
        Guid qaRunId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Disposable branch QA workspace. Branch verification never mutates the shared
/// synchronized repository. A detached Git worktree is created underneath the
/// synchronized workspace and removed when the QA run finishes.
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
        WorkspacePath=workspacePath;
        WorkspaceRelativePath=workspaceRelativePath;
        BranchName=branchName;
        CommitSha=commitSha;
        SourceRef=sourceRef;
        RefreshStatus=refreshStatus;
        Warning=warning;
        _cleanup=cleanup;
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
        if(Interlocked.Exchange(ref _disposed,1)!=0||_cleanup is null)return;
        await _cleanup();
    }
}

/// <summary>
/// Local Git branch isolation used by QA. No shell is used; every argument is
/// passed via ProcessStartInfo.ArgumentList. Remote refresh is best-effort and
/// its failure is surfaced instead of silently being treated as current data.
/// </summary>
public sealed class GitBranchQaWorkspaceManager : IBranchQaWorkspaceManager
{
    private const int MaxBranches=250;
    private readonly IGitHubRepositoryCatalog? _github;

    public GitBranchQaWorkspaceManager(IGitHubRepositoryCatalog? github=null)=>_github=github;

    public async Task<BranchRepositoryCatalogDto> ListBranchesAsync(
        string sourceWorkspace,
        string defaultBranch,
        bool refreshRemote,
        CancellationToken cancellationToken)
    {
        NeedWorkspace(sourceWorkspace);
        var normalizedDefault=NormalizeBranch(defaultBranch);
        if(!await IsGitRepositoryAsync(sourceWorkspace,cancellationToken))
            return await ListArchiveBranchesAsync(sourceWorkspace,normalizedDefault,cancellationToken);
        var (refreshStatus,refreshWarning)=await RefreshAsync(sourceWorkspace,null,refreshRemote,cancellationToken);
        var refs=await RunGitAsync(sourceWorkspace,["for-each-ref","--format=%(refname)|%(objectname)|%(committerdate:iso-strict)","refs/heads","refs/remotes/origin"],TimeSpan.FromSeconds(30),cancellationToken);
        EnsureSuccess(refs,"Unable to enumerate Git branches.");

        var selected=new Dictionary<string,(string Ref,string Sha,bool Remote,DateTimeOffset? At)>(StringComparer.OrdinalIgnoreCase);
        foreach(var line in Lines(refs.StandardOutput))
        {
            var p=line.Split('|',3);
            if(p.Length<2)continue;
            var fullRef=p[0].Trim();
            if(fullRef.Equals("refs/remotes/origin/HEAD",StringComparison.OrdinalIgnoreCase))continue;
            var remote=fullRef.StartsWith("refs/remotes/origin/",StringComparison.Ordinal);
            var local=fullRef.StartsWith("refs/heads/",StringComparison.Ordinal);
            if(!remote&&!local)continue;
            var branch=remote?fullRef["refs/remotes/origin/".Length..]:fullRef["refs/heads/".Length..];
            if(string.IsNullOrWhiteSpace(branch))continue;
            DateTimeOffset? at=null;
            if(p.Length==3&&DateTimeOffset.TryParse(p[2],CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var parsed))at=parsed;
            if(!selected.TryGetValue(branch,out var existing)||remote&&!existing.Remote)
                selected[branch]=(fullRef,p[1].Trim(),remote,at);
        }

        var defaultRef=await ResolveBranchRefAsync(sourceWorkspace,normalizedDefault,cancellationToken);
        var output=new List<BranchRepositoryRefDto>();
        foreach(var item in selected.OrderBy(x=>x.Key.Equals(normalizedDefault,StringComparison.OrdinalIgnoreCase)?0:1).ThenBy(x=>x.Key,StringComparer.OrdinalIgnoreCase).Take(MaxBranches))
        {
            var ahead=0;var behind=0;string? warning=null;
            if(defaultRef is not null)
            {
                var delta=await RunGitAsync(sourceWorkspace,["rev-list","--left-right","--count",$"{defaultRef.Value.Ref}...{item.Value.Ref}"],TimeSpan.FromSeconds(20),cancellationToken,throwOnStartFailure:false);
                if(delta.ExitCode==0)
                {
                    var bits=delta.StandardOutput.Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
                    if(bits.Length>=2){int.TryParse(bits[0],out behind);int.TryParse(bits[1],out ahead);}
                }
                else warning="Ahead/behind comparison was unavailable.";
            }
            output.Add(new(item.Key,item.Value.Sha,item.Key.Equals(normalizedDefault,StringComparison.OrdinalIgnoreCase),item.Value.Remote,ahead,behind,item.Value.At,item.Value.Remote?"origin":"local",refreshStatus,warning));
        }

        if(output.All(x=>!x.Branch.Equals(normalizedDefault,StringComparison.OrdinalIgnoreCase))&&defaultRef is not null)
            output.Insert(0,new(normalizedDefault,defaultRef.Value.Sha,true,defaultRef.Value.Remote,0,0,null,defaultRef.Value.Remote?"origin":"local",refreshStatus,null));

        return new(normalizedDefault,refreshStatus,output,refreshWarning,DateTimeOffset.UtcNow);
    }

    public async Task<BranchQaWorkspaceLease> PrepareAsync(
        string sourceWorkspace,
        string sourceWorkspaceRelativePath,
        string? branchName,
        string? requestedCommitSha,
        bool refreshRemote,
        Guid qaRunId,
        CancellationToken cancellationToken)
    {
        NeedWorkspace(sourceWorkspace);
        if(string.IsNullOrWhiteSpace(branchName))
            return new(sourceWorkspace,sourceWorkspaceRelativePath,null,requestedCommitSha,"workspace","NotRequested",null,null);

        var branch=NormalizeBranch(branchName);
        if(!await IsGitRepositoryAsync(sourceWorkspace,cancellationToken))
            return await PrepareArchiveBranchAsync(sourceWorkspace,sourceWorkspaceRelativePath,branch,requestedCommitSha,qaRunId,cancellationToken);
        await ValidateBranchAsync(sourceWorkspace,branch,cancellationToken);
        var (refreshStatus,refreshWarning)=await RefreshAsync(sourceWorkspace,branch,refreshRemote,cancellationToken);
        var resolved=await ResolveBranchRefAsync(sourceWorkspace,branch,cancellationToken)
            ?? throw new KeyNotFoundException($"Git branch '{branch}' is not available in the synchronized repository. Refresh the repository or fetch the branch first.");

        var commit=resolved.Sha;
        if(!string.IsNullOrWhiteSpace(requestedCommitSha))
        {
            var requested=NormalizeCommit(requestedCommitSha);
            var verify=await RunGitAsync(sourceWorkspace,["merge-base","--is-ancestor",requested,resolved.Ref],TimeSpan.FromSeconds(20),cancellationToken,throwOnStartFailure:false);
            if(verify.ExitCode!=0)
                throw new InvalidOperationException($"Requested revision '{requested}' is not reachable from branch '{branch}'. DIP will not label an unrelated commit as branch QA evidence.");
            commit=requested;
        }

        var safeRelative=NormalizeRelativeWorkspace(sourceWorkspaceRelativePath);
        var leaf=$"run-{qaRunId:N}";
        var targetRelative=Path.Combine(safeRelative,".dip-branch-qa",leaf).Replace('\\','/');
        var targetPath=Path.Combine(sourceWorkspace,".dip-branch-qa",leaf);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if(Directory.Exists(targetPath))Directory.Delete(targetPath,true);

        var add=await RunGitAsync(sourceWorkspace,["worktree","add","--detach",targetPath,commit],TimeSpan.FromMinutes(2),cancellationToken);
        EnsureSuccess(add,$"Unable to create an isolated QA worktree for branch '{branch}'.");

        async ValueTask Cleanup()
        {
            try
            {
                var remove=await RunGitAsync(sourceWorkspace,["worktree","remove","--force",targetPath],TimeSpan.FromMinutes(1),CancellationToken.None,throwOnStartFailure:false);
                _=remove;
                await RunGitAsync(sourceWorkspace,["worktree","prune"],TimeSpan.FromSeconds(30),CancellationToken.None,throwOnStartFailure:false);
            }
            catch { }
            try{if(Directory.Exists(targetPath))Directory.Delete(targetPath,true);}catch{ }
            try
            {
                var parent=Path.GetDirectoryName(targetPath);
                if(parent is not null&&Directory.Exists(parent)&&!Directory.EnumerateFileSystemEntries(parent).Any())Directory.Delete(parent);
            }
            catch { }
        }

        return new(targetPath,targetRelative,branch,commit,resolved.Ref,refreshStatus,refreshWarning,Cleanup);
    }

    private static async Task<(string Status,string? Warning)> RefreshAsync(string workspace,string? branch,bool requested,CancellationToken ct)
    {
        if(!requested)return("Cached",null);
        var args=string.IsNullOrWhiteSpace(branch)
            ?new[]{"fetch","--prune","--no-tags","origin"}
            :new[]{"fetch","--no-tags","origin",$"refs/heads/{branch}:refs/remotes/origin/{branch}"};
        var result=await RunGitAsync(workspace,args,TimeSpan.FromMinutes(2),ct,throwOnStartFailure:false);
        return result.ExitCode==0
            ?("Fetched",null)
            :("Cached",$"Remote refresh failed; branch QA is using the last locally cached Git ref. {OneLine(result.StandardError)}");
    }

    private static async Task<(string Ref,string Sha,bool Remote)?> ResolveBranchRefAsync(string workspace,string branch,CancellationToken ct)
    {
        foreach(var candidate in new[]{($"refs/remotes/origin/{branch}",true),($"refs/heads/{branch}",false)})
        {
            var result=await RunGitAsync(workspace,["rev-parse","--verify",candidate.Item1],TimeSpan.FromSeconds(15),ct,throwOnStartFailure:false);
            if(result.ExitCode==0)
            {
                var sha=result.StandardOutput.Trim().Split('\n',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
                if(!string.IsNullOrWhiteSpace(sha))return(candidate.Item1,sha,candidate.Item2);
            }
        }
        return null;
    }

    private static async Task ValidateBranchAsync(string workspace,string branch,CancellationToken ct)
    {
        var result=await RunGitAsync(workspace,["check-ref-format","--branch",branch],TimeSpan.FromSeconds(10),ct,throwOnStartFailure:false);
        if(result.ExitCode!=0)throw new ArgumentException($"'{branch}' is not a valid Git branch name.",nameof(branch));
    }

    private static async Task<bool> IsGitRepositoryAsync(string workspace,CancellationToken ct)
    {
        var result=await RunGitAsync(workspace,["rev-parse","--is-inside-work-tree"],TimeSpan.FromSeconds(10),ct,throwOnStartFailure:false);
        return result.ExitCode==0&&result.StandardOutput.Trim().Equals("true",StringComparison.OrdinalIgnoreCase);
    }

    private async Task<BranchRepositoryCatalogDto> ListArchiveBranchesAsync(string sourceWorkspace,string defaultBranch,CancellationToken ct)
    {
        var marker=await ReadSyncMarkerAsync(sourceWorkspace,ct)
            ?? throw new InvalidOperationException("Branch QA requires either a Git working tree or a GitHub-synchronized workspace with .dip-sync.json provenance.");
        if(_github is null)
            throw new InvalidOperationException("GitHub branch catalog is unavailable. Configure GitHub repository access or use a Git working tree workspace.");
        var (owner,repository)=ParseRepository(marker.Repository);
        var details=await _github.GetRepositoryAsync(owner,repository,ct);
        IReadOnlyList<GitHubPullRequestSnapshotDto> pulls;
        try{pulls=await _github.ListPullRequestsAsync(owner,repository,"open",ct);}
        catch(HttpRequestException){pulls=Array.Empty<GitHubPullRequestSnapshotDto>();}

        var refs=new Dictionary<string,(string Sha,DateTimeOffset? At,string Source)>(StringComparer.OrdinalIgnoreCase);
        void Observe(string? branch,string? sha,DateTimeOffset? at,string source)
        {
            if(string.IsNullOrWhiteSpace(branch))return;
            var normalized=branch.Trim();
            if(normalized.Length>200)return;
            var revision=sha?.Trim()??string.Empty;
            if(!refs.TryGetValue(normalized,out var existing)||at.HasValue&&(!existing.At.HasValue||at>existing.At))refs[normalized]=(revision,at,source);
        }
        Observe(defaultBranch,details.LatestCommitSha,details.LatestCommitAtUtc,"github-default");
        foreach(var run in details.WorkflowRuns)Observe(run.HeadBranch,run.HeadSha,run.UpdatedAtUtc??run.CreatedAtUtc,"github-actions");
        foreach(var pull in pulls)Observe(pull.HeadBranch,pull.HeadSha,null,$"pull-request:{pull.Number}");
        Observe(marker.Branch,marker.CommitSha,marker.SyncedAtUtc,"workspace-snapshot");

        var branches=refs
            .OrderBy(x=>x.Key.Equals(defaultBranch,StringComparison.OrdinalIgnoreCase)?0:1)
            .ThenBy(x=>x.Key,StringComparer.OrdinalIgnoreCase)
            .Take(MaxBranches)
            .Select(x=>new BranchRepositoryRefDto(
                x.Key,x.Value.Sha,x.Key.Equals(defaultBranch,StringComparison.OrdinalIgnoreCase),true,0,0,x.Value.At,
                x.Value.Source,"GitHubApi","Ahead/behind requires a Git working tree; branch QA itself uses an isolated authenticated GitHub archive snapshot."))
            .ToArray();
        return new(defaultBranch,"GitHubApi",branches,
            "Archive-backed workspace: active branches are discovered from the default branch, open pull requests and the synchronized snapshot; persisted DIP pipeline history is merged by the branch-quality service. Any other valid branch can still be entered and verified explicitly.",DateTimeOffset.UtcNow);
    }

    private async Task<BranchQaWorkspaceLease> PrepareArchiveBranchAsync(
        string sourceWorkspace,string sourceWorkspaceRelativePath,string branch,string? requestedCommitSha,Guid qaRunId,CancellationToken ct)
    {
        var marker=await ReadSyncMarkerAsync(sourceWorkspace,ct)
            ?? throw new InvalidOperationException("This workspace has no Git metadata and no GitHub synchronization provenance. Branch QA cannot establish branch identity safely.");
        if(_github is null)
            throw new InvalidOperationException("GitHub repository access is required to verify a branch from an archive-backed workspace.");
        ValidatePortableBranch(branch);
        var (owner,repository)=ParseRepository(marker.Repository);
        var snapshot=await _github.SyncWorkspaceAsync(qaRunId,owner,repository,branch,ct);
        try
        {
            if(!snapshot.Branch.Equals(branch,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("GitHub branch snapshot returned unexpected branch provenance.");
            if(!string.IsNullOrWhiteSpace(requestedCommitSha))
            {
                var requested=NormalizeCommit(requestedCommitSha);
                if(!snapshot.CommitSha.Equals(requested,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Requested revision '{requested}' is not the current head of branch '{branch}'. Archive-backed QA refuses to relabel another revision as branch evidence.");
            }
            var workspaceRoot=ResolveWorkspaceRoot(sourceWorkspace,marker.WorkspaceRelativePath);
            var targetPath=Path.GetFullPath(Path.Combine(workspaceRoot,snapshot.WorkspaceRelativePath.Replace('/',Path.DirectorySeparatorChar)));
            EnsureInside(workspaceRoot,targetPath);
            if(targetPath.Equals(Path.GetFullPath(sourceWorkspace),PathComparison))
                throw new InvalidOperationException("Branch QA snapshot resolved to the shared workspace; isolation was refused.");
            if(!Directory.Exists(targetPath))throw new DirectoryNotFoundException("GitHub branch snapshot was not created.");

            async ValueTask Cleanup()=>await DeleteDirectoryAsync(targetPath);
            return new(targetPath,snapshot.WorkspaceRelativePath,branch,snapshot.CommitSha,$"github:{owner}/{repository}:refs/heads/{branch}","GitHubArchive",null,Cleanup);
        }
        catch
        {
            await DeleteArchiveSnapshotAsync(sourceWorkspace,marker,snapshot);
            throw;
        }
    }

    private static async Task<GitHubWorkspaceSyncDto?> ReadSyncMarkerAsync(string sourceWorkspace,CancellationToken ct)
    {
        var path=Path.Combine(sourceWorkspace,".dip-sync.json");
        if(!File.Exists(path))return null;
        try
        {
            var json=await File.ReadAllTextAsync(path,ct);
            return JsonSerializer.Deserialize<GitHubWorkspaceSyncDto>(json,new JsonSerializerOptions(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=true});
        }
        catch(JsonException){return null;}
    }

    private static (string Owner,string Repository) ParseRepository(string value)
    {
        var parts=(value??string.Empty).Trim().Split('/',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(parts.Length!=2||parts.Any(x=>x.Length==0||x.Contains("..",StringComparison.Ordinal)||x.Any(char.IsControl)))
            throw new InvalidOperationException("GitHub synchronization provenance does not contain a valid owner/repository pair.");
        return(parts[0],parts[1]);
    }

    private static void ValidatePortableBranch(string branch)
    {
        if(branch.StartsWith('/')||branch.EndsWith('/')||branch.EndsWith('.')||branch.Contains("..",StringComparison.Ordinal)||branch.Contains("@{",StringComparison.Ordinal)||branch.Contains("//",StringComparison.Ordinal))
            throw new ArgumentException("Invalid Git branch name.",nameof(branch));
        if(branch.IndexOfAny([' ', '~', '^', ':', '?', '*', '[', '\\'])>=0||branch.Any(char.IsControl))
            throw new ArgumentException("Invalid Git branch name.",nameof(branch));
        foreach(var part in branch.Split('/'))
            if(part.Length==0||part.StartsWith('.')||part.EndsWith(".lock",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Invalid Git branch name.",nameof(branch));
    }

    private static string ResolveWorkspaceRoot(string sourceWorkspace,string sourceRelative)
    {
        var root=Path.GetFullPath(sourceWorkspace);
        var parts=(sourceRelative??string.Empty).Replace('\\','/').Split('/',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        for(var i=0;i<parts.Length;i++)root=Directory.GetParent(root)?.FullName??throw new InvalidOperationException("Could not resolve QA workspace root from synchronization provenance.");
        return Path.GetFullPath(root);
    }

    private static void EnsureInside(string root,string path)
    {
        var prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
        var full=Path.GetFullPath(path);
        if(!full.StartsWith(prefix,PathComparison))throw new InvalidOperationException("Branch snapshot escaped the configured QA workspace root.");
    }

    private static async Task DeleteArchiveSnapshotAsync(string sourceWorkspace,GitHubWorkspaceSyncDto marker,GitHubWorkspaceSyncDto snapshot)
    {
        try
        {
            var root=ResolveWorkspaceRoot(sourceWorkspace,marker.WorkspaceRelativePath);
            var target=Path.GetFullPath(Path.Combine(root,snapshot.WorkspaceRelativePath.Replace('/',Path.DirectorySeparatorChar)));
            EnsureInside(root,target);
            await DeleteDirectoryAsync(target);
        }
        catch{ }
    }

    private static Task DeleteDirectoryAsync(string path)
    {
        try{if(Directory.Exists(path))Directory.Delete(path,true);}catch{ }
        return Task.CompletedTask;
    }

    private static StringComparison PathComparison=>OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;

    private static string NormalizeBranch(string? value)
    {
        var branch=(value??string.Empty).Trim();
        if(branch.Length is <1 or >200)throw new ArgumentException("Branch must be 1-200 characters.",nameof(value));
        if(branch.StartsWith("-",StringComparison.Ordinal)||branch.Equals("HEAD",StringComparison.OrdinalIgnoreCase)||branch.Any(char.IsControl))
            throw new ArgumentException("Unsafe Git branch name.",nameof(value));
        return branch;
    }

    private static string NormalizeCommit(string value)
    {
        var commit=value.Trim();
        if(commit.Length is <7 or >64||commit.Any(x=>!Uri.IsHexDigit(x)))throw new ArgumentException("CommitSha must be 7-64 hexadecimal characters.",nameof(value));
        return commit;
    }

    private static string NormalizeRelativeWorkspace(string value)
    {
        var text=(value??string.Empty).Trim().Replace('\\','/').TrimEnd('/');
        return string.IsNullOrWhiteSpace(text)?".":text;
    }

    private static void NeedWorkspace(string workspace)
    {
        if(string.IsNullOrWhiteSpace(workspace)||!Directory.Exists(workspace))throw new DirectoryNotFoundException("The synchronized QA workspace does not exist.");
    }

    private static void EnsureSuccess(GitResult result,string message)
    {
        if(result.ExitCode!=0)throw new InvalidOperationException($"{message} {OneLine(result.StandardError)}".Trim());
    }

    private static string OneLine(string? value)
        =>string.Join(" ",(value??string.Empty).Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static IEnumerable<string> Lines(string value)
        =>value.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);

    private sealed record GitResult(int ExitCode,string StandardOutput,string StandardError);

    private static async Task<GitResult> RunGitAsync(
        string workspace,
        IReadOnlyList<string> args,
        TimeSpan timeout,
        CancellationToken ct,
        bool throwOnStartFailure=true)
    {
        using var process=new Process();
        process.StartInfo=new ProcessStartInfo
        {
            FileName="git",
            WorkingDirectory=workspace,
            UseShellExecute=false,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            CreateNoWindow=true
        };
        process.StartInfo.Environment["GIT_TERMINAL_PROMPT"]="0";
        process.StartInfo.Environment["GIT_OPTIONAL_LOCKS"]="0";
        foreach(var arg in args)process.StartInfo.ArgumentList.Add(arg);
        try
        {
            if(!process.Start())return new(127,"","git process did not start");
        }
        catch(Exception ex) when(!throwOnStartFailure)
        {
            return new(127,"",ex.Message);
        }
        catch(Exception ex)
        {
            throw new InvalidOperationException("Git is required for branch QA but could not be started.",ex);
        }

        var stdoutTask=process.StandardOutput.ReadToEndAsync();
        var stderrTask=process.StandardError.ReadToEndAsync();
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch(OperationCanceledException)
        {
            try{if(!process.HasExited)process.Kill(entireProcessTree:true);}catch{ }
            if(ct.IsCancellationRequested)throw;
            return new(124,await stdoutTask,$"Git command timed out after {timeout.TotalSeconds:0} seconds. {await stderrTask}");
        }
        return new(process.ExitCode,await stdoutTask,await stderrTask);
    }
}
