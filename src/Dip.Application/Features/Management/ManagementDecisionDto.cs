// Purpose: Defines the ManagementDecisionDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ManagementDecisionDto(string Key,string Title,string Decision,string Rationale,string Status,string? DecisionOwnerRole,DateTimeOffset? DecisionDueUtc,DateTimeOffset? DecidedAtUtc,int OpenActions,IReadOnlyList<string> RelatedItems);
