// Purpose: Defines the ResourceAllocationRecommendationDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ResourceAllocationRecommendationDto(string WorkstreamKey,string WorkstreamName,string TeamName,decimal CurrentUtilizationPercent,decimal SuggestedAdditionalHoursPerWeek,string Reason);
