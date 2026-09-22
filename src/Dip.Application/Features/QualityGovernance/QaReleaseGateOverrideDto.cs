// Purpose: Defines the QaReleaseGateOverrideDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaReleaseGateOverrideDto(Guid Id, Guid ProjectId, Guid? PolicyEvaluationId, Guid? QaRunId, string CommitSha, string Reason, string RequestedBy, QaReleaseOverrideStatus Status, string? ApprovedBy, string? DecisionReason, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset ExpiresAtUtc, DateTimeOffset? DecidedAtUtc, long Version);
