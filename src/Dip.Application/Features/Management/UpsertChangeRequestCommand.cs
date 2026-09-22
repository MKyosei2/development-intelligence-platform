// Purpose: Defines the UpsertChangeRequestCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record UpsertChangeRequestCommand(Guid ProjectId, string Key, string Title, string Description, string Status = "Proposed", string Priority = "Medium", string? RequestedByRole = null, DateTimeOffset? RequestedAtUtc = null, decimal EngineeringHoursDelta = 0m, decimal ArtHoursDelta = 0m, decimal QaHoursDelta = 0m, int ScheduleDaysDelta = 0, decimal RiskDeltaPercent = 0m, IReadOnlyList<string>? RequirementKeys = null, IReadOnlyList<string>? WorkstreamKeys = null, string? DecisionKey = null, string? ExternalReference = null);
