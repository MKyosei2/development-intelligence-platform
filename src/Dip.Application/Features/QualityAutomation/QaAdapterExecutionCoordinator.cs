// Purpose: Executes language adapters, cross-cutting QA packs, and configured media regressions.
using System.Text.Json;
using Dip.Application.Features.P2Intelligence;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Features.QualityAutomation;

internal sealed record QaAdapterExecutionBatch(
    IReadOnlyList<(Dip.Domain.ValueObjects.LanguageId Language, QaAdapterResult Result)> Outputs,
    IReadOnlyList<QaCrossCuttingResult> CrossResults,
    IReadOnlyList<object> AdapterFailures);

public sealed class QaAdapterExecutionCoordinator(
    IMediaRegressionRunner mediaRegression,
    IEnumerable<ICrossCuttingQaPack> crossPacks)
{
    private readonly IReadOnlyList<ICrossCuttingQaPack> _crossPacks = crossPacks.ToArray();

    internal async Task<QaAdapterExecutionBatch> ExecuteAsync(
        QaExecutionPlan plan,
        CancellationToken ct)
    {
        var command = plan.Command;
        var outputs = new List<(Dip.Domain.ValueObjects.LanguageId Language, QaAdapterResult Result)>();
        var failures = new List<object>();

        foreach (var selection in plan.Selections)
        {
            try
            {
                var child = command with
                {
                    Language = selection.Language,
                    AutomaticallyReproduce = false
                };
                var childRoot = Path.Combine(
                    plan.ArtifactRoot,
                    "languages",
                    selection.Language.Value);
                Directory.CreateDirectory(childRoot);
                var result = await selection.Adapter.RunAsync(
                    child,
                    plan.Workspace,
                    childRoot,
                    ct);
                outputs.Add((selection.Language, result));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new
                {
                    language = selection.Language.Value,
                    adapter = selection.Adapter.AdapterId,
                    error = ex.Message,
                    type = ex.GetType().FullName
                });
                var tool = new QaToolExecution(
                    $"adapter:{selection.Adapter.AdapterId}",
                    false,
                    false,
                    null,
                    0,
                    "",
                    "",
                    ex.Message);
                outputs.Add((
                    selection.Language,
                    new QaAdapterResult(
                        selection.Language,
                        [tool],
                        [],
                        JsonSerializer.Serialize(new { error = ex.Message }))));
            }
        }

        var crossResults = new List<QaCrossCuttingResult>();
        foreach (var pack in _crossPacks.Where(
                     x => x.Applies(plan.Profile, plan.Workspace)))
        {
            try
            {
                var packRoot = Path.Combine(
                    plan.ArtifactRoot,
                    "cross-cutting",
                    pack.PackId);
                Directory.CreateDirectory(packRoot);
                crossResults.Add(
                    await pack.RunAsync(
                        command,
                        plan.Profile,
                        plan.Workspace,
                        packRoot,
                        ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new
                {
                    language = "cross-cutting",
                    adapter = pack.PackId,
                    error = ex.Message,
                    type = ex.GetType().FullName
                });
                crossResults.Add(new(
                    pack.PackId,
                    [new QaToolExecution(
                        $"pack:{pack.PackId}",
                        false,
                        false,
                        null,
                        0,
                        "",
                        "",
                        ex.Message)],
                    [],
                    JsonSerializer.Serialize(new { error = ex.Message })));
            }
        }

        foreach (var spec in ProductionQaIntelligence.ReadMediaRegressionSpecs(
                     plan.Workspace))
        {
            if (!Enum.TryParse<MediaRegressionType>(spec.Type, true, out var mediaType))
            {
                crossResults.Add(new QaCrossCuttingResult(
                    "media-regression-config",
                    [new QaToolExecution(
                        "dip-media-regression-config",
                        true,
                        false,
                        1,
                        0,
                        "",
                        "",
                        "Unsupported media regression type in .dip/production.json.")],
                    [new QaFindingCandidate(
                        "media-regression-config",
                        Severity.Warning,
                        "dip-media-regression",
                        "Unsupported media regression type",
                        $"Type '{spec.Type}' is not Visual, Video or Audio.",
                        ".dip/production.json",
                        RuleId: "DIP-MEDIA-TYPE",
                        SourceFormat: "dip-production-profile",
                        SourceFormatVersion: "1.0")],
                    JsonSerializer.Serialize(spec)));
                continue;
            }

            try
            {
                var result = await mediaRegression.AnalyzeAsync(
                    new MediaRegressionCommand(
                        command.ProjectId,
                        command.SubjectKey,
                        command.WorkspaceRelativePath,
                        spec.BaselinePath,
                        spec.CurrentPath,
                        mediaType,
                        spec.WarningThresholdPercent,
                        spec.FailureThresholdPercent,
                        spec.VideoSampleFps),
                    ct);
                var available = !result.Status.Equals(
                    "InfrastructureFailure",
                    StringComparison.OrdinalIgnoreCase);
                var mediaFindings = new List<QaFindingCandidate>();

                if (result.Status.Equals("Failed", StringComparison.OrdinalIgnoreCase)
                    || result.Status.Equals("Warning", StringComparison.OrdinalIgnoreCase))
                {
                    var severity = result.Status.Equals(
                        "Failed",
                        StringComparison.OrdinalIgnoreCase)
                            ? Severity.Critical
                            : Severity.Warning;
                    mediaFindings.Add(new QaFindingCandidate(
                        "media-regression",
                        severity,
                        "dip-media-regression",
                        $"{mediaType} regression {result.Status}",
                        $"{result.Summary} Difference {result.DifferencePercent:0.###}% "
                        + $"(confidence {result.ConfidencePercent:0.##}%).",
                        spec.CurrentPath,
                        RuleId: "DIP-MEDIA-REGRESSION",
                        SourceFormat: "dip-media-regression",
                        SourceFormatVersion: "1.0"));
                }

                if (!available)
                {
                    failures.Add(new
                    {
                        language = "cross-cutting",
                        adapter = "media-regression",
                        error = result.Summary,
                        type = "InfrastructureFailure"
                    });
                }

                crossResults.Add(new QaCrossCuttingResult(
                    $"media-regression-{mediaType.ToString().ToLowerInvariant()}",
                    [new QaToolExecution(
                        "dip-media-regression",
                        available,
                        available,
                        available ? 0 : null,
                        0,
                        "",
                        "",
                        result.Summary)],
                    mediaFindings,
                    result.DetailJson));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new
                {
                    language = "cross-cutting",
                    adapter = "media-regression",
                    error = ex.Message,
                    type = ex.GetType().FullName
                });
                crossResults.Add(new QaCrossCuttingResult(
                    "media-regression",
                    [new QaToolExecution(
                        "dip-media-regression",
                        false,
                        false,
                        null,
                        0,
                        "",
                        "",
                        ex.Message)],
                    [],
                    JsonSerializer.Serialize(new { error = ex.Message })));
            }
        }

        return new(outputs, crossResults, failures);
    }
}
