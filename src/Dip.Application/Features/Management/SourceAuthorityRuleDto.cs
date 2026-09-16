// Purpose: Defines the SourceAuthorityRuleDto application contract.
namespace Dip.Application.Features.Management;

public sealed record SourceAuthorityRuleDto(string Key,string DataType,string Authority,string ConflictPolicy,int Priority,string Status);
