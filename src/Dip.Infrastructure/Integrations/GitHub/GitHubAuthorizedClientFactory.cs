// Purpose: Creates authenticated GitHub HTTP clients and manages PAT or GitHub App installation-token acquisition.
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Security;
using Dip.Application.Features.Integrations;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Infrastructure.Integrations.GitHub;

internal sealed class GitHubAuthorizedClientFactory
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IIntegrationCredentialStore _credentials;
    private readonly Dictionary<Guid, CachedToken> _installationTokens = new();
    private readonly SemaphoreSlim _tokenGate = new(1, 1);

    internal GitHubAuthorizedClientFactory(
        IIntegrationCredentialStore credentials) =>
        _credentials = credentials;

    internal async Task<HttpClient> CreateAuthorizedClientAsync(
        IntegrationConnection connection,
        CancellationToken cancellationToken)
    {
        var credential = await GetCredentialAsync(
            connection,
            cancellationToken);

        var client = CreateBaseClient(connection);

        try
        {
            var token = IsApp(connection.Mode)
                ? await GetInstallationTokenAsync(
                    connection,
                    credential,
                    cancellationToken)
                : credential.Token;

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new InvalidOperationException(
                    "GitHub credential did not contain a usable token.");
            }

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    internal static bool IsApp(IntegrationConnectionMode mode) =>
        mode is
            IntegrationConnectionMode.GitHubApp
            or IntegrationConnectionMode.GitHubEnterpriseApp;

    private HttpClient CreateBaseClient(
        IntegrationConnection connection)
    {
        var baseUrl = string.IsNullOrWhiteSpace(connection.BaseUrl)
            ? "https://api.github.com/"
            : connection.BaseUrl;

        return new HttpClient(
            new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            })
        {
            BaseAddress = new Uri(
                baseUrl.EndsWith('/')
                    ? baseUrl
                    : baseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(60)
        }.Tap(client =>
        {
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(
                    "application/vnd.github+json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "development-intelligence-platform");
            client.DefaultRequestHeaders.Add(
                "X-GitHub-Api-Version",
                "2022-11-28");
        });
    }

    private async Task<IntegrationCredentialInput> GetCredentialAsync(
        IntegrationConnection connection,
        CancellationToken cancellationToken)
    {
        var json = await _credentials.GetAsync(
                       connection.CredentialReference,
                       cancellationToken)
                   ?? throw new InvalidOperationException(
                       "Credential secret is missing from the local secret store.");

        return JsonSerializer.Deserialize<IntegrationCredentialInput>(
                   json,
                   JsonOptions)
               ?? throw new InvalidOperationException(
                   "Credential secret could not be decoded.");
    }

    private async Task<string> GetInstallationTokenAsync(
        IntegrationConnection connection,
        IntegrationCredentialInput credential,
        CancellationToken cancellationToken)
    {
        if (_installationTokens.TryGetValue(
                connection.Id,
                out var cached)
            && cached.ExpiresAtUtc
            > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return cached.Token;
        }

        await _tokenGate.WaitAsync(cancellationToken);

        try
        {
            if (_installationTokens.TryGetValue(
                    connection.Id,
                    out cached)
                && cached.ExpiresAtUtc
                > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return cached.Token;
            }

            if (!credential.AppId.HasValue
                || !credential.InstallationId.HasValue
                || string.IsNullOrWhiteSpace(
                    credential.PrivateKeyBase64))
            {
                throw new InvalidOperationException(
                    "GitHub App credential is incomplete.");
            }

            var jwt = CreateAppJwt(
                credential.AppId.Value,
                credential.PrivateKeyBase64);

            using var client = CreateBaseClient(connection);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwt);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"app/installations/{credential.InstallationId.Value}/access_tokens")
            {
                Content = new StringContent(
                    "{}",
                    Encoding.UTF8,
                    "application/json")
            };

            using var response = await client.SendAsync(
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            using var document = await GitHubApiJson.ReadJsonAsync(
                response,
                cancellationToken);

            var token = GitHubApiJson.String(
                            document.RootElement,
                            "token")
                        ?? throw new InvalidOperationException(
                            "GitHub App access-token response did not contain token.");

            var expires = GitHubApiJson.Date(
                              document.RootElement,
                              "expires_at")
                          ?? DateTimeOffset.UtcNow.AddMinutes(50);

            _installationTokens[connection.Id] =
                new CachedToken(token, expires);

            return token;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private static string CreateAppJwt(
        long appId,
        string privateKeyBase64)
    {
        var now = DateTimeOffset.UtcNow;
        var header = GitHubApiJson.Base64Url(
            Encoding.UTF8.GetBytes(
                """{"alg":"RS256","typ":"JWT"}"""));

        var payload = GitHubApiJson.Base64Url(
            Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(
                    new
                    {
                        iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
                        exp = now.AddMinutes(9).ToUnixTimeSeconds(),
                        iss = appId
                    })));

        var unsigned = header + "." + payload;

        var pem = privateKeyBase64.Contains(
            "BEGIN",
            StringComparison.Ordinal)
            ? privateKeyBase64
            : Encoding.UTF8.GetString(
                Convert.FromBase64String(privateKeyBase64));

        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);

        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(unsigned),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return unsigned + "." + GitHubApiJson.Base64Url(signature);
    }

    private sealed record CachedToken(
        string Token,
        DateTimeOffset ExpiresAtUtc);
}

internal static class HttpClientEvidenceIntegrationExtensions
{
    internal static T Tap<T>(
        this T value,
        Action<T> configure)
    {
        configure(value);
        return value;
    }
}
