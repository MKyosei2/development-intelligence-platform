// Purpose: Defines the UpsertWorkstreamCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record UpsertWorkstreamCommand(Guid ProjectId, string Key, string Name, string TeamKey, decimal PlannedCapacityHoursPerWeek, DateTimeOffset PlannedStartUtc, DateTimeOffset DueAtUtc, decimal CompletionPercent = 0m, string Status = "Active", string? ExternalReference = null);
