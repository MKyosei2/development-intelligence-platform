// Purpose: Routes integration credentials through a validated local or broker-backed storage boundary.
using System.Net;
using System.Net.Http.Json;
using Dip.Application.Abstractions.Security;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Security;

public sealed class EnterpriseIntegrationCredentialStore : IIntegrationCredentialStore
{
    private const string BrokerScheme = "broker-secret://";

    private readonly LocalIntegrationCredentialStore _local;
    private readonly HttpClient _httpClient;
    private readonly SecretsBrokerOptions _options;

    public EnterpriseIntegrationCredentialStore(
        LocalIntegrationCredentialStore local,
        HttpClient httpClient,
        IOptions<SecretsBrokerOptions> options)
    {
        _local = local;
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<string> StoreAsync(
        string provider,
        string secretJson,
        CancellationToken ct)
    {
        if (!_options.UseBroker)
            return await _local.StoreAsync(provider, secretJson, ct);

        using var response = await _httpClient.PostAsJsonAsync(
            "secrets",
            new { provider, secretJson },
            ct);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BrokerStoreResult>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Secret broker returned no reference.");
        return BrokerScheme + result.Reference;
    }

    public async Task<string?> GetAsync(
        string credentialReference,
        CancellationToken ct)
    {
        if (!credentialReference.StartsWith(BrokerScheme, StringComparison.OrdinalIgnoreCase))
            return await _local.GetAsync(credentialReference, ct);

        var key = credentialReference[BrokerScheme.Length..];
        using var response = await _httpClient.GetAsync(
            "secrets/" + Uri.EscapeDataString(key),
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BrokerGetResult>(cancellationToken: ct))?.SecretJson;
    }

    public async Task DeleteAsync(
        string credentialReference,
        CancellationToken ct)
    {
        if (!credentialReference.StartsWith(BrokerScheme, StringComparison.OrdinalIgnoreCase))
        {
            await _local.DeleteAsync(credentialReference, ct);
            return;
        }

        var key = credentialReference[BrokerScheme.Length..];
        using var response = await _httpClient.DeleteAsync(
            "secrets/" + Uri.EscapeDataString(key),
            ct);
        if (response.StatusCode != HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }

    private sealed record BrokerStoreResult(string Reference);
    private sealed record BrokerGetResult(string SecretJson);
}
