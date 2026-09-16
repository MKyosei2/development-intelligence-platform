// Purpose: Defines the CockpitDecisionDto application contract.
namespace Dip.Application.Features.Management;

public sealed record CockpitDecisionDto(string Type,string Key,string Title,string Severity,string Action,string? OwnerRole,DateTimeOffset? DueAtUtc);
