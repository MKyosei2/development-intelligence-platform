// Purpose: Defines the CapacityOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record CapacityOverviewDto(Guid ProjectId,int HorizonWeeks,IReadOnlyList<TeamCapacityDto> Teams,IReadOnlyList<WorkstreamCapacityDto> Workstreams,IReadOnlyList<string> Bottlenecks,DateTimeOffset GeneratedAtUtc);
