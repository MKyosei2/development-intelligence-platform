using Dip.Api.Authorization;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.Realtime;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.SignalR;

namespace Dip.Api.Hubs;

public sealed class ProjectEventsHub(
    ProjectAccessService access,
    IConfiguration configuration) : Hub
{
    public async Task SubscribeProject(Guid projectId)
    {
        if (projectId == Guid.Empty)
            throw new HubException("ProjectId is required.");

        var requiresAuthentication =
            configuration.GetValue<bool?>("Authentication:RequireAuthenticatedUser") ?? false;

        if (requiresAuthentication)
        {
            var user = Context.User;
            if (user?.Identity?.IsAuthenticated != true)
                throw new HubException("Authentication is required.");

            if (!user.IsInRole(Roles.Admin))
            {
                var subject = RequestActor.Subject(user);
                if (!await access.HasAccessAsync(
                        projectId,
                        subject,
                        ProjectMembershipRole.Viewer,
                        Context.ConnectionAborted))
                {
                    throw new HubException("Project access is denied.");
                }
            }
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GroupName(projectId),
            Context.ConnectionAborted);
    }

    public Task UnsubscribeProject(Guid projectId) =>
        Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GroupName(projectId),
            Context.ConnectionAborted);

    internal static string GroupName(Guid projectId) => $"project:{projectId:N}";
}

public sealed class ProjectRealtimePublisher(IHubContext<ProjectEventsHub> hub)
{
    public Task PublishAsync(
        Guid projectId,
        string eventType,
        string message,
        CancellationToken cancellationToken = default)
    {
        var payload = new ProjectRealtimeEventDto(
            projectId,
            eventType,
            message,
            DateTimeOffset.UtcNow);
        return hub.Clients
            .Group(ProjectEventsHub.GroupName(projectId))
            .SendAsync("ProjectEvent", payload, cancellationToken);
    }
}
