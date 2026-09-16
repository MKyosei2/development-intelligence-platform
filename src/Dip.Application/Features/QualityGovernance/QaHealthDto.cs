// Purpose: Defines the QaHealthDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaHealthDto(Guid Id,Guid ProjectId,Guid? QaRunId,string Component,QaSystemHealthStatus Status,string Detail,DateTimeOffset ObservedAtUtc);
