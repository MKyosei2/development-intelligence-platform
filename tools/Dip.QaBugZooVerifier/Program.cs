using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Infrastructure.QualityAutomation;

var options = Options.Parse(args);
var root = Path.GetFullPath(options.Root ?? FindRepositoryRoot(Environment.CurrentDirectory));
var zooRoot = Path.Combine(root, "samples", "qa-bug-zoo");
var outputRoot = Path.GetFullPath(options.Output ?? Path.Combine(root, "artifacts", "qa-bug-zoo"));
Directory.CreateDirectory(outputRoot);

if (!Directory.Exists(zooRoot))
    throw new DirectoryNotFoundException($"Bug Zoo was not found: {zooRoot}");

var allCases = Directory.EnumerateFiles(zooRoot, "expected-result.json", SearchOption.AllDirectories)
    .Select(path => BugZooCase.Load(zooRoot, path))
    .OrderBy(item => item.Language)
    .ThenBy(item => item.Name, StringComparer.Ordinal)
    .ToArray();
var skippedCases = allCases.Where(item => !item.IsSupportedOnCurrentPlatform()).ToArray();
var cases = allCases.Where(item => item.IsSupportedOnCurrentPlatform()).ToArray();

var executor = new ProcessExecutor();
var engines = new Dictionary<QaLanguage, Func<QaExecutionCommand, string, string, CancellationToken, Task<HardcodedQaResult>>>
{
    [QaLanguage.Python] = new PythonQaEngine(executor).RunAsync,
    [QaLanguage.CSharp] = new CSharpQaEngine(executor).RunAsync,
    [QaLanguage.Cpp] = new CppQaEngine(executor).RunAsync
};

var results = new List<CaseResult>();
foreach (var testCase in cases)
{
    var artifactRoot = Path.Combine(outputRoot, "cases", testCase.Language.ToString().ToLowerInvariant(), testCase.Name);
    RecreateDirectory(artifactRoot);
    var stopwatch = Stopwatch.StartNew();
    try
    {
        var command = BuildCommand(testCase);
        var first = await engines[testCase.Language](command, testCase.Workspace, Path.Combine(artifactRoot, "first"), CancellationToken.None);
        var relevant = RelevantFindings(first.Findings, testCase).ToArray();
        var noisy = first.Findings.Where(finding => finding.Severity is Severity.Critical or Severity.Warning).ToArray();
        var detected = testCase.ExpectNoFindings ? noisy.Length == 0 : relevant.Length > 0;
        var reproduced = false;
        var reproducibleAttempts = 0;

        if (testCase.MustReproduce && relevant.Length > 0)
        {
            var firstFingerprints = relevant.Select(finding => QaFingerprint.Create(testCase.Language, finding)).ToHashSet(StringComparer.Ordinal);
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var repeat = await engines[testCase.Language](command, testCase.Workspace, Path.Combine(artifactRoot, $"repeat-{attempt}"), CancellationToken.None);
                var repeatFingerprints = RelevantFindings(repeat.Findings, testCase)
                    .Select(finding => QaFingerprint.Create(testCase.Language, finding));
                if (repeatFingerprints.Any(firstFingerprints.Contains))
                    reproducibleAttempts++;
            }
            reproduced = reproducibleAttempts > 0;
        }

        stopwatch.Stop();
        results.Add(new CaseResult(
            testCase.Language.ToString(), testCase.Name, testCase.ExpectNoFindings, testCase.MustReproduce,
            detected, reproduced, reproducibleAttempts, 2, false, stopwatch.ElapsedMilliseconds,
            noisy.Length, relevant.FirstOrDefault()?.Category, relevant.FirstOrDefault()?.Severity.ToString(),
            relevant.FirstOrDefault()?.ExceptionType, relevant.FirstOrDefault()?.RuleId,
            first.Tools.Select(tool => new ToolResult(tool.Tool, tool.WasAvailable, tool.Succeeded, tool.ExitCode, tool.DurationMs)).ToArray(),
            null));
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        stopwatch.Stop();
        results.Add(new CaseResult(
            testCase.Language.ToString(), testCase.Name, testCase.ExpectNoFindings, testCase.MustReproduce,
            false, false, 0, 0, true, stopwatch.ElapsedMilliseconds, 0, null, null, null, null,
            Array.Empty<ToolResult>(), ex.ToString()));
    }
}

var bugCases = results.Where(result => !result.ExpectNoFindings).ToArray();
var cleanCases = results.Where(result => result.ExpectNoFindings).ToArray();
var reproducibleCases = results.Where(result => result.MustReproduce).ToArray();
var detected = bugCases.Count(result => result.Detected);
var falsePositives = cleanCases.Count(result => !result.Detected);
var reproduced = reproducibleCases.Count(result => result.Reproduced);
var infrastructureFailures = results.Count(result => result.InfrastructureFailure);
var detectionRecall = Percent(detected, bugCases.Length);
var falsePositiveRate = Percent(falsePositives, cleanCases.Length);
var reproductionRate = Percent(reproduced, reproducibleCases.Length);
var medianDuration = Median(results.Where(r => !r.InfrastructureFailure).Select(r => r.DurationMs).ToArray());

