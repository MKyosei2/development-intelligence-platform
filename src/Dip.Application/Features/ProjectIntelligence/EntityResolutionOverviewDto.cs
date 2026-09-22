// Purpose: Defines the EntityResolutionOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record EntityResolutionOverviewDto(Guid ProjectId, int Resolved, int Suggested, int Unresolved, IReadOnlyList<EntityMatchDto> Items, DateTimeOffset GeneratedAtUtc);
