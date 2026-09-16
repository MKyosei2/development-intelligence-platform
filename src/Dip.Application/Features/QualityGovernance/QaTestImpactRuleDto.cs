// Purpose: Defines the QaTestImpactRuleDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaTestImpactRuleDto(Guid Id,Guid ProjectId,string PathPattern,string TestKey,string CommandJson,int Priority,bool Enabled,DateTimeOffset CreatedAtUtc);
