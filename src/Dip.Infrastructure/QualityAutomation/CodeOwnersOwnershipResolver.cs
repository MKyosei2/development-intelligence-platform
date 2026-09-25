// Purpose: Implements CODEOWNERS Ownership Resolver infrastructure behavior for quality automation.
using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;
using Dip.Application.Features.QualityGovernance;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class CodeOwnersOwnershipResolver(IQaWorkspaceResolver workspaces) : IRepositoryOwnershipResolver
{
    private static readonly string[] CandidateFiles = [".github/CODEOWNERS", "CODEOWNERS", "docs/CODEOWNERS"];
    public async Task<RepositoryOwnershipResolution?> ResolveAsync(string workspaceRelativePath, string? filePath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return null;
        var workspace = Path.GetFullPath(workspaces.Resolve(workspaceRelativePath));
        var relative = NormalizeFilePath(workspace, filePath);
        if (relative is null) return null;
        foreach (var candidate in CandidateFiles)
        {
            var path = Path.Combine(workspace, candidate.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) continue;
            var lines = await File.ReadAllLinesAsync(path, ct);
            RepositoryOwnershipResolution? winner = null;
            foreach (var raw in lines)
            {
                ct.ThrowIfCancellationRequested();
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var parts = Regex.Split(line, @"\s+").Where(x => x.Length > 0).ToArray();
                if (parts.Length < 2) continue;
                var pattern = parts[0];
                if (!Matches(pattern, relative)) continue;
                var owners = parts.Skip(1).Where(x => x.StartsWith('@') || x.Contains('@')).ToArray();
                if (owners.Length == 0) continue;
                winner = new RepositoryOwnershipResolution(string.Join(", ", owners), null, pattern, candidate);
            }
            if (winner is not null) return winner;
        }
        return null;
    }
    private static string? NormalizeFilePath(string workspace, string filePath)
    {
        var value = filePath.Replace('\\', '/');
        if (!Path.IsPathRooted(filePath)) return value.TrimStart('/');
        var full = Path.GetFullPath(filePath);
        var prefix = workspace.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        return Path.GetRelativePath(workspace, full).Replace('\\', '/');
    }
    internal static bool Matches(string pattern, string relativePath)
    {
        var p = pattern.Trim().Replace('\\', '/');
        var value = relativePath.TrimStart('/').Replace('\\', '/');
        if (p.StartsWith('!')) return false;
        var anchored = p.StartsWith('/');
        p = p.TrimStart('/');
        if (p.EndsWith('/')) p += "**";
        if (!anchored && !p.Contains('/')) p = "**/" + p;
        var rx = Regex.Escape(p).Replace(@"\*\*", ".*").Replace(@"\*", "[^/]*").Replace(@"\?", "[^/]");
        if (p.StartsWith("**/", StringComparison.Ordinal)) rx = "(?:.*/)?" + rx[Regex.Escape("**/").Length..];
        return Regex.IsMatch(value, "^" + rx + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
