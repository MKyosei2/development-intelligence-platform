// Purpose: Coordinates R ea lt im eU pd at e application operations for this repository area.
using Dip.Application.Features.Realtime;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.SignalR.Client;

namespace Dip.Web.Services;

public sealed class RealtimeUpdateService(
    IConfiguration configuration,
    AuthenticationStateProvider authenticationStateProvider) : IAsyncDisposable
{
    private HubConnection? _connection;
    private Guid? _subscribedProjectId;
    private bool _started;

    public event Action<ProjectRealtimeEventDto>? ProjectEvent;
    public event Action<string>? ConnectionStateChanged;

    public HubConnectionState State => _connection?.State ?? HubConnectionState.Disconnected;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started && _connection is not null)
            return;

        var apiBase = configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/";
        var hubUri = new Uri(new Uri(EnsureTrailingSlash(apiBase)), "hubs/projects");

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = async () =>
                {
                    var state = await authenticationStateProvider.GetAuthenticationStateAsync();
                    return state.User.FindFirst("dip_api_token")?.Value;
                };
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(15)
            })
            .Build();

        _connection.On<ProjectRealtimeEventDto>("ProjectEvent", payload =>
        {
            ProjectEvent?.Invoke(payload);
        });
        _connection.Reconnecting += _ =>
        {
            ConnectionStateChanged?.Invoke("Reconnecting");
            return Task.CompletedTask;
        };
        _connection.Reconnected += async _ =>
        {
            ConnectionStateChanged?.Invoke("Connected");
            if (_subscribedProjectId is { } projectId)
                await _connection.InvokeAsync("SubscribeProject", projectId, cancellationToken);
        };
        _connection.Closed += _ =>
        {
            ConnectionStateChanged?.Invoke("Disconnected");
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync(cancellationToken);
            _started = true;
            ConnectionStateChanged?.Invoke("Connected");
        }
        catch
        {
            _started = false;
            ConnectionStateChanged?.Invoke("Unavailable");
            // Real-time updates are an enhancement. Pages continue to support manual refresh.
        }
    }

    public async Task SetProjectAsync(Guid? projectId, CancellationToken cancellationToken = default)
    {
        if (_connection is null || !_started || _connection.State != HubConnectionState.Connected)
        {
            _subscribedProjectId = projectId;
            return;
        }

        if (_subscribedProjectId == projectId)
            return;

        if (_subscribedProjectId is { } previous)
        {
            try { await _connection.InvokeAsync("UnsubscribeProject", previous, cancellationToken); }
            catch {
                // Intentionally ignored to preserve the existing best-effort or fallback behavior.
            }
        }

        _subscribedProjectId = projectId;
        if (projectId is { } current)
            await _connection.InvokeAsync("SubscribeProject", current, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
        _started = false;
    }

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith('/') ? value : value + "/";
}
