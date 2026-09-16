// Purpose: Defines the CreateTestImpactRuleCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record CreateTestImpactRuleCommand(Guid ProjectId,string PathPattern,string TestKey,string CommandJson,int Priority=0,bool Enabled=true);
