// Purpose: Runs C++ build, sanitizer, test, static-analysis, and runtime QA with compiler-compatible flags.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class CppQaEngine(ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer, IUniversalSemanticAnalyzer semantic) : IQaAdapter
{
    public CppQaEngine(ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer)
        : this(executor, normalizer, new UniversalSemanticAnalyzer()) { }

    public string AdapterId => "cpp";
    public int Priority => 500;

    public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace)
        => language == LanguageId.Cpp && File.Exists(Path.Combine(workspace, "CMakeLists.txt"));

    public async Task<QaAdapterResult> RunAsync(QaExecutionCommand command, string workspace, string artifactRoot, CancellationToken ct)
    {
        var cmake = ProcessExecutor.FindExecutable("cmake")
            ?? throw new FileNotFoundException("CMake was not found in PATH.");

        var tools = new List<QaToolExecution>();
        var findings = new List<QaFindingCandidate>();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds, 5, 1800));

        var semanticResult = await semantic.AnalyzeAsync(new(LanguageId.Cpp, workspace), ct);
        findings.AddRange(semanticResult.Findings);
        tools.Add(new(
            "dip-semantic",
            true,
            true,
            0,
            0,
            string.Empty,
            string.Empty,
            $"DIP semantic analysis scanned {semanticResult.FilesAnalyzed} C/C++ source file(s).",
            Version: "1.0"));

        var sanitizerEnvironment = new Dictionary<string, string>
        {
            ["ASAN_OPTIONS"] = OperatingSystem.IsWindows()
                ? "halt_on_error=1"
                : "detect_leaks=1:halt_on_error=1",
            ["UBSAN_OPTIONS"] = "print_stacktrace=1:halt_on_error=0"
        };

        var build = Path.Combine(workspace, ".dipqa", "asan-build");
        Directory.CreateDirectory(build);

        var compilerFamily = ResolveCompilerFamily();
        var flags = compilerFamily is "msvc" or "clang-cl"
            ? "/W4 /fsanitize=address /Zi"
            : "-Wall -Wextra -Wpedantic -fsanitize=address,undefined -fno-omit-frame-pointer -g";

        var configureArgs = new List<string>
        {
            "-S", workspace,
            "-B", build,
            "-DCMAKE_BUILD_TYPE=RelWithDebInfo",
            $"-DCMAKE_CXX_FLAGS={flags}"
        };

        // On Windows prefer clang-cl only when clang-cl is actually available;
        // otherwise allow CMake/VS to select MSVC and use MSVC-compatible flags.
        if (OperatingSystem.IsWindows()
            && compilerFamily == "clang-cl"
            && ProcessExecutor.FindExecutable("clang-cl") is { } clangCl)
        {
            configureArgs.Add($"-DCMAKE_CXX_COMPILER={clangCl}");
        }

        await Run("cmake-configure", cmake, configureArgs, Severity.Critical, "configure");
        await Run("cmake-build", cmake, ["--build", build, "--config", "RelWithDebInfo", "--parallel"], Severity.Critical, "compile");

        var ctest = ProcessExecutor.FindExecutable("ctest");
        if (ctest is not null)
            await Run("ctest", ctest, ["--test-dir", build, "-C", "RelWithDebInfo", "--output-on-failure"], Severity.Warning, "test", sanitizerEnvironment);
        else
            AddUnavailable("ctest");

        var clangTidy = ProcessExecutor.FindExecutable("clang-tidy");
        if (clangTidy is not null)
            await RunClangTidy(clangTidy, build);
        else
            AddUnavailable("clang-tidy");

        var cppcheck = ProcessExecutor.FindExecutable("cppcheck");
        if (cppcheck is not null)
            await Run("cppcheck", cppcheck, ["--enable=warning,performance,portability", "--inline-suppr", "--quiet", workspace], Severity.Warning, "static-analysis");
        else
            AddUnavailable("cppcheck");

        if (!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var executable = Path.IsPathRooted(command.RuntimeExecutable!)
                ? command.RuntimeExecutable!
                : Path.GetFullPath(Path.Combine(workspace, command.RuntimeExecutable!));
            await Run("cpp-runtime-asan", executable, command.RuntimeArguments ?? Array.Empty<string>(), Severity.Critical, "runtime", sanitizerEnvironment);
        }

        if (!OperatingSystem.IsWindows()
            && string.Equals(Environment.GetEnvironmentVariable("DIP_QA_ENABLE_TSAN"), "1", StringComparison.Ordinal))
        {
            var tsanBuild = Path.Combine(workspace, ".dipqa", "tsan-build");
            Directory.CreateDirectory(tsanBuild);
            var tsanEnv = new Dictionary<string, string> { ["TSAN_OPTIONS"] = "halt_on_error=1:history_size=7" };
            await Run("cmake-configure-tsan", cmake,
                ["-S", workspace, "-B", tsanBuild, "-DCMAKE_BUILD_TYPE=RelWithDebInfo",
                 "-DCMAKE_CXX_FLAGS=-Wall -Wextra -Wpedantic -fsanitize=thread -fno-omit-frame-pointer -g"],
                Severity.Critical, "configure");
            await Run("cmake-build-tsan", cmake,
                ["--build", tsanBuild, "--config", "RelWithDebInfo", "--parallel"],
                Severity.Critical, "compile");
            if (ctest is not null)
                await Run("ctest-tsan", ctest,
                    ["--test-dir", tsanBuild, "-C", "RelWithDebInfo", "--output-on-failure"],
                    Severity.Critical, "race", tsanEnv);
        }

        return new(LanguageId.Cpp, tools, findings, QaToolSupport.JsonSummary(LanguageId.Cpp, tools, findings));

        async Task Run(string name, string executable, IEnumerable<string> args, Severity fallback, string category, IReadOnlyDictionary<string, string>? environment = null)
        {
            var result = await QaToolSupport.ExecuteAsync(
                executor, normalizer, name, executable, args, workspace, artifactRoot,
                environment, timeout, LanguageId.Cpp, fallback, category, ct);
            tools.Add(result.Tool);
            findings.AddRange(result.Findings);
        }

        void AddUnavailable(string name) =>
            tools.Add(new(name, false, false, null, 0, string.Empty, string.Empty, $"{name} is unavailable; optional analysis was skipped."));

        async Task RunClangTidy(string executable, string buildDir)
        {
            var sources = Directory.EnumerateFiles(workspace, "*.*", SearchOption.AllDirectories)
                .Where(x => x.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase)
                         || x.EndsWith(".cc", StringComparison.OrdinalIgnoreCase)
                         || x.EndsWith(".cxx", StringComparison.OrdinalIgnoreCase))
                .Where(x => !x.Contains(Path.DirectorySeparatorChar + ".dipqa" + Path.DirectorySeparatorChar))
                .Take(100)
                .ToArray();

            foreach (var source in sources)
            {
                var result = await QaToolSupport.ExecuteAsync(
                    executor, normalizer, "clang-tidy", executable,
                    [source, "-p", buildDir, "--quiet"],
                    workspace, artifactRoot, null, timeout,
                    LanguageId.Cpp, Severity.Warning, "static-analysis", ct);
                tools.Add(result.Tool);
                findings.AddRange(result.Findings);
            }
        }
    }

    private static string ResolveCompilerFamily()
    {
        if (!OperatingSystem.IsWindows())
            return "gnu-like";

        if (ProcessExecutor.FindExecutable("clang-cl") is not null)
            return "clang-cl";

        return "msvc";
    }
}
