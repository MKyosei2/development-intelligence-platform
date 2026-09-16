// Purpose: Defines the UpsertResourceAllocationCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record UpsertResourceAllocationCommand(Guid ProjectId,string Key,string ToWorkstreamKey,decimal CapacityHoursPerWeek,DateTimeOffset StartUtc,DateTimeOffset EndUtc,string Status="Proposed",string? FromWorkstreamKey=null,string? Rationale=null,string? DecisionKey=null);
