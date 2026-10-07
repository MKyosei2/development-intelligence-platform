// Purpose: Collects release provenance with dependency manifests, source-tree digests, git revision binding, and a tamper-evident provenance-chain root.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Dip.Application.Abstractions.Provenance;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Provenance;

public sealed class ReleaseProvenanceCollector(IOptions<ReleaseProvenanceOptions> options) : IReleaseProvenanceCollector
{
    private const int MaximumSourceFiles = 20_000;
    private readonly ReleaseProvenanceOptions _options = options.Value;

    public Task<ProvenanceSnapshot> CollectAsync(string repositoryRoot, CancellationToken ct)
    {
        var allowed = Path.GetFullPath(_options.AllowedRoot);
        var root = Path.IsPathRooted(repositoryRoot)
            ? Path.GetFullPath(repositoryRoot)
            : Path.GetFullPath(Path.Combine(allowed, repositoryRoot));
        EnsureInsideRoot(root, allowed);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException(root);

        var components = new List<ProvenanceComponent>();
        foreach (var file in EnumerateSafe(root, "*.csproj", 500))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var manifestHash = Hash(file);
                var doc = XDocument.Load(file);
                foreach (var package in doc.Descendants().Where(x => x.Name.LocalName == "PackageReference"))
                {
                    var name = package.Attribute("Include")?.Value ?? package.Attribute("Update")?.Value;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var version = package.Attribute("Version")?.Value
                                  ?? package.Elements().FirstOrDefault(x => x.Name.LocalName == "Version")?.Value
                                  ?? "unspecified";
                    components.Add(new("library", name, version, Relative(root, file), manifestHash));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            {
                // Unreadable project metadata is represented by the source-tree digest;
                // dependency extraction remains best effort.
            }
        }

        foreach (var file in EnumerateSafe(root, "package.json", 100))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var manifestHash = Hash(file);
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                AddNpm(doc.RootElement, "dependencies", file, root, manifestHash, components);
                AddNpm(doc.RootElement, "devDependencies", file, root, manifestHash, components);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                components.Add(new("application", "npm-package-manifest", "unparsed", Relative(root, file), SafeHash(file)));
            }
        }

        foreach (var file in EnumerateSafe(root, "requirements*.txt", 100))
        {
            ct.ThrowIfCancellationRequested();
            var manifestHash = SafeHash(file);
            try
            {
                foreach (var line in File.ReadLines(file)
                             .Select(x => x.Trim())
                             .Where(x => x.Length > 0 && !x.StartsWith('#'))
                             .Take(2000))
                {
                    var split = line.Split(new[] { "==", ">=", "<=", "~=", "!=" }, 2, StringSplitOptions.None);
                    components.Add(new("library", split[0].Trim(), split.Length > 1 ? split[1].Trim() : "unspecified", Relative(root, file), manifestHash));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                components.Add(new("application", "python-requirements", "unparsed", Relative(root, file), manifestHash));
            }
        }

        var evidenceFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in EnumerateRepositoryFiles(root, 5000)
                     .Where(IsReleaseEvidenceFile))
        {
            ct.ThrowIfCancellationRequested();
            var hash = SafeHash(file);
            if (!string.IsNullOrWhiteSpace(hash))
                evidenceFiles[Relative(root, file)] = hash;
        }

        var treeEntries = new List<(string Path, string Sha256)>();
        foreach (var file in EnumerateRepositoryFiles(root, MaximumSourceFiles))
        {
            ct.ThrowIfCancellationRequested();
            var hash = SafeHash(file);
            if (string.IsNullOrWhiteSpace(hash))
                continue;
            var relative = Relative(root, file);
            treeEntries.Add((relative, hash));
            if (IsSourceManifest(relative) && !evidenceFiles.ContainsKey(relative))
                evidenceFiles[relative] = hash;
        }

        var sourceTreeRoot = HashTree(treeEntries);
        evidenceFiles["@dip/source-tree-root-sha256"] = sourceTreeRoot;

        var sourceCommit = ResolveGitHead(root, allowed);
        if (!string.IsNullOrWhiteSpace(sourceCommit))
            evidenceFiles["@dip/source-commit"] = sourceCommit;

        var runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        var chainRoot = HashChain(runtime, os, sourceCommit, sourceTreeRoot, evidenceFiles);
        evidenceFiles["@dip/provenance-chain-sha256"] = chainRoot;

        var snapshot = new ProvenanceSnapshot(
            root,
            runtime,
            os,
            components
                .GroupBy(x => (x.Type, x.Name, x.Version, x.Path, x.Sha256))
                .Select(x => x.First())
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Version, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            evidenceFiles);
        return Task.FromResult(snapshot);
    }

    private static IEnumerable<string> EnumerateSafe(string root, string pattern, int take)
        => EnumerateRepositoryFiles(root, Math.Max(take * 10, take))
            .Where(x => System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(pattern, Path.GetFileName(x), ignoreCase: true))
            .Take(take);

    private static IEnumerable<string> EnumerateRepositoryFiles(string root, int take)
    {
        var count = 0;
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0 && count < take)
        {
            var directory = stack.Pop();
            string[] files;
            string[] directories;
            try
            {
                files = Directory.GetFiles(directory);
                directories = Directory.GetDirectories(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (count >= take) yield break;
                FileInfo info;
                try { info = new FileInfo(file); }
                catch { continue; }
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                if (info.Length > 512L * 1024 * 1024)
                    continue;
                count++;
                yield return file;
            }

            foreach (var child in directories.OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase))
            {
                DirectoryInfo info;
                try { info = new DirectoryInfo(child); }
                catch { continue; }
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;
                var name = info.Name;
                if (name is ".git" or "bin" or "obj" or "node_modules" or "__pycache__" or ".vs" or ".dipqa" or "artifacts" or "Library" or "Temp" or "Logs")
                    continue;
                stack.Push(child);
            }
        }
    }

    private static void AddNpm(
        JsonElement rootElement,
        string property,
        string file,
        string root,
        string manifestHash,
        List<ProvenanceComponent> components)
    {
        if (!rootElement.TryGetProperty(property, out var deps) || deps.ValueKind != JsonValueKind.Object)
            return;
        foreach (var dep in deps.EnumerateObject())
            components.Add(new("library", dep.Name, dep.Value.GetString() ?? "unspecified", Relative(root, file), manifestHash));
    }

    private static bool IsSourceManifest(string relative)
    {
        var name = Path.GetFileName(relative);
        var ext = Path.GetExtension(name).ToLowerInvariant();
        return name.Equals("Directory.Packages.props", StringComparison.OrdinalIgnoreCase)
               || name.Equals("Directory.Build.props", StringComparison.OrdinalIgnoreCase)
               || name.Equals("Directory.Build.targets", StringComparison.OrdinalIgnoreCase)
               || name.Equals("global.json", StringComparison.OrdinalIgnoreCase)
               || name.Equals("package.json", StringComparison.OrdinalIgnoreCase)
               || name.Equals("package-lock.json", StringComparison.OrdinalIgnoreCase)
               || name.Equals("pnpm-lock.yaml", StringComparison.OrdinalIgnoreCase)
               || name.Equals("yarn.lock", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("requirements", StringComparison.OrdinalIgnoreCase) && ext == ".txt"
               || ext is ".csproj" or ".fsproj" or ".vbproj" or ".sln" or ".toml";
    }

    private static bool IsReleaseEvidenceFile(string path)
    {
        var name = Path.GetFileName(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return IsSourceManifest(name)
               || name.EndsWith(".lock.json", StringComparison.OrdinalIgnoreCase)
               || ext is ".dll" or ".exe" or ".zip" or ".nupkg"
               || ext == ".json" && name.Contains("manifest", StringComparison.OrdinalIgnoreCase);
    }

    private static string HashTree(IEnumerable<(string Path, string Sha256)> entries)
    {
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var entry in entries.OrderBy(x => x.Path, StringComparer.Ordinal))
        {
            var line = Encoding.UTF8.GetBytes(entry.Path.Replace('\\', '/') + "\0" + entry.Sha256.ToLowerInvariant() + "\n");
            incremental.AppendData(line);
        }
        return Convert.ToHexString(incremental.GetHashAndReset()).ToLowerInvariant();
    }

    private static string HashChain(
        string runtime,
        string os,
        string? sourceCommit,
        string sourceTreeRoot,
        IReadOnlyDictionary<string, string> files)
    {
        using var incremental = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value) => incremental.AppendData(Encoding.UTF8.GetBytes(value + "\n"));
        Add("dip.release-provenance-chain.v2");
        Add(runtime);
        Add(os);
        Add(sourceCommit ?? "unknown");
        Add(sourceTreeRoot);
        foreach (var pair in files
                     .Where(x => !x.Key.Equals("@dip/provenance-chain-sha256", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(x => x.Key, StringComparer.Ordinal))
            Add(pair.Key.Replace('\\', '/') + "\0" + pair.Value);
        return Convert.ToHexString(incremental.GetHashAndReset()).ToLowerInvariant();
    }

    private static string? ResolveGitHead(string root, string allowedRoot)
    {
        try
        {
            var dotGit = Path.Combine(root, ".git");
            string gitDirectory;
            if (Directory.Exists(dotGit))
            {
                gitDirectory = Path.GetFullPath(dotGit);
            }
            else if (File.Exists(dotGit))
            {
                var text = File.ReadAllText(dotGit).Trim();
                if (!text.StartsWith("gitdir:", StringComparison.OrdinalIgnoreCase))
                    return null;
                var value = text[7..].Trim();
                gitDirectory = Path.IsPathRooted(value)
                    ? Path.GetFullPath(value)
                    : Path.GetFullPath(Path.Combine(root, value));
            }
            else
            {
                return null;
            }

            EnsureInsideRoot(gitDirectory, allowedRoot);
            var headPath = Path.Combine(gitDirectory, "HEAD");
            if (!File.Exists(headPath)) return null;
            var head = File.ReadAllText(headPath).Trim();
            if (!head.StartsWith("ref:", StringComparison.OrdinalIgnoreCase))
                return IsGitSha(head) ? head.ToLowerInvariant() : null;

            var reference = head[4..].Trim().Replace('/', Path.DirectorySeparatorChar);
            if (reference.Contains("..", StringComparison.Ordinal))
                return null;
            var refPath = Path.GetFullPath(Path.Combine(gitDirectory, reference));
            EnsureInsideRoot(refPath, allowedRoot);
            if (File.Exists(refPath))
            {
                var value = File.ReadAllText(refPath).Trim();
                return IsGitSha(value) ? value.ToLowerInvariant() : null;
            }

            var packed = Path.Combine(gitDirectory, "packed-refs");
            if (File.Exists(packed))
            {
                var refName = head[4..].Trim();
                foreach (var line in File.ReadLines(packed))
                {
                    if (line.Length < 42 || line[0] is '#' or '^') continue;
                    var split = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (split.Length == 2 && split[1].Equals(refName, StringComparison.Ordinal) && IsGitSha(split[0]))
                        return split[0].ToLowerInvariant();
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
        return null;
    }

    private static bool IsGitSha(string value)
        => value.Length is 40 or 64 && value.All(Uri.IsHexDigit);

    private static string Hash(string file)
    {
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string? SafeHash(string file)
    {
        try { return Hash(file); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private static string Relative(string root, string file)
        => Path.GetRelativePath(root, file).Replace('\\', '/');

    private static void EnsureInsideRoot(string candidate, string root)
    {
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative))
            throw new UnauthorizedAccessException("Release provenance path must stay inside configured ReleaseProvenance:AllowedRoot.");
    }
}
