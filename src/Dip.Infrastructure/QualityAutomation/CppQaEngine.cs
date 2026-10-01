// Purpose: Runs C++ build, sanitizer, test, static-analysis, and runtime QA with compiler-compatible flags.
using System.Globalization;
using System.Text.RegularExpressions;
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
        var timeout = ResolveNativeExecutionTimeout(command.TimeoutSeconds);

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

        var compilerFamily = ResolveCompilerFamily();
        var visualStudioToolchain = OperatingSystem.IsWindows() && compilerFamily == "msvc"
            ? VisualStudioToolchainResolver.TryResolve()
            : null;

        if (OperatingSystem.IsWindows() && compilerFamily == "msvc")
            WriteMsvcToolchainEvidence(artifactRoot, visualStudioToolchain);

        var nativeEnvironment = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (visualStudioToolchain is not null)
        {
            foreach (var pair in visualStudioToolchain.Environment)
                nativeEnvironment[pair.Key] = pair.Value;
        }

        var sanitizerEnvironment = BuildSanitizerEnvironment(
            nativeEnvironment,
            OperatingSystem.IsWindows());

        var nativeEnvironmentOrNull = nativeEnvironment.Count == 0
            ? null
            : nativeEnvironment;

        var build = Path.Combine(workspace, ".dipqa", "asan-build");
        ResetQaBuildDirectory(build);

        var flags = compilerFamily is "msvc" or "clang-cl"
            ? "/W4 /fsanitize=address /Zi"
            : "-Wall -Wextra -Wpedantic -fsanitize=address,undefined -fno-omit-frame-pointer -g";

        var configureArgs = new List<string>
        {
            "-S", workspace,
            "-B", build,
            "-DCMAKE_BUILD_TYPE=RelWithDebInfo",
            "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON",
            $"-DCMAKE_CXX_FLAGS={flags}"
        };

        if (OperatingSystem.IsWindows()
            && compilerFamily == "clang-cl"
            && ProcessExecutor.FindExecutable("clang-cl") is { } clangCl)
        {
            configureArgs.Add($"-DCMAKE_CXX_COMPILER={clangCl}");
        }
        else if (OperatingSystem.IsWindows()
                 && compilerFamily == "msvc"
                 && visualStudioToolchain is not null)
        {
            configureArgs.InsertRange(0,
            [
                "-G", visualStudioToolchain.Generator,
                "-A", "x64",
                $"-DCMAKE_GENERATOR_INSTANCE={visualStudioToolchain.InstallationPath}"
            ]);
        }

        var configureTool = await Run(
            "cmake-configure",
            cmake,
            configureArgs,
            Severity.Critical,
            "configure",
            nativeEnvironmentOrNull);

        QaToolExecution? buildTool = null;
        if (configureTool.Succeeded)
        {
            buildTool = await Run(
                "cmake-build",
                cmake,
                ["--build", build, "--config", "RelWithDebInfo", "--parallel"],
                Severity.Critical,
                "compile",
                nativeEnvironmentOrNull);
        }
        else
        {
            AddBlocked("cmake-build", "cmake-build was skipped because cmake-configure failed.");
        }

        // CTest is retained as test-orchestration and sanitizer evidence. When CTest
        // completes successfully, rerunning the same executable through the direct probe
        // is redundant and can create a false product hang if the host process wrapper
        // itself stalls. The direct runtime probe is therefore a fallback for unavailable,
        // failed, or timed-out CTest execution after a successful build.
        var ctest = ProcessExecutor.FindExecutable("ctest");
        QaToolExecution? ctestTool = null;
        if (ctest is null)
        {
            AddUnavailable("ctest");
        }
        else if (!configureTool.Succeeded)
        {
            AddBlocked("ctest", "CTest was skipped because cmake-configure failed.");
        }
        else if (buildTool is null || !buildTool.Succeeded)
        {
            AddBlocked("ctest", "CTest was skipped because the C++ build did not complete successfully.");
        }
        else
        {
            var ctestCaseTimeout = ResolveCtestCaseTimeout(timeout);
            var ctestProcessTimeout = ResolveCtestProcessTimeout(timeout, ctestCaseTimeout);
            ctestTool = await Run(
                "ctest",
                ctest,
                [
                    "--test-dir", build,
                    "-C", "RelWithDebInfo",
                    "--output-on-failure",
                    "--timeout", ((int)ctestCaseTimeout.TotalSeconds).ToString(CultureInfo.InvariantCulture)
                ],
                Severity.Warning,
                "test",
                sanitizerEnvironment,
                ctestProcessTimeout);
        }

        if (configureTool.Succeeded
            && buildTool?.Succeeded == true
            && ShouldRunDirectRuntimeProbe(ctestTool)
            && TryResolvePrimaryExecutable(workspace, build) is { } runtimeExecutable)
        {
            await Run(
                "cpp-runtime-asan-auto",
                runtimeExecutable,
                Array.Empty<string>(),
                Severity.Critical,
                "runtime",
                sanitizerEnvironment,
                timeout);
        }

        // Keep cppcheck after CTest/direct-runtime fallback so native runtime evidence is
        // collected before static analysis while avoiding redundant execution on clean runs.
        var cppcheck = ProcessExecutor.FindExecutable("cppcheck");
        if (cppcheck is not null)
            await Run(
                "cppcheck",
                cppcheck,
                ["--enable=warning,performance,portability", "--force", "--inline-suppr", "--quiet", workspace],
                Severity.Warning,
                "static-analysis");
        else
            AddUnavailable("cppcheck");

        var clangTidy = ProcessExecutor.FindExecutable("clang-tidy");
        if (clangTidy is not null && configureTool.Succeeded)
            await RunClangTidy(clangTidy, build);
        else if (clangTidy is null)
            AddUnavailable("clang-tidy");
        else
            AddBlocked("clang-tidy", "clang-tidy was skipped because cmake-configure failed.");

        if (!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var executable = Path.IsPathRooted(command.RuntimeExecutable!)
                ? command.RuntimeExecutable!
                : Path.GetFullPath(Path.Combine(workspace, command.RuntimeExecutable!));
            await Run(
                "cpp-runtime-asan",
                executable,
                command.RuntimeArguments ?? Array.Empty<string>(),
                Severity.Critical,
                "runtime",
                sanitizerEnvironment,
                timeout);
        }

        if (!OperatingSystem.IsWindows()
            && string.Equals(Environment.GetEnvironmentVariable("DIP_QA_ENABLE_TSAN"), "1", StringComparison.Ordinal))
        {
            var tsanBuild = Path.Combine(workspace, ".dipqa", "tsan-build");
            ResetQaBuildDirectory(tsanBuild);
            var tsanEnv = new Dictionary<string, string> { ["TSAN_OPTIONS"] = "halt_on_error=1:history_size=7" };

            var tsanConfigure = await Run(
                "cmake-configure-tsan",
                cmake,
                ["-S", workspace, "-B", tsanBuild, "-DCMAKE_BUILD_TYPE=RelWithDebInfo", "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON", "-DCMAKE_CXX_FLAGS=-Wall -Wextra -Wpedantic -fsanitize=thread -fno-omit-frame-pointer -g"],
                Severity.Critical,
                "configure");

            QaToolExecution? tsanBuildTool = null;
            if (tsanConfigure.Succeeded)
            {
                tsanBuildTool = await Run(
                    "cmake-build-tsan",
                    cmake,
                    ["--build", tsanBuild, "--config", "RelWithDebInfo", "--parallel"],
                    Severity.Critical,
                    "compile");
            }
            else
            {
                AddBlocked("cmake-build-tsan", "ThreadSanitizer build was skipped because its CMake configure step failed.");
            }

            if (ctest is not null && tsanConfigure.Succeeded && tsanBuildTool?.Succeeded == true)
            {
                var ctestCaseTimeout = ResolveCtestCaseTimeout(timeout);
                await Run(
                    "ctest-tsan",
                    ctest,
                    [
                        "--test-dir", tsanBuild,
                        "-C", "RelWithDebInfo",
                        "--output-on-failure",
                        "--timeout", ((int)ctestCaseTimeout.TotalSeconds).ToString(CultureInfo.InvariantCulture)
                    ],
                    Severity.Critical,
                    "race",
                    tsanEnv,
                    ResolveCtestProcessTimeout(timeout, ctestCaseTimeout));
            }
            else if (ctest is not null)
            {
                AddBlocked("ctest-tsan", "ThreadSanitizer tests were skipped because configure/build did not complete successfully.");
            }
        }

        return new(LanguageId.Cpp, tools, findings, QaToolSupport.JsonSummary(LanguageId.Cpp, tools, findings));

        async Task<QaToolExecution> Run(
            string name,
            string executable,
            IEnumerable<string> args,
            Severity fallback,
            string category,
            IReadOnlyDictionary<string, string>? environment = null,
            TimeSpan? toolTimeout = null)
        {
            var result = await QaToolSupport.ExecuteAsync(
                executor,
                normalizer,
                name,
                executable,
                args,
                workspace,
                artifactRoot,
                environment,
                toolTimeout ?? timeout,
                LanguageId.Cpp,
                fallback,
                category,
                ct);

            tools.Add(result.Tool);
            findings.AddRange(result.Findings);
            return result.Tool;
        }

        void AddUnavailable(string name) =>
            tools.Add(new(name, false, false, null, 0, string.Empty, string.Empty, $"{name} is unavailable; optional analysis was skipped."));

        void AddBlocked(string name, string summary) =>
            tools.Add(new(name, true, false, null, 0, string.Empty, string.Empty, summary));

        async Task RunClangTidy(string executable, string buildDir)
        {
            var compileDb = Path.Combine(buildDir, "compile_commands.json");
            if (!File.Exists(compileDb))
            {
                tools.Add(new(
                    "clang-tidy",
                    true,
                    false,
                    null,
                    0,
                    string.Empty,
                    string.Empty,
                    "compile_commands.json was not generated; clang-tidy was not executed."));
                return;
            }

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
                    executor,
                    normalizer,
                    "clang-tidy",
                    executable,
                    [source, "-p", buildDir, "--quiet"],
                    workspace,
                    artifactRoot,
                    nativeEnvironmentOrNull,
                    timeout,
                    LanguageId.Cpp,
                    Severity.Warning,
                    "static-analysis",
                    ct);

                tools.Add(result.Tool);
                findings.AddRange(result.Findings);
            }
        }
    }

    internal static Dictionary<string, string> BuildSanitizerEnvironment(
        IReadOnlyDictionary<string, string> nativeEnvironment,
        bool isWindows)
    {
        var environment = new Dictionary<string, string>(
            nativeEnvironment,
            StringComparer.OrdinalIgnoreCase)
        {
            ["ASAN_OPTIONS"] = isWindows
                ? "halt_on_error=1:abort_on_error=1"
                : "detect_leaks=1:halt_on_error=1:allocator_may_return_null=0",
            ["UBSAN_OPTIONS"] = "print_stacktrace=1:halt_on_error=0"
        };

        if (!isWindows)
            environment["LSAN_OPTIONS"] = "exitcode=23:report_objects=1";

        return environment;
    }
    internal static TimeSpan ResolveNativeExecutionTimeout(int requestedSeconds)
        => TimeSpan.FromSeconds(Math.Clamp(requestedSeconds, 5, 1800));

    internal static TimeSpan ResolveCtestCaseTimeout(TimeSpan requestedBudget)
        => TimeSpan.FromSeconds(Math.Clamp((int)Math.Floor(requestedBudget.TotalSeconds / 4.0), 5, 30));

    internal static TimeSpan ResolveCtestProcessTimeout(TimeSpan requestedBudget, TimeSpan ctestCaseTimeout)
        => TimeSpan.FromSeconds(Math.Max(
            5,
            Math.Min(requestedBudget.TotalSeconds, ctestCaseTimeout.TotalSeconds + 10)));

    // CTest owns the normal sanitizer/test path. The direct runtime probe is a fallback
    // only when CTest was unavailable or did not complete successfully.
    internal static bool ShouldRunDirectRuntimeProbe(QaToolExecution? ctestTool)
        => ctestTool is null
           || !ctestTool.WasAvailable
           || !ctestTool.Succeeded;

    private static void ResetQaBuildDirectory(string build)
    {
        if (Directory.Exists(build))
            Directory.Delete(build, recursive: true);

        Directory.CreateDirectory(build);
    }

    private static void WriteMsvcToolchainEvidence(string artifactRoot, VisualStudioToolchain? toolchain)
    {
        try
        {
            Directory.CreateDirectory(artifactRoot);
            var path = Path.Combine(artifactRoot, "msvc-toolchain.txt");
            var lines = toolchain is null
                ? new[]
                {
                    "Resolved=False",
                    "Reason=Visual Studio with the Desktop C++ toolset could not be resolved by vswhere."
                }
                : new[]
                {
                    "Resolved=True",
                    $"Generator={toolchain.Generator}",
                    $"InstallationPath={toolchain.InstallationPath}",
                    $"DeveloperEnvironmentScript={toolchain.DeveloperEnvironmentScript}",
                    $"DeveloperEnvironmentResolved={toolchain.DeveloperEnvironmentResolved}",
                    $"DeveloperEnvironmentExitCode={(toolchain.DeveloperEnvironmentExitCode?.ToString() ?? "<none>")}",
                    $"DeveloperEnvironmentDetail={toolchain.DeveloperEnvironmentDetail}",
                    $"CompilerPath={(toolchain.CompilerPath ?? "<none>")}",
                    $"CompilerExists={toolchain.CompilerExists}",
                    $"ForwardedVariables={string.Join(",", toolchain.Environment.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}"
                };

            File.WriteAllLines(path, lines);
        }
        catch
        {
            // Diagnostic evidence must never turn a compiler-discovery failure into a
            // separate QA infrastructure failure. The CMake logs remain authoritative.
        }
    }

    private static string? TryResolvePrimaryExecutable(string workspace, string build)
    {
        try
        {
            var cmakeLists = Path.Combine(workspace, "CMakeLists.txt");
            var text = File.ReadAllText(cmakeLists);
            var match = Regex.Match(
                text,
                @"add_executable\s*\(\s*([A-Za-z0-9_.+-]+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!match.Success)
                return null;

            var name = match.Groups[1].Value;
            var candidates = OperatingSystem.IsWindows()
                ? new[]
                {
                    Path.Combine(build, "RelWithDebInfo", name + ".exe"),
                    Path.Combine(build, name + ".exe")
                }
                : new[]
                {
                    Path.Combine(build, name),
                    Path.Combine(build, "RelWithDebInfo", name)
                };

            return candidates.FirstOrDefault(File.Exists);
        }
        catch
        {
            return null;
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
