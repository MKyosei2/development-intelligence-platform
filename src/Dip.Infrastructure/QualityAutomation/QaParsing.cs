// Purpose: Implements QA Parsing infrastructure behavior for quality automation.
using System.Text.RegularExpressions;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

internal static partial class QaParsing
{
    public static IReadOnlyList<QaFindingCandidate> ParsePython(string tool, string text, bool timedOut)
    {
        var list = new List<QaFindingCandidate>();
        if (timedOut) list.Add(new("hang", Severity.Critical, tool, "Python process timed out", "The Python command exceeded its QA timeout.", ExceptionType: "Timeout"));
        foreach (Match m in PythonTracebackRegex().Matches(text))
            list.Add(new("runtime", Severity.Critical, tool, $"Unhandled {m.Groups[4].Value}", m.Groups[5].Value.Trim(), m.Groups[1].Value, int.Parse(m.Groups[2].Value), Function: m.Groups[3].Value, ExceptionType: m.Groups[4].Value, StackTrace: Trim(text)));
        foreach (Match m in PythonSyntaxRegex().Matches(text))
            list.Add(new("syntax", Severity.Critical, tool, m.Groups[3].Value, m.Groups[4].Value.Trim(), m.Groups[1].Value, int.Parse(m.Groups[2].Value), ExceptionType: m.Groups[3].Value));
        if (text.Contains("ModuleNotFoundError", StringComparison.Ordinal)) list.Add(new("import", Severity.Critical, tool, "Python module import failed", LastLine(text), ExceptionType: "ModuleNotFoundError", StackTrace: Trim(text)));
        if (text.Contains("ImportError", StringComparison.Ordinal) && !list.Any(x => x.Category == "import")) list.Add(new("import", Severity.Critical, tool, "Python import failed", LastLine(text), ExceptionType: "ImportError", StackTrace: Trim(text)));
        if (text.Contains("was never awaited", StringComparison.OrdinalIgnoreCase) || text.Contains("Task was destroyed but it is pending", StringComparison.OrdinalIgnoreCase))
            list.Add(new("async", Severity.Warning, tool, "Python asynchronous work was not completed correctly", LastLineContaining(text, "await"), ExceptionType: "AsyncLifecycleWarning", StackTrace: Trim(text), RuleId: "python-asyncio-debug"));
        foreach (Match m in PytestFailureRegex().Matches(text)) list.Add(new("test", Severity.Warning, tool, "pytest failure", m.Groups[1].Value.Trim(), RuleId: "pytest"));
        return Deduplicate(list);
    }

    public static IReadOnlyList<QaFindingCandidate> ParseCSharp(string tool, string text, bool timedOut)
    {
        var list = new List<QaFindingCandidate>();
        if (timedOut) list.Add(new("hang", Severity.Critical, tool, ".NET process timed out", "The .NET command exceeded its QA timeout.", ExceptionType: "Timeout"));
        foreach (Match m in MsBuildRegex().Matches(text))
            list.Add(new("compile", Severity.Critical, tool, $"C# {m.Groups[4].Value}", m.Groups[5].Value.Trim(), m.Groups[1].Value, ParseInt(m.Groups[2].Value), ParseInt(m.Groups[3].Value), RuleId: m.Groups[4].Value));
        foreach (Match m in MsBuildWarningRegex().Matches(text))
        {
            var rule = m.Groups[4].Value;
            var category = rule is "CS4014" or "CS1998" ? "async" : rule.StartsWith("CS86", StringComparison.Ordinal) ? "nullability" : "static-analysis";
            list.Add(new(category, Severity.Warning, tool, $"C# {rule} warning", m.Groups[5].Value.Trim(), m.Groups[1].Value, ParseInt(m.Groups[2].Value), ParseInt(m.Groups[3].Value), RuleId: rule));
        }
        foreach (Match m in DotNetExceptionRegex().Matches(text))
        {
            var qualifiedExceptionType = m.Groups[1].Value;
            var separator = qualifiedExceptionType.LastIndexOf('.');
            var exceptionType = separator >= 0 ? qualifiedExceptionType[(separator + 1)..] : qualifiedExceptionType;
            list.Add(new("runtime", Severity.Critical, tool, $"Unhandled {exceptionType}", m.Groups[2].Value.Trim(), ExceptionType: exceptionType, StackTrace: Trim(text)));
        }
        foreach (Match m in DotNetStackFrameRegex().Matches(text))
        {
            if (list.Count == 0) break;
            var last = list[^1];
            if (last.FilePath is null) list[^1] = last with { Function = m.Groups[1].Value, FilePath = m.Groups[2].Value, Line = ParseInt(m.Groups[3].Value) };
        }
        if (text.Contains("Failed!", StringComparison.OrdinalIgnoreCase) || text.Contains("Failed:", StringComparison.OrdinalIgnoreCase))
            list.Add(new("test", Severity.Warning, tool, ".NET tests failed", LastLineContaining(text, "Failed"), RuleId: "dotnet-test"));
        return Deduplicate(list);
    }

