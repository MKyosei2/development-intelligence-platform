// Purpose: Defines the AlertStateDto application contract.
namespace Dip.Application.Features.Management;

public sealed record AlertStateDto(string AlertKey, string Status, string? Actor, string? Comment, DateTimeOffset UpdatedAtUtc, DateTimeOffset? SnoozeUntilUtc, int EscalationLevel, DateTimeOffset? LastDeliveredAtUtc, DateTimeOffset? FirstSeenAtUtc = null, DateTimeOffset? ViewedAtUtc = null, string? ViewedBy = null);
