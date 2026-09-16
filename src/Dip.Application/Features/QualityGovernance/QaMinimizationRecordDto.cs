// Purpose: Defines the QaMinimizationRecordDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaMinimizationRecordDto(Guid Id,Guid ProjectId,Guid FindingId,int OriginalUnits,int MinimizedUnits,QaMinimizationStatus Status,string Detail,DateTimeOffset CreatedAtUtc);