    public static IReadOnlyList<QaFindingCandidate> ParseCpp(string tool, string text, bool timedOut)
    {
        var list = new List<QaFindingCandidate>();
        var ctest = tool.Contains("ctest", StringComparison.OrdinalIgnoreCase);
        var ctestTimeout = ctest && IsCTestTimeout(text, timedOut);

        // CTest is an orchestration layer. A CTest timeout is preserved as infrastructure
        // evidence by QaToolSupport and must not be rewritten as a product Critical hang.
        // The direct sanitizer runtime probe owns product/runtime timeout detection.
        if (timedOut && !ctest)
            list.Add(new("hang", Severity.Critical, tool, "Native process timed out", "The native command exceeded its QA timeout.", ExceptionType: "Timeout"));

        foreach (Match m in CppCompilerRegex().Matches(text))
            list.Add(new("compile", Severity.Critical, tool, $"C++ compiler {m.Groups[4].Value}", m.Groups[5].Value.Trim(), m.Groups[1].Value, ParseInt(m.Groups[2].Value), ParseInt(m.Groups[3].Value), RuleId: m.Groups[4].Value));

        foreach (Match m in GccCompilerRegex().Matches(text))
            list.Add(new("compile", Severity.Critical, tool, "C++ compiler error", m.Groups[4].Value.Trim(), m.Groups[1].Value, ParseInt(m.Groups[2].Value), ParseInt(m.Groups[3].Value), RuleId: "compiler"));

        // High-signal compiler warnings that already prove an invalid memory access
        // are promoted to memory-safety findings. Generic warnings remain warnings.
        foreach (Match m in GccCompilerWarningRegex().Matches(text))
        {
            var rule = m.Groups[5].Value;
            var detail = m.Groups[4].Value.Trim();

            if (IsMemorySafetyRule(rule) || detail.Contains("outside array bounds", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new(
                    "memory-safety",
                    Severity.Critical,
                    tool,
                    $"C++ memory-safety warning {rule}",
                    detail,
                    m.Groups[1].Value,
                    ParseInt(m.Groups[2].Value),
                    ParseInt(m.Groups[3].Value),
                    ExceptionType: MemorySafetyException(rule, detail),
                    RuleId: rule));
            }
            else
            {
                list.Add(new(
                    "static-analysis",
                    Severity.Warning,
                    tool,
                    $"C++ compiler warning {rule}",
                    detail,
                    m.Groups[1].Value,
                    ParseInt(m.Groups[2].Value),
                    ParseInt(m.Groups[3].Value),
                    RuleId: rule));
            }
        }

        // cppcheck text diagnostics are stable and include rule ids. Information-only
        // diagnostics such as "toomanyconfigs" are intentionally not product findings.
        foreach (Match m in CppCheckRegex().Matches(text))
        {
            var level = m.Groups[4].Value;
            var message = m.Groups[5].Value.Trim();
            var rule = m.Groups[6].Value;

            if (level.Equals("information", StringComparison.OrdinalIgnoreCase))
                continue;

            var memorySafety = IsMemorySafetyRule(rule)
                || message.Contains("out of bounds", StringComparison.OrdinalIgnoreCase)
                || message.Contains("use after free", StringComparison.OrdinalIgnoreCase)
                || message.Contains("double free", StringComparison.OrdinalIgnoreCase);

            list.Add(new(
                memorySafety ? "memory-safety" : "static-analysis",
                memorySafety || level.Equals("error", StringComparison.OrdinalIgnoreCase) ? Severity.Critical : Severity.Warning,
                tool,
                memorySafety ? $"cppcheck memory-safety finding {rule}" : $"cppcheck {level} {rule}",
                message,
                m.Groups[1].Value,
                ParseInt(m.Groups[2].Value),
                ParseInt(m.Groups[3].Value),
                ExceptionType: memorySafety ? MemorySafetyException(rule, message) : null,
                RuleId: rule));
        }

        var sanitizer = SanitizerKind(text);
        if (sanitizer is not null)
        {
            var frame = AsanSourceFrameRegex().Match(text);
            list.Add(new("memory-safety", Severity.Critical, tool, $"AddressSanitizer: {sanitizer}", FirstSanitizerLine(text),
                frame.Success ? frame.Groups[2].Value : null, frame.Success ? ParseInt(frame.Groups[3].Value) : null,
                Function: frame.Success ? frame.Groups[1].Value.Trim() : null, ExceptionType: sanitizer, StackTrace: Trim(text), RuleId: "asan"));
        }

        if (text.Contains("LeakSanitizer", StringComparison.OrdinalIgnoreCase)
            && text.Contains("detected memory leaks", StringComparison.OrdinalIgnoreCase))
        {
            list.Add(new(
                "memory-leak",
                Severity.Warning,
                tool,
                "LeakSanitizer detected live allocations at process exit",
                LastLineContaining(text, "SUMMARY"),
                ExceptionType: "MemoryLeak",
                StackTrace: Trim(text),
                RuleId: "lsan"));
        }

        if (text.Contains("runtime error:", StringComparison.OrdinalIgnoreCase))
            list.Add(new("undefined-behavior", Severity.Critical, tool, "UndefinedBehaviorSanitizer reported invalid native behavior", LastLineContaining(text, "runtime error:"), ExceptionType: "UndefinedBehavior", StackTrace: Trim(text), RuleId: "ubsan"));

        if (text.Contains("Access violation", StringComparison.OrdinalIgnoreCase) || text.Contains("0xC0000005", StringComparison.OrdinalIgnoreCase))
            list.Add(new("native-crash", Severity.Critical, tool, "Native access violation", LastLine(text), ExceptionType: "AccessViolation", StackTrace: Trim(text)));

        // A CTest internal/outer timeout is infrastructure evidence. Non-timeout CTest
        // failures remain test findings. This prevents cpp/clean from becoming a false
        // Critical product hang while still keeping genuine failing tests visible.
        if (ctest
            && !ctestTimeout
            && (CtestNonZeroFailureRegex().IsMatch(text)
                || text.Contains("***Failed", StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(new("test", Severity.Warning, tool, "C++ tests failed", LastLineContaining(text, "fail"), RuleId: "ctest"));
        }

        return Deduplicate(list);
    }

    public static IReadOnlyList<QaFindingCandidate> ParseForLanguage(LanguageId language, string tool, string stdout, string stderr, bool timedOut = false)
    {
        var text = stdout + "\n" + stderr;
        if (language == LanguageId.Python) return ParsePython(tool, text, timedOut);
        if (language == LanguageId.CSharp) return ParseCSharp(tool, text, timedOut);
        if (language == LanguageId.Cpp || language == LanguageId.C) return ParseCpp(tool, text, timedOut);
        return Array.Empty<QaFindingCandidate>();
    }

    public static bool MatchesFingerprint(LanguageId language, string expected, string stdout, string stderr, bool timedOut = false)
        => ParseForLanguage(language, "reproduction", stdout, stderr, timedOut).Any(x => QaFingerprint.Create(language, x) == expected);

    internal static bool IsCTestTimeout(string text, bool timedOut) =>
        timedOut
        || text.Contains("***Timeout", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Test timeout computed", StringComparison.OrdinalIgnoreCase)
        || text.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
           && text.Contains("CTest", StringComparison.OrdinalIgnoreCase);

    private static bool IsMemorySafetyRule(string rule) =>
        rule.Contains("arrayIndexOutOfBounds", StringComparison.OrdinalIgnoreCase)
        || rule.Contains("bufferAccessOutOfBounds", StringComparison.OrdinalIgnoreCase)
        || rule.Contains("outOfBounds", StringComparison.OrdinalIgnoreCase)
        || rule.Contains("useAfterFree", StringComparison.OrdinalIgnoreCase)
        || rule.Contains("doubleFree", StringComparison.OrdinalIgnoreCase)
        || rule.Contains("deallocDealloc", StringComparison.OrdinalIgnoreCase)
        || rule.Contains("array-bounds", StringComparison.OrdinalIgnoreCase)
        || rule.Contains("use-after-free", StringComparison.OrdinalIgnoreCase)
        || rule.Contains("stringop-overflow", StringComparison.OrdinalIgnoreCase);

    private static string MemorySafetyException(string rule, string message)
    {
        var value = rule + " " + message;
        if (value.Contains("use after free", StringComparison.OrdinalIgnoreCase) || value.Contains("useAfterFree", StringComparison.OrdinalIgnoreCase) || value.Contains("use-after-free", StringComparison.OrdinalIgnoreCase))
            return "heap-use-after-free";
        if (value.Contains("double free", StringComparison.OrdinalIgnoreCase) || value.Contains("doubleFree", StringComparison.OrdinalIgnoreCase) || value.Contains("deallocDealloc", StringComparison.OrdinalIgnoreCase))
            return "double-free";
        if (value.Contains("out of bounds", StringComparison.OrdinalIgnoreCase) || value.Contains("arrayIndexOutOfBounds", StringComparison.OrdinalIgnoreCase) || value.Contains("array-bounds", StringComparison.OrdinalIgnoreCase))
            return "buffer-overflow";
        return "memory-safety";
    }

    private static IReadOnlyList<QaFindingCandidate> Deduplicate(IEnumerable<QaFindingCandidate> list) =>
        list.GroupBy(x => string.Join('|', x.Category, x.FilePath, x.Line, x.ExceptionType, x.RuleId, x.Message), StringComparer.Ordinal)
            .Select(x => x.First())
            .Take(200)
            .ToArray();

    private static string Trim(string text) => text.Length <= 30000 ? text : text[..30000];
    private static string LastLine(string text) => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim() ?? "Process failed.";
    private static string LastLineContaining(string text, string token) => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault(x => x.Contains(token, StringComparison.OrdinalIgnoreCase))?.Trim() ?? LastLine(text);
    private static int? ParseInt(string value) => int.TryParse(value, out var n) ? n : null;
    private static string? SanitizerKind(string text) => new[] { "heap-use-after-free", "stack-use-after-return", "heap-buffer-overflow", "stack-buffer-overflow", "double-free", "attempting free", "use-after-poison", "global-buffer-overflow" }.FirstOrDefault(x => text.Contains(x, StringComparison.OrdinalIgnoreCase));
    private static string FirstSanitizerLine(string text) => text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(x => x.Contains("AddressSanitizer", StringComparison.OrdinalIgnoreCase))?.Trim() ?? "AddressSanitizer reported a memory-safety error.";

    [GeneratedRegex("File \\\"([^\\\"]+)\\\", line (\\d+), in ([^\\r\\n]+)[\\s\\S]*?([A-Za-z_][A-Za-z0-9_]*(?:Error|Exception)):\\s*([^\\r\\n]+)", RegexOptions.Multiline)] private static partial Regex PythonTracebackRegex();
    [GeneratedRegex("File \\\"([^\\\"]+)\\\", line (\\d+)[\\s\\S]*?\\n(?:\\s*\\^+\\s*\\n)?(SyntaxError|IndentationError|TabError):\\s*([^\\r\\n]+)", RegexOptions.Multiline)] private static partial Regex PythonSyntaxRegex();
    [GeneratedRegex("(?:FAILED|ERROR)\\s+([^\\r\\n]+)")] private static partial Regex PytestFailureRegex();
    [GeneratedRegex("^(.+\\.cs)\\((\\d+),(\\d+)\\):\\s+error\\s+([A-Z]+\\d+):\\s+(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)] private static partial Regex MsBuildRegex();
    [GeneratedRegex("^(.+\\.cs)\\((\\d+),(\\d+)\\):\\s+warning\\s+([A-Z]+\\d+):\\s+(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)] private static partial Regex MsBuildWarningRegex();
    [GeneratedRegex("(?:Unhandled exception\\.\\s*)?([A-Za-z0-9_.]+Exception):\\s*([^\\r\\n]+)")] private static partial Regex DotNetExceptionRegex();
    [GeneratedRegex("at\\s+(.+?)\\s+in\\s+(.+?):line\\s+(\\d+)")] private static partial Regex DotNetStackFrameRegex();
    [GeneratedRegex("^(.+?)[(:](\\d+)[,:](\\d+)[)]?:?\\s*(?:fatal\\s+)?error\\s+([^: ]+):?\\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)] private static partial Regex CppCompilerRegex();
    [GeneratedRegex("^(.+?):(\\d+):(\\d+):\\s+(?:fatal\\s+)?error:\\s+(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase)] private static partial Regex GccCompilerRegex();
    [GeneratedRegex("^(.+?):(\\d+):(\\d+):\\s+warning:\\s+(.+?)\\s+\\[(-W[^\\]]+)\\]$", RegexOptions.Multiline | RegexOptions.IgnoreCase)] private static partial Regex GccCompilerWarningRegex();
    [GeneratedRegex("^(.+?):(\\d+):(\\d+):\\s+(error|warning|style|performance|portability|information):\\s+(.+?)\\s+\\[([A-Za-z0-9_-]+)\\]\\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)] private static partial Regex CppCheckRegex();
    [GeneratedRegex("\\b([1-9]\\d*)\\s+tests?\\s+failed\\b", RegexOptions.IgnoreCase)] private static partial Regex CtestNonZeroFailureRegex();
    [GeneratedRegex("#\\d+\\s+.*?in\\s+([^\\r\\n]+?)\\s+([^\\s:]+\\.(?:c|cc|cpp|cxx|h|hpp)):(\\d+)(?::\\d+)?", RegexOptions.Multiline | RegexOptions.IgnoreCase)] private static partial Regex AsanSourceFrameRegex();
}
