// Purpose: Runs Management Notification background processing for DIP.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Management;
using Dip.Infrastructure.Management;
using Microsoft.Extensions.Options;

namespace Dip.Worker.Workers;

public sealed class ManagementNotificationWorker(IServiceScopeFactory scopes, IEnumerable<IManagementNotificationChannel> channels, IOptions<ManagementNotificationOptions> options, ISystemClock clock, ILogger<ManagementNotificationWorker> logger) : BackgroundService
{
    private readonly ManagementNotificationOptions _options = options.Value;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { if (_options.Enabled) await ScanAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Management notification scan failed."); }
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_options.ScanIntervalSeconds, 30, 3600)), stoppingToken);
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        var configured = channels.Where(x => x.IsConfigured).ToArray();
        if (configured.Length == 0) return;
        using var scope = scopes.CreateScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var operations = scope.ServiceProvider.GetRequiredService<ManagementOperationsService>();
        foreach (var project in await projects.ListAsync(ct))
        {
            IReadOnlyList<OperationalAlertDto> alerts;
            try
            {
                alerts = await operations.GetOperationalAlertsAsync(project.Id, 8, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not compute management alerts for project {ProjectId}.", project.Id);
                continue;
            }
            foreach (var operational in alerts.Where(x => x.Visible && x.RequiresEscalation))
            {
                var now = clock.UtcNow;
                var level = ManagementOperationsService.RequiredEscalation(operational.Alert, now, operational.State.FirstSeenAtUtc ?? operational.Alert.TriggeredAtUtc);
                var subject = $"[DIP][{operational.Alert.Severity}][L{level}] {project.Name}: {operational.Alert.Title}";
                var body = $"Category: {operational.Alert.Category}\nOwner route: {operational.RoutedRole}\nReason: {operational.Alert.Explanation}\nRecommended action: {operational.Alert.RecommendedAction}\nDue: {operational.Alert.DueAtUtc?.ToString("u") ?? "n/a"}\nAlert key: {operational.Alert.Key}\nProject: {project.Id}";
                var envelope = new ManagementNotificationEnvelope(project.Id, project.Name, operational, level, subject, body);
                var delivered = false;
                var routed = ChannelsFor(operational.RoutedRole, configured);
                foreach (var channel in routed)
                {
                    try
                    {
                        await channel.DeliverAsync(envelope, ct);
                        delivered = true;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Management alert {AlertKey} delivery through {Channel} failed.", operational.Alert.Key, channel.ChannelName);
                    }
                }
                if (delivered) await operations.RecordAlertDeliveryAsync(project.Id, operational.Alert, level, now, ct);
            }
        }
    }
    private IManagementNotificationChannel[] ChannelsFor(string role, IReadOnlyList<IManagementNotificationChannel> configured)
    {
        if (_options.RoleRoutes.Count == 0) return configured.ToArray();
        if (!_options.RoleRoutes.TryGetValue(role, out var names) && !_options.RoleRoutes.TryGetValue("*", out names)) return configured.ToArray();
        var allowed = names.Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase); return configured.Where(x => allowed.Contains(x.ChannelName)).ToArray();
    }
}
