// Purpose: Resolves project entities across source systems while treating malformed metadata as unknown rather than silently accepting it.
using System.Text.Json;
using System.Text.RegularExpressions;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ProjectIntelligence;

public sealed class ProjectEntityResolutionService
{
    private static readonly DevelopmentGraphNodeType[] CanonicalTypes =
    [
        DevelopmentGraphNodeType.Task,
        DevelopmentGraphNodeType.ActionItem,
        DevelopmentGraphNodeType.Requirement,
        DevelopmentGraphNodeType.Milestone,
        DevelopmentGraphNodeType.Feature,
        DevelopmentGraphNodeType.WorkItem
    ];

    private static readonly DevelopmentGraphNodeType[] SourceTypes =
    [
        DevelopmentGraphNodeType.PlanItem,
        DevelopmentGraphNodeType.ExternalObservation,
        DevelopmentGraphNodeType.PullRequest,
        DevelopmentGraphNodeType.Commit
    ];

    private static readonly HashSet<string> Stop = new(
        ["the", "and", "for", "task", "issue", "work", "fix", "対応", "作業", "タスク", "実装"],
        StringComparer.OrdinalIgnoreCase);

    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly ISystemClock _clock;

    public ProjectEntityResolutionService(
        DevelopmentGraphService graph,
        IDevelopmentGraphRepository repo,
        ISystemClock clock)
    {
        _graph = graph;
        _repo = repo;
        _clock = clock;
    }

    public async Task<EntityResolutionOverviewDto> ResolveProjectAsync(
        Guid projectId,
        bool persistHighConfidence,
        CancellationToken ct)
    {
        var nodes = await _repo.ListNodesAsync(projectId, null, ct);
        var edges = await _repo.ListEdgesAsync(projectId, ct);
        var canonical = nodes.Where(IsCanonical).ToArray();
        var sources = CurrentSources(nodes).ToArray();
        var items = new List<EntityMatchDto>();
        var resolved = 0;
        var suggested = 0;
        var unresolved = 0;

        foreach (var source in sources)
        {
            ct.ThrowIfCancellationRequested();
            var existing = FindEquivalent(edges, source.Id, nodes);
            if (existing is not null)
            {
                resolved++;
                items.Add(ToMatch(source, existing, 100m, "Resolved", "Existing explicit/automatic entity binding."));
                continue;
            }

            var ranked = canonical
                .Where(candidate => candidate.Id != source.Id)
                .Select(candidate => (Node: candidate, Score: Score(source, candidate), Reason: Reason(source, candidate)))
                .Where(candidate => candidate.Score >= 55m)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.Node.Name, StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToArray();

            if (ranked.Length == 0)
            {
                unresolved++;
                items.Add(ToMatch(source, null, 0m, "Unresolved", "No safe cross-source candidate was found."));
                continue;
            }

            var best = ranked[0];
            var margin = best.Score - (ranked.Length > 1 ? ranked[1].Score : 0m);
            var safe = best.Score >= 90m || (best.Score >= 82m && margin >= 15m);
            if (safe && persistHighConfidence)
            {
                await _graph.LinkAsync(
                    new(
                        projectId,
                        source.Id,
                        best.Node.Id,
                        DevelopmentGraphEdgeType.EquivalentTo,
                        best.Score,
                        false,
                        JsonSerializer.Serialize(new
                        {
                            automatic = true,
                            best.Reason,
                            resolvedAtUtc = _clock.UtcNow,
                            domainAware = true
                        })),
                    ct);
                resolved++;
                items.Add(ToMatch(source, best.Node, best.Score, "Resolved", best.Reason));
            }
            else
            {
                suggested++;
                items.Add(ToMatch(source, best.Node, best.Score, "Suggested", best.Reason));
            }
        }

        return new(
            projectId,
            resolved,
            suggested,
            unresolved,
            items.OrderBy(x => x.Resolution).ThenByDescending(x => x.Confidence).Take(2000).ToArray(),
            _clock.UtcNow);
    }

    public async Task BindAsync(BindProjectEntityCommand command, CancellationToken ct)
    {
        if (command.ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(command));

        var source = await Need(command.ProjectId, command.SourceType, command.SourceKey, true, ct);
        var target = await Need(command.ProjectId, command.TargetType, command.TargetKey, false, ct);
        await _graph.LinkAsync(
            new(
                command.ProjectId,
                source.Id,
                target.Id,
                DevelopmentGraphEdgeType.EquivalentTo,
                100m,
                false,
                JsonSerializer.Serialize(new
                {
                    automatic = false,
                    actor = command.Actor,
                    reason = command.Reason,
                    boundAtUtc = _clock.UtcNow
                })),
            ct);

        var key = $"binding:{source.Type}:{Strip(source.Key)}:{target.Type}:{Strip(target.Key)}";
        await _graph.UpsertNodeAsync(
            new(
                command.ProjectId,
                DevelopmentGraphNodeType.EntityBinding,
                key,
                $"{source.Name} = {target.Name}",
                "Active",
                "DIP",
                null,
                JsonSerializer.Serialize(new
                {
                    SourceId = source.Id,
                    TargetId = target.Id,
                    command.Actor,
                    command.Reason
                })),
            ct);
    }

