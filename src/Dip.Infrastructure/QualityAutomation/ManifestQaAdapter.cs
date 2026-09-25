// Purpose: Implements Manifest QA Adapter infrastructure behavior for quality automation.
using System.Text.Json;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

/// <summary>Executes declarative QA commands from .dip/qa.json without invoking a shell.</summary>
public sealed class ManifestQaAdapter(ProcessExecutor executor, UniversalDiagnosticNormalizer normalizer) : IQaAdapter
{
    public string AdapterId => "manifest"; public int Priority => 1000;
    public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace) => FindLanguage(workspace, language) is not null;
    public async Task<QaAdapterResult> RunAsync(QaExecutionCommand command, string workspace, string artifactRoot, CancellationToken ct)
    {
        var spec = FindLanguage(workspace, command.Language) ?? throw new InvalidOperationException($"No .dip/qa.json entry for {command.Language}.");
        var tools = new List<QaToolExecution>();
        var findings = new List<QaFindingCandidate>();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(command.TimeoutSeconds, 5, 1800));
        foreach (var c in spec.Commands ?? Array.Empty<CommandSpec>())
        {
            IEnumerable<string?> filesToRun = c.PerFile
                ? FilesFor(workspace, spec.Extensions).Select(x => (string?)x)
                : new string?[] { null };
            var files = filesToRun.Take(250).ToArray(); if (files.Length == 0 && c.PerFile) continue;
            foreach (var file in files)
            {
                var exe = ResolveExecutable(workspace, c.Executable);
                if (exe is null) { tools.Add(new(c.Name, false, false, null, 0, "", "", $"{c.Executable} is unavailable."));
                 continue;
                 }
                var args = (c.Arguments ?? Array.Empty<string>()).Select(x => Expand(x, workspace, artifactRoot, file)).ToArray();
                var result = await QaToolSupport.ExecuteAsync(executor, normalizer, c.Name, exe, args, workspace, artifactRoot, null, timeout, command.Language, SeverityFrom(c.Severity), string.IsNullOrWhiteSpace(c.Category) ? "diagnostic" : c.Category!, ct);
                tools.Add(result.Tool);
                findings.AddRange(result.Findings);
                foreach (var glob in c.Reports ?? Array.Empty<string>()) foreach (var report in ExpandGlob(workspace,
                    glob).Take(100)) findings.AddRange(normalizer.NormalizeReportFile(report,
                    command.Language,
                    c.Name));
            }
        }
        return new(command.Language, tools, findings, QaToolSupport.JsonSummary(command.Language, tools, findings));
    }
    private static LanguageSpec? FindLanguage(string workspace, LanguageId language)
    {
        var path = Path.Combine(workspace, ".dip", "qa.json");
        if (!File.Exists(path)) return null;
        try
        {
            var root = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return root?.Languages?.FirstOrDefault(x => LanguageId.TryParse(x.Id, out var id) && id == language);
        }
        catch (JsonException)
        {
            return null;
        }
    }
    private static string? ResolveExecutable(string workspace, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Path.IsPathRooted(value)) return File.Exists(value) ? value : null;
        var local = Path.GetFullPath(Path.Combine(workspace, value));
        var root = Path.GetFullPath(workspace);
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (local.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && File.Exists(local)) return local;
        // Names containing a path separator are never reinterpreted relative to PATH;
        // this prevents a manifest from escaping the workspace through ../ segments.
        if (value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar)) return null;
        return ProcessExecutor.FindExecutable(value);
    }
    private static IEnumerable<string> FilesFor(string workspace, string[]? ext)
    {
        var wanted = (ext ?? Array.Empty<string>()).Select(x => x.StartsWith('.') ? x : "." + x).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ignored = new[] { ".git", "node_modules", "bin", "obj", "artifacts", ".dipqa", "Library", "Temp", "Logs" };
        return Directory.EnumerateFiles(workspace, "*", SearchOption.AllDirectories)
            .Where(x => wanted.Count == 0 || wanted.Contains(Path.GetExtension(x)))
            .Where(x => ignored.All(name => !x.Contains(Path.DirectorySeparatorChar + name + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));
    }
    private static string Expand(string value, string workspace, string artifact, string? file) => value.Replace("{workspace}", workspace, StringComparison.Ordinal).Replace("{artifact}", artifact, StringComparison.Ordinal).Replace("{file}", file ?? string.Empty, StringComparison.Ordinal);
    private static IEnumerable<string> ExpandGlob(string workspace, string glob)
    {
        var normalized = glob.Replace('\\', '/');
        var dir = Path.GetDirectoryName(normalized.Replace('/', Path.DirectorySeparatorChar));
        var pattern = Path.GetFileName(normalized);
        var baseDir = Path.GetFullPath(Path.Combine(workspace, dir ?? "."));
        var prefix = Path.GetFullPath(workspace).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!baseDir.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && baseDir != Path.GetFullPath(workspace)) return Array.Empty<string>();
        return Directory.Exists(baseDir) ? Directory.EnumerateFiles(baseDir, pattern, SearchOption.AllDirectories) : Array.Empty<string>();
    }
    private static Severity SeverityFrom(string? s) => s?.Equals("critical", StringComparison.OrdinalIgnoreCase) == true || s?.Equals("error", StringComparison.OrdinalIgnoreCase) == true ? Severity.Critical : Severity.Warning;
    private sealed record Manifest(LanguageSpec[]? Languages);
    private sealed record LanguageSpec(string Id, string[]? Extensions, CommandSpec[]? Commands);
    private sealed record CommandSpec(string Name, string Executable, string[]? Arguments, bool PerFile, string? Category, string? Severity, string[]? Reports);
}
