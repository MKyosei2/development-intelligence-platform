// Purpose: Defines the CreateOwnershipRuleCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record CreateOwnershipRuleCommand(Guid ProjectId, string Name, string PathPattern, string Team, string? Workstream, int Priority = 0, bool Enabled = true);
