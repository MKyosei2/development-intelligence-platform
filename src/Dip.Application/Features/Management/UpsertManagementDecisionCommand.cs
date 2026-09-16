// Purpose: Defines the UpsertManagementDecisionCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record UpsertManagementDecisionCommand(Guid ProjectId,string Key,string Title,string Decision,string Rationale,string Status="Proposed",string? DecisionOwnerRole=null,DateTimeOffset? DecisionDueUtc=null,DateTimeOffset? DecidedAtUtc=null,IReadOnlyList<ManagementReference>? RelatedItems=null,string? ExternalReference=null);
