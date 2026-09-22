// Purpose: Defines the BlockerImpactDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record BlockerImpactDto(string Key, string Title, string Reason, string Severity, string Status, int AgeDays, int DirectlyBlocked, int DownstreamAffected, IReadOnlyList<string> BlockedItems, IReadOnlyList<string> DownstreamItems, string ManagementAction);
