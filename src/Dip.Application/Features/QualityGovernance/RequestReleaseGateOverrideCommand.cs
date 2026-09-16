// Purpose: Defines the RequestReleaseGateOverrideCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record RequestReleaseGateOverrideCommand(Guid ProjectId,Guid? PolicyEvaluationId,Guid? QaRunId,string? CommitSha,string Reason,DateTimeOffset ExpiresAtUtc);
