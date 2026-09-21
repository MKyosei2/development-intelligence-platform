// Purpose: Evaluates feature readiness from acceptance criteria and evidence while batching graph mutations into one unit-of-work save.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.FeatureControl;

public sealed record UpsertFeatureCommand(
    Guid ProjectId,
    string Key,
    string Name,
    string? MilestoneKey = null,
    decimal BaseRisk = 20m,
    string Status = "Active",
    string MetadataJson = "{}");

public sealed record UpsertAcceptanceCriterionCommand(
    Guid ProjectId,
    string FeatureKey,
    string CriterionKey,
    string Title,
    bool Required = true,
    decimal Weight = 1m,
    IReadOnlyList<Guid>? EvidenceIds = null,
    string? Notes = null);

public sealed record AttachAcceptanceEvidenceCommand(
    Guid ProjectId,
    string FeatureKey,
    string CriterionKey,
    IReadOnlyList<Guid> EvidenceIds);

public sealed record CriterionReadiness(
    string Key,
    string Title,
    bool Required,
    decimal Weight,
    string State,
    IReadOnlyList<Guid> EvidenceIds,
    IReadOnlyList<string> EvidenceSummary);

public sealed record FeatureReadinessResult(
    string FeatureKey,
    string Name,
    string State,
    decimal ReadinessPercent,
    int PassedCriteria,
    int TotalCriteria,
    int RequiredFailures,
    IReadOnlyList<CriterionReadiness> Criteria,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Actions,
    string Summary);

