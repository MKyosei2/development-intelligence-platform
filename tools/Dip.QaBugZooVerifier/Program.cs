// Purpose: Verifies universal QA adapters against the deterministic multi-language bug zoo and writes reproducible evidence.
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.QualityAutomation;

var options = Options.Parse(args);
var root = Path.GetFullPath(options.Root ?? FindRepositoryRoot(Environment.CurrentDirectory));
var zooRoot = Path.Combine(root, "samples", "qa-bug-zoo");
var outputRoot = Path.GetFullPath(options.Output ?? Path.Combine(root, "artifacts", "qa-bug-zoo"));
Directory.CreateDirectory(outputRoot);

if (!Directory.Exists(zooRoot))
    throw new DirectoryNotFoundException($"Bug Zoo was not found: {zooRoot}");

var allCases = Directory
    .EnumerateFiles(zooRoot, "expected-result.json", SearchOption.AllDirectories)
    .Select(path => BugZooCase.Load(zooRoot, path))
    .OrderBy(testCase => testCase.Language.Value)
    .ThenBy(testCase => testCase.Name, StringComparer.Ordinal)
    .ToArray();
var skippedCases = allCases.Where(testCase => !testCase.IsSupportedOnCurrentPlatform()).ToArray();
var cases = allCases.Where(testCase => testCase.IsSupportedOnCurrentPlatform()).ToArray();

var executor = new ProcessExecutor();
var normalizer = new UniversalDiagnosticNormalizer();
var adapters = new IQaAdapter[]
{
    new ManifestQaAdapter(executor, normalizer),
    new UnityProjectQaAdapter(executor, normalizer),
    new PythonQaEngine(executor, normalizer),
    new CSharpQaEngine(executor, normalizer),
    new CppQaEngine(executor, normalizer),
    new GenericCommandQaAdapter(executor, normalizer)
};
var registry = new QaAdapterRegistry(adapters);
var detector = new RepositoryTechnologyDetector();

var results = new List<CaseResult>(cases.Length);
foreach (var testCase in cases)
{
    results.Add(await RunCaseAsync(
        testCase,
        outputRoot,
        registry,
        detector,
        CancellationToken.None));
}

var metrics = BuildMetrics(results, skippedCases, options);
var summary = BuildSummary(root, results, skippedCases, options, metrics);
await WriteEvidenceAsync(outputRoot, summary, results, skippedCases, metrics);

Console.WriteLine(
    $"Bug Zoo: languages {string.Join(", ", metrics.ExecutedLanguages)}; "
    + $"recall {metrics.Recall:0.##}%; false positives {metrics.FalsePositiveRate:0.##}%; "
    + $"reproduction {metrics.ReproductionRate:0.##}%.");
Console.WriteLine($"Evidence: {Path.Combine(outputRoot, "qa-bug-zoo-results.json")}");

if (metrics.MissingRequiredLanguages.Count > 0)
    return 6;
if (metrics.BlockingInfrastructureFailures > 0 && !options.AllowMissingTools)
    return 2;
if (metrics.BugCases.Any(result => !result.Detected))
    return 3;
if (metrics.CleanCases.Any(result => !result.Detected))
    return 4;
if (metrics.ReproducibleCases.Any(result => result.Detected && !result.Reproduced))
    return 5;
return 0;

