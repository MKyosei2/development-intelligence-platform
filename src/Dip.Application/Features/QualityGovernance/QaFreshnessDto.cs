// Purpose: Defines the QaFreshnessDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaFreshnessDto(string Status,int AgeDays,int MaximumAgeDays,string? CommitSha,DateTimeOffset? CapturedAtUtc,string Reason,string Confidence="Unverified",decimal ConfidencePercent=0m);
