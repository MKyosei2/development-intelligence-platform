// Purpose: Defines the ManagementAlertDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ManagementAlertDto(string Key,string Severity,string Category,string Title,string Explanation,string RecommendedAction,DateTimeOffset TriggeredAtUtc,DateTimeOffset? DueAtUtc,string SourceKey);
