// Purpose: Runs C++ build, dedicated memory-safety/race contracts, tests, static analysis, and runtime QA with compiler-compatible flags.
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

        Directory.CreateDirectory(artifactRoot);
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

        var sanitizerEnvironment = BuildSanitizerEnvironment(nativeEnvironment, OperatingSystem.IsWindows());
        var nativeEnvironmentOrNull = nativeEnvironment.Count == 0 ? null : nativeEnvironment;

        var build = Path.Combine(workspace, ".dipqa", "asan-build");
        ResetQaBuildDirectory(build);
        var flags = BuildSanitizerCompilerFlags(compilerFamily, OperatingSystem.IsLinux());
        var configureArgs = new List<string>
        {
            "-S", workspace,
            "-B", build,
            "-DCMAKE_BUILD_TYPE=RelWithDebInfo",
            "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON",
            $"-DCMAKE_CXX_FLAGS={flags}"
        };

        var sanitizerLinkerFlags = BuildSanitizerLinkerFlags(compilerFamily, OperatingSystem.IsLinux());
        if (!string.IsNullOrWhiteSpace(sanitizerLinkerFlags))
            configureArgs.Add($"-DCMAKE_EXE_LINKER_FLAGS={sanitizerLinkerFlags}");
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
                "ctest-asan-ubsan",
                ctest,
                [
                    "--test-dir", build,
                    "-C", "RelWithDebInfo",
                    "--output-on-failure",
                    "--timeout", ((int)ctestCaseTimeout.TotalSeconds).ToString(CultureInfo.InvariantCulture)
                ],
                Severity.Warning,
                "memory-safety",
                sanitizerEnvironment,
                ctestProcessTimeout);
        }

        QaToolExecution? runtimeProbe = null;
        if (configureTool.Succeeded
            && buildTool?.Succeeded == true
            && ShouldRunDirectRuntimeProbe(ctestTool)
            && TryResolvePrimaryExecutable(workspace, build) is { } runtimeExecutable)
        {
            runtimeProbe = await Run(
                "cpp-runtime-asan-ubsan-auto",
                runtimeExecutable,
                Array.Empty<string>(),
                Severity.Critical,
                "memory-safety",
                sanitizerEnvironment,
                timeout);
        }

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

        QaToolExecution? explicitRuntime = null;
        if (!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var executable = ResolveWorkspaceRuntime(workspace, command.RuntimeExecutable!);
            if (executable is null)
            {
                findings.Add(new(
                    "memory-safety",
                    Severity.Critical,
                    "cpp-runtime-asan-ubsan",
                    "C++ runtime target is outside the QA workspace",
                    "RuntimeExecutable must resolve to an existing file inside the C++ QA workspace.",
                    RuleId: "DIP-CPP-RUNTIME-PATH"));
            }
            else
            {
                explicitRuntime = await Run(
                    "cpp-runtime-asan-ubsan",
                    executable,
                    command.RuntimeArguments ?? Array.Empty<string>(),
                    Severity.Critical,
                    "memory-safety",
                    sanitizerEnvironment,
                    timeout);
            }
        }

        AddMemorySafetyContract(configureTool, buildTool, ctestTool, runtimeProbe, explicitRuntime);
        await RunRaceContractAsync();

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

        void AddMemorySafetyContract(
            QaToolExecution configure,
            QaToolExecution? buildExecution,
            QaToolExecution? testExecution,
            QaToolExecution? fallbackRuntime,
            QaToolExecution? configuredRuntime)
        {
            var availableExecution = new[] { testExecution, fallbackRuntime, configuredRuntime }
                .Where(x => x is not null && x.WasAvailable)
                .Cast<QaToolExecution>()
                .ToArray();
            var instrumentationReady = configure.Succeeded && buildExecution?.Succeeded == true;
            var relevantCritical = findings.Any(x =>
                x.Severity == Severity.Critical
                && (x.Category.Contains("memory", StringComparison.OrdinalIgnoreCase)
                    || x.Tool.Contains("asan", StringComparison.OrdinalIgnoreCase)
                    || x.Tool.Contains("ubsan", StringComparison.OrdinalIgnoreCase)
                    || (x.Message?.Contains("AddressSanitizer", StringComparison.OrdinalIgnoreCase) ?? false)
                    || (x.Message?.Contains("UndefinedBehaviorSanitizer", StringComparison.OrdinalIgnoreCase) ?? false)));
            var wasAvailable = instrumentationReady && availableExecution.Length > 0;
            var succeeded = wasAvailable && availableExecution.All(x => x.Succeeded) && !relevantCritical;
            tools.Add(new(
                "cpp-memory-safety",
                wasAvailable,
                succeeded,
                succeeded ? 0 : null,
                availableExecution.Sum(x => x.DurationMs),
                string.Empty,
                string.Empty,
                wasAvailable
                    ? $"Dedicated ASan/UBSan memory-safety contract executed through {string.Join(", ", availableExecution.Select(x => x.Tool))}; success={succeeded}."
                    : "Dedicated memory-safety contract is NOT_EVALUATED because sanitizer instrumentation or an executable test/runtime path was unavailable.",
                Version: "asan-ubsan-v2"));
        }

        async Task RunRaceContractAsync()
        {
            if (OperatingSystem.IsWindows())
            {
                tools.Add(new("cpp-race-detection", false, false, null, 0, "", "", "ThreadSanitizer contract is unavailable on the current Windows QA path."));
                return;
            }

            var enabled = string.Equals(Environment.GetEnvironmentVariable("DIP_QA_ENABLE_TSAN"), "1", StringComparison.Ordinal);
            if (!enabled)
            {
                tools.Add(new("cpp-race-detection", false, false, null, 0, "", "", "ThreadSanitizer contract is disabled. Set DIP_QA_ENABLE_TSAN=1 on a compatible runner to produce dedicated race evidence."));
                return;
            }

            var tsanBuild = Path.Combine(workspace, ".dipqa", "tsan-build");
            ResetQaBuildDirectory(tsanBuild);
            var tsanEnv = new Dictionary<string, string>
            {
                ["TSAN_OPTIONS"] = "halt_on_error=1:history_size=7:second_deadlock_stack=1"
            };
            var tsanConfigure = await Run(
                "cmake-configure-tsan",
                cmake,
                ["-S", workspace, "-B", tsanBuild, "-DCMAKE_BUILD_TYPE=RelWithDebInfo", "-DCMAKE_EXPORT_COMPILE_COMMANDS=ON", "-DCMAKE_CXX_FLAGS=-Wall -Wextra -Wpedantic -fsanitize=thread -fno-omit-frame-pointer -g", "-DCMAKE_EXE_LINKER_FLAGS=-fsanitize=thread"],
                Severity.Critical,
                "race");

            QaToolExecution? tsanBuildTool = null;
            QaToolExecution? tsanTestTool = null;
            if (tsanConfigure.Succeeded)
            {
                tsanBuildTool = await Run(
                    "cmake-build-tsan",
                    cmake,
                    ["--build", tsanBuild, "--config", "RelWithDebInfo", "--parallel"],
                    Severity.Critical,
                    "race");
            }
            else
            {
                AddBlocked("cmake-build-tsan", "ThreadSanitizer build was skipped because its CMake configure step failed.");
            }

            if (ctest is not null && tsanConfigure.Succeeded && tsanBuildTool?.Succeeded == true)
            {
                var ctestCaseTimeout = ResolveCtestCaseTimeout(timeout);
                tsanTestTool = await Run(
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

            var available = tsanConfigure.Succeeded && tsanBuildTool?.Succeeded == true && tsanTestTool?.WasAvailable == true;
            var criticalRace = findings.Any(x => x.Severity == Severity.Critical
                && (x.Category.Contains("race", StringComparison.OrdinalIgnoreCase)
                    || x.Tool.Contains("tsan", StringComparison.OrdinalIgnoreCase)
                    || (x.Message?.Contains("ThreadSanitizer", StringComparison.OrdinalIgnoreCase) ?? false)));
            var succeeded = available && tsanTestTool!.Succeeded && !criticalRace;
            tools.Add(new(
                "cpp-race-detection",
                available,
                succeeded,
                tsanTestTool?.ExitCode,
                (tsanBuildTool?.DurationMs ?? 0) + (tsanTestTool?.DurationMs ?? 0),
                string.Empty,
                string.Empty,
                available
                    ? $"Dedicated ThreadSanitizer race contract executed; success={succeeded}."
                    : "ThreadSanitizer was requested but dedicated race evidence could not be completed.",
                Version: "tsan-v2"));
        }

        async Task RunClangTidy(string executable, string buildDir)
        {
            var compileDb = Path.Combine(buildDir, "compile_commands.json");
            if (!File.Exists(compileDb))
            {
                tools.Add(new("clang-tidy", true, false, null, 0, string.Empty, string.Empty, "compile_commands.json was not generated; clang-tidy was not executed."));
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

    internal static string BuildSanitizerCompilerFlags(string compilerFamily, bool isLinux)
    {
        if (compilerFamily is "msvc" or "clang-cl")
            return "/W4 /fsanitize=address /Zi";
        const string common = "-Wall -Wextra -Wpedantic -fsanitize=address,undefined -fno-omit-frame-pointer -g";
        return isLinux ? common + " -fno-pie" : common;
    }

    internal static string? BuildSanitizerLinkerFlags(string compilerFamily, bool isLinux)
    {
        if (!isLinux || compilerFamily is "msvc" or "clang-cl") return null;
        return "-no-pie";
    }

    internal static Dictionary<string, string> BuildSanitizerEnvironment(IReadOnlyDictionary<string, string> nativeEnvironment, bool isWindows)
    {
        var environment = new Dictionary<string, string>(nativeEnvironment, StringComparer.OrdinalIgnoreCase)
        {
            ["ASAN_OPTIONS"] = isWindows
                ? "halt_on_error=1:abort_on_error=1"
                : "detect_leaks=1:halt_on_error=1:allocator_may_return_null=0",
            ["UBSAN_OPTIONS"] = "print_stacktrace=1:halt_on_error=0"
        };
        if (!isWindows) environment["LSAN_OPTIONS"] = "exitcode=23:report_objects=1";
        return environment;
    }

    internal static TimeSpan ResolveNativeExecutionTimeout(int requestedSeconds)
        => TimeSpan.FromSeconds(Math.Clamp(requestedSeconds, 5, 1800));

    internal static TimeSpan ResolveCtestCaseTimeout(TimeSpan requestedBudget)
        => TimeSpan.FromSeconds(Math.Clamp((int)Math.Floor(requestedBudget.TotalSeconds / 4.0), 5, 30));

    internal static TimeSpan ResolveCtestProcessTimeout(TimeSpan requestedBudget, TimeSpan ctestCaseTimeout)
        => TimeSpan.FromSeconds(Math.Max(5, Math.Min(requestedBudget.TotalSeconds, ctestCaseTimeout.TotalSeconds + 10)));

    internal static bool ShouldRunDirectRuntimeProbe(QaToolExecution? ctestTool)
        => ctestTool is null || !ctestTool.WasAvailable || !ctestTool.Succeeded;

    private static void ResetQaBuildDirectory(string build)
    {
        if (Directory.Exists(build)) Directory.Delete(build, recursive: true);
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
            // Diagnostic evidence must never mask the authoritative compiler/tool result.
        }
    }

    private static string? TryResolvePrimaryExecutable(string workspace, string build)
    {
        try
        {
            var cmakeLists = Path.Combine(workspace, "CMakeLists.txt");
            var text = File.ReadAllText(cmakeLists);
            var match = Regex.Match(text, @"add_executable\s*\(\s*([A-Za-z0-9_.+-]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success) return null;
            var name = match.Groups[1].Value;
            var candidates = OperatingSystem.IsWindows()
                ? new[] { Path.Combine(build, "RelWithDebInfo", name + ".exe"), Path.Combine(build, name + ".exe") }
                : new[] { Path.Combine(build, name), Path.Combine(build, "RelWithDebInfo", name) };
            return candidates.FirstOrDefault(File.Exists);
        }
        catch { return null; }
    }

    private static string? ResolveWorkspaceRuntime(string workspace, string value)
    {
        if (Path.IsPathRooted(value)) return null;
        var root = Path.GetFullPath(workspace);
        var candidate = Path.GetFullPath(Path.Combine(root, value));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, comparison) && File.Exists(candidate) ? candidate : null;
    }

    private static string ResolveCompilerFamily()
    {
        if (!OperatingSystem.IsWindows()) return "gnu-like";
        if (ProcessExecutor.FindExecutable("clang-cl") is not null) return "clang-cl";
        return "msvc";
    }
}
