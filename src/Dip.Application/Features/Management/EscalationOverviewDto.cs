// Purpose: Defines the EscalationOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record EscalationOverviewDto(Guid ProjectId, int Critical, int High, int Total, IReadOnlyList<ManagementAlertDto> Alerts, DateTimeOffset GeneratedAtUtc);
