using System.Text.Json;
using System.Text.RegularExpressions;
using GenericJsonCollector.Models;

namespace GenericJsonCollector.Validation;

public sealed class CollectorReportValidator
{
    private const int MaximumItems = 10_000;
    private const int MaximumEvidenceBytes = 256 * 1024;

    private static readonly Regex ShaPattern =
        new("^[a-fA-F0-9]{7,64}$", RegexOptions.Compiled);

    private static readonly HashSet<string> AllowedSeverities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Info",
            "Warning",
            "Critical"
        };

    public IReadOnlyList<string> Validate(CollectorReport? report)
    {
        var errors = new List<string>();

        if (report is null)
        {
            errors.Add("Report is required.");
            return errors;
        }

        if (report.SchemaVersion != "1.0")
            errors.Add("SchemaVersion must be '1.0'.");

        Required(report.ReportId, 200, nameof(report.ReportId), errors);
        Required(report.ExternalRunId, 200, nameof(report.ExternalRunId), errors);
        Required(report.Repository, 300, nameof(report.Repository), errors);
        Required(report.Branch, 200, nameof(report.Branch), errors);
        Required(report.CollectorType, 100, nameof(report.CollectorType), errors);
        Required(report.CollectorVersion, 50, nameof(report.CollectorVersion), errors);

        if (report.ProjectId == Guid.Empty)
            errors.Add("ProjectId must not be empty.");

        if (report.AttemptNumber < 1)
            errors.Add("AttemptNumber must be at least 1.");

        if (!ShaPattern.IsMatch(report.CommitSha ?? string.Empty))
            errors.Add("CommitSha must contain 7-64 hexadecimal characters.");

        if (!RepositoryIsValid(report.Repository))
            errors.Add(
                "Repository must be an absolute HTTP(S) URL or owner/name.");

        if (report.StartedAt == default || report.FinishedAt == default)
            errors.Add("StartedAt and FinishedAt are required.");
        else if (report.FinishedAt < report.StartedAt)
            errors.Add("FinishedAt must not precede StartedAt.");

        var futureLimit = DateTimeOffset.UtcNow.AddHours(24);
        if (report.StartedAt > futureLimit || report.FinishedAt > futureLimit)
            errors.Add(
                "Collector timestamps must not be more than 24 hours in the future.");

        ValidateMetrics(report.Metrics, errors);
        ValidateIssues(report.Issues, errors);

        return errors;
    }

    private static void ValidateMetrics(
        IReadOnlyList<CollectorMetric>? metrics,
        ICollection<string> errors)
    {
        if (metrics is null)
        {
            errors.Add("Metrics is required.");
            return;
        }

        if (metrics.Count > MaximumItems)
        {
            errors.Add($"Metrics must not exceed {MaximumItems:N0}.");
            return;
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < metrics.Count; index++)
        {
            var metric = metrics[index];
            if (metric is null)
            {
                errors.Add($"Metrics[{index}] must not be null.");
                continue;
            }

            Required(metric.Name, 120, $"Metrics[{index}].Name", errors);
            Required(metric.Unit, 30, $"Metrics[{index}].Unit", errors);
            Optional(metric.EnvironmentId, 100, $"Metrics[{index}].EnvironmentId", errors);
            Optional(metric.ScenarioId, 100, $"Metrics[{index}].ScenarioId", errors);
            Optional(metric.Platform, 100, $"Metrics[{index}].Platform", errors);
            Optional(metric.BuildConfiguration, 100, $"Metrics[{index}].BuildConfiguration", errors);
            Optional(metric.BenchmarkVersion, 100, $"Metrics[{index}].BenchmarkVersion", errors);

            var key =
                $"{metric.Name?.Trim()}|{metric.EnvironmentId?.Trim()}|" +
                $"{metric.ScenarioId?.Trim()}|{metric.Platform?.Trim()}|" +
                $"{metric.BuildConfiguration?.Trim()}";

            if (!keys.Add(key))
                errors.Add($"Duplicate metric key: {key}.");
        }
    }

    private static void ValidateIssues(
        IReadOnlyList<CollectorIssue>? issues,
        ICollection<string> errors)
    {
        if (issues is null)
        {
            errors.Add("Issues is required.");
            return;
        }

        if (issues.Count > MaximumItems)
        {
            errors.Add($"Issues must not exceed {MaximumItems:N0}.");
            return;
        }

        for (var index = 0; index < issues.Count; index++)
        {
            var issue = issues[index];
            if (issue is null)
            {
                errors.Add($"Issues[{index}] must not be null.");
                continue;
            }

            Required(issue.Category, 80, $"Issues[{index}].Category", errors);
            Required(issue.Severity, 20, $"Issues[{index}].Severity", errors);
            Required(issue.Title, 200, $"Issues[{index}].Title", errors);
            Required(issue.Description, 4000, $"Issues[{index}].Description", errors);
            Optional(issue.Source, 1000, $"Issues[{index}].Source", errors);
            Optional(issue.RuleId, 200, $"Issues[{index}].RuleId", errors);

            if (!string.IsNullOrWhiteSpace(issue.Severity)
                && !AllowedSeverities.Contains(issue.Severity))
                errors.Add(
                    $"Issues[{index}].Severity must be Info, Warning, or Critical.");

            if (string.IsNullOrWhiteSpace(issue.EvidenceJson))
                continue;

            if (System.Text.Encoding.UTF8.GetByteCount(issue.EvidenceJson)
                > MaximumEvidenceBytes)
            {
                errors.Add(
                    $"Issues[{index}].EvidenceJson exceeds {MaximumEvidenceBytes:N0} bytes.");
                continue;
            }

            try
            {
                using var _ = JsonDocument.Parse(issue.EvidenceJson);
            }
            catch (JsonException)
            {
                errors.Add(
                    $"Issues[{index}].EvidenceJson must contain valid JSON.");
            }
        }
    }

    private static bool RepositoryIsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return uri.Scheme is "http" or "https";

        var parts = trimmed.Split(
            '/',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length == 2
               && parts.All(part => part.Length is >= 1 and <= 100);
    }

    private static void Required(
        string? value,
        int maximumLength,
        string name,
        ICollection<string> errors)
    {
        var length = value?.Trim().Length ?? 0;
        if (length is < 1 || length > maximumLength)
            errors.Add($"{name} length must be 1-{maximumLength}.");
    }

    private static void Optional(
        string? value,
        int maximumLength,
        string name,
        ICollection<string> errors)
    {
        if (value is not null && value.Trim().Length > maximumLength)
            errors.Add($"{name} length must not exceed {maximumLength}.");
    }
}
