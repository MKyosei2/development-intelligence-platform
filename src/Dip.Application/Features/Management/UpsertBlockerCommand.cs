// Purpose: Defines the UpsertBlockerCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record UpsertBlockerCommand(Guid ProjectId,string Key,string Title,string Reason,string Severity,DateTimeOffset OpenedAtUtc,string Status="Open",IReadOnlyList<string>? BlockedTaskKeys=null,IReadOnlyList<string>? BlockedWorkstreamKeys=null,IReadOnlyList<string>? BlockedMilestoneKeys=null,string? ExternalReference=null);
