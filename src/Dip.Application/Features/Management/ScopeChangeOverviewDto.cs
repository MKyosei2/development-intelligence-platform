// Purpose: Defines the ScopeChangeOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ScopeChangeOverviewDto(Guid ProjectId, int Proposed, int Approved, int Rejected, decimal PendingEngineeringHours, decimal PendingArtHours, decimal PendingQaHours, int PendingScheduleDays, decimal PendingRiskDeltaPercent, IReadOnlyList<ChangeRequestDto> Changes, DateTimeOffset GeneratedAtUtc);
