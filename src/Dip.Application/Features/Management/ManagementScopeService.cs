// Purpose: Owns requirements, scope baselines, and scope-change mutations.
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

internal sealed class ManagementScopeService
{
    private readonly DevelopmentGraphService _graph;
    private readonly IDevelopmentGraphRepository _repo;
    private readonly ISystemClock _clock;

    public ManagementScopeService(DevelopmentGraphService graph, IDevelopmentGraphRepository repo, ISystemClock clock)
    {
        _graph = graph;
        _repo = repo;
        _clock = clock;
    }

    public Task<DevelopmentGraphNodeDto> UpsertRequirementAsync(
            UpsertRequirementCommand c,
            CancellationToken ct)
    {
        ValidateKey(c.Key);

        var meta = JsonSerializer.Serialize(new
        {
            c.Key,
            Priority = NormalizePriority(c.Priority),
            ScopeState = NormalizeScope(c.ScopeState),
            CompletionPercent = Math.Clamp(c.CompletionPercent, 0m, 100m),
            AcceptanceSummary = Trim(c.AcceptanceSummary, 4000)
        });

        return _graph.UpsertNodeAsync(
            new(
                c.ProjectId,
                DevelopmentGraphNodeType.Requirement,
                $"requirement:{c.Key}",
                c.Title,
                c.Status,
                "DIP",
                c.ExternalReference,
                meta),
            ct);
    }

    public async Task LinkRequirementAsync(
            LinkRequirementCommand c,
            CancellationToken ct)
    {
        var requirement = await Need(
            c.ProjectId,
            DevelopmentGraphNodeType.Requirement,
            $"requirement:{c.RequirementKey}",
            "requirement",
            ct);

        var (type, key) = ResolveTypeAndKey(c.TargetType, c.TargetKey);
        var target = await Need(c.ProjectId, type, key, c.TargetType, ct);

        await _graph.LinkAsync(
            new(
                c.ProjectId,
                target.Id,
                requirement.Id,
                DevelopmentGraphEdgeType.Satisfies,
                90m,
                false,
                "{}"),
            ct);
    }

    public async Task<DevelopmentGraphNodeDto> CreateScopeBaselineAsync(
            CreateScopeBaselineCommand c,
            CancellationToken ct)
    {
        ValidateKey(c.Key);

        var requirements = await _repo.ListNodesAsync(
            c.ProjectId,
            DevelopmentGraphNodeType.Requirement,
            ct);

        var included = requirements.Count(IsInCurrentScope);
        var keys = requirements
            .Where(IsInCurrentScope)
            .Select(x => Strip(x.Key))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var captured = c.CapturedAtUtc ?? _clock.UtcNow;
        var meta = JsonSerializer.Serialize(new
        {
            c.Key,
            CapturedAtUtc = captured,
            BaselineRequirements = included,
            RequirementKeys = keys
        });

        return await _graph.UpsertNodeAsync(
            new(
                c.ProjectId,
                DevelopmentGraphNodeType.ScopeBaseline,
                $"scope:{c.Key}",
                c.Name,
                "Active",
                "DIP",
                null,
                meta),
            ct);
    }

    public async Task<DevelopmentGraphNodeDto> UpsertChangeRequestAsync(
            UpsertChangeRequestCommand c,
            CancellationToken ct)
    {
        ValidateKey(c.Key);

        var requirements = (c.RequirementKeys ?? [])
            .Where(NotBlank)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var workstreams = (c.WorkstreamKeys ?? [])
            .Where(NotBlank)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var requested = c.RequestedAtUtc ?? _clock.UtcNow;

        var meta = JsonSerializer.Serialize(new
        {
            c.Key,
            c.Description,
            Priority = NormalizePriority(c.Priority),
            RequestedByRole = Trim(c.RequestedByRole, 200),
            RequestedAtUtc = requested,
            EngineeringHoursDelta = c.EngineeringHoursDelta,
            ArtHoursDelta = c.ArtHoursDelta,
            QaHoursDelta = c.QaHoursDelta,
            ScheduleDaysDelta = c.ScheduleDaysDelta,
            RiskDeltaPercent = c.RiskDeltaPercent,
            RequirementKeys = requirements,
            WorkstreamKeys = workstreams,
            DecisionKey = Trim(c.DecisionKey, 240)
        });

        var request = await _graph.UpsertNodeAsync(
            new(
                c.ProjectId,
                DevelopmentGraphNodeType.ChangeRequest,
                $"change:{c.Key}",
                c.Title,
                c.Status,
                "DIP",
                c.ExternalReference,
                meta),
            ct);

        foreach (var key in requirements)
        {
            var target = await Need(
                c.ProjectId,
                DevelopmentGraphNodeType.Requirement,
                $"requirement:{key}",
                "requirement",
                ct);

            await _graph.LinkAsync(
                new(
                    c.ProjectId,
                    request.Id,
                    target.Id,
                    DevelopmentGraphEdgeType.Impacts,
                    85m,
                    NormalizePriority(c.Priority) is "Critical" or "High",
                    "{}"),
                ct);
        }

        foreach (var key in workstreams)
        {
            var target = await Need(
                c.ProjectId,
                DevelopmentGraphNodeType.Workstream,
                $"workstream:{key}",
                "workstream",
                ct);

            await _graph.LinkAsync(
                new(
                    c.ProjectId,
                    request.Id,
                    target.Id,
                    DevelopmentGraphEdgeType.Impacts,
                    85m,
                    NormalizePriority(c.Priority) is "Critical" or "High",
                    "{}"),
                ct);
        }

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
                    request.Id,
                    decision.Id,
                    DevelopmentGraphEdgeType.Requires,
                    90m,
                    true,
                    "{}"),
                ct);
        }

        return request;
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
