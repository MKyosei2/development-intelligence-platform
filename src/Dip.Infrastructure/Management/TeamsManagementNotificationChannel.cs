using System.Net.Http.Json;
using Dip.Application.Features.Management;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Management;
public sealed class TeamsManagementNotificationChannel:IManagementNotificationChannel,IDisposable
{
    private readonly ManagementNotificationOptions _o;private readonly HttpClient _http=new();public string ChannelName=>"Teams";public bool IsConfigured=>IsHttps(_o.TeamsWebhookUrl)||_o.RoleDestinations.Values.Any(x=>IsHttps(x.TeamsWebhookUrl));
    public TeamsManagementNotificationChannel(IOptions<ManagementNotificationOptions> options){_o=options.Value;}
    public async Task DeliverAsync(ManagementNotificationEnvelope m,CancellationToken ct){var url=WebhookFor(m.OperationalAlert.RoutedRole);if(!IsHttps(url))throw new InvalidOperationException($"Teams webhook is not configured for role '{m.OperationalAlert.RoutedRole}'.");using var res=await _http.PostAsJsonAsync(url,new{text=$"{m.Subject}\n\n{m.Body}"},ct);if(!res.IsSuccessStatusCode)throw new InvalidOperationException($"Teams delivery failed with HTTP {(int)res.StatusCode}.");}
    private string WebhookFor(string role){var destination=_o.DestinationFor(role);return !string.IsNullOrWhiteSpace(destination?.TeamsWebhookUrl)?destination.TeamsWebhookUrl:_o.TeamsWebhookUrl;}
    private static bool IsHttps(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var u)&&u.Scheme==Uri.UriSchemeHttps;
    public void Dispose()=>_http.Dispose();
}
