// Purpose: Defines the ScopeOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ScopeOverviewDto(Guid ProjectId, string? BaselineKey, int BaselineRequirements, int CurrentIncludedRequirements, int AddedAfterBaseline, int DeferredOrRemoved, decimal ScopeGrowthPercent, int IncompleteHighPriority, IReadOnlyList<RequirementDto> Requirements, IReadOnlyList<string> Warnings, DateTimeOffset GeneratedAtUtc);
