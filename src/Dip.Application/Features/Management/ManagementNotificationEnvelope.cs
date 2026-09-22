// Purpose: Defines ManagementNotificationEnvelope.
namespace Dip.Application.Features.Management;

public sealed record ManagementNotificationEnvelope(Guid ProjectId, string ProjectName, OperationalAlertDto OperationalAlert, int EscalationLevel, string Subject, string Body);
