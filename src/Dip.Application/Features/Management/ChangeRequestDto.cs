// Purpose: Defines the ChangeRequestDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ChangeRequestDto(string Key, string Title, string Description, string Status, string Priority, string? RequestedByRole, DateTimeOffset RequestedAtUtc, decimal EngineeringHoursDelta, decimal ArtHoursDelta, decimal QaHoursDelta, int ScheduleDaysDelta, decimal RiskDeltaPercent, IReadOnlyList<string> RequirementKeys, IReadOnlyList<string> WorkstreamKeys, string? DecisionKey, string ManagementImpact);
