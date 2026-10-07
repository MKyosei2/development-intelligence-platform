// Purpose: Runs deep polyglot QA with explicit shader/security evidence categories and bounded project-owned tool contracts.
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// Deep polyglot adapter for languages without a more specialized engine. Tool
/// names and categories are intentionally capability-specific so autonomous QA
/// can require shader/security/race evidence without accepting unrelated generic
/// adapter success.
/// </summary>
public sealed class DeepLanguagePackQaAdapter(
    ProcessExecutor executor,
    UniversalDiagnosticNormalizer normalizer,
    IUniversalSemanticAnalyzer semantic,
    IQaCapabilityPlanner planner) : IQaAdapter
{
    private static readonly HashSet<LanguageId> Supported = new()
    {
        LanguageId.JavaScript,
        LanguageId.TypeScript,
        LanguageId.Java,
        LanguageId.Kotlin,
        LanguageId.Go,
        LanguageId.Rust,
        LanguageId.Lua,
        LanguageId.Ruby,
        LanguageId.Php,
        LanguageId.Swift,
        LanguageId.ObjectiveC,
        LanguageId.Bash,
        LanguageId.PowerShell,
        LanguageId.Sql,
        LanguageId.Glsl,
        LanguageId.Hlsl,
        LanguageId.Gdscript,
        LanguageId.C
    };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public string AdapterId => "deep-language-pack";
    public int Priority => 300;

    public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace)
        => Supported.Contains(language);

    public async Task<QaAdapterResult> RunAsync(
        QaExecutionCommand command,
        string workspace,
        string artifactRoot,
        CancellationToken ct)
    {
        Directory.CreateDirectory(artifactRoot);
        var tools = new List<QaToolExecution>();
        var findings = new List<QaFindingCandidate>();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds, 5, 1800));

        var semanticResult = await semantic.AnalyzeAsync(new(command.Language, workspace), ct);
        findings.AddRange(semanticResult.Findings);
        tools.Add(new(
            "dip-semantic",
            true,
            true,
            0,
            0,
            "",
            "",
            $"Structural/data-flow analysis scanned {semanticResult.FilesAnalyzed} source file(s).",
            Version: "1.0"));

        var semanticSecurity = semanticResult.Findings.Where(IsSecurityFinding).ToArray();
        var semanticSecurityCritical = semanticSecurity.Any(x => x.Severity == Severity.Critical);
        tools.Add(new(
            "security-dip-semantic",
            true,
            !semanticSecurityCritical,
            semanticSecurityCritical ? 1 : 0,
            0,
            "",
            "",
            $"Semantic security evidence: {semanticSecurity.Length} relevant finding(s), "
            + $"{semanticSecurity.Count(x => x.Severity == Severity.Critical)} Critical.",
            Version: "1.0"));

        async Task Run(
            string name,
            string exe,
            IEnumerable<string> args,
            string category = "compile",
            Severity severity = Severity.Critical,
            IReadOnlyDictionary<string, string>? env = null)
        {
            var resolved = ResolveExecutable(workspace, exe);
            if (resolved is null)
            {
                tools.Add(new(
                    name,
                    false,
                    false,
                    null,
                    0,
                    "",
                    "",
                    $"{exe} is unavailable; {category} capability is NOT_EVALUATED."));
                return;
            }

            var result = await QaToolSupport.ExecuteAsync(
                executor,
                normalizer,
                name,
                resolved,
                args,
                workspace,
                artifactRoot,
                env,
                timeout,
                command.Language,
                severity,
                category,
                ct);
            tools.Add(result.Tool);
            findings.AddRange(result.Findings);
        }

        async Task PerFile(
            string name,
            string exe,
            string[] extensions,
            Func<string, IEnumerable<string>> args,
            string category = "compile",
            Severity severity = Severity.Critical)
        {
            foreach (var file in SourceFiles(workspace, extensions).Take(300))
                await Run(name, exe, args(file), category, severity);
        }

        async Task RunLocalSemgrep()
        {
            var config = File.Exists(Path.Combine(workspace, ".semgrep.yml"))
                ? ".semgrep.yml"
                : File.Exists(Path.Combine(workspace, "semgrep.yml"))
                    ? "semgrep.yml"
                    : null;
            if (config is null)
            {
                tools.Add(new(
                    "security-semgrep",
                    false,
                    false,
                    null,
                    0,
                    "",
                    "",
                    "No local Semgrep policy file exists; Semgrep security evidence is NOT_EVALUATED."));
                return;
            }

            await Run(
                "security-semgrep",
                "semgrep",
                ["scan", "--config", config, "--no-git-ignore", "--error", "."],
                "security",
                Severity.Warning);
        }

        var language = command.Language;
        if (language == LanguageId.JavaScript || language == LanguageId.TypeScript)
        {
            if (language == LanguageId.JavaScript)
                await PerFile("node-check", "node", [".js", ".mjs", ".cjs"], file => ["--check", file]);

            if (language == LanguageId.TypeScript)
            {
                var arguments = File.Exists(Path.Combine(workspace, "tsconfig.json"))
                    ? new[] { "--noEmit", "--pretty", "false", "-p", "tsconfig.json" }
                    : new[] { "--noEmit", "--pretty", "false" };
                await Run("typescript", "tsc", arguments, "type-check");
            }

            if (File.Exists(Path.Combine(workspace, "package.json")))
            {
                await Run("npm-lint", "npm", ["run", "lint", "--if-present"], "static-analysis", Severity.Warning);
                await Run("npm-test", "npm", ["test", "--if-present"], "test", Severity.Warning);
                if (PackageContains(workspace, "fast-check"))
                    await Run("property-tests-fast-check", "npm", ["test", "--if-present"], "property-test", Severity.Warning);
            }
        }
        else if (language == LanguageId.Go)
        {
            await Run("go-test", "go", ["test", "./..."], "test", Severity.Warning);
            await Run("go-vet", "go", ["vet", "./..."], "static-analysis", Severity.Warning);
            await Run("go-race", "go", ["test", "-race", "./..."], "race", Severity.Critical);
            if (SourceContains(workspace, "func Fuzz"))
                await Run("go-fuzz", "go", ["test", "-fuzz=.", "-fuzztime=3s", "./..."], "fuzz", Severity.Critical);
        }
        else if (language == LanguageId.Rust)
        {
            await Run("cargo-check", "cargo", ["check", "--all-targets"]);
            await Run("cargo-clippy", "cargo", ["clippy", "--all-targets", "--", "-D", "warnings"], "static-analysis", Severity.Warning);
            await Run("cargo-test", "cargo", ["test", "--all"], "test", Severity.Warning);

            var fuzzTargets = Directory.Exists(Path.Combine(workspace, "fuzz", "fuzz_targets"))
                ? Directory.EnumerateFiles(Path.Combine(workspace, "fuzz", "fuzz_targets"), "*.rs").Take(8).ToArray()
                : Array.Empty<string>();
            foreach (var target in fuzzTargets)
            {
                await Run(
                    "cargo-fuzz-" + Path.GetFileNameWithoutExtension(target),
                    "cargo",
                    ["fuzz", "run", Path.GetFileNameWithoutExtension(target), "--", "-max_total_time=3"],
                    "fuzz",
                    Severity.Critical);
            }

            if (ProcessExecutor.FindExecutable("cargo-miri") is not null)
                await Run("cargo-miri", "cargo", ["miri", "test"], "race", Severity.Warning);
        }
        else if (language == LanguageId.Java || language == LanguageId.Kotlin)
        {
            if (File.Exists(Path.Combine(workspace, "pom.xml")))
            {
                await Run(
                    "maven-test",
                    LocalWrapper(workspace, "mvnw") ?? "mvn",
                    ["-B", "test"],
                    "test",
                    Severity.Warning);
            }
            else if (File.Exists(Path.Combine(workspace, "build.gradle"))
                     || File.Exists(Path.Combine(workspace, "build.gradle.kts")))
            {
                await Run(
                    "gradle-test",
                    LocalWrapper(workspace, "gradlew") ?? "gradle",
                    ["test", "--no-daemon"],
                    "test",
                    Severity.Warning);
            }
            else if (language == LanguageId.Java)
            {
                var files = SourceFiles(workspace, [".java"]).Take(500).ToArray();
                if (files.Length > 0)
                {
                    var outputDirectory = Path.Combine(workspace, ".dipqa", "javac");
                    Directory.CreateDirectory(outputDirectory);
                    await Run("javac", "javac", new[] { "-Xlint", "-d", outputDirectory }.Concat(files));
                }
            }
            else
            {
                var files = SourceFiles(workspace, [".kt", ".kts"]).Take(300).ToArray();
                if (files.Length > 0)
                {
                    await Run(
                        "kotlinc",
                        "kotlinc",
                        files.Concat(["-d", Path.Combine(workspace, ".dipqa", "kotlin.jar")]));
                }
            }
        }
        else if (language == LanguageId.Ruby)
        {
            await PerFile("ruby-syntax", "ruby", [".rb"], file => ["-c", file]);
            await Run("rubocop", "rubocop", ["--format", "simple"], "static-analysis", Severity.Warning);
            if (File.Exists(Path.Combine(workspace, "Gemfile")))
                await Run("rspec", "rspec", [], "test", Severity.Warning);
        }
        else if (language == LanguageId.Php)
        {
            await PerFile("php-lint", "php", [".php"], file => ["-l", file]);
            await Run("phpstan", "phpstan", ["analyse", "--no-progress"], "static-analysis", Severity.Warning);
            if (File.Exists(Path.Combine(workspace, "phpunit.xml"))
                || File.Exists(Path.Combine(workspace, "phpunit.xml.dist")))
            {
                await Run("phpunit", "phpunit", ["--colors=never"], "test", Severity.Warning);
            }
        }
        else if (language == LanguageId.Bash)
        {
            await PerFile("bash-syntax", "bash", [".sh"], file => ["-n", file]);
            await PerFile(
                "shellcheck",
                "shellcheck",
                [".sh"],
                file => ["-f", "gcc", file],
                "static-analysis",
                Severity.Warning);
        }
        else if (language == LanguageId.PowerShell)
        {
            const string parserCommand =
                "$e=$null;$t=$null;"
                + "[System.Management.Automation.Language.Parser]::ParseFile($args[0],[ref]$t,[ref]$e)|Out-Null;"
                + "if($e.Count){$e|%{Write-Error $_};exit 1}";
            await PerFile(
                "powershell-parse",
                "pwsh",
                [".ps1"],
                file => ["-NoProfile", "-NonInteractive", "-Command", parserCommand, file]);
        }
        else if (language == LanguageId.Lua)
        {
            await PerFile("lua-parse", "luac", [".lua"], file => ["-p", file]);
            if (Directory.EnumerateFiles(workspace, "*_spec.lua", SearchOption.AllDirectories).Any())
                await Run("busted", "busted", [], "test", Severity.Warning);
        }
        else if (language == LanguageId.Swift)
        {
            if (File.Exists(Path.Combine(workspace, "Package.swift")))
            {
                await Run("swift-build", "swift", ["build"], "compile");
                await Run("swift-test", "swift", ["test"], "test", Severity.Warning);
            }
            else
            {
                var files = SourceFiles(workspace, [".swift"]).Take(300).ToArray();
                if (files.Length > 0)
                    await Run("swift-typecheck", "swiftc", new[] { "-typecheck" }.Concat(files), "type-check");
            }
        }
        else if (language == LanguageId.ObjectiveC)
        {
            await PerFile(
                "clang-objectivec",
                "clang",
                [".m", ".mm"],
                file => ["-fsyntax-only", "-Wall", "-Wextra", file]);
        }
        else if (language == LanguageId.C)
        {
            await PerFile(
                "clang-c",
                "clang",
                [".c"],
                file => ["-fsyntax-only", "-Wall", "-Wextra", "-Wpedantic", file]);
            await Run(
                "cppcheck-c",
                "cppcheck",
                ["--enable=warning,performance,portability", "--inline-suppr", "--quiet", "."],
                "static-analysis",
                Severity.Warning);
        }
        else if (language == LanguageId.Glsl)
        {
            await PerFile(
                "shader-glslang",
                "glslangValidator",
                [".glsl", ".vert", ".frag", ".geom", ".comp"],
                file => [file],
                "shader",
                Severity.Critical);
        }
        else if (language == LanguageId.Hlsl)
        {
            await RunHlslContractsAsync();
        }
        else if (language == LanguageId.Gdscript)
        {
            var godot = ProcessExecutor.FindExecutable("godot4") is not null ? "godot4" : "godot";
            if (File.Exists(Path.Combine(workspace, "project.godot")))
            {
                await Run(
                    "godot-headless",
                    godot,
                    ["--headless", "--path", workspace, "--editor", "--quit"],
                    "game-engine",
                    Severity.Warning);
            }
        }
        else if (language == LanguageId.Sql)
        {
            await Run("sqlfluff", "sqlfluff", ["lint", "."], "static-analysis", Severity.Warning);
        }

        await RunLocalSemgrep();

        if (!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var candidate = ResolveWorkspaceFile(workspace, command.RuntimeExecutable!);
            if (candidate is null)
            {
                findings.Add(new(
                    "runtime",
                    Severity.Critical,
                    "deep-language-pack",
                    "Invalid runtime target",
                    "RuntimeExecutable must resolve to a file inside the workspace.",
                    RuleId: "DIP-RUN-PATH"));
            }
            else
            {
                await Run(
                    "runtime-probe",
                    candidate,
                    command.RuntimeArguments ?? Array.Empty<string>(),
                    "runtime",
                    Severity.Critical);
            }
        }

        var pack = planner.Describe(language, new(workspace, [language], [], [], [], []), workspace);
        var summary = JsonSerializer.Serialize(new
        {
            language = language.Value,
            adapter = AdapterId,
            capabilities = pack.Capabilities.Select(x => new
            {
                x.Capability,
                x.Tool,
                x.RequiresProjectConfiguration
            }),
            tools = tools.Select(x => new
            {
                x.Tool,
                x.WasAvailable,
                x.Succeeded,
                x.ExitCode
            }),
            semanticFiles = semanticResult.FilesAnalyzed,
            securityFindingCount = semanticSecurity.Length,
            findingCount = findings.Count
        });
        var normalized = findings
            .GroupBy(
                x => string.Join('|', x.RuleId, x.FilePath, x.Line, x.Message),
                StringComparer.Ordinal)
            .Select(x => x.First())
            .Take(2000)
            .ToArray();
        return new(language, tools, normalized, summary);

        async Task RunHlslContractsAsync()
        {
            var manifestPath = Path.Combine(workspace, ".dip", "shader-contracts.json");
            if (!File.Exists(manifestPath))
            {
                tools.Add(new(
                    "shader-hlsl-contract",
                    false,
                    false,
                    null,
                    0,
                    "",
                    "",
                    "HLSL requires explicit .dip/shader-contracts.json file/entry/profile contracts; "
                    + "DIP does not guess shader entry points."));
                return;
            }

            ShaderContractManifest? manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ShaderContractManifest>(
                    await File.ReadAllTextAsync(manifestPath, ct),
                    Json);
            }
            catch (JsonException ex)
            {
                findings.Add(new(
                    "shader",
                    Severity.Critical,
                    "shader-hlsl-contract",
                    "Invalid HLSL shader contract manifest",
                    ex.Message,
                    ".dip/shader-contracts.json",
                    RuleId: "DIP-HLSL-CONTRACT-JSON"));
                tools.Add(new(
                    "shader-hlsl-contract",
                    true,
                    false,
                    1,
                    0,
                    "",
                    "",
                    "HLSL shader contract manifest is invalid JSON."));
                return;
            }

            var contracts = manifest?.Hlsl?
                .Where(x => !string.IsNullOrWhiteSpace(x.File)
                            && !string.IsNullOrWhiteSpace(x.Entry)
                            && !string.IsNullOrWhiteSpace(x.Profile))
                .Take(200)
                .ToArray()
                ?? Array.Empty<HlslShaderContract>();
            if (contracts.Length == 0)
            {
                tools.Add(new(
                    "shader-hlsl-contract",
                    true,
                    false,
                    null,
                    0,
                    "",
                    "",
                    "HLSL shader contract manifest contains no executable file/entry/profile contracts."));
                return;
            }

            var dxc = ResolveExecutable(workspace, "dxc");
            var fxc = ResolveExecutable(workspace, "fxc");
            if (dxc is null && fxc is null)
            {
                tools.Add(new(
                    "shader-hlsl-compiler",
                    false,
                    false,
                    null,
                    0,
                    "",
                    "",
                    "Neither dxc nor fxc is available; HLSL contracts are NOT_EVALUATED."));
                return;
            }

            foreach (var contract in contracts)
            {
                var file = ResolveWorkspaceFile(workspace, contract.File);
                if (file is null)
                {
                    findings.Add(new(
                        "shader",
                        Severity.Critical,
                        "shader-hlsl-contract",
                        "HLSL shader contract file is missing",
                        $"Configured shader '{contract.File}' does not resolve inside the QA workspace.",
                        contract.File,
                        RuleId: "DIP-HLSL-CONTRACT-PATH"));
                    continue;
                }

                var output = Path.Combine(
                    artifactRoot,
                    "shaders",
                    SafeName(contract.File) + ".bin");
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                if (dxc is not null)
                {
                    await Run(
                        "shader-dxc",
                        dxc,
                        ["-T", contract.Profile, "-E", contract.Entry, file, "-Fo", output],
                        "shader",
                        Severity.Critical);
                }
                else
                {
                    await Run(
                        "shader-fxc",
                        fxc!,
                        ["/T", contract.Profile, "/E", contract.Entry, "/Fo", output, file],
                        "shader",
                        Severity.Critical);
                }
            }
        }
    }

    private static bool IsSecurityFinding(QaFindingCandidate finding)
    {
        var text = $"{finding.Category} {finding.Tool} {finding.RuleId} {finding.Title} {finding.Message}";
        var securityTokens = new[]
        {
            "security",
            "secret",
            "injection",
            "credential",
            "path traversal",
            "unsafe",
            "auth",
            "cve",
            "vulnerability"
        };
        return securityTokens.Any(token => text.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ResolveExecutable(string workspace, string executable)
    {
        if (Path.IsPathRooted(executable))
            return File.Exists(executable) ? executable : null;
        var local = Path.Combine(workspace, executable);
        if (File.Exists(local))
            return local;
        return ProcessExecutor.FindExecutable(executable);
    }

    private static string? ResolveWorkspaceFile(string workspace, string value)
    {
        if (Path.IsPathRooted(value))
            return null;
        var root = Path.GetFullPath(workspace);
        var candidate = Path.GetFullPath(Path.Combine(root, value));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return candidate.StartsWith(prefix, comparison) && File.Exists(candidate)
            ? candidate
            : null;
    }

    private static string? LocalWrapper(string workspace, string name)
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { name + ".bat", name + ".cmd", name }
            : new[] { name };
        foreach (var candidate in candidates)
        {
            var path = Path.Combine(workspace, candidate);
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    private static bool PackageContains(string workspace, string text)
    {
        var path = Path.Combine(workspace, "package.json");
        return File.Exists(path)
               && File.ReadAllText(path).Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SourceContains(string workspace, string text)
    {
        foreach (var file in SourceFiles(workspace, [".go", ".rs", ".java", ".kt", ".js", ".ts"]).Take(2000))
        {
            try
            {
                if (File.ReadAllText(file).Contains(text, StringComparison.Ordinal))
                    return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "Skipping unreadable QA source '{0}': {1}: {2}",
                    file,
                    ex.GetType().Name,
                    ex.Message);
            }
        }
        return false;
    }

    private static IEnumerable<string> SourceFiles(string root, IEnumerable<string> extensions)
    {
        var exts = extensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignored = new HashSet<string>(
            [
                ".git",
                "node_modules",
                "bin",
                "obj",
                "artifacts",
                ".dipqa",
                "Library",
                "Temp",
                "Logs",
                "Build",
                "build",
                "target",
                "vendor"
            ],
            StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var directory = stack.Pop();
            string[] directories;
            string[] files;
            try
            {
                directories = Directory.GetDirectories(directory);
                files = Directory.GetFiles(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceWarning(
                    "Skipping unreadable QA directory '{0}': {1}: {2}",
                    directory,
                    ex.GetType().Name,
                    ex.Message);
                continue;
            }

            foreach (var file in files)
            {
                if (exts.Contains(Path.GetExtension(file)))
                    yield return file;
            }

            foreach (var child in directories)
            {
                if (!ignored.Contains(Path.GetFileName(child)))
                    stack.Push(child);
            }
        }
    }

    private static string SafeName(string value)
    {
        var text = string.Concat(
            value.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));
        return string.IsNullOrWhiteSpace(text)
            ? "shader"
            : text[..Math.Min(100, text.Length)];
    }

    private sealed record ShaderContractManifest(IReadOnlyList<HlslShaderContract>? Hlsl);
    private sealed record HlslShaderContract(string File, string Entry, string Profile);
}
