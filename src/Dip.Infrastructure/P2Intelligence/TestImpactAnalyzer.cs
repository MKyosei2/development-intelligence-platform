// Purpose: Provides infrastructure behavior for this application boundary.
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;

namespace Dip.Infrastructure.P2Intelligence;

/// <summary>
/// Conservative test-impact selector for portfolio/CI use.
/// It never claims full dependency precision: project references are authoritative,
/// while path/name/token similarity is used only as a ranking signal.
/// </summary>
public sealed class TestImpactAnalyzer(IQaWorkspaceResolver workspaceResolver) : ITestImpactAnalyzer
{
    private static readonly string[] TestFileSuffixes =
        ["Tests.cs", "Test.cs", "Tests.cpp", "Test.cpp", "_test.py", "test_.py", ".spec.ts", ".test.ts", ".spec.js", ".test.js"];
    private static readonly HashSet<string> TestSourceExtensions =
        new([".cs", ".cpp", ".cc", ".cxx", ".c", ".py", ".ts", ".tsx", ".js", ".jsx"], StringComparer.OrdinalIgnoreCase);

    public Task<TestImpactResult> AnalyzeAsync(TestImpactCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);
        var workspace = workspaceResolver.Resolve(command.WorkspaceRelativePath);
        var changed = command.ChangedFiles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var candidates = EnumerateTests(workspace).ToArray();
        var changedProjects = changed
            .Select(path => FindNearestProject(workspace, path))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var impactedTestProjects = FindReferencingTestProjects(workspace, changedProjects);
        var changedTokens = BuildChangedTokens(workspace, changed);
        var unresolved = new List<string>();
        var scored = new List<TestImpactSelection>();

        foreach (var changedFile in changed)
        {
            var full = SafePath(workspace, changedFile);
            if (!File.Exists(full) && !Directory.Exists(full))
                unresolved.Add(changedFile);
        }

        foreach (var test in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reasons = new List<string>();
            decimal score = 0m;
            var normalized = Normalize(Path.GetRelativePath(workspace, test));

            var owningProject = FindNearestProject(workspace, normalized);
            if (owningProject is not null && impactedTestProjects.Contains(owningProject, StringComparer.OrdinalIgnoreCase))
            {
                score += 70m;
                reasons.Add("The test project references a changed production project.");
            }

            var testTokens = Tokenize(Path.GetFileNameWithoutExtension(test));
            var overlap = testTokens.Count(t => changedTokens.Contains(t));
            if (overlap > 0)
            {
                score += Math.Min(25m, overlap * 6m);
                reasons.Add($"{overlap} changed symbol/path token(s) overlap the test name.");
            }

            foreach (var changedFile in changed)
            {
                var common = CommonDirectoryDepth(changedFile, normalized);
                if (common >= 2)
                {
                    score += Math.Min(15m, common * 2m);
                    reasons.Add($"Shares {common} directory segment(s) with {changedFile}.");
                    break;
                }

                var changedBase = Path.GetFileNameWithoutExtension(changedFile);
                var testBase = Path.GetFileNameWithoutExtension(normalized);
                if (!string.IsNullOrWhiteSpace(changedBase)
                    && testBase.Contains(changedBase, StringComparison.OrdinalIgnoreCase))
                {
                    score += 30m;
                    reasons.Add($"Test name directly references changed file '{changedBase}'.");
                    break;
                }
            }

            if (score <= 0m) continue;
            scored.Add(new(normalized, Math.Min(100m, decimal.Round(score, 2)), reasons.Distinct().ToArray()));
        }