static async Task<CaseResult> RunCaseAsync(
    BugZooCase testCase,
    string outputRoot,
    QaAdapterRegistry registry,
    RepositoryTechnologyDetector detector,
    CancellationToken ct)
{
    var artifactRoot = Path.Combine(
        outputRoot,
        "cases",
        testCase.Language.Value,
        testCase.Name);
    RecreateDirectory(artifactRoot);
    var stopwatch = Stopwatch.StartNew();

    try
    {
        var profile = await detector.DetectAsync(testCase.Workspace, ct);
        var selection = registry
            .Resolve(testCase.Language, profile, testCase.Workspace)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"No adapter for {testCase.Language}.");
        var command = BuildCommand(testCase);
        var first = await selection.Adapter.RunAsync(
            command,
            testCase.Workspace,
            Path.Combine(artifactRoot, "first"),
            ct);
        var relevant = RelevantFindings(first.Findings, testCase).ToArray();
        var noisy = first.Findings
            .Where(finding => finding.Severity is Severity.Critical or Severity.Warning)
            .ToArray();
        var infrastructureFailure = first.Tools.Count == 0 || first.Tools.All(tool => !tool.WasAvailable);
        var detected = testCase.ExpectNoFindings ? noisy.Length == 0 : relevant.Length > 0;
        var reproduced = false;
        var reproducibleAttempts = 0;

        if (testCase.MustReproduce && relevant.Length > 0 && !infrastructureFailure)
        {
            var firstFingerprints = relevant
                .Select(finding => QaFingerprint.Create(testCase.Language, finding))
                .ToHashSet(StringComparer.Ordinal);
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var repeat = await selection.Adapter.RunAsync(
                    command,
                    testCase.Workspace,
                    Path.Combine(artifactRoot, $"repeat-{attempt}"),
                    ct);
                if (RelevantFindings(repeat.Findings, testCase)
                    .Select(finding => QaFingerprint.Create(testCase.Language, finding))
                    .Any(firstFingerprints.Contains))
                {
                    reproducibleAttempts++;
                }
            }
            reproduced = reproducibleAttempts > 0;
        }

        stopwatch.Stop();
        return new(
            testCase.Language.Value,
            testCase.Name,
            selection.Adapter.AdapterId,
            testCase.ExpectNoFindings,
            testCase.MustReproduce,
            detected,
            reproduced,
            reproducibleAttempts,
            2,
            infrastructureFailure,
            stopwatch.ElapsedMilliseconds,
            noisy.Length,
            relevant.FirstOrDefault()?.Category,
            relevant.FirstOrDefault()?.Severity.ToString(),
            relevant.FirstOrDefault()?.ExceptionType,
            relevant.FirstOrDefault()?.RuleId,
            first.Tools.Select(tool => new ToolResult(
                tool.Tool,
                tool.WasAvailable,
                tool.Succeeded,
                tool.ExitCode,
                tool.DurationMs)).ToArray(),
            profile.BuildSystems,
            profile.Frameworks,
            profile.GameEngines,
            null);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        stopwatch.Stop();
        return new(
            testCase.Language.Value,
            testCase.Name,
            "unresolved",
            testCase.ExpectNoFindings,
            testCase.MustReproduce,
            false,
            false,
            0,
            0,
            true,
            stopwatch.ElapsedMilliseconds,
            0,
            null,
            null,
            null,
            null,
            Array.Empty<ToolResult>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            ex.ToString());
    }
}

static BugZooMetrics BuildMetrics(
    IReadOnlyList<CaseResult> results,
    IReadOnlyList<BugZooCase> skippedCases,
    Options options)
{
    var bugCases = results.Where(result => !result.ExpectNoFindings && !result.InfrastructureFailure).ToArray();
    var cleanCases = results.Where(result => result.ExpectNoFindings && !result.InfrastructureFailure).ToArray();
    var reproducibleCases = results.Where(result => result.MustReproduce && !result.InfrastructureFailure).ToArray();
    var requiredSet = options.RequiredLanguages
        .Select(language => language.Value)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var executedLanguages = results
        .Where(result => !result.InfrastructureFailure)
        .Select(result => result.Language)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(language => language)
        .ToArray();
    var missingRequired = options.RequiredLanguages
        .Where(language => !executedLanguages.Contains(language.Value, StringComparer.OrdinalIgnoreCase))
        .Select(language => language.Value)
        .ToArray();

    return new(
        bugCases,
        cleanCases,
        reproducibleCases,
        Percent(bugCases.Count(result => result.Detected), bugCases.Length),
        Percent(cleanCases.Count(result => !result.Detected), cleanCases.Length),
        Percent(reproducibleCases.Count(result => result.Reproduced), reproducibleCases.Length),
        results.Count(result => result.InfrastructureFailure),
        results.Count(result =>
            result.InfrastructureFailure
            && (requiredSet.Count == 0 || requiredSet.Contains(result.Language))),
        executedLanguages,
        missingRequired,
        Median(results.Where(result => !result.InfrastructureFailure).Select(result => result.DurationMs).ToArray()),
        skippedCases.Count);
}

