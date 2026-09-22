// Purpose: Defines the QaOwnershipRuleDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaOwnershipRuleDto(Guid Id, Guid ProjectId, string Name, string PathPattern, string Team, string? Workstream, int Priority, bool Enabled, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
