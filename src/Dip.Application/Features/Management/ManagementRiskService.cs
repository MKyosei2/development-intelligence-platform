// Purpose: Owns blocker and risk mutations and their graph relationships.
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

internal sealed class ManagementRiskService
{
    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly ISystemClock _clock;

    public ManagementRiskService(DevelopmentGraphService graph, IDevelopmentGraphRepository repo, ISystemClock clock)
    {
        _graph = graph;
        _repo = repo;
        _clock = clock;
    }

    public async Task<DevelopmentGraphNodeDto> UpsertBlockerAsync(
            UpsertBlockerCommand c,
            CancellationToken ct)
        {
            ValidateKey(c.Key);
    
            var opened = c.OpenedAtUtc == default ? _clock.UtcNow : c.OpenedAtUtc;
            var blockedTasks = (c.BlockedTaskKeys ?? [])
                .Where(NotBlank)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var blockedWorkstreams = (c.BlockedWorkstreamKeys ?? [])
                .Where(NotBlank)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var blockedMilestones = (c.BlockedMilestoneKeys ?? [])
                .Where(NotBlank)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
    
            var meta = JsonSerializer.Serialize(new
            {
                c.Key,
                c.Reason,
                Severity = NormalizeSeverity(c.Severity),
                OpenedAtUtc = opened,
                BlockedTaskKeys = blockedTasks,
                BlockedWorkstreamKeys = blockedWorkstreams,
                BlockedMilestoneKeys = blockedMilestones
            });
    
            var blocker = await _graph.UpsertNodeAsync(
                new(
                    c.ProjectId,
                    DevelopmentGraphNodeType.Blocker,
                    $"blocker:{c.Key}",
                    c.Title,
                    c.Status,
                    "DIP",
                    c.ExternalReference,
                    meta),
                ct);
    
            foreach (var key in blockedTasks)
            {
                var node = await Need(
                    c.ProjectId,
                    DevelopmentGraphNodeType.Task,
                    $"task:{key}",
                    "task",
                    ct);
                await LinkBlocker(c.ProjectId, blocker.Id, node.Id, ct);
            }
    
            foreach (var key in blockedWorkstreams)
            {
                var node = await Need(
                    c.ProjectId,
                    DevelopmentGraphNodeType.Workstream,
                    $"workstream:{key}",
                    "workstream",
                    ct);
                await LinkBlocker(c.ProjectId, blocker.Id, node.Id, ct);
            }
    
            foreach (var key in blockedMilestones)
            {
                var node = await Need(
                    c.ProjectId,
                    DevelopmentGraphNodeType.Milestone,
                    $"milestone:{key}",
                    "milestone",
                    ct);
                await LinkBlocker(c.ProjectId, blocker.Id, node.Id, ct);
            }
    
            return blocker;
        }
    
    public async Task<DevelopmentGraphNodeDto> UpsertRiskAsync(
            UpsertRiskCommand c,
            CancellationToken ct)
        {
            ValidateKey(c.Key);
    
            var probability = Math.Clamp(c.ProbabilityPercent, 0m, 100m);
            var impact = Math.Clamp(c.ImpactPercent, 0m, 100m);
            var exposure = probability * impact / 100m;
    
            var affected = (c.AffectedItems ?? [])
                .Where(x => x is not null && NotBlank(x.Type) && NotBlank(x.Key))
                .DistinctBy(
                    x => $"{x.Type}:{x.Key}",
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
    
            var meta = JsonSerializer.Serialize(new
            {
                c.Key,
                c.Description,
                ProbabilityPercent = probability,
                ImpactPercent = impact,
                ExposureScore = exposure,
                c.Category,
                OwnerRole = Trim(c.OwnerRole, 200),
                c.ReviewDueUtc,
                Trigger = Trim(c.Trigger, 2000),
                Mitigation = Trim(c.Mitigation, 4000),
                AffectedItems = affected
            });
    
            var risk = await _graph.UpsertNodeAsync(
                new(
                    c.ProjectId,
                    DevelopmentGraphNodeType.Risk,
                    $"risk:{c.Key}",
                    c.Title,
                    c.Status,
                    "DIP",
                    c.ExternalReference,
                    meta),
                ct);
    
            foreach (var item in affected)
            {
                var (type, key) = ResolveTypeAndKey(item.Type, item.Key);
                var target = await Need(c.ProjectId, type, key, item.Type, ct);
    
                await _graph.LinkAsync(
                    new(
                        c.ProjectId,
                        risk.Id,
                        target.Id,
                        DevelopmentGraphEdgeType.Affects,
                        Math.Clamp(exposure, 10m, 100m),
                        exposure >= 70m,
                        JsonSerializer.Serialize(new { managementRisk = true })),
                    ct);
            }
    
            return risk;
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