static object BuildSummary(
    string repositoryRoot,
    IReadOnlyList<CaseResult> results,
    IReadOnlyList<BugZooCase> skippedCases,
    Options options,
    BugZooMetrics metrics) =>
    new
    {
        schemaVersion = "2.0",
        adapterArchitecture = "universal",
        generatedAt = DateTimeOffset.UtcNow,
        repositoryRoot,
        cases = results.Count,
        injectedBugCases = metrics.BugCases.Count,
        detectedBugCases = metrics.BugCases.Count(result => result.Detected),
        detectionRecallPercent = metrics.Recall,
        cleanControlCases = metrics.CleanCases.Count,
        falsePositiveCases = metrics.CleanCases.Count(result => !result.Detected),
        falsePositiveRatePercent = metrics.FalsePositiveRate,
        reproductionEligibleCases = metrics.ReproducibleCases.Count,
        reproducedCases = metrics.ReproducibleCases.Count(result => result.Reproduced),
        reproductionRatePercent = metrics.ReproductionRate,
        infrastructureFailures = metrics.InfrastructureFailures,
        blockingInfrastructureFailures = metrics.BlockingInfrastructureFailures,
        executedLanguages = metrics.ExecutedLanguages,
        requiredLanguages = options.RequiredLanguages.Select(language => language.Value).ToArray(),
        missingRequiredLanguages = metrics.MissingRequiredLanguages,
        platformSkippedCases = skippedCases.Select(testCase => $"{testCase.Language.Value}/{testCase.Name}").ToArray(),
        medianCaseDurationMs = metrics.MedianCaseDurationMs,
        results
    };

static async Task WriteEvidenceAsync(
    string outputRoot,
    object summary,
    IReadOnlyList<CaseResult> results,
    IReadOnlyList<BugZooCase> skippedCases,
    BugZooMetrics metrics)
{
    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    await File.WriteAllTextAsync(
        Path.Combine(outputRoot, "qa-bug-zoo-results.json"),
        JsonSerializer.Serialize(summary, jsonOptions));
    await File.WriteAllTextAsync(
        Path.Combine(outputRoot, "qa-bug-zoo-results.md"),
        Markdown(
            results,
            metrics.Recall,
            metrics.FalsePositiveRate,
            metrics.ReproductionRate,
            metrics.InfrastructureFailures,
            skippedCases,
            metrics.ExecutedLanguages,
            metrics.MissingRequiredLanguages,
            metrics.MedianCaseDurationMs));
}

