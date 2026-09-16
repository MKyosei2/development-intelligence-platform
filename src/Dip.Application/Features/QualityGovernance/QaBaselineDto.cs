// Purpose: Defines the QaBaselineDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaBaselineDto(Guid Id,Guid ProjectId,string SubjectKey,string MetricKey,string BaselineCommit,string LastKnownGoodCommit,string? FirstBadCommit,decimal BaselineValue,decimal CurrentValue,decimal RegressionPercent,QaRegressionStatus Status,DateTimeOffset UpdatedAtUtc);