        var selected = scored
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.TestPath, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(command.MaxSelectedTests, 1, 500))
            .ToArray();

        var reduction = candidates.Length == 0 ? 0m :
            decimal.Round(Math.Max(0m, 100m - (decimal)selected.Length / candidates.Length * 100m), 2);
        var hash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join("\n", changed)))).ToLowerInvariant();

        var status = candidates.Length == 0 ? "NoTestsFound"
            : selected.Length == 0 ? "FallbackFullSuite"
            : unresolved.Count > 0 ? "Partial"
            : "Selected";

        return Task.FromResult(new TestImpactResult(
            status, hash, candidates.Length, selected.Length, reduction, selected, unresolved));
    }

    private static IEnumerable<string> EnumerateTests(string workspace)
    {
        foreach (var file in Directory.EnumerateFiles(workspace, "*", SearchOption.AllDirectories))
        {
            var normalized = Normalize(Path.GetRelativePath(workspace, file));
            if (IsIgnored(normalized)) continue;
            var name = Path.GetFileName(file);
            var isExplicitTestName = TestFileSuffixes.Any(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase));
            var isUnderTests = normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
                             || normalized.StartsWith("tests/", StringComparison.OrdinalIgnoreCase);
            if (isExplicitTestName || (isUnderTests && TestSourceExtensions.Contains(Path.GetExtension(file))))
                yield return file;
        }
    }

    private static HashSet<string> FindReferencingTestProjects(string workspace, IReadOnlyCollection<string> changedProjects)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (changedProjects.Count == 0) return result;

        foreach (var project in Directory.EnumerateFiles(workspace, "*.csproj", SearchOption.AllDirectories))
        {
            var rel = Normalize(Path.GetRelativePath(workspace, project));
            if (IsIgnored(rel) || !rel.Contains("test", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                var doc = XDocument.Load(project);
                foreach (var reference in doc.Descendants().Where(x => x.Name.LocalName == "ProjectReference"))
                {
                    var include = reference.Attribute("Include")?.Value;
                    if (string.IsNullOrWhiteSpace(include)) continue;
                    var normalizedInclude = include.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
                    var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, normalizedInclude));
                    if (changedProjects.Any(cp => Path.GetFullPath(cp).Equals(resolved, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Add(project);
                        break;
                    }
                }
            }
            catch {
                // Intentionally ignored to preserve the existing best-effort or fallback behavior.
            }
        }
        return result;
    }

    private static string? FindNearestProject(string workspace, string relativePath)
    {
        var full = SafePath(workspace, relativePath);
        var directory = File.Exists(full) ? Path.GetDirectoryName(full)! :
            Directory.Exists(full) ? full : Path.GetDirectoryName(full);
        var workspaceFull = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar);
        while (!string.IsNullOrWhiteSpace(directory)
              && directory.StartsWith(workspaceFull, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(directory))
            {
                var project = Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (project is not null) return project;
            }
            var parent = Directory.GetParent(directory)?.FullName;
            if (parent is null || parent.Equals(directory, StringComparison.OrdinalIgnoreCase)) break;
            directory = parent;
        }
        return null;
    }

    private static HashSet<string> BuildChangedTokens(string workspace, IReadOnlyCollection<string> changed)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in changed)
        {
            foreach (var token in Tokenize(path)) tokens.Add(token);
            var full = SafePath(workspace, path);
            if (!File.Exists(full)) continue;
            try
            {
                var text = File.ReadAllText(full);
                foreach (var token in Tokenize(text).Take(500)) tokens.Add(token);
            }
            catch {
                // Intentionally ignored to preserve the existing best-effort or fallback behavior.
            }
        }
        return tokens;
    }

    private static IEnumerable<string> Tokenize(string text) =>
        text.Split([' ', '\t', '\r', '\n', '/', '\\', '.', ':', ';', '-', '_', '(', ')', '[', ']', '{', '}', ','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length >= 3 && x.Length <= 80 && x.Any(char.IsLetterOrDigit))
            .Select(x => x.ToLowerInvariant());

    private static int CommonDirectoryDepth(string a, string b)
    {
        var aa = Normalize(a).Split('/', StringSplitOptions.RemoveEmptyEntries);
        var bb = Normalize(b).Split('/', StringSplitOptions.RemoveEmptyEntries);
        var max = Math.Min(Math.Max(0, aa.Length - 1), Math.Max(0, bb.Length - 1));
        var count = 0;
        for (var i = 0; i < max; i++)
        {
            if (!aa[i].Equals(bb[i], StringComparison.OrdinalIgnoreCase)) break;
            count++;
        }
        return count;
    }

    private static bool IsIgnored(string p) =>
        HasPathSegment(p, "bin") || HasPathSegment(p, "obj") || HasPathSegment(p, "node_modules")
        || HasPathSegment(p, "Library") || HasPathSegment(p, ".git") || HasPathSegment(p, "artifacts");

    private static bool HasPathSegment(string path, string segment)
    {
        var normalized = Normalize(path);
        return normalized.Equals(segment, StringComparison.OrdinalIgnoreCase)
               || normalized.StartsWith(segment + "/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/" + segment + "/", StringComparison.OrdinalIgnoreCase)
               || normalized.EndsWith("/" + segment, StringComparison.OrdinalIgnoreCase);
    }

    private static string SafePath(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidOperationException("Changed/test path must be workspace-relative.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
           && !full.Equals(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path escapes the configured workspace.");
        return full;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
}