var summary = new
{
    schemaVersion = "1.0",
    generatedAt = DateTimeOffset.UtcNow,
    repositoryRoot = root,
    cases = results.Count,
    injectedBugCases = bugCases.Length,
    detectedBugCases = detected,
    detectionRecallPercent = detectionRecall,
    cleanControlCases = cleanCases.Length,
    falsePositiveCases = falsePositives,
    falsePositiveRatePercent = falsePositiveRate,
    reproductionEligibleCases = reproducibleCases.Length,
    reproducedCases = reproduced,
    reproductionRatePercent = reproductionRate,
    infrastructureFailures,
    platformSkippedCases = skippedCases.Select(item => $"{item.Language}/{item.Name}").ToArray(),
    medianCaseDurationMs = medianDuration,
    results
};

var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
await File.WriteAllTextAsync(Path.Combine(outputRoot, "qa-bug-zoo-results.json"), JsonSerializer.Serialize(summary, jsonOptions));
await File.WriteAllTextAsync(Path.Combine(outputRoot, "qa-bug-zoo-results.md"), Markdown(results, bugCases.Length, detected, detectionRecall, cleanCases.Length, falsePositives, falsePositiveRate, reproducibleCases.Length, reproduced, reproductionRate, infrastructureFailures, skippedCases, medianDuration));

Console.WriteLine($"Bug Zoo: detected {detected}/{bugCases.Length} ({detectionRecall:0.##}%), false positives {falsePositives}/{cleanCases.Length} ({falsePositiveRate:0.##}%), reproduced {reproduced}/{reproducibleCases.Length} ({reproductionRate:0.##}%).");
Console.WriteLine($"Evidence: {Path.Combine(outputRoot, "qa-bug-zoo-results.json")}");

if (infrastructureFailures > 0 && !options.AllowMissingTools) return 2;
if (bugCases.Any(result => !result.Detected && !result.InfrastructureFailure)) return 3;
if (cleanCases.Any(result => !result.Detected && !result.InfrastructureFailure)) return 4;
if (reproducibleCases.Any(result => result.Detected && !result.Reproduced && !result.InfrastructureFailure)) return 5;
return 0;

static QaExecutionCommand BuildCommand(BugZooCase testCase)
{
    var runtime = testCase.Language switch
    {
        QaLanguage.Python => File.Exists(Path.Combine(testCase.Workspace, "app.py")) ? "app.py" : null,
        QaLanguage.CSharp when testCase.MustReproduce || string.Equals(testCase.Category, "runtime", StringComparison.OrdinalIgnoreCase) || string.Equals(testCase.Category, "hang", StringComparison.OrdinalIgnoreCase)
            => CSharpRuntime(testCase.Workspace),
        _ => null
    };
    var timeout = string.Equals(testCase.Category, "hang", StringComparison.OrdinalIgnoreCase) ? 5 : 120;
    return new QaExecutionCommand(Guid.Empty, $"bug-zoo:{testCase.Language}:{testCase.Name}", testCase.Language, ".", null, runtime, Array.Empty<string>(), timeout, false, false, false);
}

