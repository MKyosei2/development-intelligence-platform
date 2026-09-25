// Purpose: Implements C# QA Engine infrastructure responsibilities for quality automation.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

public sealed class CSharpQaEngine(ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer, IUniversalSemanticAnalyzer semantic) : IQaAdapter
{
    public CSharpQaEngine(ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer) : this(executor, normalizer, new UniversalSemanticAnalyzer()) { }
    public string AdapterId => "csharp";
    public int Priority => 500;
    public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace) => language == LanguageId.CSharp;

    public async Task<QaAdapterResult> RunAsync(QaExecutionCommand command, string workspace, string artifactRoot, CancellationToken ct)
    {
        var dotnet = ProcessExecutor.FindExecutable("dotnet") ?? throw new FileNotFoundException("dotnet SDK was not found in PATH.");
        var tools = new List<QaToolExecution>(); var findings = new List<QaFindingCandidate>();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds, 5, 1800));
        var semanticResult = await semantic.AnalyzeAsync(new(LanguageId.CSharp, workspace), ct); findings.AddRange(semanticResult.Findings);
        tools.Add(new("dip-semantic", true, true, 0, 0, string.Empty, string.Empty, $"DIP semantic analysis scanned {semanticResult.FilesAnalyzed} C# source file(s).", Version: "1.0"));
        await Run("dotnet-restore", dotnet, ["restore"], Severity.Critical, "restore");
        await Run("dotnet-build", dotnet, ["build", "--no-restore", "-c", "Release", "-v", "minimal"], Severity.Critical, "compile");
        await Run("dotnet-test", dotnet, ["test", "--no-build", "-c", "Release", "--logger", "console;verbosity=minimal"], Severity.Warning, "test");
        await RunOptional("dotnet-format-analyzers", dotnet, ["format", "analyzers", "--verify-no-changes", "--no-restore", "--verbosity", "minimal"], Severity.Warning, "static-analysis");
        if (!string.IsNullOrWhiteSpace(command.RuntimeExecutable))
        {
            var runtime = command.RuntimeExecutable!; string executable; IEnumerable<string> args;
            if (runtime.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) { executable = dotnet; args = new[] { runtime }.Concat(command.RuntimeArguments ?? Array.Empty<string>()); }
            else { executable = Path.IsPathRooted(runtime) ? runtime : Path.GetFullPath(Path.Combine(workspace, runtime)); args = command.RuntimeArguments ?? Array.Empty<string>(); }
            await Run("dotnet-runtime", executable, args, Severity.Critical, "runtime");
        }
        return new(LanguageId.CSharp, tools, findings, QaToolSupport.JsonSummary(LanguageId.CSharp, tools, findings));
        async Task Run(string name, string executable, IEnumerable<string> args, Severity fallback, string category)
        {
            var result = await QaToolSupport.ExecuteAsync(executor, normalizer, name, executable, args, workspace, artifactRoot, null, timeout, LanguageId.CSharp, fallback, category, ct);
            tools.Add(result.Tool);
            findings.AddRange(result.Findings);
        }
        async Task RunOptional(string name, string executable, IEnumerable<string> args, Severity fallback, string category)
        {
            var result = await QaToolSupport.ExecuteAsync(executor, normalizer, name, executable, args, workspace, artifactRoot, null, timeout, LanguageId.CSharp, fallback, category, ct);
            if (result.Process is not null && (result.Process.StandardError.Contains("Could not execute because", StringComparison.OrdinalIgnoreCase) || result.Process.StandardError.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                tools.Add(result.Tool with { WasAvailable = false, Summary = $"{name} is unavailable; optional analysis was skipped." });
            else { tools.Add(result.Tool); findings.AddRange(result.Findings); }
        }
    }
}
