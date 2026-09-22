// Purpose: Defines the DependencyOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record DependencyOverviewDto(Guid ProjectId, int Links, int CriticalLinks, int CrossWorkstreamLinks, bool HasCycle, IReadOnlyList<string> CycleNodes, IReadOnlyList<DependencyLinkDto> Dependencies, DateTimeOffset GeneratedAtUtc);
