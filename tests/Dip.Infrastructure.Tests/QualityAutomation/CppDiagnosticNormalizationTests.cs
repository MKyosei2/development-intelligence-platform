// Purpose: Locks C++ diagnostic normalization against the Bug Zoo regressions found in final verification.
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
        const string stdout =
            "100% tests passed, 0 tests failed out of 1";

        var findings = _normalizer.NormalizeText(LanguageId.Cpp, "ctest", stdout, string.Empty);

        findings.Should().NotContain(x => x.Category == "test");
    }

    [Fact]
    public void Ctest_non_zero_failed_is_a_failure()
    {
        const string stdout =
            "50% tests passed, 1 tests failed out of 2";

        var findings = _normalizer.NormalizeText(LanguageId.Cpp, "ctest", stdout, string.Empty);

        findings.Should().Contain(x => x.Category == "test" && x.RuleId == "ctest");
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

    [Fact]
    public void Ctest_timeout_remains_a_critical_hang_when_the_real_budget_is_exhausted()
    {
        var findings = _normalizer.NormalizeText(
            LanguageId.Cpp,
            "ctest",
            string.Empty,
            string.Empty,
            timedOut: true);

        findings.Should().ContainSingle(x =>
            x.Category == "hang"
            && x.Severity.ToString() == "Critical"
            && x.ExceptionType == "Timeout");
    }

    [Fact]
    public void Direct_runtime_fallback_is_skipped_when_ctest_returns_an_exit_code()
    {
        var passed = new QaToolExecution("ctest", true, true, 0, 1, "", "", "pass");
        var failed = new QaToolExecution("ctest", true, false, 8, 1, "", "", "failed");

        CppQaEngine.ShouldRunDirectRuntimeFallback(passed).Should().BeFalse();
        CppQaEngine.ShouldRunDirectRuntimeFallback(failed).Should().BeFalse();
    }

    [Fact]
    public void Direct_runtime_fallback_is_used_when_ctest_cannot_return_an_exit_code()
    {
        var timedOut = new QaToolExecution("ctest", true, false, null, 15000, "", "", "timeout");

        CppQaEngine.ShouldRunDirectRuntimeFallback(null).Should().BeTrue();
        CppQaEngine.ShouldRunDirectRuntimeFallback(timedOut).Should().BeTrue();
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
}
