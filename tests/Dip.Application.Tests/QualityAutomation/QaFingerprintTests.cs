// Purpose: Verifies QA Fingerprint behavior, edge cases, and repository invariants.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class QaFingerprintTests
{
    [Fact]
    public void SameFinding_RelativeAndAbsoluteLikePath_HasStableFingerprint()
    {
        var a = new QaFindingCandidate(
            "runtime",
            Severity.Critical,
            "python-runtime",
            "Unhandled TypeError",
            "bad type",
            "samples/bug/app.py",
            12,
            ExceptionType: "TypeError",
            StackTrace: "Traceback (most recent call last):\n...");

        var b = a with { FilePath = "C:/repo/samples/bug/app.py" };

        QaFingerprint.Create(LanguageId.Python, a)
            .Should()
            .Be(QaFingerprint.Create(LanguageId.Python, b));
    }

    [Fact]
    public void DifferentException_ProducesDifferentFingerprint()
    {
        var a = new QaFindingCandidate(
            "runtime",
            Severity.Critical,
            "runtime",
            "A",
            "x",
            "app.py",
            1,
            ExceptionType: "TypeError");

        var b = a with { ExceptionType = "ValueError" };

        QaFingerprint.Create(LanguageId.Python, a)
            .Should()
            .NotBe(QaFingerprint.Create(LanguageId.Python, b));
    }

    [Fact]
    public void CppMemorySafety_CTestWrapperAndDirectRuntime_HaveSameFingerprint()
    {
        var ctestFinding = new QaFindingCandidate(
            "memory-safety",
            Severity.Critical,
            "ctest",
            "AddressSanitizer: heap-buffer-overflow",
            "AddressSanitizer reported a heap-buffer-overflow.",
            "/work/samples/qa-bug-zoo/cpp/buffer_overflow/main.cpp",
            2,
            Function: "main",
            ExceptionType: "heap-buffer-overflow",
            StackTrace:
                "Internal ctest changing into directory: /work/.dipqa/asan-build\n" +
                "Test project /work/.dipqa/asan-build\n" +
                "AddressSanitizer: heap-buffer-overflow",
            RuleId: "asan");

        var directRuntimeFinding = ctestFinding with
        {
            Tool = "cpp-runtime-asan-auto",
            Message = "AddressSanitizer reported the same heap-buffer-overflow directly.",
            FilePath = "C:/repo/samples/qa-bug-zoo/cpp/buffer_overflow/main.cpp",
            StackTrace =
                "/work/samples/qa-bug-zoo/cpp/buffer_overflow/main.cpp:2:35: runtime error\n" +
                "AddressSanitizer: heap-buffer-overflow"
        };

        QaFingerprint.Create(LanguageId.Cpp, ctestFinding)
            .Should()
            .Be(QaFingerprint.Create(LanguageId.Cpp, directRuntimeFinding));
    }

    [Fact]
    public void CppMemorySafety_DifferentException_ProducesDifferentFingerprint()
    {
        var overflow = CppMemorySafetyFinding(
            exceptionType: "heap-buffer-overflow",
            filePath: "samples/qa-bug-zoo/cpp/buffer_overflow/main.cpp",
            line: 2);

        var useAfterFree = overflow with { ExceptionType = "heap-use-after-free" };

        QaFingerprint.Create(LanguageId.Cpp, overflow)
            .Should()
            .NotBe(QaFingerprint.Create(LanguageId.Cpp, useAfterFree));
    }

    [Fact]
    public void CppMemorySafety_DifferentLine_ProducesDifferentFingerprint()
    {
        var line2 = CppMemorySafetyFinding(
            exceptionType: "heap-buffer-overflow",
            filePath: "samples/qa-bug-zoo/cpp/buffer_overflow/main.cpp",
            line: 2);

        var line3 = line2 with { Line = 3 };

        QaFingerprint.Create(LanguageId.Cpp, line2)
            .Should()
            .NotBe(QaFingerprint.Create(LanguageId.Cpp, line3));
    }

    [Fact]
    public void CppMemorySafety_DifferentFile_ProducesDifferentFingerprint()
    {
        var main = CppMemorySafetyFinding(
            exceptionType: "heap-buffer-overflow",
            filePath: "samples/qa-bug-zoo/cpp/buffer_overflow/main.cpp",
            line: 2);

        var enemy = main with { FilePath = "samples/qa-bug-zoo/cpp/buffer_overflow/enemy.cpp" };

        QaFingerprint.Create(LanguageId.Cpp, main)
            .Should()
            .NotBe(QaFingerprint.Create(LanguageId.Cpp, enemy));
    }

    private static QaFindingCandidate CppMemorySafetyFinding(
        string exceptionType,
        string filePath,
        int line) =>
        new(
            "memory-safety",
            Severity.Critical,
            "asan",
            $"AddressSanitizer: {exceptionType}",
            exceptionType,
            filePath,
            line,
            Function: "main",
            ExceptionType: exceptionType,
            StackTrace: $"AddressSanitizer: {exceptionType}",
            RuleId: "asan");
}
