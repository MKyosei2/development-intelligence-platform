// Purpose: Describes QA capabilities using the same tool names and configuration boundaries that adapters actually execute.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class QaCapabilityPlanner : IQaCapabilityPlanner
{
    public QaLanguagePack Describe(LanguageId language, QaTechnologyProfile profile, string workspace)
    {
        var capabilities = new List<QaCapabilityDescriptor>
        {
            new(QaCapability.AstAnalysis, "dip-semantic", "Language-neutral structural analysis executed by UniversalSemanticAnalyzer."),
            new(QaCapability.DataFlowAnalysis, "dip-semantic", "Bounded source-to-sink analysis executed by UniversalSemanticAnalyzer."),
            new(QaCapability.Security, "security-dip-semantic", "Semantic security findings are emitted as dedicated security evidence."),
            new(QaCapability.DeadlockDetection, "dip-semantic", "Static lock/deadlock-risk analysis; runtime proof requires an engine/language-specific runner."),
            new(QaCapability.LspDiagnostics, "dip-ide-bridge", "Normalized IDE/LSP diagnostic ingestion.", true)
        };

        void Add(QaCapability capability, string tool, string detail, bool config = false)
            => capabilities.Add(new(capability, tool, detail, config));

        if (language == LanguageId.JavaScript)
        {
            Add(QaCapability.Compile, "node-check", "node --check per JavaScript source file.");
            Add(QaCapability.StaticAnalysis, "npm-lint", "npm run lint --if-present.", true);
            Add(QaCapability.UnitTest, "npm-test", "npm test --if-present.", true);
            Add(QaCapability.PropertyTest, "property-tests-fast-check", "Project tests when fast-check is declared.", true);
            Add(QaCapability.Fuzz, "jazzer.js/.dip/qa.json", "Project-owned JavaScript fuzz target.", true);
        }
        else if (language == LanguageId.TypeScript)
        {
            Add(QaCapability.TypeCheck, "typescript", "tsc --noEmit type check.");
            Add(QaCapability.StaticAnalysis, "npm-lint", "npm run lint --if-present.", true);
            Add(QaCapability.UnitTest, "npm-test", "npm test --if-present.", true);
            Add(QaCapability.PropertyTest, "property-tests-fast-check", "Project tests when fast-check is declared.", true);
            Add(QaCapability.Fuzz, "jazzer.js/.dip/qa.json", "Project-owned TypeScript fuzz target.", true);
        }
        else if (language == LanguageId.Go)
        {
            Add(QaCapability.Compile, "go-test", "go test ./... compiles and executes tests.");
            Add(QaCapability.StaticAnalysis, "go-vet", "go vet ./...");
            Add(QaCapability.RaceDetection, "go-race", "go test -race ./...");
            Add(QaCapability.Fuzz, "go-fuzz", "Native Go fuzz targets discovered from func Fuzz declarations.", true);
            Add(QaCapability.PropertyTest, "testing/quick", "Project-owned property tests.", true);
        }
        else if (language == LanguageId.Rust)
        {
            Add(QaCapability.Compile, "cargo-check", "cargo check --all-targets.");
            Add(QaCapability.StaticAnalysis, "cargo-clippy", "cargo clippy --all-targets.");
            Add(QaCapability.UnitTest, "cargo-test", "cargo test --all.");
            Add(QaCapability.Fuzz, "cargo-fuzz-*", "cargo-fuzz targets under fuzz/fuzz_targets.", true);
            Add(QaCapability.RaceDetection, "cargo-miri", "Miri concurrency/undefined-behavior checks when installed.", true);
            Add(QaCapability.PropertyTest, "proptest/quickcheck", "Project-owned Rust property tests.", true);
        }
        else if (language == LanguageId.Java || language == LanguageId.Kotlin)
        {
            Add(QaCapability.Compile, language == LanguageId.Java ? "javac/maven-test/gradle-test" : "kotlinc/gradle-test", "Compiler or project build tool selected from repository structure.");
            Add(QaCapability.StaticAnalysis, "security-semgrep", "Local Semgrep rules when .semgrep.yml/semgrep.yml exists.", true);
            Add(QaCapability.UnitTest, "maven-test/gradle-test", "Project test task when Maven/Gradle is present.", true);
            Add(QaCapability.PropertyTest, "jqwik/Kotest", "Project-owned property tests.", true);
            Add(QaCapability.Fuzz, "Jazzer/.dip/qa.json", "Project-owned JVM fuzz target.", true);
        }
        else if (language == LanguageId.Ruby)
        {
            Add(QaCapability.Compile, "ruby-syntax", "ruby -c per source file.");
            Add(QaCapability.StaticAnalysis, "rubocop", "RuboCop static analysis.", true);
            Add(QaCapability.UnitTest, "rspec", "RSpec when configured.", true);
            Add(QaCapability.PropertyTest, "rantly", "Project-owned property tests.", true);
        }
        else if (language == LanguageId.Php)
        {
            Add(QaCapability.Compile, "php-lint", "php -l per source file.");
            Add(QaCapability.StaticAnalysis, "phpstan", "PHPStan static analysis.", true);
            Add(QaCapability.UnitTest, "phpunit", "PHPUnit when configured.", true);
            Add(QaCapability.PropertyTest, "eris", "Project-owned property tests.", true);
        }
        else if (language == LanguageId.Bash)
        {
            Add(QaCapability.Compile, "bash-syntax", "bash -n per shell script.");
            Add(QaCapability.StaticAnalysis, "shellcheck", "ShellCheck per shell script.", true);
            Add(QaCapability.UnitTest, "bats/.dip/qa.json", "Project-owned shell tests.", true);
        }
        else if (language == LanguageId.PowerShell)
        {
            Add(QaCapability.Compile, "powershell-parse", "PowerShell parser validation.");
            Add(QaCapability.StaticAnalysis, "PSScriptAnalyzer/.dip/qa.json", "Project-owned PSScriptAnalyzer execution.", true);
            Add(QaCapability.UnitTest, "Pester/.dip/qa.json", "Project-owned Pester execution.", true);
        }
        else if (language == LanguageId.Lua)
        {
            Add(QaCapability.Compile, "lua-parse", "luac -p per Lua source file.");
            Add(QaCapability.UnitTest, "busted", "Busted when *_spec.lua tests are present.", true);
            Add(QaCapability.PropertyTest, "lua-quickcheck", "Project-owned property tests.", true);
        }
        else if (language == LanguageId.Swift)
        {
            Add(QaCapability.TypeCheck, "swift-typecheck/swift-build", "swiftc -typecheck or SwiftPM build.");
            Add(QaCapability.UnitTest, "swift-test", "SwiftPM tests when Package.swift exists.", true);
            Add(QaCapability.Fuzz, "libFuzzer/.dip/qa.json", "Project-owned Swift fuzz target.", true);
        }
        else if (language == LanguageId.C || language == LanguageId.ObjectiveC)
        {
            Add(QaCapability.Compile, language == LanguageId.C ? "clang-c" : "clang-objectivec", "clang -fsyntax-only per source file.");
            Add(QaCapability.StaticAnalysis, "cppcheck-c/security-semgrep", "cppcheck or local Semgrep rules.", true);
            Add(QaCapability.Fuzz, "libFuzzer/AFL++/.dip/qa.json", "Project-owned native fuzz target.", true);
            Add(QaCapability.RaceDetection, ".dip/qa.json", "Language/project-specific race detector command.", true);
        }
        else if (language == LanguageId.Cpp)
        {
            Add(QaCapability.Compile, "cmake-configure/cmake-build", "CMake RelWithDebInfo sanitizer build.");
            Add(QaCapability.StaticAnalysis, "cppcheck/clang-tidy", "cppcheck and compile_commands-backed clang-tidy when installed.", true);
            Add(QaCapability.UnitTest, "ctest-asan-ubsan", "CTest under ASan/UBSan instrumentation.", true);
            Add(QaCapability.Runtime, "cpp-memory-safety", "Dedicated ASan/UBSan memory-safety contract.");
            Add(QaCapability.RaceDetection, "cpp-race-detection", "Dedicated ThreadSanitizer contract; requires DIP_QA_ENABLE_TSAN=1 on a compatible host.", true);
            Add(QaCapability.Fuzz, "libFuzzer/AFL++/.dip/qa.json", "Project-owned native fuzz target.", true);
        }
        else if (language == LanguageId.Sql)
        {
            Add(QaCapability.StaticAnalysis, "sqlfluff", "SQLFluff lint.");
            Add(QaCapability.Database, "DIP DB QA", "Migration/query contract checks.", true);
        }
        else if (language == LanguageId.Glsl)
        {
            Add(QaCapability.Compile, "shader-glslang", "glslangValidator shader compilation.");
            Add(QaCapability.GameEngine, "DIP Game QA", "Engine integration checks for shader consumers.", true);
        }
        else if (language == LanguageId.Hlsl)
        {
            Add(QaCapability.Compile, "shader-dxc/shader-fxc", "HLSL compilation from explicit .dip/shader-contracts.json entry/profile contracts.", true);
            Add(QaCapability.GameEngine, "DIP Game QA", "Engine integration checks for shader consumers.", true);
        }
        else if (language == LanguageId.Gdscript)
        {
            Add(QaCapability.Compile, "godot-headless", "Godot headless project/script validation.", true);
            Add(QaCapability.GameEngine, "godot-headless", "Godot engine validation.", true);
        }
        else
        {
            Add(QaCapability.Compile, ".dip/qa.json", "Custom compile command.", true);
            Add(QaCapability.UnitTest, ".dip/qa.json", "Custom test command.", true);
            Add(QaCapability.Fuzz, ".dip/qa.json", "Custom fuzz command.", true);
        }

        if (!capabilities.Any(x => x.Capability == QaCapability.PropertyTest))
            Add(QaCapability.PropertyTest, ".dip/qa.json", "Language/project-specific property-test command.", true);
        if (!capabilities.Any(x => x.Capability == QaCapability.Fuzz))
            Add(QaCapability.Fuzz, ".dip/qa.json", "Language/project-specific fuzz command.", true);
        if (!capabilities.Any(x => x.Capability == QaCapability.RaceDetection))
            Add(QaCapability.RaceDetection, ".dip/qa.json", "Language/project-specific race/concurrency command.", true);

        if (profile.GameEngines.Count > 0)
        {
            var engineTool = profile.GameEngines.Contains("Unity", StringComparer.OrdinalIgnoreCase)
                ? "unity-editmode/unity-playmode/unity-scene-smoke/unity-player-build"
                : profile.GameEngines.Contains("Unreal Engine", StringComparer.OrdinalIgnoreCase)
                    ? "unreal-blueprint-validation/unreal-data-validation"
                    : string.Join('/', profile.GameEngines);
            Add(QaCapability.GameEngine, engineTool, "Engine-aware QA with runtime availability reported explicitly.");
        }

        Add(QaCapability.Runtime, "DIP Runtime Defect Intelligence", "Crash/hang/memory/runtime correlation.", true);
        Add(QaCapability.ApiContract, "DIP API QA", "HTTP/OpenAPI contract checks.", true);
        Add(QaCapability.DistributedSystem, "DIP Distributed QA", "Retry/timeout/idempotency/health checks.", true);

        return new(language, capabilities.DistinctBy(x => (x.Capability, x.Tool)).ToArray());
    }
}
