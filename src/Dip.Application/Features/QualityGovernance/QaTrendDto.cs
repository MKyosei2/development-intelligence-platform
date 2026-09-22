// Purpose: Defines the QaTrendDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaTrendDto(Guid Id, Guid ProjectId, Guid QaRunId, int OpenFindingCount, int CriticalCount, int WarningCount, int FlakyCount, int ActiveWaiverCount, decimal TechnicalDebtScore, DateTimeOffset CapturedAtUtc);
