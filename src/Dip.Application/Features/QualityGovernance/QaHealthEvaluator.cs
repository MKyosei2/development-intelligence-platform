// Purpose: Evaluates QA orchestrator and tool self-health separately from product findings.
using System.Text.Json;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityGovernance;

public sealed record QaHealthEvaluation(
    QaSystemHealthStatus Status,
    string Detail,
    IReadOnlyList<QaSystemHealthSnapshot> ToolSnapshots);

public sealed class QaHealthEvaluator
{
    public QaHealthEvaluation Evaluate(QaRun run, DateTimeOffset now)
    {
        var status = AssessSelfHealth(run);
        return new(status, HealthDetail(status), BuildToolHealthSnapshots(run, now));
    }

    private static QaSystemHealthStatus AssessSelfHealth(QaRun run)
    {
        if (run.Status == QaRunStatus.InfrastructureFailure)
            return QaSystemHealthStatus.Unavailable;
        if (string.IsNullOrWhiteSpace(run.SummaryJson))
            return QaSystemHealthStatus.Degraded;

        try
        {
            using var document = JsonDocument.Parse(run.SummaryJson);
            var root = document.RootElement;
            var adapterFailures =
                root.TryGetProperty("adapterFailures", out var failures)
                && failures.ValueKind == JsonValueKind.Array
                    ? failures.GetArrayLength() : 0;
            var unavailable =
                root.TryGetProperty("unavailableLanguages", out var languages)
                && languages.ValueKind == JsonValueKind.Array
                    ? languages.GetArrayLength() : 0;
            var unsupported =
                root.TryGetProperty("unsupportedDetectedLanguages", out var detected)
                && detected.ValueKind == JsonValueKind.Array
                    ? detected.GetArrayLength() : 0;
            return adapterFailures + unavailable + unsupported > 0
                ? QaSystemHealthStatus.Degraded
                : QaSystemHealthStatus.Healthy;
        }
        catch (JsonException)
        {
            return QaSystemHealthStatus.Degraded;
        }
    }

    private static IReadOnlyList<QaSystemHealthSnapshot> BuildToolHealthSnapshots(QaRun run, DateTimeOffset now)
    {
        var result = new List<QaSystemHealthSnapshot>();
        if (string.IsNullOrWhiteSpace(run.SummaryJson))
            return result;

        try
        {
            using var document = JsonDocument.Parse(run.SummaryJson);
            if (!document.RootElement.TryGetProperty("tools", out var tools)
                || tools.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var tool in tools.EnumerateArray().Take(100))
            {
                var name = ReadString(tool, "Tool") ?? ReadString(tool, "tool") ?? "unknown";
                var available = ReadBool(tool, "WasAvailable") ?? ReadBool(tool, "wasAvailable") ?? false;
                var sandboxJson = ReadString(tool, "SandboxJson") ?? ReadString(tool, "sandboxJson");
                var sandboxEnabled = ReadSandboxEnabled(sandboxJson);
                var status = !available || sandboxEnabled == false
                    ? QaSystemHealthStatus.Degraded
                    : QaSystemHealthStatus.Healthy;
                var detail = !available
                    ? "QA dependency/tool was unavailable; this is infrastructure evidence, not a product defect."
                    : sandboxEnabled == false
                        ? "QA tool executed without the configured process sandbox boundary."
                        : "QA tool was available and the configured execution boundary was applied when supported.";

                result.Add(new QaSystemHealthSnapshot(
                    Guid.NewGuid(), run.ProjectId, run.Id, Clip("tool:" + name, 120),
                    status, detail, now));
            }
        }
        catch (JsonException)
        {
            // Malformed tool detail keeps the orchestrator health degraded.
        }

        return result;
    }

    private static bool? ReadSandboxEnabled(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return ReadBool(document.RootElement, "Enabled")
                ?? ReadBool(document.RootElement, "enabled");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? ReadBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : null;

    private static string Clip(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string HealthDetail(QaSystemHealthStatus health) => health switch
    {
        QaSystemHealthStatus.Unavailable =>
            "QA adapters/tools were unavailable or failed before product quality could be determined.",
        QaSystemHealthStatus.Degraded =>
            "QA infrastructure completed with an unavailable adapter/tool, unsupported detected technology, or incomplete execution boundary. Product findings are tracked separately from QA self-health.",
        _ =>
            "QA orchestration infrastructure completed successfully. Product failures, warnings, and regressions are tracked separately from QA self-health."
    };
}
