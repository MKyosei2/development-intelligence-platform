// Purpose: Defines the DecisionActionOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record DecisionActionOverviewDto(Guid ProjectId,int DecisionsPending,int OpenActions,int OverdueActions,IReadOnlyList<ManagementDecisionDto> Decisions,IReadOnlyList<ManagementActionDto> Actions,DateTimeOffset GeneratedAtUtc);
