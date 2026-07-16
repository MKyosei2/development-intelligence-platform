using GenericJsonCollector.Models;

namespace GenericJsonCollector.Validation;

public sealed class CollectorReportValidator
{
    private static readonly HashSet<string> AllowedSeverities =
        new(StringComparer.OrdinalIgnoreCase) { "Info", "Warning", "Critical" };

    public IReadOnlyList<string> Validate(CollectorReport report)
    {
        var errors = new List<string>();

        if (report.SchemaVersion != "1.0")
            errors.Add("SchemaVersion must be '1.0'.");

        if (report.ProjectId == Guid.Empty)
            errors.Add("ProjectId must not be empty.");

        if (string.IsNullOrWhiteSpace(report.CollectorType))
            errors.Add("CollectorType is required.");

        if (string.IsNullOrWhiteSpace(report.CollectorVersion))
            errors.Add("CollectorVersion is required.");

        if (report.CommitSha.Length is < 7 or > 64 || !report.CommitSha.All(Uri.IsHexDigit))
            errors.Add("CommitSha must be a 7-64 character hexadecimal string.");

        if (string.IsNullOrWhiteSpace(report.Branch))
            errors.Add("Branch is required.");

        if (report.FinishedAt < report.StartedAt)
            errors.Add("FinishedAt must not be earlier than StartedAt.");

        foreach (var metric in report.Metrics)
        {
            if (string.IsNullOrWhiteSpace(metric.Name))
                errors.Add("Metric name is required.");
            if (string.IsNullOrWhiteSpace(metric.Unit))
                errors.Add($"Metric '{metric.Name}' must have a unit.");
        }

        foreach (var issue in report.Issues)
        {
            if (!AllowedSeverities.Contains(issue.Severity))
                errors.Add($"Issue severity '{issue.Severity}' is invalid.");
            if (string.IsNullOrWhiteSpace(issue.Title))
                errors.Add("Issue title is required.");
        }

        return errors;
    }
}
