// Purpose: Implements Slack Management Notification Channel infrastructure behavior for management.
using System.Net.Http.Json;
using Dip.Application.Features.Management;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Management;
public sealed class SlackManagementNotificationChannel : IManagementNotificationChannel, IDisposable
{
    private readonly ManagementNotificationOptions _o;
    private readonly HttpClient _http = new();
    public string ChannelName => "Slack";
    public bool IsConfigured => IsHttps(_o.SlackWebhookUrl) || _o.RoleDestinations.Values.Any(x => IsHttps(x.SlackWebhookUrl));
    public SlackManagementNotificationChannel(IOptions<ManagementNotificationOptions> options) { _o = options.Value; }
    public async Task DeliverAsync(ManagementNotificationEnvelope m, CancellationToken ct)
    {
        var url = WebhookFor(m.OperationalAlert.RoutedRole);
        if (!IsHttps(url)) throw new InvalidOperationException($"Slack webhook is not configured for role '{m.OperationalAlert.RoutedRole}'.");
        using var res = await _http.PostAsJsonAsync(url, new { text = $"*{m.Subject}*\n{m.Body}" }, ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Slack delivery failed with HTTP {(int)res.StatusCode}.");
    }
    private string WebhookFor(string role) { var destination = _o.DestinationFor(role); return !string.IsNullOrWhiteSpace(destination?.SlackWebhookUrl) ? destination.SlackWebhookUrl : _o.SlackWebhookUrl; }
    private static bool IsHttps(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps;
    public void Dispose() => _http.Dispose();
}
