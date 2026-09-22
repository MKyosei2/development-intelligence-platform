// Purpose: Owns management decisions and resulting action-item mutations.
using System.Globalization;
using System.IO;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.Milestones;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

internal sealed class ManagementDecisionService
{
    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly ISystemClock _clock;

    public ManagementDecisionService(DevelopmentGraphService graph, IDevelopmentGraphRepository repo, ISystemClock clock)
    {
        _graph = graph;
        _repo = repo;
        _clock = clock;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertManagementDecisionAsync(
            UpsertManagementDecisionCommand c,
            CancellationToken ct)
        {
            ValidateKey(c.Key);
    
            var related = (c.RelatedItems ?? [])
                .Where(x => x is not null && NotBlank(x.Type) && NotBlank(x.Key))
                .DistinctBy(
                    x => $"{x.Type}:{x.Key}",
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
    
            var meta = JsonSerializer.Serialize(new
            {
                c.Key,
                c.Decision,
                c.Rationale,
                DecisionOwnerRole = Trim(c.DecisionOwnerRole, 200),
                c.DecisionDueUtc,
                c.DecidedAtUtc,
                RelatedItems = related
            });
    
            var decision = await _graph.UpsertNodeAsync(
                new(
                    c.ProjectId,
                    DevelopmentGraphNodeType.ManagementDecision,
                    $"decision:{c.Key}",
                    c.Title,
                    c.Status,
                    "DIP",
                    c.ExternalReference,
                    meta),
                ct);
    
            foreach (var item in related)
            {
                var (type, key) = ResolveTypeAndKey(item.Type, item.Key);
                var target = await Need(c.ProjectId, type, key, item.Type, ct);
    
                await _graph.LinkAsync(
                    new(
                        c.ProjectId,
                        decision.Id,
                        target.Id,
                        DevelopmentGraphEdgeType.RelatesTo,
                        75m,
                        false,
                        JsonSerializer.Serialize(new { managementDecision = true })),
                    ct);
            }
    
            return decision;
        }
    
    public async Task<DevelopmentGraphNodeDto> UpsertActionItemAsync(
            UpsertActionItemCommand c,
            CancellationToken ct)
        {
            ValidateKey(c.Key);
    
            var priority = NormalizePriority(c.Priority);
            var completion = Math.Clamp(c.CompletionPercent, 0m, 100m);
            var meta = JsonSerializer.Serialize(new
            {
                c.Key,
                Priority = priority,
                OwnerRole = Trim(c.OwnerRole, 200),
                c.DueAtUtc,
                CompletionPercent = completion,
                DecisionKey = Trim(c.DecisionKey, 240),
                Target = c.Target
            });
    
            var action = await _graph.UpsertNodeAsync(
                new(
                    c.ProjectId,
                    DevelopmentGraphNodeType.ActionItem,
                    $"action:{c.Key}",
                    c.Title,
                    c.Status,
                    "DIP",
                    c.ExternalReference,
                    meta),
                ct);
    
            if (NotBlank(c.DecisionKey))
            {
                var decision = await Need(
                    c.ProjectId,
                    DevelopmentGraphNodeType.ManagementDecision,
                    $"decision:{c.DecisionKey}",
                    "decision",
                    ct);
    
                await _graph.LinkAsync(
                    new(
                        c.ProjectId,
                        decision.Id,
                        action.Id,
                        DevelopmentGraphEdgeType.ResultsIn,
                        90m,
                        false,
                        "{\"managementAction\":true}"),
                    ct);
            }
    
            if (c.Target is not null
                && NotBlank(c.Target.Type)
                && NotBlank(c.Target.Key))
            {
                var (type, key) = ResolveTypeAndKey(c.Target.Type, c.Target.Key);
                var target = await Need(c.ProjectId, type, key, c.Target.Type, ct);
    
                var edgeType = type == DevelopmentGraphNodeType.Risk
                    ? DevelopmentGraphEdgeType.Mitigates
                    : DevelopmentGraphEdgeType.RelatesTo;
    
                await _graph.LinkAsync(
                    new(
                        c.ProjectId,
                        action.Id,
                        target.Id,
                        edgeType,
                        85m,
                        type == DevelopmentGraphNodeType.Risk,
                        "{}"),
                    ct);
            }
    
            return action;
        }

    private async Task<DevelopmentGraphNode> Need(
            Guid projectId,
            DevelopmentGraphNodeType type,
            string key,
            string label,
            CancellationToken ct) =>
            await _repo.FindNodeAsync(projectId, type, key, ct)
            ?? throw new KeyNotFoundException($"{label} '{Strip(key)}' was not found.");
    
    private async Task LinkBlocker(
            Guid projectId,
            Guid blockerId,
            Guid targetId,
            CancellationToken ct)
        {
            await _graph.LinkAsync(
                new(
                    projectId,
                    blockerId,
                    targetId,
                    DevelopmentGraphEdgeType.Blocks,
                    100m,
                    true,
                    "{\"managementBlocker\":true}"),
                ct);
        }
    
    private static (DevelopmentGraphNodeType Type, string Key) ResolveTypeAndKey(
            string type,
            string key)
        {
            ValidateKey(key);
    
            return type.Trim().ToLowerInvariant() switch
            {
                "task" => (DevelopmentGraphNodeType.Task, $"task:{key}"),
                "workstream" => (DevelopmentGraphNodeType.Workstream, $"workstream:{key}"),
                "team" => (DevelopmentGraphNodeType.Team, $"team:{key}"),
                "milestone" => (DevelopmentGraphNodeType.Milestone, $"milestone:{key}"),
                "requirement" => (DevelopmentGraphNodeType.Requirement, $"requirement:{key}"),
                "feature" => (DevelopmentGraphNodeType.Feature, $"feature:{key}"),
                "blocker" => (DevelopmentGraphNodeType.Blocker, $"blocker:{key}"),
                "risk" => (DevelopmentGraphNodeType.Risk, $"risk:{key}"),
                "decision" => (DevelopmentGraphNodeType.ManagementDecision, $"decision:{key}"),
                "action" => (DevelopmentGraphNodeType.ActionItem, $"action:{key}"),
                "change" => (DevelopmentGraphNodeType.ChangeRequest, $"change:{key}"),
                _ => throw new ArgumentException(
                    $"Unsupported management node type '{type}'.",
                    nameof(type))
            };
        }
    
    private static void ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > 240)
            {
                throw new ArgumentException(
                    "Key is required and must be at most 240 characters.",
                    nameof(key));
            }
        }
    