    private async Task<DevelopmentGraphNode> Need(
        Guid projectId,
        string type,
        string key,
        bool source,
        CancellationToken ct)
    {
        if (!Enum.TryParse<DevelopmentGraphNodeType>(type, true, out var parsedType))
            throw new ArgumentException($"Unknown node type '{type}'.");
        if (source && !SourceTypes.Contains(parsedType))
            throw new ArgumentException($"'{type}' cannot be used as an entity-resolution source.");

        var candidates = await _repo.ListNodesAsync(projectId, parsedType, ct);
        var found = candidates.FirstOrDefault(x =>
            Strip(x.Key).Equals(key, StringComparison.OrdinalIgnoreCase)
            || x.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"{type}:{key} was not found.");
        if (!source && !IsCanonical(found))
            throw new ArgumentException($"'{type}' cannot be used as a canonical target.");
        return found;
    }

    private static bool IsCanonical(DevelopmentGraphNode node)
    {
        if (CanonicalTypes.Contains(node.Type))
            return true;
        var kind = MetadataString(node, "kind").ToLowerInvariant();
        return kind is "media.shot" or "media.task" or "software.task" or "software.issue"
            or "generic.task";
    }

    private static IEnumerable<DevelopmentGraphNode> CurrentSources(IReadOnlyList<DevelopmentGraphNode> nodes)
    {
        var latestImport = nodes
            .Where(x => x.Type == DevelopmentGraphNodeType.ProjectPlanImport && x.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.LastObservedAtUtc)
            .FirstOrDefault();
        var importKey = latestImport is null ? null : MetadataString(latestImport, "ImportKey");

        foreach (var node in nodes.Where(x => SourceTypes.Contains(x.Type)))
        {
            if (node.Type == DevelopmentGraphNodeType.PlanItem
                && importKey is not null
                && !string.Equals(MetadataString(node, "ImportKey"), importKey, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            yield return node;
        }
    }

    private static DevelopmentGraphNode? FindEquivalent(
        IReadOnlyList<DevelopmentGraphEdge> edges,
        Guid sourceId,
        IReadOnlyList<DevelopmentGraphNode> nodes)
    {
        var edge = edges
            .Where(x => x.SourceNodeId == sourceId && x.Type == DevelopmentGraphEdgeType.EquivalentTo)
            .OrderByDescending(x => x.LastObservedAtUtc)
            .FirstOrDefault();
        return edge is null ? null : nodes.FirstOrDefault(x => x.Id == edge.TargetNodeId);
    }

    private static decimal Score(DevelopmentGraphNode source, DevelopmentGraphNode target)
    {
        var sourceKey = Normalize(ExtractLogicalKey(source));
        var targetKey = Normalize(ExtractLogicalKey(target));
        decimal raw;

        if (sourceKey.Length >= 3 && targetKey.Length >= 3 && sourceKey.Equals(targetKey, StringComparison.OrdinalIgnoreCase))
        {
            raw = 100m;
        }
        else
        {
            var references = References(source);
            if (references.Any(reference =>
                    reference.Contains(ExtractLogicalKey(target), StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(target.ExternalReference)
                        && reference.Equals(target.ExternalReference, StringComparison.OrdinalIgnoreCase))))
            {
                raw = 97m;
            }
            else if (source.Name.Contains(ExtractLogicalKey(target), StringComparison.OrdinalIgnoreCase)
                     || target.Name.Contains(ExtractLogicalKey(source), StringComparison.OrdinalIgnoreCase))
            {
                raw = 94m;
            }
            else
            {
                var a = Tokens(source.Name);
                var b = Tokens(target.Name);
                if (a.Count == 0 || b.Count == 0)
                    return 0m;
                var overlap = a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count();
                var union = a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
                var jaccard = (decimal)overlap / Math.Max(1, union);
                var containment = (decimal)overlap / Math.Max(1, Math.Min(a.Count, b.Count));
                raw = Math.Round(Math.Clamp(jaccard * 70m + containment * 30m, 0m, 92m), 1);
            }
        }

        var sourceDomain = MetadataString(source, "domainId");
        var targetDomain = MetadataString(target, "domainId");
        if (!string.IsNullOrWhiteSpace(sourceDomain) && !string.IsNullOrWhiteSpace(targetDomain))
            raw = sourceDomain.Equals(targetDomain, StringComparison.OrdinalIgnoreCase)
                ? Math.Min(100m, raw + 3m)
                : Math.Max(0m, raw - 15m);

        var sourceKind = KindFamily(MetadataString(source, "kind"));
        var targetKind = KindFamily(MetadataString(target, "kind"));
        if (sourceKind.Length > 0 && sourceKind.Equals(targetKind, StringComparison.OrdinalIgnoreCase))
            raw = Math.Min(100m, raw + 2m);
        return raw;
    }

    private static string Reason(DevelopmentGraphNode source, DevelopmentGraphNode target)
    {
        var sourceKey = ExtractLogicalKey(source);
        var targetKey = ExtractLogicalKey(target);
        var reason = Normalize(sourceKey) == Normalize(targetKey)
            ? $"Exact logical key match '{sourceKey}'."
            : References(source).Any(reference => reference.Contains(targetKey, StringComparison.OrdinalIgnoreCase))
                ? $"Source reference explicitly mentions '{targetKey}'."
                : source.Name.Contains(targetKey, StringComparison.OrdinalIgnoreCase)
                    ? $"Source title explicitly mentions target key '{targetKey}'."
                    : "Normalized title-token similarity.";

        var sourceDomain = MetadataString(source, "domainId");
        var targetDomain = MetadataString(target, "domainId");
        if (!string.IsNullOrWhiteSpace(sourceDomain) && !string.IsNullOrWhiteSpace(targetDomain))
            reason += sourceDomain.Equals(targetDomain, StringComparison.OrdinalIgnoreCase)
                ? $" Same domain '{sourceDomain}'."
                : $" Cross-domain candidate ({sourceDomain} -> {targetDomain}) was penalized.";
        return reason;
    }

    private static EntityMatchDto ToMatch(
        DevelopmentGraphNode source,
        DevelopmentGraphNode? target,
        decimal confidence,
        string resolution,
        string reason) =>
        new(
            source.Type.ToString(), Strip(source.Key), source.Name, source.Provider ?? "Unknown",
            target?.Type.ToString(), target is null ? null : Strip(target.Key), target?.Name,
            confidence, resolution, reason, source.LastObservedAtUtc);

    private static string ExtractLogicalKey(DevelopmentGraphNode node)
    {
        var key = MetadataString(node, "Key");
        if (!string.IsNullOrWhiteSpace(key))
            return key;
        var sourceObjectId = MetadataString(node, "sourceObjectId");
        if (!string.IsNullOrWhiteSpace(sourceObjectId))
            return sourceObjectId;
        var stripped = Strip(node.Key);
        var parts = stripped.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1] : stripped;
    }

    private static IReadOnlyList<string> References(DevelopmentGraphNode node)
    {
        if (!TryMetadataObject(node, out var root))
            return [];
        var property = root.EnumerateObject().FirstOrDefault(x => x.Name.Equals("References", StringComparison.OrdinalIgnoreCase));
        if (property.Value.ValueKind != JsonValueKind.Array)
            return [];
        return property.Value.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString() ?? string.Empty)
            .Where(x => x.Length > 0)
            .ToArray();
    }

    private static HashSet<string> Tokens(string value) =>
        Regex.Matches(value.ToLowerInvariant(), @"[\p{L}\p{N}]+", RegexOptions.CultureInvariant)
            .Select(x => x.Value)
            .Where(x => x.Length > 1 && !Stop.Contains(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string MetadataString(DevelopmentGraphNode node, string name)
    {
        if (!TryMetadataObject(node, out var root))
            return string.Empty;
        foreach (var property in root.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String)
                return property.Value.GetString() ?? string.Empty;
        return string.Empty;
    }

    private static bool TryMetadataObject(DevelopmentGraphNode node, out JsonElement root)
    {
        root = default;
        if (string.IsNullOrWhiteSpace(node.MetadataJson))
            return false;
        try
        {
            using var document = JsonDocument.Parse(node.MetadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            root = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string KindFamily(string kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
            return string.Empty;
        var index = kind.LastIndexOf('.');
        return index >= 0 && index < kind.Length - 1 ? kind[(index + 1)..] : kind;
    }

    private static string Strip(string value)
    {
        var index = value.LastIndexOf(':');
        return index >= 0 ? value[(index + 1)..] : value;
    }
}
