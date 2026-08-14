using System.Text.Json;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Infrastructure.QualityAutomation;

internal sealed record QaManifest(
    string? Language,
    IReadOnlyList<QaManifestStep> Steps)
{
    public static QaManifest? TryLoad(string workspace)
    {
        var path=Path.Combine(workspace,".dipqa","qa.json");
        if(!File.Exists(path))return null;
        var options=new JsonSerializerOptions{PropertyNameCaseInsensitive=true};
        var manifest=JsonSerializer.Deserialize<QaManifest>(File.ReadAllText(path),options)
            ??throw new InvalidDataException(".dipqa/qa.json is empty or invalid.");
        if(manifest.Steps is null||manifest.Steps.Count==0)throw new InvalidDataException(".dipqa/qa.json must contain at least one QA step.");
        if(!string.IsNullOrWhiteSpace(manifest.Language)&&!LanguageId.TryParse(manifest.Language,out _))
            throw new InvalidDataException($"Invalid manifest language id '{manifest.Language}'.");
        return manifest;
    }
}

internal sealed record QaManifestStep(
    string Name,
    string Executable,
    IReadOnlyList<string>? Arguments=null,
    string Category="diagnostic",
    string Severity="Warning",
    string? DiagnosticFormat=null,
    string? DiagnosticPath=null,
    int? TimeoutSeconds=null,
    IReadOnlyDictionary<string,string>? Environment=null)
{
    public Severity ParsedSeverity=>Severity.Trim().ToLowerInvariant() switch
    {
        "critical" or "error" or "fatal" or "high"=>Dip.Domain.Enums.Severity.Critical,
        "info" or "information" or "note" or "low"=>Dip.Domain.Enums.Severity.Info,
        _=>Dip.Domain.Enums.Severity.Warning
    };
}