static string? CSharpRuntime(string workspace)
{
    var project = Directory.EnumerateFiles(workspace, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
    if (project is null) return null;
    var assembly = Path.GetFileNameWithoutExtension(project);
    return Path.Combine("bin", "Release", "net8.0", assembly + ".dll");
}

static IEnumerable<QaFindingCandidate> RelevantFindings(IEnumerable<QaFindingCandidate> findings, BugZooCase testCase)
{
    if (testCase.ExpectNoFindings) return Array.Empty<QaFindingCandidate>();
    return findings.Where(finding =>
        string.Equals(finding.Category, testCase.Category, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(finding.Severity.ToString(), testCase.Severity, StringComparison.OrdinalIgnoreCase) &&
        (string.IsNullOrWhiteSpace(testCase.ExceptionType) || string.Equals(finding.ExceptionType, testCase.ExceptionType, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(testCase.RuleId) || string.Equals(finding.RuleId, testCase.RuleId, StringComparison.OrdinalIgnoreCase)));
}

static decimal Percent(int numerator, int denominator) => denominator == 0 ? 0 : decimal.Round(100m * numerator / denominator, 2);
static long Median(long[] values)
{
    if (values.Length == 0) return 0;
    Array.Sort(values);
    return values.Length % 2 == 1 ? values[values.Length / 2] : (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2;
}
static void RecreateDirectory(string path)
{
    if (Directory.Exists(path)) Directory.Delete(path, true);
    Directory.CreateDirectory(path);
}
static string FindRepositoryRoot(string start)
{
    var current = new DirectoryInfo(Path.GetFullPath(start));
    while (current != null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, "samples", "qa-bug-zoo"))) return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Unable to locate repository root containing samples/qa-bug-zoo.");
}
static string Markdown(IReadOnlyList<CaseResult> results, int bugCount, int detected, decimal recall, int cleanCount, int falsePositives, decimal fpRate, int reproCount, int reproduced, decimal reproRate, int infraFailures, IReadOnlyList<BugZooCase> skippedCases, long medianMs)
{
    var sb = new StringBuilder();
    sb.AppendLine("# DIP QA Bug Zoo Results").AppendLine();
    sb.AppendLine($"- Injected bugs: **{bugCount}**");
    sb.AppendLine($"- Detected: **{detected}/{bugCount} ({recall:0.##}%)**");
    sb.AppendLine($"- Clean controls: **{cleanCount}**");
    sb.AppendLine($"- False positives: **{falsePositives}/{cleanCount} ({fpRate:0.##}%)**");
    sb.AppendLine($"- Reproduced: **{reproduced}/{reproCount} ({reproRate:0.##}%)**");
    sb.AppendLine($"- Infrastructure failures: **{infraFailures}**");
    sb.AppendLine($"- Platform-skipped cases: **{skippedCases.Count}**");
    if (skippedCases.Count > 0) sb.AppendLine($"  - {string.Join(", ", skippedCases.Select(item => $"{item.Language}/{item.Name}"))}");
    sb.AppendLine($"- Median case duration: **{medianMs} ms**").AppendLine();
    sb.AppendLine("| Language | Case | Expected | Detected | Reproduced | Infrastructure | Duration ms |");
    sb.AppendLine("|---|---|---|---:|---:|---:|---:|");
    foreach (var result in results)
        sb.AppendLine($"| {result.Language} | {result.Name} | {(result.ExpectNoFindings ? "clean" : result.MatchedCategory ?? "bug")} | {(result.Detected ? "yes" : "no")} | {(result.MustReproduce ? (result.Reproduced ? "yes" : "no") : "n/a")} | {(result.InfrastructureFailure ? "fail" : "ok")} | {result.DurationMs} |");
    sb.AppendLine().AppendLine("Results are evidence from the actual DIP language QA engines. A clean-control failure is counted as a false positive.");
    return sb.ToString();
}

sealed record ToolResult(string Tool, bool WasAvailable, bool Succeeded, int? ExitCode, long DurationMs);
sealed record CaseResult(string Language, string Name, bool ExpectNoFindings, bool MustReproduce, bool Detected, bool Reproduced, int ReproducedAttempts, int ReproductionAttempts, bool InfrastructureFailure, long DurationMs, int FindingCount, string? MatchedCategory, string? MatchedSeverity, string? MatchedExceptionType, string? MatchedRuleId, IReadOnlyList<ToolResult> Tools, string? Error);

sealed record BugZooCase(QaLanguage Language, string Name, string Workspace, bool ExpectNoFindings, string? Category, string? Severity, string? ExceptionType, string? RuleId, bool MustReproduce, bool OptionalOnWindows, bool OptionalOnNonWindows)
{
    public static BugZooCase Load(string zooRoot, string expectedPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(expectedPath));
        var json = document.RootElement;
        var workspace = Path.GetDirectoryName(expectedPath)!;
        var relative = Path.GetRelativePath(zooRoot, workspace).Replace('\\', '/').Split('/');
        if (relative.Length < 2) throw new InvalidDataException($"Unexpected Bug Zoo path: {workspace}");
        var language = relative[0].ToLowerInvariant() switch
        {
            "python" => QaLanguage.Python,
            "csharp" => QaLanguage.CSharp,
            "cpp" => QaLanguage.Cpp,
            _ => throw new InvalidDataException($"Unsupported Bug Zoo language folder: {relative[0]}")
        };
        return new BugZooCase(
            language,
            relative[1],
            workspace,
            GetBool(json, "expectNoFindings"),
            GetString(json, "category"),
            GetString(json, "severity"),
            GetString(json, "exceptionType"),
            GetString(json, "ruleId"),
            GetBool(json, "mustReproduce"),
            GetBool(json, "optionalOnWindows"),
            GetBool(json, "optionalOnNonWindows"));
    }
    public bool IsSupportedOnCurrentPlatform() => !(OperatingSystem.IsWindows() && OptionalOnWindows) && !(!OperatingSystem.IsWindows() && OptionalOnNonWindows);
    static string? GetString(JsonElement json, string name) => json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    static bool GetBool(JsonElement json, string name) => json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}

sealed record Options(string? Root, string? Output, bool AllowMissingTools)
{
    public static Options Parse(string[] args)
    {
        string? root = null;
        string? output = null;
        var allowMissing = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--root" when index + 1 < args.Length: root = args[++index]; break;
                case "--output" when index + 1 < args.Length: output = args[++index]; break;
                case "--allow-missing-tools": allowMissing = true; break;
            }
        }
        return new Options(root, output, allowMissing);
    }
}
