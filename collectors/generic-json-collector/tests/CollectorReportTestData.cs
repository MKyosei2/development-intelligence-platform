// Purpose: Provides valid collector-report fixtures and temporary-file helpers for Generic JSON Collector tests.
using System.Text.Json;
using System.Text.Json.Serialization;
using GenericJsonCollector.Models;

namespace GenericJsonCollector.Tests;

internal static class CollectorReportTestData
{
    public static CollectorReport CreateValid(
        IReadOnlyList<CollectorMetric>? metrics = null,
        IReadOnlyList<CollectorIssue>? issues = null)
    {
        var started = DateTimeOffset.UtcNow.AddMinutes(-2);
        return new CollectorReport(
            SchemaVersion: "1.0",
            ReportId: "report-001",
            ExternalRunId: "run-001",
            AttemptNumber: 1,
            CollectorType: "generic-json",
            CollectorVersion: "1.0.0",
            ProjectId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Repository: "openai/example",
            CommitSha: "abcdef1",
            Branch: "main",
            StartedAt: started,
            FinishedAt: started.AddMinutes(1),
            Status: CollectorRunStatus.Succeeded,
            Metrics: metrics ?? Array.Empty<CollectorMetric>(),
            Issues: issues ?? Array.Empty<CollectorIssue>());
    }

    public static string Serialize(CollectorReport report)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(report, options);
    }

    public static string NewTempPath()
        => Path.Combine(Path.GetTempPath(), "dip-generic-json-tests", Guid.NewGuid().ToString("N"), "report.json");

    public static void Cleanup(string path)
    {
        var directory = Directory.GetParent(path)?.FullName;
        if (directory is not null && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
