// Purpose: Owns team capacity and shared-resource metadata mutations.
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

internal sealed class ManagementTeamService
{
    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly ISystemClock _clock;

    public ManagementTeamService(DevelopmentGraphService graph, IDevelopmentGraphRepository repo, ISystemClock clock)
    {
        _graph = graph;
        _repo = repo;
        _clock = clock;
    }

    public Task<DevelopmentGraphNodeDto> UpsertTeamAsync(
            UpsertTeamCommand c,
            CancellationToken ct)
        {
            ValidateKey(c.Key);
            if (c.CapacityHoursPerWeek <= 0m)
                throw new ArgumentOutOfRangeException(nameof(c.CapacityHoursPerWeek));
            if (c.SharedPoolCapacityHoursPerWeek.HasValue
                && c.SharedPoolCapacityHoursPerWeek.Value <= 0m)
                throw new ArgumentOutOfRangeException(nameof(c.SharedPoolCapacityHoursPerWeek));
    
            var sharedKey = Trim(c.SharedResourceKey, 200);
            if (!string.IsNullOrWhiteSpace(sharedKey)
                && !c.SharedPoolCapacityHoursPerWeek.HasValue)
            {
                throw new ArgumentException(
                    "SharedPoolCapacityHoursPerWeek is required when SharedResourceKey is configured.",
                    nameof(c));
            }
    
            var sharedCapacity = string.IsNullOrWhiteSpace(sharedKey)
                ? null
                : c.SharedPoolCapacityHoursPerWeek;
    
            var meta = JsonSerializer.Serialize(new
            {
                c.Key,
                CapacityHoursPerWeek = Math.Clamp(c.CapacityHoursPerWeek, 1m, 100000m),
                BufferPercent = Math.Clamp(c.BufferPercent, 0m, 80m),
                SharedResourceKey = sharedKey,
                SharedPoolCapacityHoursPerWeek = sharedCapacity.HasValue
                    ? Math.Clamp(sharedCapacity.Value, 1m, 1000000m)
                    : (decimal?)null
            });
    
            return _graph.UpsertNodeAsync(
                new(
                    c.ProjectId,
                    DevelopmentGraphNodeType.Team,
                    $"team:{c.Key}",
                    c.Name,
                    c.Status,
                    "DIP",
                    c.ExternalReference,
                    meta),
                ct);
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
