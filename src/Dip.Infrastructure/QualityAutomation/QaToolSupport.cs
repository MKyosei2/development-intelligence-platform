using System.Diagnostics;
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

internal static class QaToolSupport
{
    public static async Task<(QaToolExecution Tool,IReadOnlyList<QaFindingCandidate> Findings,ProcessExecutionResult? Process)> ExecuteAsync(ProcessExecutor executor,UniversalDiagnosticNormalizer normalizer,string toolName,string executable,IEnumerable<string> args,string workspace,string artifactRoot,IReadOnlyDictionary<string,string>? environment,TimeSpan timeout,LanguageId language,Severity fallbackSeverity,string fallbackCategory,CancellationToken ct)
    {
        var safe=string.Concat(toolName.Select(c=>char.IsLetterOrDigit(c)?char.ToLowerInvariant(c):'-'));var dir=Path.Combine(artifactRoot,"tools",safe);Directory.CreateDirectory(dir);var stdout=Path.Combine(dir,"stdout.log");var stderr=Path.Combine(dir,"stderr.log");
        var process=await executor.RunQaAsync(executable,args,workspace,environment,timeout,stdout,stderr,ct);var combined=process.StandardOutput+"\n"+process.StandardError;var unavailable=process.ExitCode is null&&!process.TimedOut||combined.Contains("No module named",StringComparison.OrdinalIgnoreCase)&&combined.Contains(toolName,StringComparison.OrdinalIgnoreCase);
        var findings=normalizer.NormalizeText(language,toolName,process.StandardOutput,process.StandardError,process.TimedOut).ToList();
        if(!unavailable&&!process.TimedOut&&process.ExitCode is not 0&&findings.Count==0)findings.Add(new(fallbackCategory,fallbackSeverity,toolName,$"{toolName} failed",LastUsefulLine(combined),RuleId:toolName));
        var succeeded=!process.TimedOut&&process.ExitCode==0;var summary=unavailable?$"{toolName} is unavailable; the tool-specific check was skipped.":succeeded?$"{toolName} completed successfully.":$"{toolName} failed with exit code {(process.ExitCode?.ToString()??"timeout/start failure")}.";
        var resolved=ResolveExecutable(executable);var version=ResolveVersion(resolved);var sandboxJson=process.Sandbox is null?null:JsonSerializer.Serialize(process.Sandbox);
        return(new(toolName,!unavailable,succeeded,process.ExitCode,process.DurationMs,stdout,stderr,summary,resolved??executable,version,sandboxJson,process.CpuTimeMs),findings,process);
    }
    public static string JsonSummary(LanguageId language,IReadOnlyList<QaToolExecution> tools,IReadOnlyList<QaFindingCandidate> findings)=>JsonSerializer.Serialize(new{language=language.Value,tools=tools.Select(x=>new{x.Tool,x.WasAvailable,x.Succeeded,x.ExitCode,x.DurationMs,x.Summary,x.Executable,x.Version,x.SandboxJson,x.CpuTimeMs}),findingCount=findings.Count,critical=findings.Count(x=>x.Severity==Severity.Critical),warning=findings.Count(x=>x.Severity==Severity.Warning)});
    private static string LastUsefulLine(string text)=>text.Split(['\r','\n'],StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim()??"Command failed without textual diagnostics.";
    private static string? ResolveExecutable(string executable)=>Path.IsPathRooted(executable)?(File.Exists(executable)?Path.GetFullPath(executable):null):ProcessExecutor.FindExecutable(executable);
    private static string? ResolveVersion(string? executable){if(string.IsNullOrWhiteSpace(executable)||!File.Exists(executable))return null;try{var info=FileVersionInfo.GetVersionInfo(executable);return string.IsNullOrWhiteSpace(info.ProductVersion)?info.FileVersion:info.ProductVersion;}catch{return null;}}
}
