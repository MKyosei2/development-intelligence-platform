// Purpose: Implements A pi Au th en ti ca ti on Ha nd le r web UI behavior.
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.Authorization;

namespace Dip.Web.Services;

public sealed class ApiAuthenticationHandler(
    AuthenticationStateProvider authenticationStateProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        var token = state.User.FindFirst("dip_api_token")?.Value;
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
