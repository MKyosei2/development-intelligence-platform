// Purpose: Defines the ManagementActionDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ManagementActionDto(string Key,string Title,string Status,string Priority,string? OwnerRole,DateTimeOffset? DueAtUtc,bool Overdue,decimal CompletionPercent,string? DecisionKey,string? Target,string? TargetType=null,string? TargetKey=null);
