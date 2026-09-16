// Purpose: Defines the SourceAuthorityRuleCommand application contract.
namespace Dip.Application.Features.Management;

public sealed record SourceAuthorityRuleCommand(Guid ProjectId,string Key,string DataType,string Authority,string ConflictPolicy="Manual",int Priority=100,string Status="Active");
