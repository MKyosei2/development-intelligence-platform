// Purpose: Defines the QaRetentionRuleDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaRetentionRuleDto(Guid Id, Guid ProjectId, string KindPattern, string RetentionClass, int RetentionDays, bool PreserveOnCritical, bool Enabled, DateTimeOffset CreatedAtUtc);
