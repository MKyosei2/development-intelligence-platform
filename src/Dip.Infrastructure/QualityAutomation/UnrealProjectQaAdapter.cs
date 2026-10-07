// Purpose: Executes dedicated Unreal Engine Blueprint, Automation, Cook, Build and Package QA without treating static repository checks as engine-runtime evidence.
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class UnrealProjectQaAdapter(
    ProcessExecutor executor,
    UniversalDiagnosticNormalizer normalizer) : IQaAdapter
{
    public string AdapterId => "unreal";
    public int Priority => 850;

    public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace)
        => language == LanguageId.Cpp
           && profile.GameEngines.Any(x => x.Contains("Unreal", StringComparison.OrdinalIgnoreCase));

    public async Task<QaAdapterResult> RunAsync(
        QaExecutionCommand command,
        string workspace,
        string artifactRoot,
        CancellationToken ct)
    {
        Directory.CreateDirectory(artifactRoot);
        var tools = new List<QaToolExecution>();
        var findings = new List<QaFindingCandidate>();
        var project = Directory.EnumerateFiles(workspace, "*.uproject", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (project is null)
        {
            findings.Add(Finding(
                "unreal-project",
                Severity.Critical,
                "Unreal project descriptor is missing",
                "Repository was detected as Unreal Engine but no root .uproject file exists.",
                ".",
                "DIP-UNREAL-UPROJECT"));
            tools.Add(new("unreal-project-descriptor", true, false, null, 0, "", "", "Root .uproject descriptor is missing."));
            return Result(tools, findings, artifactRoot);
        }

        try
        {
            using var descriptor = JsonDocument.Parse(await File.ReadAllTextAsync(project, ct));
            if (descriptor.RootElement.ValueKind != JsonValueKind.Object)
                throw new JsonException(".uproject root must be a JSON object.");
            tools.Add(new("unreal-project-descriptor", true, true, 0, 0, "", "", ".uproject descriptor JSON is valid."));
        }
        catch (JsonException ex)
        {
            findings.Add(Finding(
                "unreal-project",
                Severity.Critical,
                "Invalid .uproject JSON",
                ex.Message,
                Path.GetFileName(project),
                "DIP-UNREAL-UPROJECT-JSON"));
            tools.Add(new("unreal-project-descriptor", true, false, null, 0, "", "", ".uproject descriptor JSON is invalid."));
            return Result(tools, findings, artifactRoot);
        }

        var editor = ResolveEditorCommand();
        if (editor is null)
        {
            AddUnavailable("unreal-blueprint-compile", "UnrealEditor-Cmd is unavailable.");
            AddUnavailable("unreal-automation", "UnrealEditor-Cmd is unavailable.");
            AddUnavailable("unreal-cook", "UnrealEditor-Cmd is unavailable.");
            AddUnavailable("unreal-build-cook-package", "Unreal AutomationTool is unavailable.");
            findings.AddRange(await ReadExistingEngineFailuresAsync(workspace, ct));
            return Result(tools, findings, artifactRoot);
        }

        var timeout = TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds, 60, 7200));
        await RunEditorAsync(
            "unreal-blueprint-compile",
            [project, "-run=CompileAllBlueprints", "-unattended", "-nop4", "-NullRHI", "-NoSound", "-UTF8Output"],
            "blueprint",
            Severity.Critical);

        await RunEditorAsync(
            "unreal-automation",
            [
                project,
                "-ExecCmds=Automation RunTests Project.; Quit",
                "-TestExit=Automation Test Queue Empty",
                "-ReportExportPath=" + Path.Combine(workspace, ".dipqa", "unreal-automation-report"),
                "-unattended", "-nop4", "-NullRHI", "-NoSound", "-UTF8Output"
            ],
            "test",
            Severity.Critical);

        var platform = ResolveTargetPlatform();
        await RunEditorAsync(
            "unreal-cook",
            [project, "-run=Cook", "-TargetPlatform=" + platform, "-unversioned", "-unattended", "-nop4", "-UTF8Output"],
            "cook",
            Severity.Critical);

        var uat = ResolveUatCommand(editor);
        if (uat is null)
        {
            AddUnavailable("unreal-build-cook-package", "RunUAT/AutomationTool was not found; package QA is NOT_EVALUATED.");
        }
        else
        {
            var archive = Path.Combine(workspace, ".dipqa", "unreal-package");
            Directory.CreateDirectory(archive);
            var args = new[]
            {
                "BuildCookRun",
                "-project=" + project,
                "-noP4",
                "-build",
                "-cook",
                "-stage",
                "-package",
                "-pak",
                "-archive",
                "-archivedirectory=" + archive,
                "-targetplatform=" + platform,
                "-clientconfig=Development",
                "-unattended",
                "-utf8output"
            };
            var execution = await QaToolSupport.ExecuteAsync(
                executor,
                normalizer,
                "unreal-build-cook-package",
                uat,
                args,
                workspace,
                artifactRoot,
                null,
                timeout,
                LanguageId.Cpp,
                Severity.Critical,
                "package",
                ct);
            tools.Add(execution.Tool);
            findings.AddRange(execution.Findings);
        }

        findings.AddRange(await ReadExistingEngineFailuresAsync(workspace, ct));
        findings = findings
            .GroupBy(x => string.Join('|', x.Category, x.FilePath, x.Line, x.RuleId, x.Message), StringComparer.Ordinal)
            .Select(x => x.First())
            .Take(2000)
            .ToList();
        return Result(tools, findings, artifactRoot);

        async Task RunEditorAsync(
            string toolName,
            IReadOnlyList<string> args,
            string category,
            Severity severity)
        {
            var execution = await QaToolSupport.ExecuteAsync(
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
                severity,
                category,
                ct);
            tools.Add(execution.Tool);
            findings.AddRange(execution.Findings);
        }

        void AddUnavailable(string tool, string summary)
            => tools.Add(new(tool, false, false, null, 0, "", "", summary));
    }

    private static QaAdapterResult Result(
        IReadOnlyList<QaToolExecution> tools,
        IReadOnlyList<QaFindingCandidate> findings,
        string artifactRoot)
    {
        var summary = JsonSerializer.Serialize(new
        {
            adapter = "unreal",
            artifactRoot,
            tools = tools.Select(x => new { x.Tool, x.WasAvailable, x.Succeeded, x.ExitCode, x.SandboxJson }),
            findingCount = findings.Count,
            critical = findings.Count(x => x.Severity == Severity.Critical)
        });
        return new(LanguageId.Cpp, tools, findings, summary);
    }

    private static string? ResolveEditorCommand()
    {
        if (SandboxRunnerConfigured())
        {
            var workerCommand = Environment.GetEnvironmentVariable("UNREAL_SANDBOX_EDITOR_COMMAND")?.Trim();
            if (!string.IsNullOrWhiteSpace(workerCommand)) return workerCommand;
            return OperatingSystem.IsWindows() ? "UnrealEditor-Cmd.exe" : "UnrealEditor-Cmd";
        }

        foreach (var key in new[] { "UNREAL_EDITOR_CMD", "UE_EDITOR_CMD", "UNREAL_EDITOR_PATH" })
        {
            var configured = Environment.GetEnvironmentVariable(key)?.Trim();
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

    private static string? ResolveUatCommand(string editor)
    {
        if (SandboxRunnerConfigured())
        {
            var workerCommand = Environment.GetEnvironmentVariable("UNREAL_SANDBOX_UAT_COMMAND")?.Trim();
            if (!string.IsNullOrWhiteSpace(workerCommand)) return workerCommand;
            return OperatingSystem.IsWindows() ? "RunUAT.bat" : "RunUAT.sh";
        }

        foreach (var key in new[] { "UNREAL_UAT_PATH", "UE_UAT_PATH" })
        {
            var configured = Environment.GetEnvironmentVariable(key)?.Trim();
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                return Path.GetFullPath(configured);
        }

        if (Path.IsPathRooted(editor))
        {
            try
            {
                var directory = new FileInfo(editor).Directory;
                while (directory is not null)
                {
                    var candidate = Path.Combine(
                        directory.FullName,
                        "Engine",
                        "Build",
                        "BatchFiles",
                        OperatingSystem.IsWindows() ? "RunUAT.bat" : "RunUAT.sh");
                    if (File.Exists(candidate)) return candidate;
                    var direct = Path.Combine(directory.FullName, "Build", "BatchFiles", OperatingSystem.IsWindows() ? "RunUAT.bat" : "RunUAT.sh");
                    if (File.Exists(direct)) return direct;
                    directory = directory.Parent;
                }
            }
            catch
            {
                // Discovery failure is represented as unavailable, not as a product defect.
            }
        }

        return null;
    }

    private static string ResolveTargetPlatform()
    {
        var configured = Environment.GetEnvironmentVariable("UNREAL_TARGET_PLATFORM")?.Trim();
        if (!string.IsNullOrWhiteSpace(configured) && configured.Length <= 40 && configured.All(x => char.IsLetterOrDigit(x) || x is '_' or '-'))
            return configured;
        if (OperatingSystem.IsWindows()) return "Win64";
        if (OperatingSystem.IsMacOS()) return "Mac";
        return "Linux";
    }

    private static bool SandboxRunnerConfigured()
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DIP_QA_SANDBOX_RUNNER"))
           || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DIP_QA_SANDBOX_IMAGE"));

    private static async Task<IReadOnlyList<QaFindingCandidate>> ReadExistingEngineFailuresAsync(string workspace, CancellationToken ct)
    {
        var findings = new List<QaFindingCandidate>();
        var saved = Path.Combine(workspace, "Saved", "Logs");
        if (!Directory.Exists(saved)) return findings;
        foreach (var log in Directory.EnumerateFiles(saved, "*.log")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Take(10))
        {
            foreach (var line in (await File.ReadAllLinesAsync(log, ct)).TakeLast(50_000))
            {
                if (!line.Contains("Error", StringComparison.OrdinalIgnoreCase)
                    && !line.Contains("Failed", StringComparison.OrdinalIgnoreCase)
                    && !line.Contains("Automation Test Failed", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (line.Contains("Blueprint", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("PackagingResults", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("ShaderCompileWorker", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("DataValidation", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Automation", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(Finding(
                        "unreal-runtime",
                        Severity.Critical,
                        "Unreal Engine failure evidence",
                        line.Trim(),
                        Path.GetRelativePath(workspace, log),
                        "DIP-UNREAL-LOG"));
                }
                if (findings.Count >= 200) break;
            }
        }
        return findings;
    }

    private static QaFindingCandidate Finding(
        string category,
        Severity severity,
        string title,
        string message,
        string file,
        string rule)
        => new(
            category,
            severity,
            "unreal-qa",
            title,
            message,
            file,
            RuleId: rule,
            SourceFormat: "unreal-engine",
            SourceFormatVersion: "2.0");
}
