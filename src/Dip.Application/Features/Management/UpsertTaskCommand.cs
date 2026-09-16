// Purpose: Defines the UpsertTaskCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record UpsertTaskCommand(Guid ProjectId,string Key,string Name,string WorkstreamKey,decimal EstimateHours,decimal RemainingHours,DateTimeOffset PlannedStartUtc,DateTimeOffset DueAtUtc,decimal CompletionPercent=0m,string Status="Planned",string Priority="Medium",string? OwnerLabel=null,string? ExternalReference=null);