static QaExecutionCommand BuildCommand(BugZooCase testCase)
{
    var runtime = testCase.RuntimeExecutable;
    if (string.IsNullOrWhiteSpace(runtime))
    {
        if (testCase.Language == LanguageId.Python && File.Exists(Path.Combine(testCase.Workspace, "app.py")))
        {
            runtime = "app.py";
        }
        else if (testCase.Language == LanguageId.CSharp
                 && (testCase.MustReproduce
                     || string.Equals(testCase.Category, "runtime", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(testCase.Category, "hang", StringComparison.OrdinalIgnoreCase)))
        {
            runtime = CSharpRuntime(testCase.Workspace);
        }
    }

    var timeout = string.Equals(testCase.Category, "hang", StringComparison.OrdinalIgnoreCase) ? 5 : 120;
    return new(
        Guid.Empty,
        $"bug-zoo:{testCase.Language.Value}:{testCase.Name}",
        testCase.Language,
        ".",
        null,
        runtime,
        Array.Empty<string>(),
        timeout,
        false,
        false,
        false);
}

static string? CSharpRuntime(string workspace)
{
    var project = Directory
        .EnumerateFiles(workspace, "*.csproj", SearchOption.TopDirectoryOnly)
        .FirstOrDefault();
    return project is null
        ? null
        : Path.Combine("bin", "Release", "net8.0", Path.GetFileNameWithoutExtension(project) + ".dll");
}

static IEnumerable<QaFindingCandidate> RelevantFindings(
    IEnumerable<QaFindingCandidate> findings,
    BugZooCase testCase)
{
    if (testCase.ExpectNoFindings)
        return Array.Empty<QaFindingCandidate>();

    return findings.Where(finding =>
        string.Equals(finding.Category, testCase.Category, StringComparison.OrdinalIgnoreCase)
        && string.Equals(finding.Severity.ToString(), testCase.Severity, StringComparison.OrdinalIgnoreCase)
        && (string.IsNullOrWhiteSpace(testCase.ExceptionType)
            || string.Equals(finding.ExceptionType, testCase.ExceptionType, StringComparison.OrdinalIgnoreCase))
        && (string.IsNullOrWhiteSpace(testCase.RuleId)
            || string.Equals(finding.RuleId, testCase.RuleId, StringComparison.OrdinalIgnoreCase)));
}

static decimal Percent(int numerator, int denominator) =>
    denominator == 0 ? 0 : decimal.Round(100m * numerator / denominator, 2);

static long Median(long[] values)
{
    if (values.Length == 0)
        return 0;
    Array.Sort(values);
    return values.Length % 2 == 1
        ? values[values.Length / 2]
        : (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2;
}

static void RecreateDirectory(string path)
{
    if (Directory.Exists(path))
        Directory.Delete(path, true);
    Directory.CreateDirectory(path);
}

static string FindRepositoryRoot(string start)
{
    var directory = new DirectoryInfo(Path.GetFullPath(start));
    while (directory is not null)
    {
        if (Directory.Exists(Path.Combine(directory.FullName, "samples", "qa-bug-zoo")))
            return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Unable to locate repository root.");
}

static string Markdown(
    IReadOnlyList<CaseResult> results,
    decimal recall,
    decimal falsePositiveRate,
    decimal reproductionRate,
    int infrastructureFailures,
    IReadOnlyList<BugZooCase> skipped,
    IReadOnlyList<string> languages,
    IReadOnlyList<string> missing,
    long median)
{
    var builder = new StringBuilder();
    builder.AppendLine("# DIP Universal QA Bug Zoo Results").AppendLine();
    builder.AppendLine($"- Executed languages: **{string.Join(", ", languages)}**");
    builder.AppendLine($"- Detection recall: **{recall:0.##}%**");
    builder.AppendLine($"- False-positive rate: **{falsePositiveRate:0.##}%**");
    builder.AppendLine($"- Stable-fingerprint reproduction: **{reproductionRate:0.##}%**");
    builder.AppendLine($"- Infrastructure failures: **{infrastructureFailures}**");
    builder.AppendLine($"- Platform skipped: **{skipped.Count}**");
    builder.AppendLine($"- Missing required languages: **{(missing.Count == 0 ? "none" : string.Join(", ", missing))}**");
    builder.AppendLine($"- Median case duration: **{median} ms**").AppendLine();
    builder.AppendLine("| Language | Adapter | Case | Expected | Detected | Reproduced | Infra | ms |");
    builder.AppendLine("|---|---|---|---|---:|---:|---:|---:|");
    foreach (var result in results)
    {
        builder.AppendLine(
            $"| {result.Language} | {result.Adapter} | {result.Name} | "
            + $"{(result.ExpectNoFindings ? "clean" : result.MatchedCategory ?? "bug")} | "
            + $"{(result.Detected ? "yes" : "no")} | "
            + $"{(result.MustReproduce ? result.Reproduced ? "yes" : "no" : "n/a")} | "
            + $"{(result.InfrastructureFailure ? "fail" : "ok")} | {result.DurationMs} |");
    }
    return builder.ToString();
}

sealed record ToolResult(
    string Tool,
    bool WasAvailable,
    bool Succeeded,
    int? ExitCode,
    long DurationMs);

sealed record CaseResult(
    string Language,
    string Name,
    string Adapter,
    bool ExpectNoFindings,
    bool MustReproduce,
    bool Detected,
    bool Reproduced,
    int ReproducedAttempts,
    int ReproductionAttempts,
    bool InfrastructureFailure,
    long DurationMs,
    int FindingCount,
    string? MatchedCategory,
    string? MatchedSeverity,
    string? MatchedExceptionType,
    string? MatchedRuleId,
    IReadOnlyList<ToolResult> Tools,
    IReadOnlyList<string> BuildSystems,
    IReadOnlyList<string> Frameworks,
    IReadOnlyList<string> GameEngines,
    string? Error);

sealed record BugZooMetrics(
    IReadOnlyList<CaseResult> BugCases,
    IReadOnlyList<CaseResult> CleanCases,
    IReadOnlyList<CaseResult> ReproducibleCases,
    decimal Recall,
    decimal FalsePositiveRate,
    decimal ReproductionRate,
    int InfrastructureFailures,
    int BlockingInfrastructureFailures,
    IReadOnlyList<string> ExecutedLanguages,
    IReadOnlyList<string> MissingRequiredLanguages,
    long MedianCaseDurationMs,
    int SkippedCases);

sealed record BugZooCase(
    LanguageId Language,
    string Name,
    string Workspace,
    bool ExpectNoFindings,
    string? Category,
    string? Severity,
    string? ExceptionType,
    string? RuleId,
    bool MustReproduce,
    bool OptionalOnWindows,
    bool OptionalOnNonWindows,
    string? RuntimeExecutable)
{
    public static BugZooCase Load(string zooRoot, string expectedPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(expectedPath));
        var json = document.RootElement;
        var workspace = Path.GetDirectoryName(expectedPath)!;
        var relative = Path
            .GetRelativePath(zooRoot, workspace)
            .Replace('\\', '/')
            .Split('/');
        if (relative.Length < 2)
            throw new InvalidDataException($"Unexpected Bug Zoo path: {workspace}");

        return new(
            LanguageId.Parse(relative[0]),
            relative[1],
            workspace,
            Bool(json, "expectNoFindings"),
            String(json, "category"),
            String(json, "severity"),
            String(json, "exceptionType"),
            String(json, "ruleId"),
            Bool(json, "mustReproduce"),
            Bool(json, "optionalOnWindows"),
            Bool(json, "optionalOnNonWindows"),
            String(json, "runtimeExecutable"));
    }

    public bool IsSupportedOnCurrentPlatform() =>
        !(OperatingSystem.IsWindows() && OptionalOnWindows)
        && !(!OperatingSystem.IsWindows() && OptionalOnNonWindows);

    private static string? String(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool Bool(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}

sealed record Options(
    string? Root,
    string? Output,
    bool AllowMissingTools,
    IReadOnlyList<LanguageId> RequiredLanguages)
{
    public static Options Parse(string[] args)
    {
        string? root = null;
        string? output = null;
        var allowMissingTools = false;
        var required = new List<LanguageId>();

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--root" when index + 1 < args.Length:
                    root = args[++index];
                    break;
                case "--output" when index + 1 < args.Length:
                    output = args[++index];
                    break;
                case "--allow-missing-tools":
                    allowMissingTools = true;
                    break;
                case "--require-languages" when index + 1 < args.Length:
                    required.AddRange(
                        args[++index]
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(LanguageId.Parse));
                    break;
            }
        }

        return new(root, output, allowMissingTools, required);
    }
}