    private static bool NotBlank(string? value) =>
            !string.IsNullOrWhiteSpace(value);
    
    private static string NormalizePriority(string? priority) =>
            string.IsNullOrWhiteSpace(priority)
                ? "Medium"
                : priority.Trim() switch
                {
                    "1" => "Critical",
                    "2" => "High",
                    "3" => "Medium",
                    "4" => "Low",
                    var value => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                        value.ToLowerInvariant())
                };
    
    private static string NormalizeSeverity(string? value) =>
            NormalizePriority(value);
    
    private static string NormalizeScope(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? "Included"
                : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                    value.Trim().ToLowerInvariant());
    
    private static string? Trim(string? value, int max) =>
            string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim()[..Math.Min(value.Trim().Length, max)];
    
    internal static bool IsInCurrentScope(DevelopmentGraphNode node)
        {
            if (node.Type != DevelopmentGraphNodeType.Requirement)
                return false;
    
            var scope = MetaString(node, "ScopeState", "Included");
    
            return !scope.Equals("Removed", StringComparison.OrdinalIgnoreCase)
                   && !scope.Equals("Deferred", StringComparison.OrdinalIgnoreCase)
                   && !node.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
        }
    
    internal static string MetaString(
            DevelopmentGraphNode node,
            string name,
            string fallback = "")
        {
            using var document = ParseMetadata(node);
            return document.RootElement.TryGetProperty(name, out var value)
                   && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? fallback
                : fallback;
        }
    
    internal static decimal MetaDecimal(
            DevelopmentGraphNode node,
            string name,
            decimal fallback = 0m)
        {
            using var document = ParseMetadata(node);
            return document.RootElement.TryGetProperty(name, out var value)
                   && value.TryGetDecimal(out var parsed)
                ? parsed
                : fallback;
        }
    
    internal static IReadOnlyList<string> MetaStringArray(
            DevelopmentGraphNode node,
            string name)
        {
            using var document = ParseMetadata(node);
    
            if (!document.RootElement.TryGetProperty(name, out var value)
                || value.ValueKind != JsonValueKind.Array)
            {
                return [];
            }
    
            return value
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => x.GetString() ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
        }
    
    internal static DateTimeOffset MetaDate(
            DevelopmentGraphNode node,
            string name,
            DateTimeOffset fallback)
        {
            using var document = ParseMetadata(node);
            return document.RootElement.TryGetProperty(name, out var value)
                   && value.TryGetDateTimeOffset(out var parsed)
                ? parsed
                : fallback;
        }
    
    internal static string Strip(string key) =>
            key.Contains(':') ? key[(key.IndexOf(':') + 1)..] : key;
    
    internal static bool IsDone(string status) =>
            status.Equals("Done", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Closed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Resolved", StringComparison.OrdinalIgnoreCase);
    
    internal static bool IsBlocked(string status) =>
            status.Equals("Blocked", StringComparison.OrdinalIgnoreCase)
            || status.Equals("AtRisk", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Critical", StringComparison.OrdinalIgnoreCase);
    
    private static JsonDocument ParseMetadata(DevelopmentGraphNode node)
        {
            try
            {
                var document = JsonDocument.Parse(node.MetadataJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    document.Dispose();
                    throw new InvalidDataException(
                        $"Development Graph node '{node.Key}' metadata must be a JSON object.");
                }
    
                return document;
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException(
                    $"Development Graph node '{node.Key}' contains invalid metadata JSON.",
                    ex);
            }
        }
}
