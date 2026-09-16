// Purpose: Provides the Q aW or ks pa ce Re so lv er infrastructure implementation.
using Dip.Application.Features.QualityAutomation;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class QaWorkspaceResolver:IQaWorkspaceResolver
{
    private readonly string _workspaceRoot;
    private readonly string _artifactRoot;
    public QaWorkspaceResolver(IOptions<QaExecutionOptions> options)
    {
        _workspaceRoot=Path.GetFullPath(options.Value.WorkspaceRoot);
        _artifactRoot=Path.GetFullPath(options.Value.ArtifactRoot);
    }
    public string Resolve(string workspaceRelativePath)
    {
        if(string.IsNullOrWhiteSpace(workspaceRelativePath))throw new ArgumentException("WorkspaceRelativePath is required.",nameof(workspaceRelativePath));
        if(Path.IsPathRooted(workspaceRelativePath))throw new InvalidOperationException("QA workspace must be relative to the configured Qa:WorkspaceRoot.");
        var resolved=Path.GetFullPath(Path.Combine(_workspaceRoot,workspaceRelativePath));
        if(!Directory.Exists(resolved)||!IsInside(CanonicalDirectory(_workspaceRoot),CanonicalDirectory(resolved)))throw new DirectoryNotFoundException("QA workspace is outside the configured root or escapes it through a symbolic link.");
        return resolved;
    }
    public string QaArtifactRoot(Guid projectId,Guid qaRunId)
    {
        var path=Path.Combine(_artifactRoot,projectId.ToString("N"),$"run-{qaRunId:N}");Directory.CreateDirectory(path);return path;
    }
    private static string CanonicalDirectory(string path){var full=Path.GetFullPath(path);try{return new DirectoryInfo(full).ResolveLinkTarget(true)?.FullName??full;}catch{return full;}}
    private static bool IsInside(string root,string path){var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;return path.Equals(root,comparison)||path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,comparison);}
}
