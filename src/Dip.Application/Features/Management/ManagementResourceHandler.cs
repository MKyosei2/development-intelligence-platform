// Purpose: Maintains shared-resource membership without coupling the management facade to metadata mutation details.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

internal sealed class ManagementResourceHandler(
    DevelopmentGraphService graph,
    IDevelopmentGraphRepository repository)
{
    public async Task<DevelopmentGraphNodeDto> UpsertSharedMembershipAsync(
        UpsertSharedResourceMembershipCommand command,
        CancellationToken ct)
    {
        EnsureProject(command.ProjectId);
        EnsureKey(command.TeamKey);

        var team = await repository.FindNodeAsync(
            command.ProjectId,
            DevelopmentGraphNodeType.Team,
            $"team:{command.TeamKey.Trim()}",
            ct)
            ?? throw new KeyNotFoundException($"Team '{command.TeamKey}' was not found.");

        var sharedKey = Clean(command.SharedResourceKey, 200);
        if (!string.IsNullOrWhiteSpace(sharedKey)
            && (!command.SharedPoolCapacityHoursPerWeek.HasValue
                || command.SharedPoolCapacityHoursPerWeek.Value <= 0m))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command.SharedPoolCapacityHoursPerWeek),
                "A positive shared pool weekly capacity is required when a SharedResourceKey is configured.");
        }

        var metadata = MergeMetadata(
            team.MetadataJson,
            new Dictionary<string, object?>
            {
                ["SharedResourceKey"] = sharedKey,
                ["SharedPoolCapacityHoursPerWeek"] = string.IsNullOrWhiteSpace(sharedKey)
                    ? null
                    : Math.Clamp(command.SharedPoolCapacityHoursPerWeek!.Value, 1m, 1_000_000m)
            });

        return await graph.UpsertNodeAsync(
            new(
                command.ProjectId,
                team.Type,
                team.Key,
                team.Name,
                team.Status,
                team.Provider,
                team.ExternalReference,
                metadata),
            ct);
    }

    private static string MergeMetadata(
        string json,
        IReadOnlyDictionary<string, object?> additions)
    {
        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var document = JsonDocument.Parse(json);
            foreach (var property in document.RootElement.EnumerateObject())
                map[property.Name] = JsonSerializer.Deserialize<object?>(property.Value.GetRawText());
        }
        catch (JsonException)
        {
            // Preserve the legacy behavior: malformed historical metadata is replaced by the requested fields.
            map.Clear();
        }

        foreach (var addition in additions)
            map[addition.Key] = addition.Value;

        return JsonSerializer.Serialize(map);
    }

    private static string? Clean(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim()[..Math.Min(max, value.Trim().Length)];

    private static void EnsureProject(Guid projectId)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
    }

    private static void EnsureKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length > 240)
            throw new ArgumentException("Key is required and must be at most 240 characters.", nameof(key));
    }
}
