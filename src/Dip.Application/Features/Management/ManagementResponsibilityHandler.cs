// Purpose: Applies responsibility assignments as one transaction-bounded development-graph mutation.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

internal sealed class ManagementResponsibilityHandler(
    DevelopmentGraphService graph,
    IDevelopmentGraphRepository repository)
{
    public async Task<DevelopmentGraphNodeDto> UpsertAsync(
        UpsertResponsibilityCommand command,
        CancellationToken ct)
    {
        EnsureProject(command.ProjectId);
        EnsureKey(command.Key);

        var (targetType, targetKey) = ManagementOperationsService.ResolveManagementType(
            command.Target.Type,
            command.Target.Key);
        var target = await repository.FindNodeAsync(
            command.ProjectId,
            targetType,
            targetKey,
            ct)
            ?? throw new KeyNotFoundException(
                $"{command.Target.Type} '{command.Target.Key}' was not found.");

        var consulted = CleanRoles(command.ConsultedRoles);
        var informed = CleanRoles(command.InformedRoles);
        if (string.IsNullOrWhiteSpace(command.ResponsibleRole)
            && string.IsNullOrWhiteSpace(command.AccountableRole)
            && consulted.Count == 0
            && informed.Count == 0)
        {
            throw new ArgumentException(
                "At least one RACI role must be supplied.",
                nameof(command));
        }

        var metadata = JsonSerializer.Serialize(
            new
            {
                command.Key,
                TargetType = command.Target.Type,
                TargetKey = command.Target.Key,
                ResponsibleRole = Clean(command.ResponsibleRole, 120),
                AccountableRole = Clean(command.AccountableRole, 120),
                ConsultedRoles = consulted,
                InformedRoles = informed
            });

        return await graph.MutateAsync(
            async (mutation, token) =>
            {
                var node = await mutation.UpsertNodeAsync(
                    new(
                        command.ProjectId,
                        DevelopmentGraphNodeType.ResponsibilityAssignment,
                        $"raci:{command.Key}",
                        $"RACI {command.Target.Type}:{command.Target.Key}",
                        command.Status,
                        "DIP",
                        null,
                        metadata),
                    token);

                if (!string.IsNullOrWhiteSpace(command.ResponsibleRole))
                {
                    await mutation.LinkAsync(
                        new(
                            command.ProjectId,
                            node.Id,
                            target.Id,
                            DevelopmentGraphEdgeType.ResponsibleFor,
                            100m,
                            true,
                            JsonSerializer.Serialize(new { Role = command.ResponsibleRole })),
                        token);
                }

                if (!string.IsNullOrWhiteSpace(command.AccountableRole))
                {
                    await mutation.LinkAsync(
                        new(
                            command.ProjectId,
                            node.Id,
                            target.Id,
                            DevelopmentGraphEdgeType.AccountableFor,
                            100m,
                            true,
                            JsonSerializer.Serialize(new { Role = command.AccountableRole })),
                        token);
                }

                if (consulted.Count > 0)
                {
                    await mutation.LinkAsync(
                        new(
                            command.ProjectId,
                            node.Id,
                            target.Id,
                            DevelopmentGraphEdgeType.ConsultedOn,
                            70m,
                            false,
                            JsonSerializer.Serialize(new { Roles = consulted })),
                        token);
                }

                if (informed.Count > 0)
                {
                    await mutation.LinkAsync(
                        new(
                            command.ProjectId,
                            node.Id,
                            target.Id,
                            DevelopmentGraphEdgeType.InformedOn,
                            50m,
                            false,
                            JsonSerializer.Serialize(new { Roles = informed })),
                        token);
                }

                return node;
            },
            ct);
    }

    private static IReadOnlyList<string> CleanRoles(IEnumerable<string>? roles) =>
        (roles ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim()[..Math.Min(120, value.Trim().Length)])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

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
