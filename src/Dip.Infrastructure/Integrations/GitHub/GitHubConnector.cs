// Purpose: Coordinates GitHub connection health, resource discovery, repository synchronization, and capability reporting through focused collaborators.
using System.Net;
using System.Security.Cryptography;
using Dip.Application.Abstractions.Security;
using Dip.Application.Features.Integrations;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Infrastructure.Integrations.GitHub;

public sealed class GitHubConnector : IIntegrationConnector
{
    private readonly GitHubAuthorizedClientFactory _clients;
    private readonly GitHubDiscoveryService _discovery;
    private readonly GitHubSyncService _sync;

    public GitHubConnector(IIntegrationCredentialStore credentials)
    {
        _clients = new GitHubAuthorizedClientFactory(credentials);
        _discovery = new GitHubDiscoveryService();
        _sync = new GitHubSyncService();
    }

    public string Provider => "GitHub";

    public bool Supports(IntegrationConnection connection) =>
        connection.Provider.Equals(
            Provider,
            StringComparison.OrdinalIgnoreCase)
        && connection.Mode is
            IntegrationConnectionMode.GitHubPat
            or IntegrationConnectionMode.GitHubApp
            or IntegrationConnectionMode.GitHubEnterprisePat
            or IntegrationConnectionMode.GitHubEnterpriseApp;

    public async Task<ConnectorHealth> TestConnectionAsync(
        IntegrationConnection connection,
        CancellationToken cancellationToken)
    {
        if (!connection.Enabled)
        {
            return new ConnectorHealth(
                ConnectionHealthState.Disabled,
                "Connection is disabled.",
                DateTimeOffset.UtcNow,
                connection.ExternalAccount,
                Array.Empty<ConnectorCapability>());
        }

        try
        {
            using var client = await _clients.CreateAuthorizedClientAsync(
                connection,
                cancellationToken);

            string? account = null;

            if (GitHubAuthorizedClientFactory.IsApp(connection.Mode))
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    "installation/repositories?per_page=1");
                using var response = await client.SendAsync(
                    request,
                    cancellationToken);

                if (response.StatusCode is
                    HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden)
                {
                    return Unauthorized(
                        "GitHub App installation credentials were rejected.");
                }

                response.EnsureSuccessStatusCode();
                using var document = await GitHubApiJson.ReadJsonAsync(
                    response,
                    cancellationToken);

                if (document.RootElement.TryGetProperty(
                        "repositories",
                        out var repositories)
                    && repositories.ValueKind == System.Text.Json.JsonValueKind.Array
                    && repositories.GetArrayLength() > 0)
                {
                    account = GitHubApiJson.OwnerOf(repositories[0]);
                }
            }
            else
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    "user");
                using var response = await client.SendAsync(
                    request,
                    cancellationToken);

                if (response.StatusCode is
                    HttpStatusCode.Unauthorized
                    or HttpStatusCode.Forbidden)
                {
                    return Unauthorized("GitHub token was rejected.");
                }

                response.EnsureSuccessStatusCode();
                using var document = await GitHubApiJson.ReadJsonAsync(
                    response,
                    cancellationToken);
                account = GitHubApiJson.String(
                    document.RootElement,
                    "login");
            }

            var capabilities =
                await _discovery.GetCapabilitiesWithClientAsync(
                    connection,
                    client,
                    cancellationToken);

            var degraded = capabilities.Any(item => !item.Supported);

            return new ConnectorHealth(
                degraded
                    ? ConnectionHealthState.Degraded
                    : ConnectionHealthState.Healthy,
                degraded
                    ? "GitHub authentication succeeded, but one or more "
                      + "repository capabilities are unavailable."
                    : "GitHub authentication and repository capability probe succeeded.",
                DateTimeOffset.UtcNow,
                account,
                capabilities);
        }
        catch (InvalidOperationException exception)
        {
            return new ConnectorHealth(
                ConnectionHealthState.Misconfigured,
                GitHubApiJson.Safe(exception.Message),
                DateTimeOffset.UtcNow,
                null,
                Array.Empty<ConnectorCapability>());
        }
        catch (HttpRequestException exception)
        {
            return new ConnectorHealth(
                ConnectionHealthState.Unavailable,
                "GitHub request failed: "
                + GitHubApiJson.Safe(exception.Message),
                DateTimeOffset.UtcNow,
                null,
                Array.Empty<ConnectorCapability>());
        }
        catch (CryptographicException exception)
        {
            return new ConnectorHealth(
                ConnectionHealthState.Misconfigured,
                "GitHub App key could not be used: "
                + GitHubApiJson.Safe(exception.Message),
                DateTimeOffset.UtcNow,
                null,
                Array.Empty<ConnectorCapability>());
        }
    }

    public async Task<IReadOnlyList<ConnectorResourceDto>> DiscoverAsync(
        IntegrationConnection connection,
        ConnectorDiscoveryQuery query,
        CancellationToken cancellationToken)
    {
        if (!connection.Enabled)
            throw new InvalidOperationException("Connection is disabled.");

        using var client = await _clients.CreateAuthorizedClientAsync(
            connection,
            cancellationToken);

        return await _discovery.DiscoverWithClientAsync(
            connection,
            client,
            query,
            cancellationToken);
    }

    public async Task<ConnectorSyncResult> SyncAsync(
        IntegrationConnection connection,
        ProjectSourceBinding binding,
        CancellationToken cancellationToken)
    {
        if (!binding.ResourceType.Equals(
                "repository",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "GitHub connector currently syncs repository resources only.");
        }

        using var client = await _clients.CreateAuthorizedClientAsync(
            connection,
            cancellationToken);

        return await _sync.SyncWithClientAsync(
            connection,
            binding,
            client,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ConnectorCapability>> GetCapabilitiesAsync(
        IntegrationConnection connection,
        CancellationToken cancellationToken)
    {
        using var client = await _clients.CreateAuthorizedClientAsync(
            connection,
            cancellationToken);

        return await _discovery.GetCapabilitiesWithClientAsync(
            connection,
            client,
            cancellationToken);
    }

    private static ConnectorHealth Unauthorized(string message) =>
        new(
            ConnectionHealthState.Unauthorized,
            message,
            DateTimeOffset.UtcNow,
            null,
            Array.Empty<ConnectorCapability>());
}
