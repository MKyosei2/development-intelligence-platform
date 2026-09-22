// Purpose: Defines the OperationalAlertDto application contract.
namespace Dip.Application.Features.Management;

public sealed record OperationalAlertDto(ManagementAlertDto Alert, AlertStateDto State, bool Visible, bool RequiresEscalation, string RoutedRole);
