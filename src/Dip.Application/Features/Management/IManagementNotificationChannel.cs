// Purpose: Defines the IManagementNotificationChannel application boundary.
namespace Dip.Application.Features.Management;

public interface IManagementNotificationChannel { string ChannelName { get;
 } bool IsConfigured { get;
 } Task DeliverAsync(ManagementNotificationEnvelope message, CancellationToken ct);
 }
