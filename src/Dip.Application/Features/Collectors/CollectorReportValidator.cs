using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Shared.Contracts.Collectors;
namespace Dip.Application.Features.Collectors;
public sealed class CollectorReportValidator
{
    const int MaximumItems=10_000; const int MaximumEvidenceBytes=256*1024;
    static readonly Regex ShaPattern=new("^[a-fA-F0-9]{7,64}$",RegexOptions.Compiled);
    static readonly HashSet<string> Severities=new(StringComparer.OrdinalIgnoreCase){"Info","Warning","Critical"};
    public void ValidateAndThrow(CollectorReport report){ArgumentNullException.ThrowIfNull(report);var errors=Validate(report);if(errors.Count>0)throw new ArgumentException(string.Join(" ",errors));}
    public IReadOnlyList<string> Validate(CollectorReport? report)
    {
        var errors=new List<string>(); if(report is null){errors.Add("Report is required.");return errors;}
        if(report.SchemaVersion!="1.0")errors.Add("SchemaVersion must be '1.0'.");
        Required(report.ReportId,200,nameof(report.ReportId),errors);Required(report.ExternalRunId,200,nameof(report.ExternalRunId),errors);Required(report.Repository,300,nameof(report.Repository),errors);Required(report.Branch,200,nameof(report.Branch),errors);Required(report.CollectorType,100,nameof(report.CollectorType),errors);Required(report.CollectorVersion,50,nameof(report.CollectorVersion),errors);
        if(report.ProjectId==Guid.Empty)errors.Add("ProjectId must not be empty.");if(report.AttemptNumber<1)errors.Add("AttemptNumber must be at least 1.");if(!ShaPattern.IsMatch(report.CommitSha??string.Empty))errors.Add("CommitSha must contain 7-64 hexadecimal characters.");
        if(report.StartedAt==default||report.FinishedAt==default)errors.Add("StartedAt and FinishedAt are required.");else if(report.FinishedAt<report.StartedAt)errors.Add("FinishedAt must not precede StartedAt.");
        if(report.Metrics is null)errors.Add("Metrics is required.");else if(report.Metrics.Count>MaximumItems)errors.Add($"Metrics must not exceed {MaximumItems:N0}.");else foreach(var m in report.Metrics){Required(m.Name,120,"Metric.Name",errors);Required(m.Unit,30,"Metric.Unit",errors);}
        if(report.Issues is null)errors.Add("Issues is required.");else if(report.Issues.Count>MaximumItems)errors.Add($"Issues must not exceed {MaximumItems:N0}.");else foreach(var issue in report.Issues){Required(issue.Category,80,"Issue.Category",errors);Required(issue.Severity,20,"Issue.Severity",errors);Required(issue.Title,200,"Issue.Title",errors);Required(issue.Description,4000,"Issue.Description",errors);if(!string.IsNullOrWhiteSpace(issue.Severity)&&!Severities.Contains(issue.Severity))errors.Add("Issue.Severity must be Info, Warning, or Critical.");if(!string.IsNullOrWhiteSpace(issue.EvidenceJson)){if(System.Text.Encoding.UTF8.GetByteCount(issue.EvidenceJson)>MaximumEvidenceBytes)errors.Add("Issue.EvidenceJson is too large.");else try{using var _=JsonDocument.Parse(issue.EvidenceJson);}catch(JsonException){errors.Add("Issue.EvidenceJson must contain valid JSON.");}}}
        return errors;
    }
    static void Required(string? value,int max,string name,ICollection<string> errors){var n=value?.Trim().Length??0;if(n<1||n>max)errors.Add($"{name} length must be 1-{max}.");}
}
