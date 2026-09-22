// Purpose: Provides infrastructure behavior for this application boundary.
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>
/// Deep polyglot adapter for languages that do not have a more specialized
/// Python/C#/C++/Unity engine. Each capability is optional and evidence records
/// distinguish unavailable infrastructure from product defects.
/// </summary>
public sealed class DeepLanguagePackQaAdapter(
    ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer,
    IUniversalSemanticAnalyzer semantic, IQaCapabilityPlanner planner) : IQaAdapter
{
    private static readonly HashSet<LanguageId> Supported = new()
    {
        LanguageId.JavaScript,LanguageId.TypeScript,LanguageId.Java,LanguageId.Kotlin,
        LanguageId.Go,LanguageId.Rust,LanguageId.Lua,LanguageId.Ruby,LanguageId.Php,
        LanguageId.Swift,LanguageId.ObjectiveC,LanguageId.Bash,LanguageId.PowerShell,
        LanguageId.Sql,LanguageId.Glsl,LanguageId.Hlsl,LanguageId.Gdscript,LanguageId.C
    };
    public string AdapterId => "deep-language-pack";
    public int Priority => 300;
    public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace) => Supported.Contains(language);

    public async Task<QaAdapterResult> RunAsync(QaExecutionCommand command, string workspace, string artifactRoot, CancellationToken ct)
    {
        var tools = new List<QaToolExecution>(); var findings = new List<QaFindingCandidate>(); var timeout = TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds, 5, 1800));
        var semanticResult = await semantic.AnalyzeAsync(new(command.Language, workspace), ct); findings.AddRange(semanticResult.Findings);
        tools.Add(new("dip-semantic", true, true, 0, 0, "", "", $"Structural/data-flow analysis scanned {semanticResult.FilesAnalyzed} source file(s).", Version: "1.0"));

        async Task Run(string name, string exe, IEnumerable<string> args, string category = "compile", Severity severity = Severity.Critical, IReadOnlyDictionary<string, string>? env = null)
        {
            var resolved = ResolveExecutable(workspace, exe);
            if (resolved is null) { tools.Add(new(name, false, false, null, 0, "", "", $"{exe} is unavailable; capability skipped."));
             return;
             }
            var result = await QaToolSupport.ExecuteAsync(executor, normalizer, name, resolved, args, workspace, artifactRoot, env, timeout, command.Language, severity, category, ct);
            tools.Add(result.Tool);
            findings.AddRange(result.Findings);
        }
        async Task PerFile(string name, string exe, string[] extensions, Func<string, IEnumerable<string>> args, string category = "compile", Severity severity = Severity.Critical)
        { foreach (var f in SourceFiles(workspace, extensions).Take(300)) await Run(name, exe, args(f), category, severity); }
        async Task RunLocalSemgrep()
        {
            if (!File.Exists(Path.Combine(workspace, ".semgrep.yml")) && !File.Exists(Path.Combine(workspace, "semgrep.yml"))) return;
            var cfg = File.Exists(Path.Combine(workspace, ".semgrep.yml")) ? ".semgrep.yml" : "semgrep.yml";
            await Run("semgrep", "semgrep", ["scan", "--config", cfg, "--no-git-ignore", "--error", "."], "static-analysis", Severity.Warning);
        }

        var l = command.Language;
        if (l == LanguageId.JavaScript || l == LanguageId.TypeScript)
        {
            if (l == LanguageId.JavaScript) await PerFile("node-check", "node", [".js", ".mjs", ".cjs"], f => ["--check", f]);
            if (l == LanguageId.TypeScript) await Run("typescript", "tsc", File.Exists(Path.Combine(workspace, "tsconfig.json")) ? ["--noEmit", "--pretty", "false", "-p", "tsconfig.json"] : ["--noEmit", "--pretty", "false"], "type-check");
            if (File.Exists(Path.Combine(workspace, "package.json")))
            {
                await Run("npm-lint", "npm", ["run", "lint", "--if-present"], "static-analysis", Severity.Warning);
                await Run("npm-test", "npm", ["test", "--if-present"], "test", Severity.Warning);
                if (PackageContains(workspace, "fast-check")) await Run("property-tests-fast-check", "npm", ["test", "--if-present"], "property-test", Severity.Warning);
            }
        }
        else if (l == LanguageId.Go)
        {
            await Run("go-test", "go", ["test", "./..."], "test", Severity.Warning);
            await Run("go-vet", "go", ["vet", "./..."], "static-analysis", Severity.Warning);
            await Run("go-race", "go", ["test", "-race", "./..."], "race", Severity.Critical);
            if (SourceContains(workspace, "func Fuzz")) await Run("go-fuzz", "go", ["test", "-fuzz=.", "-fuzztime=3s", "./..."], "fuzz", Severity.Critical);
        }
        else if (l == LanguageId.Rust)
        {
            await Run("cargo-check", "cargo", ["check", "--all-targets"]);
            await Run("cargo-clippy", "cargo", ["clippy", "--all-targets", "--", "-D", "warnings"], "static-analysis", Severity.Warning);
            await Run("cargo-test", "cargo", ["test", "--all"], "test", Severity.Warning);
            var fuzzTargets = Directory.Exists(Path.Combine(workspace, "fuzz", "fuzz_targets")) ? Directory.EnumerateFiles(Path.Combine(workspace, "fuzz", "fuzz_targets"), "*.rs").Take(8).ToArray() : [];
            foreach (var target in fuzzTargets) await Run("cargo-fuzz-" + Path.GetFileNameWithoutExtension(target), "cargo", ["fuzz", "run", Path.GetFileNameWithoutExtension(target), "--", "-max_total_time=3"], "fuzz", Severity.Critical);
            if (ProcessExecutor.FindExecutable("cargo-miri") is not null) await Run("cargo-miri", "cargo", ["miri", "test"], "race", Severity.Warning);
        }
        else if (l == LanguageId.Java || l == LanguageId.Kotlin)
        {
            if (File.Exists(Path.Combine(workspace, "pom.xml"))) await Run("maven-test", LocalWrapper(workspace, "mvnw") ?? "mvn", ["-B", "test"], "test", Severity.Warning);
            else if (File.Exists(Path.Combine(workspace, "build.gradle")) || File.Exists(Path.Combine(workspace, "build.gradle.kts"))) await Run("gradle-test", LocalWrapper(workspace, "gradlew") ?? "gradle", ["test", "--no-daemon"], "test", Severity.Warning);
            else if (l == LanguageId.Java)
            {
                var files = SourceFiles(workspace, [".java"]).Take(500).ToArray();
                if (files.Length > 0)
                {
                    Directory.CreateDirectory(Path.Combine(workspace, ".dipqa", "javac"));
                    await Run("javac", "javac", new[] { "-Xlint", "-d", Path.Combine(workspace, ".dipqa", "javac") }.Concat(files));
                }
            }
            else { var files = SourceFiles(workspace, [".kt", ".kts"]).Take(300).ToArray(); if (files.Length > 0) await Run("kotlinc", "kotlinc", files.Concat(["-d", Path.Combine(workspace, ".dipqa", "kotlin.jar")])); }
        }
        else if (l == LanguageId.Ruby)
        {
            await PerFile("ruby-syntax", "ruby", [".rb"], f => ["-c", f]);
            await Run("rubocop", "rubocop", ["--format", "simple"], "static-analysis", Severity.Warning);
            if (File.Exists(Path.Combine(workspace, "Gemfile"))) await Run("rspec", "rspec", [], "test", Severity.Warning);
        }
        else if (l == LanguageId.Php)
        {
            await PerFile("php-lint", "php", [".php"], f => ["-l", f]);
            await Run("phpstan", "phpstan", ["analyse", "--no-progress"], "static-analysis", Severity.Warning);
            if (File.Exists(Path.Combine(workspace, "phpunit.xml")) || File.Exists(Path.Combine(workspace, "phpunit.xml.dist"))) await Run("phpunit", "phpunit", ["--colors=never"], "test", Severity.Warning);
        }
        else if (l == LanguageId.Bash) { await PerFile("bash-syntax", "bash", [".sh"], f => ["-n", f]); await PerFile("shellcheck", "shellcheck", [".sh"], f => ["-f", "gcc", f], "static-analysis", Severity.Warning); }
        else if (l == LanguageId.PowerShell) await PerFile("powershell-parse",
            "pwsh",
            [".ps1"],
            f => ["-NoProfile",
            "-NonInteractive",
            "-Command",
            "$e=$null;$t=$null;[System.Management.Automation.Language.Parser]::ParseFile($args[0],[ref]$t,[ref]$e)|Out-Null;if($e.Count){$e|%{Write-Error $_};exit 1}",
            f]);
        else if (l == LanguageId.Lua)
        {
            await PerFile("lua-parse", "luac", [".lua"], f => ["-p", f]);
            if (Directory.EnumerateFiles(workspace, "*_spec.lua", SearchOption.AllDirectories).Any()) await Run("busted", "busted", [], "test", Severity.Warning);
        }
        else if (l == LanguageId.Swift)
        {
            if (File.Exists(Path.Combine(workspace, "Package.swift")))
            {
                await Run("swift-build", "swift", ["build"], "compile");
                await Run("swift-test", "swift", ["test"], "test", Severity.Warning);
            }
            else
            {
                var files = SourceFiles(workspace, [".swift"]).Take(300).ToArray();
                if (files.Length > 0) await Run("swift-typecheck", "swiftc", new[] { "-typecheck" }.Concat(files), "type-check");
            }
        }
        else if (l == LanguageId.ObjectiveC) await PerFile("clang-objectivec", "clang", [".m", ".mm"], f => ["-fsyntax-only", "-Wall", "-Wextra", f]);
        else if (l == LanguageId.C) { await PerFile("clang-c", "clang", [".c"], f => ["-fsyntax-only", "-Wall", "-Wextra", "-Wpedantic", f]); await Run("cppcheck-c", "cppcheck", ["--enable=warning,performance,portability", "--inline-suppr", "--quiet", "."], "static-analysis", Severity.Warning); }
        else if (l == LanguageId.Glsl) await PerFile("glslang-validator", "glslangValidator", [".glsl", ".vert", ".frag", ".geom", ".comp"], f => [f]);
        else if (l == LanguageId.Hlsl)
        {
            // Entry point/profile are project contracts; never guess them. .dip/qa.json can execute dxc/fxc safely.
            tools.Add(new("hlsl-contract", true, true, 0, 0, "", "", "HLSL detected. Configure entry point/profile in .dip/qa.json; DIP deliberately does not guess shader contracts."));
        }
        else if (l == LanguageId.Gdscript)
        {
            var godot = ProcessExecutor.FindExecutable("godot4") is not null ? "godot4" : "godot";
            if (File.Exists(Path.Combine(workspace, "project.godot"))) await Run("godot-headless", godot, ["--headless", "--path", workspace, "--editor", "--quit"], "game-engine", Severity.Warning);
        }
        else if (l == LanguageId.Sql) await Run("sqlfluff", "sqlfluff", ["lint", "."], "static-analysis", Severity.Warning);

        await RunLocalSemgrep();
        if (!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var candidate = ResolveWorkspaceFile(workspace, command.RuntimeExecutable!); if (candidate is null) findings.Add(new("runtime", Severity.Critical, "deep-language-pack", "Invalid runtime target", "RuntimeExecutable must resolve to a file inside the workspace.", RuleId: "DIP-RUN-PATH"));
            else await Run("runtime-probe", candidate, command.RuntimeArguments ?? Array.Empty<string>(), "runtime", Severity.Critical);
        }
        var pack = planner.Describe(l, new(workspace, [l], [], [], [], []), workspace);
        var summary = JsonSerializer.Serialize(new { language = l.Value, adapter = AdapterId, capabilities = pack.Capabilities.Select(x => new { x.Capability, x.Tool, x.RequiresProjectConfiguration }), tools, semanticFiles = semanticResult.FilesAnalyzed, findingCount = findings.Count });
        return new(l, tools, findings.GroupBy(x => string.Join('|', x.RuleId, x.FilePath, x.Line, x.Message), StringComparer.Ordinal).Select(x => x.First()).Take(2000).ToArray(), summary);
    }

    private static string? ResolveExecutable(string workspace, string executable)
    {
        if (Path.IsPathRooted(executable)) return File.Exists(executable) ? executable : null;
        var local = Path.Combine(workspace, executable);
        if (File.Exists(local)) return local;
        return ProcessExecutor.FindExecutable(executable);
    }
    private static string? ResolveWorkspaceFile(string workspace, string value)
    {
        if (Path.IsPathRooted(value)) return null;
        var root = Path.GetFullPath(workspace);
        var c = Path.GetFullPath(Path.Combine(root, value));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return c.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) && File.Exists(c) ? c : null;
    }
    private static string? LocalWrapper(string workspace, string name)
    {
        foreach (var n in OperatingSystem.IsWindows() ? new[] { name + ".bat", name + ".cmd", name } : new[] { name })
        {
            var p = Path.Combine(workspace, n);
            if (File.Exists(p)) return p;
        }
        return null;
    }
    private static bool PackageContains(string workspace, string text) { var p = Path.Combine(workspace, "package.json"); return File.Exists(p) && File.ReadAllText(p).Contains(text, StringComparison.OrdinalIgnoreCase); }
    private static bool SourceContains(string workspace,
        string text) => SourceFiles(workspace,
        [".go",
        ".rs",
        ".java",
        ".kt",
        ".js",
        ".ts"]).Take(2000).Any(f =>
        {
            try
            {
                return File.ReadAllText(f).Contains(text,
        StringComparison.Ordinal);
            }
            catch { return false; }
        });
    private static IEnumerable<string> SourceFiles(string root, IEnumerable<string> extensions)
    {
        var exts = extensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignored = new HashSet<string>(new[] { ".git", "node_modules", "bin", "obj", "artifacts", ".dipqa", "Library", "Temp", "Logs", "Build", "build", "target", "vendor" }, StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] dirs, files;
            try
            {
                dirs = Directory.GetDirectories(dir);
                files = Directory.GetFiles(dir);
            }
            catch
            {
                continue;
            }
            foreach (var file in files) if (exts.Contains(Path.GetExtension(file))) yield return file;
            foreach (var child in dirs) if (!ignored.Contains(Path.GetFileName(child))) stack.Push(child);
        }
    }
}
