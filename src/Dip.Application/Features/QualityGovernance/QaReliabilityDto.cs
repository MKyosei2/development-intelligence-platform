// Purpose: Defines the QaReliabilityDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaReliabilityDto(Guid Id, Guid ProjectId, string TestKey, int PassCount, int FailCount, int ConsecutivePasses, int ConsecutiveFailures, TestReliabilityClassification Classification, string? LastCommitSha, DateTimeOffset FirstObservedAtUtc, DateTimeOffset LastObservedAtUtc, decimal FailureRatePercent);
