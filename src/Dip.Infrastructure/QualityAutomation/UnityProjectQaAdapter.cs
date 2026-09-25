// Purpose: Implements Unity Project QA Adapter infrastructure responsibilities for quality automation.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// Engine-aware Unity QA. It deliberately takes precedence over the normal
/// dotnet adapter because Unity projects are compiled and tested by the Editor,
/// not by assuming that a standalone .sln/.csproj is authoritative.
/// When a Unity Editor is not installed in the container, DIP still performs
/// repository-local semantic/source analysis instead of returning an empty QA run.
/// </summary>
public sealed class UnityProjectQaAdapter(
    ProcessExecutor executor,
    UniversalDiagnosticNormalizer normalizer,
    IUniversalSemanticAnalyzer semantic) : IQaAdapter
{
    public UnityProjectQaAdapter(ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer) : this(executor, normalizer, new UniversalSemanticAnalyzer()) { }
    public string AdapterId => "unity";
    public int Priority => 800;
    public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace)
        => language == LanguageId.CSharp && profile.GameEngines.Contains("Unity", StringComparer.OrdinalIgnoreCase);

    public async Task<QaAdapterResult> RunAsync(QaExecutionCommand command, string workspace, string artifactRoot, CancellationToken ct)
    {
        var tools = new List<QaToolExecution>();
        var findings = new List<QaFindingCandidate>();
        var unity = FindUnityEditor();
        if (unity is null)
        {
            // A Linux Docker runtime normally does not contain the licensed Unity
            // Editor. Do not silently produce zero findings: retain engine-aware
            // status while still analyzing the checked-out C# source.
            var semanticResult = await semantic.AnalyzeAsync(
                new(LanguageId.CSharp, workspace),
                ct);
            findings.AddRange(semanticResult.Findings);
            tools.Add(new(
                "dip-semantic",
                true,
                true,
                0,
                0,
                string.Empty,
                string.Empty,
                $"Unity Editor is unavailable; DIP semantic analysis scanned {semanticResult.FilesAnalyzed} source file(s).",
                Version: "1.0"));
            tools.Add(new(
                "unity-editmode",
                false,
                false,
                null,
                0,
                string.Empty,
                string.Empty,
                "Unity Editor was not found. Engine EditMode tests were skipped; source-level DIP analysis still ran. Set UNITY_EDITOR_PATH (or UNITY_PATH) on a compatible runner to add Unity Test Framework evidence."));
            return new(
                LanguageId.CSharp,
                tools,
                findings
                    .GroupBy(x => string.Join('|', x.Category, x.FilePath, x.Line, x.RuleId, x.Message), StringComparer.Ordinal)
                    .Select(x => x.First())
                    .Take(500)
                    .ToArray(),
                QaToolSupport.JsonSummary(LanguageId.CSharp, tools, findings));
        }

        Directory.CreateDirectory(artifactRoot);
        var timeout = TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds, 30, 1800));
        var testXml = Path.Combine(artifactRoot, "unity-editmode-results.xml");
        var unityLog = Path.Combine(artifactRoot, "unity-editmode.log");
        var args = new[] { "-batchmode", "-nographics", "-projectPath", workspace, "-runTests", "-testPlatform", "EditMode", "-testResults", testXml, "-logFile", unityLog };
        var result = await QaToolSupport.ExecuteAsync(executor, normalizer, "unity-editmode", unity, args, workspace, artifactRoot, null, timeout, LanguageId.CSharp, Severity.Warning, "test", ct);
        tools.Add(result.Tool); findings.AddRange(result.Findings);

        if (File.Exists(unityLog))
        {
            var text = await File.ReadAllTextAsync(unityLog, ct);
            findings.AddRange(normalizer.NormalizeText(LanguageId.CSharp, "unity-editor", string.Empty, text, result.Process?.TimedOut == true));
        }
        if (File.Exists(testXml)) findings.AddRange(normalizer.NormalizeReportFile(testXml, LanguageId.CSharp, "unity-test-framework"));

        if (!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var runtime = ResolveWorkspaceFile(workspace, command.RuntimeExecutable!);
            if (runtime is null) findings.Add(new("runtime", Severity.Critical, "unity-runtime", "Unity runtime executable is invalid", "RuntimeExecutable must resolve to an existing file inside the QA workspace.", RuleId: "unity-runtime-path"));
            else
            {
                var runtimeResult = await QaToolSupport.ExecuteAsync(executor, normalizer, "unity-runtime", runtime, command.RuntimeArguments ?? Array.Empty<string>(), workspace, artifactRoot, null, timeout, LanguageId.CSharp, Severity.Critical, "runtime", ct);
                tools.Add(runtimeResult.Tool); findings.AddRange(runtimeResult.Findings);
            }
        }

        findings = findings.GroupBy(x => string.Join('|', x.Category, x.FilePath, x.Line, x.RuleId, x.Message), StringComparer.Ordinal).Select(x => x.First()).Take(500).ToList();
        return new(LanguageId.CSharp, tools, findings, QaToolSupport.JsonSummary(LanguageId.CSharp, tools, findings));
    }

    private static string? ResolveWorkspaceFile(string workspace, string value)
    {
        if (Path.IsPathRooted(value)) return null;
        var root = Path.GetFullPath(workspace);
        var candidate = Path.GetFullPath(Path.Combine(root, value));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate) ? candidate : null;
    }

    private static string? FindUnityEditor()
    {
        foreach (var key in new[] { "UNITY_EDITOR_PATH", "UNITY_PATH" })
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value) && File.Exists(value)) return Path.GetFullPath(value);
        }
        var fromPath = ProcessExecutor.FindExecutable(OperatingSystem.IsWindows() ? "Unity.exe" : "Unity");
        if (fromPath is not null) return fromPath;
        if (OperatingSystem.IsWindows())
        {
            var hub = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Unity", "Hub", "Editor");
            try
            {
                return Directory.Exists(hub) ? Directory.EnumerateFiles(hub, "Unity.exe", SearchOption.AllDirectories).OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault() : null;
            }
            catch
            {
                return null;
            }
        }
        if (OperatingSystem.IsMacOS())
        {
            var path = "/Applications/Unity/Hub/Editor";
            try
            {
                return Directory.Exists(path) ? Directory.EnumerateFiles(path, "Unity", SearchOption.AllDirectories).FirstOrDefault(x => x.EndsWith("/Unity.app/Contents/MacOS/Unity", StringComparison.Ordinal)) : null;
            }
            catch
            {
                return null;
            }
        }
        return null;
    }
}
