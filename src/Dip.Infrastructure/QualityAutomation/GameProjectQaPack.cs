// Purpose: Runs engine-aware cross-cutting QA while delegating Unreal execution to a dedicated bounded adapter.
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// Repository-level game-engine QA. Unity execution is owned by UnityProjectQaAdapter;
/// Unreal execution is delegated to the dedicated adapter in this file; Godot keeps
/// a bounded descriptor check here. Generic static checks never masquerade as an
/// installed engine runtime.
/// </summary>
public sealed class GameProjectQaPack : ICrossCuttingQaPack
{
    private readonly UnrealProjectQaAdapter _unreal;

    public GameProjectQaPack(ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer)
    {
        _unreal = new UnrealProjectQaAdapter(executor, normalizer);
    }

    public string PackId => "game-project";

    public bool Applies(QaTechnologyProfile profile, string workspace)
        => profile.GameEngines.Count > 0;

    public async Task<QaCrossCuttingResult> RunAsync(
        QaExecutionCommand command,
        QaTechnologyProfile profile,
        string workspace,
        string artifactRoot,
        CancellationToken ct)
    {
        Directory.CreateDirectory(artifactRoot);
        var findings = new List<QaFindingCandidate>();
        var tools = new List<QaToolExecution>();
        var evidence = new List<string>();

        if (profile.GameEngines.Contains("Unity", StringComparer.OrdinalIgnoreCase))
            await UnityStaticAsync();

        if (profile.GameEngines.Contains("Unreal Engine", StringComparer.OrdinalIgnoreCase))
        {
            var result = await _unreal.RunAsync(command, workspace, Path.Combine(artifactRoot, "unreal"), ct);
            tools.AddRange(result.Tools);
            findings.AddRange(result.Findings);
            evidence.AddRange(result.Evidence);
        }

        if (profile.GameEngines.Contains("Godot", StringComparer.OrdinalIgnoreCase))
            await GodotAsync();

        if (tools.Count == 0)
            tools.Add(new("game-project-static", true, true, 0, 0, "", "", $"Engine-aware repository QA completed for {string.Join(", ", profile.GameEngines)}."));

        var summary = JsonSerializer.Serialize(new
        {
            pack = PackId,
            engines = profile.GameEngines,
            evidence,
            tools = tools.Select(x => new { x.Tool, x.WasAvailable, x.Succeeded, x.ExitCode }),
            findingCount = findings.Count
        });
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "game-project-qa.json"), summary, ct);
        return new(PackId, tools, findings, summary);

        async Task UnityStaticAsync()
        {
            var editor = Path.Combine(workspace, "ProjectSettings", "EditorSettings.asset");
            if (File.Exists(editor))
            {
                var text = await File.ReadAllTextAsync(editor, ct);
                if (text.Contains("m_SerializationMode: 0", StringComparison.Ordinal))
                    findings.Add(F("unity-serialization", Severity.Warning, "Force Text serialization is not enabled", "Text serialization improves deterministic asset diff/reference diagnostics; verify this is intentional.", "ProjectSettings/EditorSettings.asset", "DIP-UNITY-SERIALIZATION"));
            }

            var packages = Path.Combine(workspace, "Packages", "manifest.json");
            if (File.Exists(packages))
            {
                var text = await File.ReadAllTextAsync(packages, ct);
                if (text.Contains("com.unity.addressables", StringComparison.OrdinalIgnoreCase)
                    && !Directory.Exists(Path.Combine(workspace, "Assets", "AddressableAssetsData")))
                    findings.Add(F("unity-addressables", Severity.Warning, "Addressables package detected without AddressableAssetsData", "Verify Addressables initialization, profiles, and content build state.", "Packages/manifest.json", "DIP-UNITY-ADDRESSABLES"));
            }

            var logs = Directory.Exists(Path.Combine(workspace, "Logs"))
                ? Directory.EnumerateFiles(Path.Combine(workspace, "Logs"), "*.log").Take(20)
                : Array.Empty<string>();
            foreach (var log in logs)
            {
                var text = await File.ReadAllTextAsync(log, ct);
                if (text.Contains("Shader error", StringComparison.OrdinalIgnoreCase)
                    || text.Contains("Shader compiler", StringComparison.OrdinalIgnoreCase)
                       && text.Contains("error", StringComparison.OrdinalIgnoreCase))
                    findings.Add(F("shader", Severity.Critical, "Unity shader compilation error observed", "A Unity log contains shader compiler error evidence.", Path.GetRelativePath(workspace, log), "DIP-UNITY-SHADER"));
            }
            evidence.Add("Unity: repository static checks; engine execution is owned by UnityProjectQaAdapter");
        }

        async Task GodotAsync()
        {
            var project = Path.Combine(workspace, "project.godot");
            if (!File.Exists(project))
            {
                findings.Add(F("godot-project", Severity.Critical, "project.godot is missing", "Godot was detected but its project descriptor is unavailable.", ".", "DIP-GODOT-PROJECT"));
                return;
            }
            var text = await File.ReadAllTextAsync(project, ct);
            if (!text.Contains("config_version", StringComparison.OrdinalIgnoreCase))
                findings.Add(F("godot-project", Severity.Warning, "Godot config_version not observed", "Verify that project.godot is complete and generated by a supported engine version.", "project.godot", "DIP-GODOT-CONFIG"));
            evidence.Add("Godot: project descriptor static checks");
        }
    }

    private static QaFindingCandidate F(string category, Severity severity, string title, string message, string file, string rule)
        => new(category, severity, "dip-game-qa", title, message, file, RuleId: rule, SourceFormat: "dip-game-qa", SourceFormatVersion: "2.0");

    /// <summary>
    /// Dedicated Unreal runner. It owns descriptor validation, existing log evidence,
    /// Blueprint compilation and DataValidation commandlets when an Editor runtime is
    /// actually available. Missing engine infrastructure is reported as unavailable.
    /// </summary>
    private sealed class UnrealProjectQaAdapter(
        ProcessExecutor executor,
        UniversalDiagnosticNormalizer normalizer)
    {
        public async Task<UnrealQaResult> RunAsync(
            QaExecutionCommand command,
            string workspace,
            string artifactRoot,
            CancellationToken ct)
        {
            Directory.CreateDirectory(artifactRoot);
            var findings = new List<QaFindingCandidate>();
            var tools = new List<QaToolExecution>();
            var evidence = new List<string>();
            var project = Directory.EnumerateFiles(workspace, "*.uproject", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (project is null)
            {
                findings.Add(F("unreal-project", Severity.Critical, "Unreal project descriptor is missing", "Repository was detected as Unreal but no root .uproject was found.", ".", "DIP-UNREAL-UPROJECT"));
                tools.Add(new("unreal-project-descriptor", true, false, null, 0, "", "", "Root .uproject descriptor is missing."));
                return new(tools, findings, evidence);
            }

            try
            {
                using var _ = JsonDocument.Parse(await File.ReadAllTextAsync(project, ct));
                tools.Add(new("unreal-project-descriptor", true, true, 0, 0, "", "", ".uproject descriptor JSON is valid."));
                evidence.Add("Unreal: valid .uproject descriptor");
            }
            catch (JsonException ex)
            {
                findings.Add(F("unreal-project", Severity.Critical, "Invalid .uproject JSON", ex.Message, Path.GetFileName(project), "DIP-UNREAL-UPROJECT-JSON"));
                tools.Add(new("unreal-project-descriptor", true, false, null, 0, "", "", ".uproject descriptor JSON is invalid."));
            }

            await ParseSavedLogsAsync();

            var editor = FindUnrealEditorCmd();
            if (editor is null)
            {
                tools.Add(new(
                    "unreal-editor-runtime",
                    false,
                    false,
                    null,
                    0,
                    "",
                    "",
                    "UnrealEditor-Cmd was not found. Blueprint/DataValidation runtime checks are NOT_EVALUATED; static descriptor/log evidence remains available."));
                return new(tools, findings, evidence);
            }

            var timeout = TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds, 30, 1800));
            await RunEditorAsync(
                "unreal-blueprint-validation",
                [project, "-run=CompileAllBlueprints", "-unattended", "-nop4", "-NullRHI", "-NoSound", "-UTF8Output"],
                "blueprint");
            await RunEditorAsync(
                "unreal-data-validation",
                [project, "-run=DataValidation", "-unattended", "-nop4", "-NullRHI", "-NoSound", "-UTF8Output"],
                "asset-validation");
            return new(tools, findings, evidence);

            async Task RunEditorAsync(string toolName, IReadOnlyList<string> args, string category)
            {
                var result = await QaToolSupport.ExecuteAsync(
                    executor,
                    normalizer,
                    toolName,
                    editor,
                    args,
                    workspace,
                    artifactRoot,
                    null,
                    timeout,
                    LanguageId.Cpp,
                    Severity.Critical,
                    category,
                    ct);
                tools.Add(result.Tool);
                findings.AddRange(result.Findings);
                evidence.Add($"Unreal: {toolName} exit={result.Tool.ExitCode?.ToString() ?? "n/a"}");
            }

            async Task ParseSavedLogsAsync()
            {
                var saved = Path.Combine(workspace, "Saved", "Logs");
                if (!Directory.Exists(saved)) return;
                foreach (var log in Directory.EnumerateFiles(saved, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).Take(10))
                {
                    var lines = (await File.ReadAllLinesAsync(log, ct))
                        .Where(x => Regex.IsMatch(x, "Blueprint.*(?:Error|Compile failed)|PackagingResults.*Error|ShaderCompileWorker.*(?:Error|Failed)|LogNet.*(?:timeout|failure)|DataValidation.*(?:Invalid|Error)", RegexOptions.IgnoreCase))
                        .Take(100);
                    foreach (var line in lines)
                        findings.Add(F("unreal-runtime", Severity.Critical, "Unreal engine failure evidence", line.Trim(), Path.GetRelativePath(workspace, log), "DIP-UNREAL-LOG"));
                }
                evidence.Add("Unreal: Saved/Logs failure correlation");
            }
        }

        private static string? FindUnrealEditorCmd()
        {
            foreach (var key in new[] { "UNREAL_EDITOR_CMD", "UE_EDITOR_CMD", "UNREAL_EDITOR_PATH" })
            {
                var configured = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                    return Path.GetFullPath(configured);
            }
            foreach (var candidate in OperatingSystem.IsWindows()
                         ? new[] { "UnrealEditor-Cmd.exe", "UE4Editor-Cmd.exe" }
                         : new[] { "UnrealEditor-Cmd", "UE4Editor-Cmd" })
            {
                var resolved = ProcessExecutor.FindExecutable(candidate);
                if (resolved is not null) return resolved;
            }
            return null;
        }

        public sealed record UnrealQaResult(
            IReadOnlyList<QaToolExecution> Tools,
            IReadOnlyList<QaFindingCandidate> Findings,
            IReadOnlyList<string> Evidence);
    }
}
