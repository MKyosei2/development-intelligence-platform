// Purpose: Provides the stable management-work facade while focused services own team, task, scope, risk, decision, and allocation behavior.
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

public sealed class WorkManagementService
{
    private readonly ManagementTeamService _teams;
    private readonly ManagementTaskService _tasks;
    private readonly ManagementScopeService _scope;
    private readonly ManagementRiskService _risks;
    private readonly ManagementDecisionService _decisions;
    private readonly ManagementResourceAllocationService _allocations;

    public WorkManagementService(DevelopmentGraphService graph, IDevelopmentGraphRepository repo, ISystemClock clock)
    {
        _teams = new(graph, repo, clock);
        _tasks = new(graph, repo, clock);
        _scope = new(graph, repo, clock);
        _risks = new(graph, repo, clock);
        _decisions = new(graph, repo, clock);
        _allocations = new(graph, repo, clock);
    }

    public Task<DevelopmentGraphNodeDto> UpsertTeamAsync(UpsertTeamCommand c, CancellationToken ct) => _teams.UpsertTeamAsync(c, ct);
    public Task<DevelopmentGraphNodeDto> UpsertWorkstreamAsync(UpsertWorkstreamCommand c, CancellationToken ct) => _tasks.UpsertWorkstreamAsync(c, ct);
    public Task<DevelopmentGraphNodeDto> UpsertTaskAsync(UpsertTaskCommand c, CancellationToken ct) => _tasks.UpsertTaskAsync(c, ct);
    public Task LinkDependencyAsync(LinkManagementDependencyCommand c, CancellationToken ct) => _tasks.LinkDependencyAsync(c, ct);
    public Task<DevelopmentGraphNodeDto> UpsertBlockerAsync(UpsertBlockerCommand c, CancellationToken ct) => _risks.UpsertBlockerAsync(c, ct);
    public Task<DevelopmentGraphNodeDto> UpsertRequirementAsync(UpsertRequirementCommand c, CancellationToken ct) => _scope.UpsertRequirementAsync(c, ct);
    public Task LinkRequirementAsync(LinkRequirementCommand c, CancellationToken ct) => _scope.LinkRequirementAsync(c, ct);
    public Task<DevelopmentGraphNodeDto> CreateScopeBaselineAsync(CreateScopeBaselineCommand c, CancellationToken ct) => _scope.CreateScopeBaselineAsync(c, ct);
    public Task LinkTaskToMilestoneAsync(LinkTaskToMilestoneCommand c, CancellationToken ct) => _tasks.LinkTaskToMilestoneAsync(c, ct);
    public Task<DevelopmentGraphNodeDto> UpsertRiskAsync(UpsertRiskCommand c, CancellationToken ct) => _risks.UpsertRiskAsync(c, ct);
    public Task<DevelopmentGraphNodeDto> UpsertManagementDecisionAsync(UpsertManagementDecisionCommand c, CancellationToken ct) => _decisions.UpsertManagementDecisionAsync(c, ct);
    public Task<DevelopmentGraphNodeDto> UpsertActionItemAsync(UpsertActionItemCommand c, CancellationToken ct) => _decisions.UpsertActionItemAsync(c, ct);
    public Task<DevelopmentGraphNodeDto> UpsertResourceAllocationAsync(UpsertResourceAllocationCommand c, CancellationToken ct) => _allocations.UpsertResourceAllocationAsync(c, ct);
    public Task<DevelopmentGraphNodeDto> UpsertChangeRequestAsync(UpsertChangeRequestCommand c, CancellationToken ct) => _scope.UpsertChangeRequestAsync(c, ct);

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
