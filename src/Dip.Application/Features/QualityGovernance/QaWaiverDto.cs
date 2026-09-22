// Purpose: Defines the QaWaiverDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaWaiverDto(Guid Id, Guid ProjectId, string Name, string Reason, string RequestedBy, string? ApprovedBy, string? DecisionReason, string? Fingerprint, string? RuleId, string? PathPattern, WaiverStatus Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset ExpiresAtUtc, DateTimeOffset? ApprovedAtUtc, DateTimeOffset? RevokedAtUtc, long Version);
