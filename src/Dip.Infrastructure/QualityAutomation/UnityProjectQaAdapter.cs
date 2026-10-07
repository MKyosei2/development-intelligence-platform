// Purpose: Runs Unity EditMode, PlayMode, scene-smoke, Player-build, semantic, and optional runtime QA with explicit engine evidence.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// Engine-aware Unity QA. Unity projects are compiled/tested by the Editor; when
/// the Editor is unavailable the adapter exposes that boundary explicitly while
/// retaining source-level semantic analysis. Engine-only checks never become PASS
/// from generic C# analysis.
/// </summary>
public sealed class UnityProjectQaAdapter(
    ProcessExecutor executor,
    UniversalDiagnosticNormalizer normalizer,
    IUniversalSemanticAnalyzer semantic) : IQaAdapter
{
    public UnityProjectQaAdapter(ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer)
        : this(executor, normalizer, new UniversalSemanticAnalyzer()) { }

    public string AdapterId => "unity";
    public int Priority => 800;

    public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace)
        => language == LanguageId.CSharp
           && profile.GameEngines.Contains("Unity", StringComparer.OrdinalIgnoreCase);

    public async Task<QaAdapterResult> RunAsync(
        QaExecutionCommand command,
        string workspace,
        string artifactRoot,
        CancellationToken ct)
    {
        Directory.CreateDirectory(artifactRoot);
        var tools = new List<QaToolExecution>();
        var findings = new List<QaFindingCandidate>();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds, 30, 1800));

        var semanticResult = await semantic.AnalyzeAsync(new(LanguageId.CSharp, workspace), ct);
        findings.AddRange(semanticResult.Findings);
        tools.Add(new(
            "dip-semantic",
            true,
            true,
            0,
            0,
            string.Empty,
            string.Empty,
            $"Unity source semantic analysis scanned {semanticResult.FilesAnalyzed} C# file(s).",
            Version: "1.0"));

        var unity = FindUnityEditor();
        if (unity is null)
        {
            foreach (var name in new[] { "unity-editmode", "unity-playmode", "unity-scene-smoke", "unity-player-build" })
            {
                tools.Add(new(
                    name,
                    false,
                    false,
                    null,
                    0,
                    string.Empty,
                    string.Empty,
                    "Unity Editor was not found. Engine execution is NOT_EVALUATED; source-level semantic QA does not satisfy this engine capability."));
            }
            return Final();
        }

        await RunTestPlatformAsync("EditMode", "unity-editmode");
        await RunTestPlatformAsync("PlayMode", "unity-playmode");

        var generatedScript = Path.Combine(workspace, "Assets", "Editor", "__DIPQA_GeneratedRunner.cs");
        var generatedDirectory = Path.GetDirectoryName(generatedScript)!;
        var createdDirectory = !Directory.Exists(generatedDirectory);
        if (File.Exists(generatedScript))
        {
            tools.Add(new(
                "unity-generated-runner",
                true,
                false,
                null,
                0,
                string.Empty,
                string.Empty,
                "Reserved generated QA script path already exists; scene/build checks failed closed to avoid overwriting project source."));
            tools.Add(new("unity-scene-smoke", true, false, null, 0, "", "", "Scene smoke was blocked because the reserved generated-runner path already exists."));
            tools.Add(new("unity-player-build", true, false, null, 0, "", "", "Player Build was blocked because the reserved generated-runner path already exists."));
        }
        else
        {
            try
            {
                Directory.CreateDirectory(generatedDirectory);
                await File.WriteAllTextAsync(generatedScript, GeneratedUnityQaScript, ct);
                await RunSceneSmokeAsync();
                await RunPlayerBuildAsync();
            }
            finally
            {
                try
                {
                    if (File.Exists(generatedScript)) File.Delete(generatedScript);
                    if (createdDirectory && Directory.Exists(generatedDirectory) && !Directory.EnumerateFileSystemEntries(generatedDirectory).Any())
                        Directory.Delete(generatedDirectory);
                }
                catch (IOException ex)
                {
                    tools.Add(new("unity-generated-runner-cleanup", true, false, null, 0, "", "", "Generated Unity QA script cleanup failed: " + ex.Message));
                }
                catch (UnauthorizedAccessException ex)
                {
                    tools.Add(new("unity-generated-runner-cleanup", true, false, null, 0, "", "", "Generated Unity QA script cleanup failed: " + ex.Message));
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var runtime = ResolveWorkspaceFile(workspace, command.RuntimeExecutable!);
            if (runtime is null)
            {
                findings.Add(new(
                    "runtime",
                    Severity.Critical,
                    "unity-runtime",
                    "Unity runtime executable is invalid",
                    "RuntimeExecutable must resolve to an existing file inside the QA workspace.",
                    RuleId: "unity-runtime-path"));
            }
            else
            {
                var runtimeResult = await QaToolSupport.ExecuteAsync(
                    executor,
                    normalizer,
                    "unity-runtime",
                    runtime,
                    command.RuntimeArguments ?? Array.Empty<string>(),
                    workspace,
                    artifactRoot,
                    null,
                    timeout,
                    LanguageId.CSharp,
                    Severity.Critical,
                    "runtime",
                    ct);
                tools.Add(runtimeResult.Tool);
                findings.AddRange(runtimeResult.Findings);
            }
        }

        return Final();

        async Task RunTestPlatformAsync(string platform, string toolName)
        {
            var testXml = Path.Combine(artifactRoot, $"{toolName}-results.xml");
            var unityLog = Path.Combine(artifactRoot, $"{toolName}.log");
            var args = new[]
            {
                "-batchmode", "-nographics", "-projectPath", workspace,
                "-runTests", "-testPlatform", platform,
                "-testResults", testXml,
                "-logFile", unityLog
            };
            var result = await QaToolSupport.ExecuteAsync(
                executor,
                normalizer,
                toolName,
                unity,
                args,
                workspace,
                artifactRoot,
                null,
                timeout,
                LanguageId.CSharp,
                Severity.Warning,
                "test",
                ct);
            tools.Add(result.Tool);
            findings.AddRange(result.Findings);
            await NormalizeUnityArtifactsAsync(unityLog, testXml, result.Process?.TimedOut == true, toolName);
        }

        async Task RunSceneSmokeAsync()
        {
            var output = Path.Combine(artifactRoot, "unity-scene-smoke.json");
            var log = Path.Combine(artifactRoot, "unity-scene-smoke.log");
            var environment = new Dictionary<string, string>
            {
                ["DIP_UNITY_SCENE_SMOKE_OUTPUT"] = output
            };
            var result = await QaToolSupport.ExecuteAsync(
                executor,
                normalizer,
                "unity-scene-smoke",
                unity,
                ["-batchmode", "-nographics", "-projectPath", workspace, "-executeMethod", "Dip.GeneratedQa.Runner.SceneSmoke", "-logFile", log],
                workspace,
                artifactRoot,
                environment,
                timeout,
                LanguageId.CSharp,
                Severity.Critical,
                "game-engine",
                ct);
            tools.Add(result.Tool);
            findings.AddRange(result.Findings);
            await NormalizeUnityArtifactsAsync(log, null, result.Process?.TimedOut == true, "unity-scene-smoke");
            if (!File.Exists(output) && result.Tool.Succeeded)
                findings.Add(new("game-engine", Severity.Critical, "unity-scene-smoke", "Unity scene-smoke evidence is missing", "Generated scene smoke method completed without producing its required evidence artifact.", RuleId: "DIP-UNITY-SCENE-EVIDENCE"));
        }

        async Task RunPlayerBuildAsync()
        {
            var target = ResolveBuildTarget();
            var output = ResolveBuildOutput(artifactRoot, target);
            var report = Path.Combine(artifactRoot, "unity-player-build.json");
            var log = Path.Combine(artifactRoot, "unity-player-build.log");
            var environment = new Dictionary<string, string>
            {
                ["DIP_UNITY_BUILD_OUTPUT"] = output,
                ["DIP_UNITY_BUILD_REPORT"] = report,
                ["DIP_UNITY_BUILD_TARGET"] = target
            };
            var result = await QaToolSupport.ExecuteAsync(
                executor,
                normalizer,
                "unity-player-build",
                unity,
                ["-batchmode", "-nographics", "-projectPath", workspace, "-executeMethod", "Dip.GeneratedQa.Runner.PlayerBuild", "-logFile", log],
                workspace,
                artifactRoot,
                environment,
                timeout,
                LanguageId.CSharp,
                Severity.Critical,
                "game-engine",
                ct);
            tools.Add(result.Tool);
            findings.AddRange(result.Findings);
            await NormalizeUnityArtifactsAsync(log, null, result.Process?.TimedOut == true, "unity-player-build");
            if (!File.Exists(report) && result.Tool.Succeeded)
                findings.Add(new("game-engine", Severity.Critical, "unity-player-build", "Unity Player build evidence is missing", "Generated Player Build completed without producing its required report artifact.", RuleId: "DIP-UNITY-BUILD-EVIDENCE"));
        }

        async Task NormalizeUnityArtifactsAsync(string? logPath, string? reportPath, bool timedOut, string source)
        {
            if (!string.IsNullOrWhiteSpace(logPath) && File.Exists(logPath))
            {
                var text = await File.ReadAllTextAsync(logPath, ct);
                findings.AddRange(normalizer.NormalizeText(LanguageId.CSharp, source, string.Empty, text, timedOut));
            }
            if (!string.IsNullOrWhiteSpace(reportPath) && File.Exists(reportPath))
                findings.AddRange(normalizer.NormalizeReportFile(reportPath, LanguageId.CSharp, "unity-test-framework"));
        }

        QaAdapterResult Final()
        {
            var normalized = findings
                .GroupBy(x => string.Join('|', x.Category, x.FilePath, x.Line, x.RuleId, x.Message), StringComparer.Ordinal)
                .Select(x => x.First())
                .Take(1000)
                .ToArray();
            return new(LanguageId.CSharp, tools, normalized, QaToolSupport.JsonSummary(LanguageId.CSharp, tools, normalized));
        }
    }

    private static string ResolveBuildTarget()
    {
        var configured = Environment.GetEnvironmentVariable("DIP_UNITY_BUILD_TARGET");
        if (!string.IsNullOrWhiteSpace(configured)) return configured.Trim();
        if (OperatingSystem.IsWindows()) return "StandaloneWindows64";
        if (OperatingSystem.IsMacOS()) return "StandaloneOSX";
        return "StandaloneLinux64";
    }

    private static string ResolveBuildOutput(string artifactRoot, string target)
    {
        var configured = Environment.GetEnvironmentVariable("DIP_UNITY_BUILD_OUTPUT");
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
        if (target.Contains("Windows", StringComparison.OrdinalIgnoreCase)) return Path.Combine(artifactRoot, "player", "DipQaPlayer.exe");
        if (target.Contains("Android", StringComparison.OrdinalIgnoreCase)) return Path.Combine(artifactRoot, "player", "DipQaPlayer.apk");
        if (target.Contains("OSX", StringComparison.OrdinalIgnoreCase)) return Path.Combine(artifactRoot, "player", "DipQaPlayer.app");
        if (target.Contains("iOS", StringComparison.OrdinalIgnoreCase)) return Path.Combine(artifactRoot, "player-ios");
        return Path.Combine(artifactRoot, "player", "DipQaPlayer");
    }

    private static string? ResolveWorkspaceFile(string workspace, string value)
    {
        if (Path.IsPathRooted(value)) return null;
        var root = Path.GetFullPath(workspace);
        var candidate = Path.GetFullPath(Path.Combine(root, value));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, comparison) && File.Exists(candidate) ? candidate : null;
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
                return Directory.Exists(hub)
                    ? Directory.EnumerateFiles(hub, "Unity.exe", SearchOption.AllDirectories).OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
                    : null;
            }
            catch { return null; }
        }
        if (OperatingSystem.IsMacOS())
        {
            var path = "/Applications/Unity/Hub/Editor";
            try
            {
                return Directory.Exists(path)
                    ? Directory.EnumerateFiles(path, "Unity", SearchOption.AllDirectories).FirstOrDefault(x => x.EndsWith("/Unity.app/Contents/MacOS/Unity", StringComparison.Ordinal))
                    : null;
            }
            catch { return null; }
        }
        return null;
    }

    private const string GeneratedUnityQaScript = """
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dip.GeneratedQa
{
    public static class Runner
    {
        [Serializable]
        private sealed class SceneSmokeReport
        {
            public string[] scenes;
            public string[] failures;
        }

        [Serializable]
        private sealed class PlayerBuildReport
        {
            public string target;
            public string output;
            public string result;
            public ulong totalSize;
            public double durationSeconds;
        }

        public static void SceneSmoke()
        {
            var output = Environment.GetEnvironmentVariable("DIP_UNITY_SCENE_SMOKE_OUTPUT");
            var scenes = EditorBuildSettings.scenes.Where(x => x.enabled && !string.IsNullOrWhiteSpace(x.path)).Select(x => x.path).ToArray();
            var failures = new System.Collections.Generic.List<string>();
            foreach (var scenePath in scenes)
            {
                try
                {
                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    if (!scene.IsValid() || !scene.isLoaded)
                        failures.Add(scenePath + ": scene did not load as a valid scene");
                }
                catch (Exception ex)
                {
                    failures.Add(scenePath + ": " + ex.GetType().Name + ": " + ex.Message);
                }
            }
            if (!string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllText(output, JsonUtility.ToJson(new SceneSmokeReport { scenes = scenes, failures = failures.ToArray() }, true));
            }
            EditorApplication.Exit(failures.Count == 0 ? 0 : 21);
        }

        public static void PlayerBuild()
        {
            var output = Environment.GetEnvironmentVariable("DIP_UNITY_BUILD_OUTPUT");
            var reportPath = Environment.GetEnvironmentVariable("DIP_UNITY_BUILD_REPORT");
            var targetText = Environment.GetEnvironmentVariable("DIP_UNITY_BUILD_TARGET");
            if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(targetText))
            {
                Debug.LogError("DIP Unity build output/target environment is missing.");
                EditorApplication.Exit(22);
                return;
            }

            try
            {
                var target = (BuildTarget)Enum.Parse(typeof(BuildTarget), targetText, true);
                var scenes = EditorBuildSettings.scenes.Where(x => x.enabled && !string.IsNullOrWhiteSpace(x.path)).Select(x => x.path).ToArray();
                if (scenes.Length == 0)
                    throw new InvalidOperationException("No enabled EditorBuildSettings scenes are available for Player Build.");
                var directory = Path.GetDirectoryName(output);
                if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = output,
                    target = target,
                    options = BuildOptions.StrictMode
                });
                if (!string.IsNullOrWhiteSpace(reportPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                    File.WriteAllText(reportPath, JsonUtility.ToJson(new PlayerBuildReport
                    {
                        target = target.ToString(),
                        output = output,
                        result = build.summary.result.ToString(),
                        totalSize = build.summary.totalSize,
                        durationSeconds = build.summary.totalTime.TotalSeconds
                    }, true));
                }
                EditorApplication.Exit(build.summary.result == BuildResult.Succeeded ? 0 : 23);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(24);
            }
        }
    }
}
#endif
""";
}
