// Purpose: Defines the ResourceAllocationDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ResourceAllocationDto(string Key,string Status,string? FromWorkstreamKey,string ToWorkstreamKey,decimal CapacityHoursPerWeek,DateTimeOffset StartUtc,DateTimeOffset EndUtc,string? Rationale,string? DecisionKey);
