// Purpose: Defines the ResourceAllocationOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ResourceAllocationOverviewDto(Guid ProjectId,IReadOnlyList<ResourceAllocationDto> Allocations,IReadOnlyList<ResourceAllocationRecommendationDto> Recommendations,DateTimeOffset GeneratedAtUtc);
