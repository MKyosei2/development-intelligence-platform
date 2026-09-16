// Purpose: Defines the SharedCapacityConflictDto application contract.
namespace Dip.Application.Features.Management;

public sealed record SharedCapacityConflictDto(string TeamName,decimal AvailableHoursPerWeek,decimal PlannedHoursPerWeek,decimal ShortageHoursPerWeek,IReadOnlyList<string> Projects,string Recommendation);
