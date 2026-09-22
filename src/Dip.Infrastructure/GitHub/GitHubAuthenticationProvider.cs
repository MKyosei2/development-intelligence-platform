// Purpose: Owns GitHub HTTP transport, authentication mode, GitHub App JWT exchange, and shared installation-token caching.
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Features.GitHubIntegration;
using Dip.Infrastructure.QualityAutomation;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.GitHub;

internal sealed class GitHubAuthenticationProvider : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly GitHubOptions _options;
    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private string? _installationToken;
    private DateTimeOffset _installationTokenExpiresAtUtc;

    internal GitHubAuthenticationProvider(GitHubOptions options)
    {
        _options = options;
        _httpClient = new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            AllowAutoRedirect = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        {
            BaseAddress = new Uri(GitHubJsonMapper.EnsureTrailingSlash(
                string.IsNullOrWhiteSpace(_options.ApiBaseUrl) ? "https://api.github.com/" : _options.ApiBaseUrl)),
            Timeout = TimeSpan.FromMinutes(10)
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            string.IsNullOrWhiteSpace(_options.ProductHeader) ? "development-intelligence-platform" : _options.ProductHeader);
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    internal HttpClient HttpClient => _httpClient;
    internal GitHubOptions Options => _options;

    public async Task<GitHubConnectionStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var mode = ResolveMode();
        if (mode == "NotConfigured")
        {
            return new GitHubConnectionStatusDto(
                false,
                false,
                mode,
                null,
                null,
                GitHubJsonMapper.EmptyToNull(_options.AppSlug),
                BuildInstallUrl(),
                "Configure a GitHub App installation or a read-only GitHub token to browse repositories.");
        }

        try
        {
            if (mode == "GitHubApp")
            {
                using var request = await CreateRequestAsync(
                    HttpMethod.Get,
                    "installation/repositories?per_page=1",
                    cancellationToken);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
                var account = GitHubJsonMapper.TryFirstOwner(document.RootElement);
                return new GitHubConnectionStatusDto(
                    true,
                    true,
                    mode,
                    _options.InstallationId,
                    account,
                    GitHubJsonMapper.EmptyToNull(_options.AppSlug),
                    BuildInstallUrl(),
                    "GitHub App installation is connected. Repository access follows the installation selection.");
            }

            using var userRequest = await CreateRequestAsync(
                HttpMethod.Get,
                "user",
                cancellationToken);
            using var userResponse = await _httpClient.SendAsync(userRequest, cancellationToken);
            userResponse.EnsureSuccessStatusCode();
            using var userDocument = await GitHubJsonMapper.ReadJsonAsync(userResponse, cancellationToken);
            var login = GitHubJsonMapper.GetString(userDocument.RootElement, "login");
            return new GitHubConnectionStatusDto(
                true,
                true,
                mode,
                null,
                login,
                GitHubJsonMapper.EmptyToNull(_options.AppSlug),
                BuildInstallUrl(),
                "GitHub token connection is active. Prefer GitHub App mode for portfolio and production deployments.");
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or CryptographicException)
        {
            return new GitHubConnectionStatusDto(
                true,
                false,
                mode,
                _options.InstallationId > 0 ? _options.InstallationId : null,
                null,
                GitHubJsonMapper.EmptyToNull(_options.AppSlug),
                BuildInstallUrl(),
                "GitHub connection failed: " + GitHubJsonMapper.SafeMessage(exception));
        }
    }

    internal async Task<HttpRequestMessage> CreateRequestAsync(
            HttpMethod method,
            string relativePath,
            CancellationToken cancellationToken)
    {
        var token = await GetAccessTokenAsync(cancellationToken);
        var request = new HttpRequestMessage(method, relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!_options.HasGitHubAppCredentials)
        {
            if (string.IsNullOrWhiteSpace(_options.Token))
                throw new InvalidOperationException("GitHub repository access is not configured.");
            return _options.Token.Trim();
        }

        if (!string.IsNullOrWhiteSpace(_installationToken)
            && _installationTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
        {
            return _installationToken;
        }

        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_installationToken)
                && _installationTokenExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return _installationToken;
            }

            var jwt = CreateAppJwt();
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"app/installations/{_options.InstallationId}/access_tokens");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            using var document = await GitHubJsonMapper.ReadJsonAsync(response, cancellationToken);
            _installationToken = GitHubJsonMapper.GetString(document.RootElement, "token")
                ?? throw new InvalidOperationException("GitHub App token response did not contain token.");
            _installationTokenExpiresAtUtc = GitHubJsonMapper.GetDate(document.RootElement, "expires_at")
                ?? DateTimeOffset.UtcNow.AddMinutes(50);
            return _installationToken;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private string CreateAppJwt()
    {
        var now = DateTimeOffset.UtcNow;
        var header = GitHubJsonMapper.Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"RS256\",\"typ\":\"JWT\"}"));
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
            exp = now.AddMinutes(9).ToUnixTimeSeconds(),
            iss = _options.AppId
        });
        var payloadPart = GitHubJsonMapper.Base64Url(payload);
        var signingInput = Encoding.ASCII.GetBytes(header + "." + payloadPart);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(LoadPrivateKeyPem());
        var signature = rsa.SignData(
            signingInput,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return header + "." + payloadPart + "." + GitHubJsonMapper.Base64Url(signature);
    }

    private string LoadPrivateKeyPem()
    {
        if (!string.IsNullOrWhiteSpace(_options.AppPrivateKeyBase64))
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(_options.AppPrivateKeyBase64.Trim()));
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("GitHub:AppPrivateKeyBase64 is not valid Base64.", exception);
            }
        }

        if (string.IsNullOrWhiteSpace(_options.AppPrivateKeyPath))
            throw new InvalidOperationException("GitHub App private key is not configured.");
        var fullPath = Path.GetFullPath(_options.AppPrivateKeyPath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("GitHub App private key file was not found.", fullPath);
        return File.ReadAllText(fullPath);
    }

    internal void EnsureConfigured()
    {
        if (!_options.HasGitHubAppCredentials && string.IsNullOrWhiteSpace(_options.Token))
            throw new InvalidOperationException("GitHub repository access is not configured.");
    }

    private string ResolveMode()
    {
        if (_options.HasGitHubAppCredentials)
            return "GitHubApp";
        if (!string.IsNullOrWhiteSpace(_options.Token))
            return "Token";
        return "NotConfigured";
    }

    private string? BuildInstallUrl() => string.IsNullOrWhiteSpace(_options.AppSlug)
            ? null
            : $"https://github.com/apps/{Uri.EscapeDataString(_options.AppSlug.Trim())}/installations/new";

    public void Dispose()
    {
        _httpClient.Dispose();
        _tokenGate.Dispose();
    }
}
