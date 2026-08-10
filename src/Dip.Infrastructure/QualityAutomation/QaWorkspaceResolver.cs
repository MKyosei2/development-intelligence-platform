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
        if(!IsInside(_workspaceRoot,resolved)||!Directory.Exists(resolved))throw new DirectoryNotFoundException("QA workspace is outside the configured root or does not exist.");
        return resolved;
    }
    public string QaArtifactRoot(Guid projectId,Guid qaRunId)
    {
        var path=Path.Combine(_artifactRoot,projectId.ToString("N"),$"run-{qaRunId:N}");Directory.CreateDirectory(path);return path;
    }
    private static bool IsInside(string root,string path)=>path.Equals(root,StringComparison.OrdinalIgnoreCase)||path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);
}
