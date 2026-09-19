// Purpose: Simulates deterministic scope, schedule, QA-capacity, and release-risk tradeoffs.
using System.IO;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Scenarios;

public sealed class WhatIfSimulationService
{
    private readonly IDevelopmentGraphRepository _graph;
    private readonly IChangeRiskRepository _risk;
    private readonly IReleaseReadinessRepository _release;
    private readonly IP2IntelligenceRepository _records;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public WhatIfSimulationService(
        IDevelopmentGraphRepository graph,
        IChangeRiskRepository risk,
        IReleaseReadinessRepository release,
        IP2IntelligenceRepository records,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _graph = graph;
        _risk = risk;
        _release = release;
        _records = records;
        _uow = uow;
        _clock = clock;
    }

    public async Task<WhatIfSimulationResult> SimulateAsync(
        WhatIfSimulationCommand command,
        CancellationToken ct)
    {
        var features = await _graph.ListNodesAsync(
            command.ProjectId,
            DevelopmentGraphNodeType.Feature,
            ct);

        var latestRisk = await _risk.GetLatestAsync(command.ProjectId, null, ct);
        var release = await _release.GetLatestAsync(command.ProjectId, ct);

        var baseline = Math.Clamp(
            (latestRisk?.RiskScore ?? 25m)
            + (release?.Decision == ReleaseDecision.NoGo
                ? 25m
                : release?.Decision == ReleaseDecision.ConditionalGo
                    ? 10m
                    : 0m)
            + features.Count(x =>
                string.Equals(x.Status, "NotReady", StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Status, "Blocked", StringComparison.OrdinalIgnoreCase)) * 5m,
            0m,
            100m);

        var excluded = features
            .Where(feature => command.ExcludedFeatureKeys.Any(key =>
                feature.Key.Equals($"feature:{key}", StringComparison.OrdinalIgnoreCase)
                || feature.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        decimal removedRisk = 0m;
        foreach (var feature in excluded)
        {
            removedRisk += ReadDecimal(feature.MetadataJson, "BaseRisk", 20m) * .45m;
            if (string.Equals(feature.Status, "NotReady", StringComparison.OrdinalIgnoreCase)
                || string.Equals(feature.Status, "Blocked", StringComparison.OrdinalIgnoreCase))
            {
                removedRisk += 8m;
            }
        }

        var capacity = Math.Clamp(command.QaCapacityMultiplier, .25m, 4m);
        var qaDelta = decimal.Round((capacity - 1m) * 100m, 2);

        var scheduleDelta =
            -excluded.Length * 2
            + command.ScheduleBufferDays
            - (int)Math.Round(
                (double)((capacity - 1m) * Math.Max(1, features.Count) / 3m),
                MidpointRounding.AwayFromZero);

        var simulated = Math.Clamp(
            decimal.Round(
                baseline
                - removedRisk
                - (capacity - 1m) * 8m
                + Math.Max(0, -command.ScheduleBufferDays) * 2m,
                2),
            0m,
            100m);

        var assumptions = new List<string>
        {
            $"Excluded feature(s): {excluded.Length}.",
            $"Schedule buffer adjustment: {command.ScheduleBufferDays:+#;-#;0} day(s).",
            $"QA capacity multiplier: {capacity:0.##}x."
        };

        var tradeoffs = new List<string>();

        if (excluded.Length > 0)
        {
            tradeoffs.Add(
                "Reduced release scope lowers integration and QA exposure but removes planned product value.");
        }

        if (capacity > 1m)
        {
            tradeoffs.Add(
                "Higher QA capacity can reduce validation latency but may require staffing or automation cost.");
        }

        if (command.ScheduleBufferDays > 0)
        {
            tradeoffs.Add(
                "Additional schedule buffer reduces timing pressure but delays delivery.");
        }

        if (tradeoffs.Count == 0)
            tradeoffs.Add("Scenario changes are small; baseline risk remains the dominant signal.");

        var summary =
            $"Scenario '{command.ScenarioName}' changes modeled release risk from "
            + $"{baseline:0.##} to {simulated:0.##} and schedule by "
            + $"{scheduleDelta:+#;-#;0} day(s). "
            + "This is deterministic decision support, not a delivery guarantee.";

        var result = new WhatIfSimulationResult(
            command.ScenarioName,
            baseline,
            simulated,
            features.Count,
            Math.Max(0, features.Count - excluded.Length),
            scheduleDelta,
            qaDelta,
            assumptions,
            tradeoffs,
            summary);

        await _records.AddAsync(
            new P2IntelligenceRecord(
                Guid.NewGuid(),
                command.ProjectId,
                $"scenario:{command.ScenarioName}",
                P2IntelligenceKind.WhatIfSimulation,
                P2IntelligenceStatus.Informational,
                simulated,
                EvidenceConfidence.Medium,
                summary,
                JsonSerializer.Serialize(result),
                null,
                _clock.UtcNow),
            ct);

        await _uow.SaveChangesAsync(ct);
        return result;
    }

    private static decimal ReadDecimal(
        string json,
        string name,
        decimal fallback)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty(name, out var value)
                   && value.TryGetDecimal(out var parsed)
                ? parsed
                : fallback;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"What-if source metadata is invalid JSON; '{name}' cannot be evaluated safely.",
                ex);
        }
    }
}