public sealed class FeatureReadinessService
{
    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly IEvidenceRepository _evidence;
    private readonly IP2IntelligenceRepository _records;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public FeatureReadinessService(
        DevelopmentGraphService graph,
        IDevelopmentGraphRepository repo,
        IEvidenceRepository evidence,
        IP2IntelligenceRepository records,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _graph = graph;
        _repo = repo;
        _evidence = evidence;
        _records = records;
        _uow = uow;
        _clock = clock;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertFeatureAsync(
        UpsertFeatureCommand command,
        CancellationToken ct)
    {
        var metadata = Merge(
            command.MetadataJson,
            new Dictionary<string, object?>
            {
                ["FeatureKey"] = command.Key,
                ["BaseRisk"] = Math.Clamp(command.BaseRisk, 0m, 100m)
            });

        var milestone = string.IsNullOrWhiteSpace(command.MilestoneKey)
            ? null
            : await _repo.FindNodeAsync(
                command.ProjectId,
                DevelopmentGraphNodeType.Milestone,
                $"milestone:{command.MilestoneKey}",
                ct);

        return await _graph.MutateAsync(
            async (graph, token) =>
            {
                var feature = await graph.UpsertNodeAsync(
                    new(
                        command.ProjectId,
                        DevelopmentGraphNodeType.Feature,
                        $"feature:{command.Key}",
                        command.Name,
                        command.Status,
                        "DIP",
                        null,
                        metadata),
                    token);

                if (milestone is not null)
                {
                    await graph.LinkAsync(
                        new(
                            command.ProjectId,
                            milestone.Id,
                            feature.Id,
                            DevelopmentGraphEdgeType.Contains,
                            80m,
                            false,
                            "{}"),
                        token);
                }

                return feature;
            },
            ct);
    }

    public async Task<DevelopmentGraphNodeDto> UpsertCriterionAsync(
        UpsertAcceptanceCriterionCommand command,
        CancellationToken ct)
    {
        var feature = await RequireFeature(command.ProjectId, command.FeatureKey, ct);
        var evidenceIds = command.EvidenceIds ?? Array.Empty<Guid>();

        return await _graph.MutateAsync(
            async (graph, token) =>
            {
                var criterion = await graph.UpsertNodeAsync(
                    new(
                        command.ProjectId,
                        DevelopmentGraphNodeType.AcceptanceCriterion,
                        $"criterion:{command.FeatureKey}:{command.CriterionKey}",
                        command.Title,
                        evidenceIds.Count > 0 ? "EvidenceAttached" : "Unverified",
                        "DIP",
                        null,
                        JsonSerializer.Serialize(new
                        {
                            command.FeatureKey,
                            command.CriterionKey,
                            command.Required,
                            Weight = Math.Clamp(command.Weight, 0.1m, 100m),
                            EvidenceIds = evidenceIds,
                            command.Notes
                        })),
                    token);

                await graph.LinkAsync(
                    new(
                        command.ProjectId,
                        feature.Id,
                        criterion.Id,
                        DevelopmentGraphEdgeType.Contains,
                        command.Required ? 100m : 60m,
                        command.Required,
                        "{}"),
                    token);

                return criterion;
            },
            ct);
    }

    public async Task<DevelopmentGraphNodeDto> AttachEvidenceAsync(
        AttachAcceptanceEvidenceCommand command,
        CancellationToken ct)
    {
        var criterion = await _repo.FindNodeAsync(
            command.ProjectId,
            DevelopmentGraphNodeType.AcceptanceCriterion,
            $"criterion:{command.FeatureKey}:{command.CriterionKey}",
            ct)
            ?? throw new KeyNotFoundException(
                $"Acceptance criterion '{command.CriterionKey}' was not found for feature '{command.FeatureKey}'.");

        using var document = JsonDocument.Parse(criterion.MetadataJson);
        var root = document.RootElement;
        var required = root.TryGetProperty("Required", out var requiredValue)
            && requiredValue.GetBoolean();
        var weight = root.TryGetProperty("Weight", out var weightValue)
            && weightValue.TryGetDecimal(out var parsedWeight)
                ? parsedWeight
                : 1m;
        var notes = root.TryGetProperty("Notes", out var notesValue)
            && notesValue.ValueKind == JsonValueKind.String
                ? notesValue.GetString()
                : null;
        var evidence = await _evidence.ListForProjectAsync(command.ProjectId, null, ct);
        var selected = evidence.Where(item => command.EvidenceIds.Contains(item.Id)).ToArray();

        return await _graph.MutateAsync(
            async (graph, token) =>
            {
                var updated = await graph.UpsertNodeAsync(
                    new(
                        command.ProjectId,
                        DevelopmentGraphNodeType.AcceptanceCriterion,
                        criterion.Key,
                        criterion.Name,
                        command.EvidenceIds.Count > 0 ? "EvidenceAttached" : "Unverified",
                        "DIP",
                        criterion.ExternalReference,
                        JsonSerializer.Serialize(new
                        {
                            FeatureKey = command.FeatureKey,
                            CriterionKey = command.CriterionKey,
                            Required = required,
                            Weight = weight,
                            EvidenceIds = command.EvidenceIds,
                            Notes = notes
                        })),
                    token);

                foreach (var item in selected)
                {
                    var evidenceNode = await graph.UpsertNodeAsync(
                        EvidenceCommand(command.ProjectId, item),
                        token);
                    await graph.LinkAsync(
                        EvidenceLink(command.ProjectId, updated.Id, evidenceNode.Id, item, required),
                        token);
                }

                return updated;
            },
            ct);
    }

    public async Task<FeatureReadinessResult> EvaluateAsync(
        Guid projectId,
        string featureKey,
        int evidenceFreshnessDays = 30,
        CancellationToken ct = default)
    {
        var feature = await RequireFeature(projectId, featureKey, ct);
        var edges = await _repo.ListEdgesAsync(projectId, ct);
        var criterionIds = edges
            .Where(edge => edge.SourceNodeId == feature.Id && edge.Type == DevelopmentGraphEdgeType.Contains)
            .Select(edge => edge.TargetNodeId)
            .ToHashSet();
        var nodes = await _repo.ListNodesAsync(projectId, DevelopmentGraphNodeType.AcceptanceCriterion, ct);
        var criteria = nodes.Where(node => criterionIds.Contains(node.Id)).ToArray();
        var evidence = await _evidence.ListForProjectAsync(
            projectId,
            _clock.UtcNow.AddDays(-Math.Clamp(evidenceFreshnessDays, 1, 365)),
            ct);
        var evidenceById = evidence.ToDictionary(item => item.Id);
        var resultCriteria = new List<CriterionReadiness>();
        var blockers = new List<string>();
        var actions = new List<string>();
        decimal totalWeight = 0m;
        decimal passedWeight = 0m;
        var requiredFailures = 0;

        await _graph.StageMutationAsync(
            async (graph, token) =>
            {
                foreach (var criterion in criteria)
                {
                    using var document = JsonDocument.Parse(criterion.MetadataJson);
                    var root = document.RootElement;
                    var key = root.TryGetProperty("CriterionKey", out var keyValue)
                        ? keyValue.GetString() ?? criterion.Key
                        : criterion.Key;
                    var required = root.TryGetProperty("Required", out var requiredValue)
                        && requiredValue.GetBoolean();
                    var weight = root.TryGetProperty("Weight", out var weightValue)
                        && weightValue.TryGetDecimal(out var parsedWeight)
                            ? Math.Max(0.1m, parsedWeight)
                            : 1m;
                    var ids = root.TryGetProperty("EvidenceIds", out var evidenceIds)
                        && evidenceIds.ValueKind == JsonValueKind.Array
                            ? evidenceIds.EnumerateArray()
                                .Where(value => value.TryGetGuid(out _))
                                .Select(value => value.GetGuid())
                                .Distinct()
                                .ToArray()
                            : [];
                    var linked = ids
                        .Where(evidenceById.ContainsKey)
                        .Select(id => evidenceById[id])
                        .ToArray();
                    var failed = linked.Any(item => item.Outcome == EvidenceOutcome.Failed);
                    var success = linked.Any(item => item.Outcome == EvidenceOutcome.Succeeded) && !failed;
                    var warning = !failed && !success && linked.Any(item => item.Outcome == EvidenceOutcome.Warning);
                    var state = failed ? "Failed" : success ? "Passed" : warning ? "Warning" : "Unverified";

                    await graph.UpsertNodeAsync(
                        new(
                            projectId,
                            DevelopmentGraphNodeType.AcceptanceCriterion,
                            criterion.Key,
                            criterion.Name,
                            state,
                            criterion.Provider,
                            criterion.ExternalReference,
                            criterion.MetadataJson),
                        token);

                    foreach (var item in linked)
                    {
                        var evidenceNode = await graph.UpsertNodeAsync(EvidenceCommand(projectId, item), token);
                        await graph.LinkAsync(
                            EvidenceLink(projectId, criterion.Id, evidenceNode.Id, item, required),
                            token);
                    }

                    totalWeight += weight;
                    if (success)
                        passedWeight += weight;
                    if (required && !success)
                    {
                        requiredFailures++;
                        blockers.Add($"Required criterion '{criterion.Name}' is {state.ToLowerInvariant()}.");
                        actions.Add($"Provide fresh passing evidence for '{criterion.Name}'.");
                    }

                    resultCriteria.Add(new(
                        key,
                        criterion.Name,
                        required,
                        weight,
                        state,
                        ids,
                        linked.Select(item => $"{item.Outcome}: {item.Title}").Take(6).ToArray()));
                }

                return true;
            },
            ct);

        var percent = criteria.Length == 0
            ? 0m
            : decimal.Round(totalWeight == 0m ? 0m : passedWeight / totalWeight * 100m, 2);
        var readiness = criteria.Length == 0
            ? ReadinessState.Unknown
            : requiredFailures > 0
                ? ReadinessState.NotReady
                : percent >= 95m
                    ? ReadinessState.Ready
                    : ReadinessState.NeedsAttention;
        var passedCriteria = resultCriteria.Count(item => item.State == "Passed");
        var summary =
            $"Feature '{feature.Name}' readiness is {percent:0.##}% with {passedCriteria}/{criteria.Length} "
            + $"acceptance criteria passing and {requiredFailures} required gap(s).";
        var result = new FeatureReadinessResult(
            featureKey,
            feature.Name,
            readiness.ToString(),
            percent,
            passedCriteria,
            criteria.Length,
            requiredFailures,
            resultCriteria,
            blockers,
            actions.Distinct().ToArray(),
            summary);

        await _graph.StageMutationAsync(
            async (graph, token) =>
            {
                await graph.UpsertNodeAsync(
                    new(
                        projectId,
                        DevelopmentGraphNodeType.Feature,
                        feature.Key,
                        feature.Name,
                        readiness.ToString(),
                        feature.Provider,
                        feature.ExternalReference,
                        Merge(
                            feature.MetadataJson,
                            new Dictionary<string, object?>
                            {
                                ["ReadinessPercent"] = percent,
                                ["RequiredFailures"] = requiredFailures,
                                ["EvaluatedAtUtc"] = _clock.UtcNow
                            })),
                    token);
                return true;
            },
            ct);

        await _records.AddAsync(
            new P2IntelligenceRecord(
                Guid.NewGuid(),
                projectId,
                $"feature:{featureKey}",
                P2IntelligenceKind.FeatureReadiness,
                requiredFailures > 0 ? P2IntelligenceStatus.Warning : P2IntelligenceStatus.Informational,
                100m - percent,
                EvidenceConfidence.High,
                summary,
                JsonSerializer.Serialize(result),
                feature.ExternalReference,
                _clock.UtcNow),
            ct);
        await _uow.SaveChangesAsync(ct);
        return result;
    }

    private async Task<DevelopmentGraphNode> RequireFeature(
        Guid projectId,
        string key,
        CancellationToken ct) =>
        await _repo.FindNodeAsync(
            projectId,
            DevelopmentGraphNodeType.Feature,
            $"feature:{key}",
            ct)
        ?? throw new KeyNotFoundException($"Feature '{key}' was not found.");

    private static UpsertDevelopmentGraphNodeCommand EvidenceCommand(Guid projectId, EvidenceRecord evidence) =>
        new(
            projectId,
            DevelopmentGraphNodeType.Evidence,
            $"evidence:{evidence.Id}",
            evidence.Title,
            evidence.Outcome.ToString(),
            evidence.SourceType.ToString(),
            evidence.SourceReference,
            JsonSerializer.Serialize(new
            {
                evidence.EvidenceKind,
                evidence.Outcome,
                evidence.Summary,
                evidence.ObservedAtUtc
            }));

    private static LinkDevelopmentGraphNodesCommand EvidenceLink(
        Guid projectId,
        Guid criterionId,
        Guid evidenceNodeId,
        EvidenceRecord evidence,
        bool required) =>
        new(
            projectId,
            criterionId,
            evidenceNodeId,
            DevelopmentGraphEdgeType.VerifiedBy,
            evidence.Outcome == EvidenceOutcome.Succeeded ? 100m : 70m,
            required,
            JsonSerializer.Serialize(new { evidenceId = evidence.Id }));

    private static string Merge(string json, IReadOnlyDictionary<string, object?> additions)
    {
        Dictionary<string, object?> map;
        try
        {
            map = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                string.IsNullOrWhiteSpace(json) ? "{}" : json) ?? new();
        }
        catch (JsonException)
        {
            map = new();
        }

        foreach (var pair in additions)
            map[pair.Key] = pair.Value;
        return JsonSerializer.Serialize(map);
    }
}
