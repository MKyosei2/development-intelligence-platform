// Purpose: Defines the CreateRetentionRuleCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record CreateRetentionRuleCommand(Guid ProjectId,string KindPattern,string RetentionClass,int RetentionDays,bool PreserveOnCritical=true,bool Enabled=true);
