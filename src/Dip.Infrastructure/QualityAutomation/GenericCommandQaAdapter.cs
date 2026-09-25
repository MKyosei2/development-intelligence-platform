// Purpose: Implements Generic Command QA Adapter infrastructure responsibilities for quality automation.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class GenericCommandQaAdapter(ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer, IUniversalSemanticAnalyzer semantic) : IQaAdapter
{
    public GenericCommandQaAdapter(ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer) : this(executor, normalizer, new UniversalSemanticAnalyzer()) { }
    private static readonly HashSet<LanguageId> Supported = new() { LanguageId.JavaScript, LanguageId.TypeScript, LanguageId.Java, LanguageId.Kotlin, LanguageId.Go, LanguageId.Rust, LanguageId.Lua, LanguageId.Ruby, LanguageId.Php, LanguageId.Swift, LanguageId.ObjectiveC, LanguageId.Bash, LanguageId.PowerShell, LanguageId.Sql, LanguageId.Glsl, LanguageId.C, LanguageId.Cpp };
    public string AdapterId => "builtin-generic"; public int Priority => 100;
    public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace) => Supported.Contains(language);
    public async Task<QaAdapterResult> RunAsync(QaExecutionCommand command, string workspace, string artifactRoot, CancellationToken ct)
    {
        var tools = new List<QaToolExecution>(); var findings = new List<QaFindingCandidate>(); var timeout = TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds, 5, 1800));
        var semanticResult = await semantic.AnalyzeAsync(new(command.Language, workspace), ct); findings.AddRange(semanticResult.Findings);
        tools.Add(new("dip-semantic", true, true, 0, 0, string.Empty, string.Empty, $"DIP semantic analysis scanned {semanticResult.FilesAnalyzed} {command.Language.Value} source file(s).", Version: "1.0"));
        async Task Run(string name, string exe, IEnumerable<string> args, string category = "compile", Severity severity = Severity.Critical)
        {
            var resolved = ProcessExecutor.FindExecutable(exe);
            if (resolved is null)
            {
                tools.Add(new(name, false, false, null, 0, "", "", $"{exe} is unavailable; check skipped."));
                return;
            }
            var r = await QaToolSupport.ExecuteAsync(executor, normalizer, name, resolved, args, workspace, artifactRoot, null, timeout, command.Language, severity, category, ct);
            tools.Add(r.Tool);
            findings.AddRange(r.Findings);
        }
        async Task PerFile(string name, string exe, string[] extensions, Func<string, IEnumerable<string>> args)
        { var files = SourceFiles(workspace, extensions).Take(200).ToArray(); if (files.Length == 0) return; foreach (var f in files) await Run(name, exe, args(f)); }
        var l = command.Language;
        if (l == LanguageId.JavaScript) await PerFile("node-check", "node", [".js", ".mjs", ".cjs"], f => ["--check", f]);
        else if (l == LanguageId.TypeScript)
        {
            if (File.Exists(Path.Combine(workspace, "tsconfig.json"))) await Run("typescript", "tsc", ["--noEmit", "--pretty", "false", "-p", "tsconfig.json"]);
            else { var files = SourceFiles(workspace, [".ts", ".tsx"]).Take(300).ToArray(); if (files.Length > 0) await Run("typescript", "tsc", new[] { "--noEmit", "--pretty", "false", "--target", "ES2020", "--skipLibCheck" }.Concat(files)); }
        }
        else if (l == LanguageId.Go) { await Run("go-test", "go", ["test", "./..."], "compile", Severity.Critical); await Run("go-vet", "go", ["vet", "./..."], "static-analysis", Severity.Warning); }
        else if (l == LanguageId.Rust) { await Run("cargo-check", "cargo", ["check", "--all-targets"]); await Run("cargo-test", "cargo", ["test", "--all"], "test", Severity.Warning); }
        else if (l == LanguageId.Java)
        {
            var files = SourceFiles(workspace, [".java"]).Take(500).ToArray();
            if (files.Length > 0)
            {
                Directory.CreateDirectory(Path.Combine(artifactRoot, "javac-classes"));
                await Run("javac", "javac", new[] { "-Xlint", "-d", Path.Combine(artifactRoot, "javac-classes") }.Concat(files));
            }
        }
        else if (l == LanguageId.Kotlin)
        {
            var files = SourceFiles(workspace, [".kt", ".kts"]).Take(300).ToArray();
            if (files.Length > 0) await Run("kotlinc", "kotlinc", files.Concat(["-d", Path.Combine(artifactRoot, "kotlin.jar")]));
        }
        else if (l == LanguageId.Ruby) await PerFile("ruby-syntax", "ruby", [".rb"], f => ["-c", f]);
        else if (l == LanguageId.Php) await PerFile("php-lint", "php", [".php"], f => ["-l", f]);
        else if (l == LanguageId.Bash) await PerFile("bash-syntax", "bash", [".sh"], f => ["-n", f]);
        else if (l == LanguageId.PowerShell) await PerFile("powershell-parse",
            "pwsh",
            [".ps1"],
            f => ["-NoProfile",
            "-NonInteractive",
            "-Command",
            "$e=$null;$t=$null;[System.Management.Automation.Language.Parser]::ParseFile($args[0],[ref]$t,[ref]$e)|Out-Null;if($e.Count){$e|%{Write-Error $_};exit 1}",
            f]);
        else if (l == LanguageId.Lua) await PerFile("lua-parse", "luac", [".lua"], f => ["-p", f]);
        else if (l == LanguageId.Swift)
        {
            var files = SourceFiles(workspace, [".swift"]).Take(300).ToArray();
            if (files.Length > 0) await Run("swift-typecheck", "swiftc", new[] { "-typecheck" }.Concat(files));
        }
        else if (l == LanguageId.ObjectiveC) await PerFile("clang-objectivec", "clang", [".m", ".mm"], f => ["-fsyntax-only", f]);
        else if (l == LanguageId.C) await PerFile("clang-c", "clang", [".c"], f => ["-fsyntax-only", "-Wall", "-Wextra", f]);
        else if (l == LanguageId.Cpp)
        {
            var solution = Directory.EnumerateFiles(workspace, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
            if (OperatingSystem.IsWindows() && solution is not null) await Run("msbuild-cpp", "msbuild", [solution, "/m", "/p:Configuration=Release"], "compile", Severity.Critical);
            await Run("cppcheck", "cppcheck", ["--enable=warning,performance,portability", "--inline-suppr", "--quiet", workspace], "static-analysis", Severity.Warning);
            var compileDb = Path.Combine(workspace, "compile_commands.json"); var tidy = ProcessExecutor.FindExecutable("clang-tidy");
            if (File.Exists(compileDb) && tidy is not null) foreach (var source in SourceFiles(workspace,
                [".cpp",
                ".cc",
                ".cxx"]).Take(100)) await Run("clang-tidy",
                "clang-tidy",
                [source,
                "-p",
                workspace,
                "--quiet"],
                "static-analysis",
                Severity.Warning);
        }
        else if (l == LanguageId.Glsl) await PerFile("glslang-validator", "glslangValidator", [".glsl", ".vert", ".frag", ".geom"], f => [f]);
        else if (l == LanguageId.Sql) await Run("sqlfluff", "sqlfluff", ["lint", "."], "static-analysis", Severity.Warning);
        if (!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var candidate = Path.GetFullPath(Path.Combine(workspace, command.RuntimeExecutable!)); var prefix = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate)) await Run("generic-runtime", candidate, command.RuntimeArguments ?? Array.Empty<string>(), "runtime");
        }
        return new(l, tools, findings, QaToolSupport.JsonSummary(l, tools, findings));
    }
    private static IEnumerable<string> SourceFiles(string root, IEnumerable<string> extensions)
    {
        var exts = extensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignored = new HashSet<string>(new[] { ".git", "node_modules", "bin", "obj", "artifacts", ".dipqa", "Library", "Temp", "Logs", "Build", "build" }, StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] dirs;
            string[] files;
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
