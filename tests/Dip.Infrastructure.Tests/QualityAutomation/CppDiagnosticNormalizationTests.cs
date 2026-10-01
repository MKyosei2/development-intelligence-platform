// Purpose: Locks C++ diagnostic normalization and deterministic native execution against Bug Zoo regressions.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class CppDiagnosticNormalizationTests
{
    private readonly UniversalDiagnosticNormalizer _normalizer = new();

    [Fact]
    public void Cppcheck_out_of_bounds_is_memory_safety()
    {
        const string stderr =
            "/work/main.cpp:2:32: error: Array 'p[2]' accessed at index 4, which is out of bounds. [arrayIndexOutOfBounds]";

        var findings = _normalizer.NormalizeText(LanguageId.Cpp, "cppcheck", string.Empty, stderr);

        findings.Should().ContainSingle(x =>
            x.Category == "memory-safety"
            && x.Severity.ToString() == "Critical"
            && x.RuleId == "arrayIndexOutOfBounds");
    }

    [Fact]
    public void Cppcheck_information_only_does_not_create_product_finding()
    {
        const string stderr =
            "nofile:0:0: information: Too many #ifdef configurations - cppcheck only checks 12 configurations. [toomanyconfigs]";

        var findings = _normalizer.NormalizeText(LanguageId.Cpp, "cppcheck", string.Empty, stderr);

        findings.Should().BeEmpty();
    }

    [Fact]
    public void Ctest_zero_failed_is_not_a_failure()
    {
        const string stdout = "100% tests passed, 0 tests failed out of 1";

        var findings = _normalizer.NormalizeText(LanguageId.Cpp, "ctest", stdout, string.Empty);

        findings.Should().NotContain(x => x.Category == "test");
    }

    [Fact]
    public void Ctest_non_zero_non_timeout_failure_is_a_test_finding()
    {
        const string stdout = "50% tests passed, 1 tests failed out of 2\n1 - unit_test (Failed)";

        var findings = _normalizer.NormalizeText(LanguageId.Cpp, "ctest", stdout, string.Empty);

        findings.Should().Contain(x => x.Category == "test" && x.RuleId == "ctest");
    }

    [Fact]
    public void Ctest_outer_timeout_is_infrastructure_evidence_not_product_hang()
    {
        var findings = _normalizer.NormalizeText(
            LanguageId.Cpp,
            "ctest",
            string.Empty,
            string.Empty,
            timedOut: true);

        findings.Should().BeEmpty();
        QaToolSupport.IsInfrastructureTimeoutEvidence("ctest", true, string.Empty).Should().BeTrue();
    }

    [Fact]
    public void Ctest_internal_timeout_is_infrastructure_evidence_not_product_test_failure()
    {
        const string stdout =
            "1/1 Test #1: clean ...................***Timeout  15.00 sec\n"
            + "0% tests passed, 1 tests failed out of 1";

        var findings = _normalizer.NormalizeText(LanguageId.Cpp, "ctest", stdout, string.Empty);

        findings.Should().BeEmpty();
        QaToolSupport.IsInfrastructureTimeoutEvidence("ctest", false, stdout).Should().BeTrue();
    }

    [Fact]
    public void Direct_runtime_timeout_remains_a_critical_product_hang()
    {
        var findings = _normalizer.NormalizeText(
            LanguageId.Cpp,
            "cpp-runtime-asan-auto",
            string.Empty,
            string.Empty,
            timedOut: true);

        findings.Should().ContainSingle(x =>
            x.Category == "hang"
            && x.Severity.ToString() == "Critical"
            && x.ExceptionType == "Timeout");
    }

    [Fact]
    public void Gcc_array_bounds_warning_is_memory_safety()
    {
        const string stderr =
            "/work/main.cpp:2:34: warning: array subscript 4 is outside array bounds of 'int [2]' [-Warray-bounds]";

        var findings = _normalizer.NormalizeText(LanguageId.Cpp, "cmake-build", string.Empty, stderr);

        findings.Should().Contain(x =>
            x.Category == "memory-safety"
            && x.Severity.ToString() == "Critical"
            && x.RuleId == "-Warray-bounds");
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(15, 15)]
    [InlineData(120, 120)]
    [InlineData(1800, 1800)]
    [InlineData(2400, 1800)]
    public void Native_execution_timeout_respects_the_requested_qa_budget(
        int requestedSeconds,
        int expectedSeconds)
    {
        CppQaEngine
            .ResolveNativeExecutionTimeout(requestedSeconds)
            .Should()
            .Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData(5, 5)]
    [InlineData(20, 5)]
    [InlineData(120, 30)]
    [InlineData(600, 30)]
    public void Ctest_case_timeout_is_bounded_and_deterministic(int budgetSeconds, int expectedSeconds)
    {
        CppQaEngine
            .ResolveCtestCaseTimeout(TimeSpan.FromSeconds(budgetSeconds))
            .Should()
            .Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Theory]
    [InlineData(5, 5, 5)]
    [InlineData(20, 5, 15)]
    [InlineData(120, 30, 40)]
    public void Ctest_process_timeout_leaves_a_fixed_cleanup_margin_without_exceeding_budget(
        int budgetSeconds,
        int caseTimeoutSeconds,
        int expectedProcessTimeoutSeconds)
    {
        CppQaEngine
            .ResolveCtestProcessTimeout(
                TimeSpan.FromSeconds(budgetSeconds),
                TimeSpan.FromSeconds(caseTimeoutSeconds))
            .Should()
            .Be(TimeSpan.FromSeconds(expectedProcessTimeoutSeconds));
    }

    [Fact]
    public void Direct_runtime_probe_is_skipped_after_successful_ctest_and_used_as_fallback_otherwise()
    {
        var passed = new QaToolExecution("ctest", true, true, 0, 1, "", "", "pass");
        var unavailable = new QaToolExecution("ctest", false, false, null, 0, "", "", "unavailable");
        var failed = new QaToolExecution("ctest", true, false, 8, 1, "", "", "failed");
        var timedOut = new QaToolExecution("ctest", true, false, null, 40000, "", "", "timeout");

        CppQaEngine.ShouldRunDirectRuntimeProbe(null).Should().BeTrue();
        CppQaEngine.ShouldRunDirectRuntimeProbe(unavailable).Should().BeTrue();
        CppQaEngine.ShouldRunDirectRuntimeProbe(failed).Should().BeTrue();
        CppQaEngine.ShouldRunDirectRuntimeProbe(timedOut).Should().BeTrue();
        CppQaEngine.ShouldRunDirectRuntimeProbe(passed).Should().BeFalse();
    }

    [Fact]
    public void Linux_sanitizer_environment_preserves_lsan_exit_path_without_forced_abort()
    {
        var environment = CppQaEngine.BuildSanitizerEnvironment(
            new Dictionary<string, string>(),
            isWindows: false);

        environment["ASAN_OPTIONS"].Should().Contain("detect_leaks=1");
        environment["ASAN_OPTIONS"].Should().Contain("halt_on_error=1");
        environment["ASAN_OPTIONS"].Should().Contain("allocator_may_return_null=0");
        environment["ASAN_OPTIONS"].Should().NotContain("abort_on_error=1");
        environment["LSAN_OPTIONS"].Should().Be("exitcode=23:report_objects=1");
    }

    [Fact]
    public void Windows_sanitizer_environment_preserves_existing_abort_policy_without_lsan()
    {
        var environment = CppQaEngine.BuildSanitizerEnvironment(
            new Dictionary<string, string>(),
            isWindows: true);

        environment["ASAN_OPTIONS"].Should().Be("halt_on_error=1:abort_on_error=1");
        environment["UBSAN_OPTIONS"].Should().Be("print_stacktrace=1:halt_on_error=0");
        environment.ContainsKey("LSAN_OPTIONS").Should().BeFalse();
    }
    [Fact]
    public void Asan_heap_buffer_overflow_remains_exact()
    {
        const string stderr =
            "ERROR: AddressSanitizer: heap-buffer-overflow on address 0x000000000000\n"
            + "#0 0x1 in main /work/main.cpp:2:32";

        var findings = _normalizer.NormalizeText(LanguageId.Cpp, "cpp-runtime-asan-auto", string.Empty, stderr);

        findings.Should().Contain(x =>
            x.Category == "memory-safety"
            && x.ExceptionType == "heap-buffer-overflow"
            && x.RuleId == "asan");
    }

    [Fact]
    public void Lsan_memory_leak_remains_exact()
    {
        const string stderr =
            "==42==ERROR: LeakSanitizer: detected memory leaks\n"
            + "Direct leak of 16384 byte(s) in 1 object(s) allocated from:\n"
            + "SUMMARY: AddressSanitizer: 16384 byte(s) leaked in 1 allocation(s).";

        var findings = _normalizer.NormalizeText(LanguageId.Cpp, "cpp-runtime-asan-auto", string.Empty, stderr);

        findings.Should().ContainSingle(x =>
            x.Category == "memory-leak"
            && x.Severity.ToString() == "Warning"
            && x.ExceptionType == "MemoryLeak"
            && x.RuleId == "lsan");
    }
}
