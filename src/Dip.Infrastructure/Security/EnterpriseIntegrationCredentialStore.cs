// Purpose: Provides the E nt er pr is eI nt eg ra ti on Cr ed en ti al St or e infrastructure implementation.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dip.Application.Abstractions.Security;
using Microsoft.Extensions.Configuration;
namespace Dip.Infrastructure.Security;

/// <summary>Routes integration secrets to the existing encrypted local store or an external secret broker without changing connectors.</summary>
public sealed class EnterpriseIntegrationCredentialStore: IIntegrationCredentialStore, IDisposable
{
    private const string BrokerScheme="broker-secret://";
    private readonly LocalIntegrationCredentialStore _local;private readonly IConfiguration _configuration;private readonly HttpClient _http=new();
    public EnterpriseIntegrationCredentialStore(LocalIntegrationCredentialStore local,IConfiguration configuration){_local=local;_configuration=configuration;var token=_configuration["DIP:Secrets:BrokerToken"]??Environment.GetEnvironmentVariable("DIP_SECRET_BROKER_TOKEN");if(!string.IsNullOrWhiteSpace(token))_http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);}
    public async Task<string> StoreAsync(string provider,string secretJson,CancellationToken ct)
    {
        var mode=(_configuration["DIP:Secrets:Provider"]??Environment.GetEnvironmentVariable("DIP_SECRET_PROVIDER")??"local").Trim();if(!mode.Equals("broker",StringComparison.OrdinalIgnoreCase))return await _local.StoreAsync(provider,secretJson,ct);
        var endpoint=BrokerEndpoint();using var response=await _http.PostAsJsonAsync(new Uri(endpoint,"secrets"),new{provider,secretJson},ct);response.EnsureSuccessStatusCode();var result=await response.Content.ReadFromJsonAsync<BrokerStoreResult>(cancellationToken:ct)??throw new InvalidOperationException("Secret broker returned no reference.");return BrokerScheme+result.Reference;
    }
    public async Task<string?> GetAsync(string credentialReference,CancellationToken ct)
    {if(!credentialReference.StartsWith(BrokerScheme,StringComparison.OrdinalIgnoreCase))return await _local.GetAsync(credentialReference,ct);var key=credentialReference[BrokerScheme.Length..];using var response=await _http.GetAsync(new Uri(BrokerEndpoint(),"secrets/"+Uri.EscapeDataString(key)),ct);if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return null;response.EnsureSuccessStatusCode();return (await response.Content.ReadFromJsonAsync<BrokerGetResult>(cancellationToken:ct))?.SecretJson;}
    public async Task DeleteAsync(string credentialReference,CancellationToken ct)
    {if(!credentialReference.StartsWith(BrokerScheme,StringComparison.OrdinalIgnoreCase)){await _local.DeleteAsync(credentialReference,ct);return;}var key=credentialReference[BrokerScheme.Length..];using var response=await _http.DeleteAsync(new Uri(BrokerEndpoint(),"secrets/"+Uri.EscapeDataString(key)),ct);if(response.StatusCode!=System.Net.HttpStatusCode.NotFound)response.EnsureSuccessStatusCode();}
    private Uri BrokerEndpoint(){var raw=_configuration["DIP:Secrets:BrokerUrl"]??Environment.GetEnvironmentVariable("DIP_SECRET_BROKER_URL");if(!Uri.TryCreate(raw,UriKind.Absolute,out var uri))throw new InvalidOperationException("DIP secret broker URL is not configured.");return uri.ToString().EndsWith('/')?uri:new Uri(uri.ToString()+"/");}
    public void Dispose()=>_http.Dispose();private sealed record BrokerStoreResult(string Reference);private sealed record BrokerGetResult(string SecretJson);
}
