// Purpose: Defines the ProjectProgressOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record ProjectProgressOverviewDto(Guid ProjectId,int PlanItems,int OnTrack,int Watch,int AtRisk,int UnverifiedClaims,int StaleItems,IReadOnlyList<ReconstructedProgressDto> Items,IReadOnlyList<PlanActualContradictionDto> Contradictions,DateTimeOffset GeneratedAtUtc);
