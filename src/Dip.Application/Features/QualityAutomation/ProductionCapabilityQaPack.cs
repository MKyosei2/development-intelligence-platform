// Purpose: Runs deterministic production-integrity checks and records unavailable or malformed metadata as explicit QA findings.
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation;

/// <summary>
/// Cross-language production integrity checks. Unavailable runtime/reference verification
/// is reported explicitly and is never upgraded to PASS.
/// </summary>
public sealed class ProductionCapabilityQaPack : ICrossCuttingQaPack
{
    private static readonly HashSet<string> ProductionExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".uplugin", ".uproject", ".unity", ".prefab", ".asset", ".mat", ".shader",
        ".ma", ".mb", ".blend", ".fbx", ".abc", ".usd", ".usda", ".usdc",
        ".exr", ".dpx", ".png", ".jpg", ".jpeg", ".wav", ".aif", ".aiff"
    };

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "bin", "obj", "node_modules", "Library", "Temp", "Logs", "Build", "build", "artifacts", ".dipqa"
    };

    private static readonly Regex SequencePattern = new(
        @"^(?<stem>.*?)(?<frame>\d{3,8})(?<ext>\.[^.]+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MayaReferencePattern = new(
        "file\\s+-r[^;]*?\"(?<path>[^\"]+\\.(?:ma|mb))\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string PackId => "production-capability";

    public bool Applies(QaTechnologyProfile profile, string workspace)
    {
        if (profile.GameEngines.Count > 0 || File.Exists(Path.Combine(workspace, ".dip", "production.json")))
            return true;
        try
        {
            return EnumerateFiles(workspace).Take(4000).Any(path => ProductionExtensions.Contains(Path.GetExtension(path)));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task<QaCrossCuttingResult> RunAsync(
        QaExecutionCommand command,
        QaTechnologyProfile profile,
        string workspace,
        string artifactRoot,
        CancellationToken ct)
    {
        var findings = new List<QaFindingCandidate>();
        var evidence = new List<string>();
        var files = EnumerateFiles(workspace).Take(25000).ToArray();
        var production = ProductionQaIntelligence.DetectProfile(profile, workspace);

        ValidateManifest();
        await ValidatePluginDescriptorsAsync();
        ValidateUnityAssetMetadata();
        await ValidateMayaReferencesAsync();
        ValidateZeroByteAssets();
        ValidateFrameSequences();

        var runtimeConfigured = !string.IsNullOrWhiteSpace(command.RuntimeExecutable);
        var manifestPath = Path.Combine(workspace, ".dip", "production.json");
        var referenceConfigured = ProductionQaIntelligence.ReadMediaRegressionSpecs(workspace).Count > 0
                                  || File.Exists(manifestPath) && ReadManifestFlag(manifestPath, "referenceQaConfigured", findings);

        var summaryObject = new
        {
            schema = "dip-production-qa-v1",
            pack = PackId,
            production.Domain,
            production.Genres,
            production.Capabilities,
            production.Evidence,
            staticChecksExecuted = true,
            runtimeQaConfigured = runtimeConfigured,
            referenceQaConfigured = referenceConfigured,
            runtimeStatus = runtimeConfigured ? "configured-for-orchestrated-reproduction" : "not-configured-not-passed",
            referenceStatus = referenceConfigured ? "configured" : "not-configured-not-passed",
            findingCount = findings.Count,
            evidence
        };

        var summary = JsonSerializer.Serialize(summaryObject, new JsonSerializerOptions { WriteIndented = true });
        Directory.CreateDirectory(artifactRoot);
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "production-capability-qa.json"), summary, ct);

        return new QaCrossCuttingResult(
            PackId,
            [new QaToolExecution("dip-production-capability", true, true, 0, 0, "", "", "Deterministic production-integrity checks completed.")],
            findings,
            summary);

        void ValidateManifest()
        {
            if (!File.Exists(manifestPath))
                return;
            try
            {
                using var _ = JsonDocument.Parse(File.ReadAllText(manifestPath));
                evidence.Add("valid .dip/production.json");
            }
            catch (JsonException ex)
            {
                findings.Add(F("production-profile", Severity.Critical, "Invalid production QA profile", ex.Message, ".dip/production.json", "DIP-PRODUCTION-MANIFEST"));
            }
            catch (IOException ex)
            {
                findings.Add(F("production-profile", Severity.Warning, "Production QA profile could not be read", ex.Message, ".dip/production.json", "DIP-PRODUCTION-MANIFEST-IO"));
            }
            catch (UnauthorizedAccessException ex)
            {
                findings.Add(F("production-profile", Severity.Warning, "Production QA profile could not be read", ex.Message, ".dip/production.json", "DIP-PRODUCTION-MANIFEST-IO"));
            }
        }

        async Task ValidatePluginDescriptorsAsync()
        {
            foreach (var path in files.Where(x => Path.GetExtension(x).Equals(".uplugin", StringComparison.OrdinalIgnoreCase)).Take(200))
            {
                try
                {
                    using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("Modules", out _) && !root.TryGetProperty("CanContainContent", out _))
                        findings.Add(F("plugin-compatibility", Severity.Warning, "Plugin descriptor has no module/content declaration", "Verify that the .uplugin descriptor intentionally contains neither Modules nor CanContainContent.", Rel(path), "DIP-UPLUGIN-DESCRIPTOR"));
                    evidence.Add($"parsed plugin descriptor: {Rel(path)}");
                }
                catch (JsonException ex)
                {
                    findings.Add(F("plugin-compatibility", Severity.Critical, "Invalid Unreal plugin descriptor", ex.Message, Rel(path), "DIP-UPLUGIN-JSON"));
                }
                catch (IOException ex)
                {
                    findings.Add(F("plugin-compatibility", Severity.Warning, "Unreal plugin descriptor could not be read", ex.Message, Rel(path), "DIP-UPLUGIN-IO"));
                }
                catch (UnauthorizedAccessException ex)
                {
                    findings.Add(F("plugin-compatibility", Severity.Warning, "Unreal plugin descriptor could not be read", ex.Message, Rel(path), "DIP-UPLUGIN-IO"));
                }
            }
        }

        void ValidateUnityAssetMetadata()
        {
            var assetsRoot = Path.Combine(workspace, "Assets");
            if (!Directory.Exists(assetsRoot))
                return;
            var unityFiles = files.Where(x => x.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)).Take(18000).ToArray();

            foreach (var asset in unityFiles.Where(x => !x.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).Take(12000))
            {
                if (Directory.Exists(asset))
                    continue;
                if (!File.Exists(asset + ".meta"))
                    findings.Add(F("unity-meta", Severity.Warning, "Unity asset is missing its .meta file", "Missing .meta files can regenerate GUIDs and break serialized references when the project moves between machines or branches.", Rel(asset), "DIP-UNITY-META-MISSING"));
            }

            var guidOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var meta in unityFiles.Where(x => x.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).Take(12000))
            {
                try
                {
                    string? guid = null;
                    using var reader = new StreamReader(meta);
                    for (var i = 0; i < 16 && !reader.EndOfStream; i++)
                    {
                        var line = reader.ReadLine();
                        if (line is null)
                            break;
                        if (line.StartsWith("guid:", StringComparison.OrdinalIgnoreCase))
                        {
                            guid = line[5..].Trim();
                            break;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        findings.Add(F("unity-meta", Severity.Warning, "Unity metadata has no readable GUID", "The .meta file could not provide a GUID in its header and cannot be trusted as reference evidence.", Rel(meta), "DIP-UNITY-META-GUID"));
                        continue;
                    }
                    if (guidOwner.TryGetValue(guid, out var first) && !first.Equals(meta, StringComparison.OrdinalIgnoreCase))
                        findings.Add(F("unity-guid-collision", Severity.Critical, "Duplicate Unity asset GUID", $"The GUID '{guid}' is used by both '{Rel(first)}' and '{Rel(meta)}'. Serialized asset references are ambiguous and the project is not safe to publish.", Rel(meta), "DIP-UNITY-GUID-COLLISION"));
                    else
                        guidOwner[guid] = meta;
                }
                catch (IOException ex)
                {
                    findings.Add(F("unity-meta", Severity.Warning, "Unity metadata could not be read", ex.Message, Rel(meta), "DIP-UNITY-META-IO"));
                }
                catch (UnauthorizedAccessException ex)
                {
                    findings.Add(F("unity-meta", Severity.Warning, "Unity metadata could not be read", ex.Message, Rel(meta), "DIP-UNITY-META-IO"));
                }
            }
            evidence.Add($"validated Unity metadata for {unityFiles.Length} Assets file(s)");
        }

        async Task ValidateMayaReferencesAsync()
        {
            foreach (var path in files.Where(x => Path.GetExtension(x).Equals(".ma", StringComparison.OrdinalIgnoreCase)).Take(100))
            {
                string text;
                try
                {
                    text = await File.ReadAllTextAsync(path, ct);
                }
                catch (IOException ex)
                {
                    findings.Add(F("dcc-reference", Severity.Warning, "Maya scene could not be read", ex.Message, Rel(path), "DIP-MAYA-IO"));
                    continue;
                }
                catch (UnauthorizedAccessException ex)
                {
                    findings.Add(F("dcc-reference", Severity.Warning, "Maya scene could not be read", ex.Message, Rel(path), "DIP-MAYA-IO"));
                    continue;
                }

                foreach (Match match in MayaReferencePattern.Matches(text).Cast<Match>().Take(200))
                {
                    var reference = match.Groups["path"].Value.Replace('/', Path.DirectorySeparatorChar);
                    if (string.IsNullOrWhiteSpace(reference))
                        continue;
                    if (Path.IsPathRooted(reference))
                    {
                        var workspacePrefix = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                        var absolute = Path.GetFullPath(reference);
                        if (!absolute.StartsWith(workspacePrefix, StringComparison.OrdinalIgnoreCase))
                            findings.Add(F("dcc-reference", Severity.Warning, "Maya reference depends on an external absolute path", $"Reference '{reference}' points outside the QA workspace and may break on another artist/build machine.", Rel(path), "DIP-MAYA-ABSOLUTE-REFERENCE"));
                        if (!File.Exists(absolute))
                            findings.Add(F("dcc-reference", Severity.Warning, "Maya absolute reference is missing", $"Reference '{reference}' does not exist on the verification machine.", Rel(path), "DIP-MAYA-REFERENCE"));
                        continue;
                    }
                    var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, reference));
                    if (!File.Exists(resolved))
                        findings.Add(F("dcc-reference", Severity.Warning, "Maya relative reference is missing", $"Reference '{reference}' does not resolve relative to this scene.", Rel(path), "DIP-MAYA-REFERENCE"));
                }
            }
        }

        void ValidateZeroByteAssets()
        {
            foreach (var path in files.Where(x => ProductionExtensions.Contains(Path.GetExtension(x))).Take(10000))
            {
                try
                {
                    if (new FileInfo(path).Length == 0)
                        findings.Add(F("asset-integrity", Severity.Warning, "Zero-byte production asset", "A production asset is empty and cannot be treated as a valid published artifact.", Rel(path), "DIP-ASSET-ZERO-BYTE"));
                }
                catch (IOException ex)
                {
                    findings.Add(F("asset-integrity", Severity.Warning, "Production asset metadata could not be read", ex.Message, Rel(path), "DIP-ASSET-IO"));
                }
                catch (UnauthorizedAccessException ex)
                {
                    findings.Add(F("asset-integrity", Severity.Warning, "Production asset metadata could not be read", ex.Message, Rel(path), "DIP-ASSET-IO"));
                }
            }
        }

        void ValidateFrameSequences()
        {
            var sequenceFiles = files.Where(x => IsFrameExtension(Path.GetExtension(x))).Take(12000)
                .Select(path => (Path: path, Match: SequencePattern.Match(Path.GetFileName(path))))
                .Where(x => x.Match.Success)
                .GroupBy(x => (Dir: Path.GetDirectoryName(x.Path) ?? string.Empty, Stem: x.Match.Groups["stem"].Value, Ext: x.Match.Groups["ext"].Value), StringTupleComparer.OrdinalIgnoreCase)
                .Take(200);
            foreach (var group in sequenceFiles)
            {
                var frames = group.Select(x => int.Parse(x.Match.Groups["frame"].Value)).Distinct().OrderBy(x => x).ToArray();
                if (frames.Length < 3)
                    continue;
                var missing = Enumerable.Range(frames[0], frames[^1] - frames[0] + 1).Except(frames).Take(20).ToArray();
                if (missing.Length > 0)
                    findings.Add(F("frame-sequence", Severity.Warning, "Frame sequence contains gaps", $"Missing frame(s): {string.Join(", ", missing)}. Validate intentional handles/omissions before publish.", Rel(group.First().Path), "DIP-FRAME-SEQUENCE-GAP"));
            }
        }

        string Rel(string path) => Path.GetRelativePath(workspace, path).Replace('\\', '/');
    }

    private static bool ReadManifestFlag(string path, string name, ICollection<QaFindingCandidate> findings)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
        }
        catch (JsonException ex)
        {
            findings.Add(F("production-profile", Severity.Critical, "Invalid production QA profile", ex.Message, ".dip/production.json", "DIP-PRODUCTION-MANIFEST"));
            return false;
        }
        catch (IOException ex)
        {
            findings.Add(F("production-profile", Severity.Warning, "Production QA profile could not be read", ex.Message, ".dip/production.json", "DIP-PRODUCTION-MANIFEST-IO"));
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            findings.Add(F("production-profile", Severity.Warning, "Production QA profile could not be read", ex.Message, ".dip/production.json", "DIP-PRODUCTION-MANIFEST-IO"));
            return false;
        }
    }

    private static bool IsFrameExtension(string extension) =>
        extension.Equals(".exr", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".dpx", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            string[] directories;
            string[] files;
            try
            {
                directories = Directory.GetDirectories(current);
                files = Directory.GetFiles(current);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var file in files)
                yield return file;
            foreach (var directory in directories)
                if (!IgnoredDirectories.Contains(Path.GetFileName(directory)))
                    stack.Push(directory);
        }
    }

    private static QaFindingCandidate F(string category, Severity severity, string title, string message, string file, string rule) =>
        new(category, severity, "dip-production-qa", title, message, file, RuleId: rule, SourceFormat: "dip-production-qa", SourceFormatVersion: "1.0");

    private sealed class StringTupleComparer : IEqualityComparer<(string Dir, string Stem, string Ext)>
    {
        public static readonly StringTupleComparer OrdinalIgnoreCase = new();
        public bool Equals((string Dir, string Stem, string Ext) x, (string Dir, string Stem, string Ext) y) =>
            string.Equals(x.Dir, y.Dir, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Stem, y.Stem, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Ext, y.Ext, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string Dir, string Stem, string Ext) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Dir),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Stem),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Ext));
    }
}
