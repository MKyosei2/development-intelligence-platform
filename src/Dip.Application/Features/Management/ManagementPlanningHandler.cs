// Purpose: Applies work-calendar planning changes and their graph relationship as one atomic mutation boundary.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

internal sealed class ManagementPlanningHandler(
    DevelopmentGraphService graph,
    IDevelopmentGraphRepository repository)
{
    public async Task<DevelopmentGraphNodeDto> UpsertWorkCalendarAsync(
        UpsertWorkCalendarCommand command,
        CancellationToken ct)
    {
        EnsureProject(command.ProjectId);
        EnsureKey(command.Key);
        if (command.HoursPerDay <= 0m || command.HoursPerDay > 24m)
            throw new ArgumentOutOfRangeException(nameof(command.HoursPerDay));

        var days = (command.WorkingDays is { Count: > 0 }
                ? command.WorkingDays
                : [
                    DayOfWeek.Monday,
                    DayOfWeek.Tuesday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Thursday,
                    DayOfWeek.Friday
                ])
            .Distinct()
            .ToArray();
        if (days.Length == 0)
            throw new ArgumentException("At least one working day is required.", nameof(command));

        DevelopmentGraphNode? team = null;
        if (!string.IsNullOrWhiteSpace(command.TeamKey))
        {
            team = await repository.FindNodeAsync(
                command.ProjectId,
                DevelopmentGraphNodeType.Team,
                $"team:{command.TeamKey.Trim()}",
                ct)
                ?? throw new KeyNotFoundException($"Team '{command.TeamKey}' was not found.");
        }

        var exceptions = (command.Exceptions ?? [])
            .Where(item => item.Hours > 0m)
            .Select(item => item with
            {
                Hours = Math.Clamp(item.Hours, 0m, 10_000m),
                Category = string.IsNullOrWhiteSpace(item.Category) ? "Other" : item.Category.Trim()
            })
            .ToArray();
        if (exceptions.Any(item => item.EndsAtUtc < item.StartsAtUtc))
        {
            throw new ArgumentException(
                "Calendar exception end must not precede start.",
                nameof(command));
        }

        var metadata = JsonSerializer.Serialize(
            new
            {
                command.Key,
                TeamKey = Clean(command.TeamKey, 240),
                HoursPerDay = Math.Clamp(command.HoursPerDay, 0.1m, 24m),
                WorkingDays = days,
                RecurringMeetingHoursPerWeek = Math.Max(0m, command.RecurringMeetingHoursPerWeek),
                SupportDutyHoursPerWeek = Math.Max(0m, command.SupportDutyHoursPerWeek),
                Exceptions = exceptions,
                TimeZone = string.IsNullOrWhiteSpace(command.TimeZone) ? "UTC" : command.TimeZone.Trim()
            });

        return await graph.MutateAsync(
            async (mutation, token) =>
            {
                var node = await mutation.UpsertNodeAsync(
                    new(
                        command.ProjectId,
                        DevelopmentGraphNodeType.WorkCalendar,
                        $"calendar:{command.Key}",
                        command.Name,
                        command.Status,
                        "DIP",
                        null,
                        metadata),
                    token);

                if (team is not null)
                {
                    await mutation.LinkAsync(
                        new(
                            command.ProjectId,
                            node.Id,
                            team.Id,
                            DevelopmentGraphEdgeType.RelatesTo,
                            80m,
                            false,
                            "{\"capacityCalendar\":true}"),
                        token);
                }

                return node;
            },
            ct);
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
