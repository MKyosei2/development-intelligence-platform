// Purpose: Implements G it Hu bC on ne ct or infrastructure integration for g it hu b.
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

public sealed class GitHubConnector : IIntegrationConnector
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IIntegrationCredentialStore _credentials;
    private readonly Dictionary<Guid, CachedToken> _installationTokens = new();
    private readonly SemaphoreSlim _tokenGate = new(1, 1);

    public GitHubConnector(IIntegrationCredentialStore credentials)
        => _credentials = credentials;

    public string Provider => "GitHub";

    public bool Supports(IntegrationConnection connection)
        => connection.Provider.Equals(Provider, StringComparison.OrdinalIgnoreCase)
           && connection.Mode is
               IntegrationConnectionMode.GitHubPat or
               IntegrationConnectionMode.GitHubApp or
               IntegrationConnectionMode.GitHubEnterprisePat or
               IntegrationConnectionMode.GitHubEnterpriseApp;

    public async Task<ConnectorHealth> TestConnectionAsync(
        IntegrationConnection connection,
        CancellationToken ct)
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
            var credential = await GetCredentialAsync(connection, ct);
            using var client = await CreateAuthorizedClientAsync(connection, credential, ct);

            string? account = null;
            if (IsApp(connection.Mode))
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    "installation/repositories?per_page=1");
                using var response = await client.SendAsync(request, ct);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    return Unauthorized("GitHub App installation credentials were rejected.");

                response.EnsureSuccessStatusCode();
                using var document = await ReadJsonAsync(response, ct);
                if (document.RootElement.TryGetProperty("repositories", out var repositories) &&
                    repositories.ValueKind == JsonValueKind.Array &&
                    repositories.GetArrayLength() > 0)
                {
                    account = OwnerOf(repositories[0]);
                }
            }
            else
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, "user");
                using var response = await client.SendAsync(request, ct);
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    return Unauthorized("GitHub token was rejected.");

                response.EnsureSuccessStatusCode();
                using var document = await ReadJsonAsync(response, ct);
                account = String(document.RootElement, "login");
            }

            var capabilities = await GetCapabilitiesWithClientAsync(
                connection,
                client,
                ct);

            var degraded = capabilities.Any(item => !item.Supported);
            return new ConnectorHealth(
                degraded ? ConnectionHealthState.Degraded : ConnectionHealthState.Healthy,
                degraded
                    ? "GitHub authentication succeeded, but one or more repository capabilities are unavailable."
                    : "GitHub authentication and repository capability probe succeeded.",
                DateTimeOffset.UtcNow,
                account,
                capabilities);
        }
        catch (InvalidOperationException exception)
        {
            return new ConnectorHealth(
                ConnectionHealthState.Misconfigured,
                Safe(exception.Message),
                DateTimeOffset.UtcNow,
                null,
                Array.Empty<ConnectorCapability>());
        }
        catch (HttpRequestException exception)
        {
            return new ConnectorHealth(
                ConnectionHealthState.Unavailable,
                "GitHub request failed: " + Safe(exception.Message),
                DateTimeOffset.UtcNow,
                null,
                Array.Empty<ConnectorCapability>());
        }
        catch (CryptographicException exception)
        {
            return new ConnectorHealth(
                ConnectionHealthState.Misconfigured,
                "GitHub App key could not be used: " + Safe(exception.Message),
                DateTimeOffset.UtcNow,
                null,
                Array.Empty<ConnectorCapability>());
        }
    }

    public async Task<IReadOnlyList<ConnectorResourceDto>> DiscoverAsync(
        IntegrationConnection connection,
        ConnectorDiscoveryQuery query,
        CancellationToken ct)
    {
        if (!connection.Enabled)
            throw new InvalidOperationException("Connection is disabled.");

        var credential = await GetCredentialAsync(connection, ct);
        using var client = await CreateAuthorizedClientAsync(connection, credential, ct);
        return await DiscoverWithClientAsync(connection, client, query, ct);
    }

    public async Task<ConnectorSyncResult> SyncAsync(
        IntegrationConnection connection,
        ProjectSourceBinding binding,
        CancellationToken ct)
    {
        if (!binding.ResourceType.Equals("repository", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "GitHub Difficulty-1 connector currently syncs repository resources only.");

        var parts = binding.ExternalResourceName.Split('/', 2);
        if (parts.Length != 2 ||
            string.IsNullOrWhiteSpace(parts[0]) ||
            string.IsNullOrWhiteSpace(parts[1]))
            throw new InvalidOperationException(
                "GitHub binding ExternalResourceName must be owner/repository.");

        var credential = await GetCredentialAsync(connection, ct);
        using var client = await CreateAuthorizedClientAsync(connection, credential, ct);

        var branch = BindingBranch(binding.ConfigurationJson);
        if (string.IsNullOrWhiteSpace(branch))
        {
            using var repositoryResponse = await client.GetAsync(
                $"repos/{Escape(parts[0])}/{Escape(parts[1])}",
                ct);
            repositoryResponse.EnsureSuccessStatusCode();
            using var repositoryDocument = await ReadJsonAsync(repositoryResponse, ct);
            branch = String(repositoryDocument.RootElement, "default_branch") ?? "main";
        }

        using var response = await client.GetAsync(
            $"repos/{Escape(parts[0])}/{Escape(parts[1])}/commits/{Escape(branch)}",
            ct);
        response.EnsureSuccessStatusCode();
        using var document = await ReadJsonAsync(response, ct);
        var root = document.RootElement;

        var sha = String(root, "sha")
                  ?? throw new InvalidOperationException(
                      "GitHub commit response did not contain SHA.");

        var commit = root.TryGetProperty("commit", out var commitNode)
            ? commitNode
            : default;
        var message = commit.ValueKind == JsonValueKind.Object
            ? String(commit, "message")?.Split('\n')[0]
            : null;

        var observed = DateTimeOffset.UtcNow;
        if (commit.ValueKind == JsonValueKind.Object &&
            commit.TryGetProperty("committer", out var committer))
            observed = Date(committer, "date") ?? observed;

        var actor = root.TryGetProperty("author", out var author) &&
                    author.ValueKind == JsonValueKind.Object
            ? String(author, "login")
            : null;

        var url = String(root, "html_url");
        var metadata = JsonSerializer.Serialize(
            new
            {
                repository = binding.ExternalResourceName,
                repositoryId = binding.ExternalResourceId,
                branch,
                commitSha = sha,
                message,
                url
            },
            JsonOptions);

        var observation = new ConnectorSyncObservation(
            EvidenceSourceType.GitHub,
            $"connection:{connection.Id:N}:repository:{binding.ExternalResourceId}",
            $"github:commit:{sha}",
            EvidenceEventType.SourceCommit,
            $"repo:{binding.ExternalResourceName}",
            "github.commit",
            $"GitHub commit {Short(sha)}",
            string.IsNullOrWhiteSpace(message)
                ? $"Observed {binding.ExternalResourceName}@{Short(sha)}."
                : message,
            EvidenceOutcome.Changed,
            EvidenceConfidence.High,
            observed,
            url,
            actor,
            metadata);

        return new ConnectorSyncResult(
            Observed: 1,
            Observations: new[] { observation },
            Warnings: Array.Empty<string>());
    }

    public async Task<IReadOnlyList<ConnectorCapability>> GetCapabilitiesAsync(
        IntegrationConnection connection,
        CancellationToken ct)
    {
        var credential = await GetCredentialAsync(connection, ct);
        using var client = await CreateAuthorizedClientAsync(connection, credential, ct);
        return await GetCapabilitiesWithClientAsync(connection, client, ct);
    }

    private async Task<IReadOnlyList<ConnectorResourceDto>> DiscoverWithClientAsync(
        IntegrationConnection connection,
        HttpClient client,
        ConnectorDiscoveryQuery query,
        CancellationToken ct)
    {
        var limit = Math.Clamp(query.Limit, 1, 500);
        var output = new List<ConnectorResourceDto>();
        var maxPages = Math.Max(1, (int)Math.Ceiling(limit / 100d));

        for (var page = 1; page <= maxPages && output.Count < limit; page++)
        {
            var path = IsApp(connection.Mode)
                ? $"installation/repositories?per_page=100&page={page}"
                : $"user/repos?per_page=100&page={page}&sort=updated&affiliation=owner,collaborator,organization_member";

            using var response = await client.GetAsync(path, ct);
            response.EnsureSuccessStatusCode();
            using var document = await ReadJsonAsync(response, ct);
            var array = IsApp(connection.Mode)
                ? document.RootElement.GetProperty("repositories")
                : document.RootElement;

            var pageCount = 0;
            foreach (var item in array.EnumerateArray())
            {
                pageCount++;
                var resource = MapRepository(item);
                if (!Matches(resource, query))
                    continue;
                output.Add(resource);
                if (output.Count >= limit)
                    break;
            }

            if (pageCount < 100)
                break;
        }

        return output
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(item => item.FullName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<IReadOnlyList<ConnectorCapability>> GetCapabilitiesWithClientAsync(
        IntegrationConnection connection,
        HttpClient client,
        CancellationToken ct)
    {
        IReadOnlyList<ConnectorResourceDto> resources;
        try
        {
            resources = await DiscoverWithClientAsync(
                connection,
                client,
                new ConnectorDiscoveryQuery(Limit: 1),
                ct);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException)
        {
            return new[]
            {
                new ConnectorCapability("repository.metadata", false, Safe(exception.Message)),
                new ConnectorCapability("repository.contents", false, "Repository discovery failed."),
                new ConnectorCapability("repository.pull_requests", false, "Repository discovery failed."),
                new ConnectorCapability("repository.actions", false, "Repository discovery failed."),
                new ConnectorCapability("repository.workspace_sync", false, "Repository discovery failed.")
            };
        }

        if (resources.Count == 0)
        {
            return new[]
            {
                new ConnectorCapability("repository.metadata", true, "Authentication succeeded; no repository is visible."),
                new ConnectorCapability("repository.contents", false, "No repository is available for capability probing."),
                new ConnectorCapability("repository.pull_requests", false, "No repository is available for capability probing."),
                new ConnectorCapability("repository.actions", false, "No repository is available for capability probing."),
                new ConnectorCapability("repository.workspace_sync", false, "No repository is available for capability probing.")
            };
        }

        var resource = resources[0];
        var repoPath = $"repos/{Escape(resource.FullName.Split('/')[0])}/{Escape(resource.Name)}";
        var metadata = true;
        var contents = await IsSuccessAsync(
            client,
            $"{repoPath}/commits/{Escape(resource.DefaultBranch ?? "main")}",
            ct);
        var pulls = await IsSuccessAsync(client, $"{repoPath}/pulls?per_page=1", ct);
        var actions = await IsSuccessAsync(client, $"{repoPath}/actions/runs?per_page=1", ct);

        return new[]
        {
            new ConnectorCapability("repository.metadata", metadata),
            new ConnectorCapability("repository.contents", contents,
                contents ? null : "Latest commit endpoint is not readable."),
            new ConnectorCapability("repository.pull_requests", pulls,
                pulls ? null : "Pull-request endpoint is not readable."),
            new ConnectorCapability("repository.actions", actions,
                actions ? null : "Actions endpoint is not readable."),
            new ConnectorCapability("repository.workspace_sync", contents,
                contents ? null : "Workspace sync requires repository content access.")
        };
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(
        IntegrationConnection connection,
        IntegrationCredentialInput credential,
        CancellationToken ct)
    {
        var client = CreateBaseClient(connection);
        try
        {
            var token = IsApp(connection.Mode)
                ? await GetInstallationTokenAsync(connection, credential, ct)
                : credential.Token;

            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("GitHub credential did not contain a usable token.");

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

    private HttpClient CreateBaseClient(IntegrationConnection connection)
    {
        var baseUrl = string.IsNullOrWhiteSpace(connection.BaseUrl)
            ? "https://api.github.com/"
            : connection.BaseUrl;

        return new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        })
        {
            BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl
                : baseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(60)
        }.Tap(client =>
        {
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "development-intelligence-platform");
            client.DefaultRequestHeaders.Add(
                "X-GitHub-Api-Version",
                "2022-11-28");
        });
    }

    private async Task<IntegrationCredentialInput> GetCredentialAsync(
        IntegrationConnection connection,
        CancellationToken ct)
    {
        var json = await _credentials.GetAsync(connection.CredentialReference, ct)
                   ?? throw new InvalidOperationException(
                       "Credential secret is missing from the local secret store.");

        return JsonSerializer.Deserialize<IntegrationCredentialInput>(json, JsonOptions)
               ?? throw new InvalidOperationException(
                   "Credential secret could not be decoded.");
    }

    private async Task<string> GetInstallationTokenAsync(
        IntegrationConnection connection,
        IntegrationCredentialInput credential,
        CancellationToken ct)
    {
        if (_installationTokens.TryGetValue(connection.Id, out var cached) &&
            cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
            return cached.Token;

        await _tokenGate.WaitAsync(ct);
        try
        {
            if (_installationTokens.TryGetValue(connection.Id, out cached) &&
                cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(2))
                return cached.Token;

            if (!credential.AppId.HasValue ||
                !credential.InstallationId.HasValue ||
                string.IsNullOrWhiteSpace(credential.PrivateKeyBase64))
                throw new InvalidOperationException(
                    "GitHub App credential is incomplete.");

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
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            using var response = await client.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            using var document = await ReadJsonAsync(response, ct);

            var token = String(document.RootElement, "token")
                        ?? throw new InvalidOperationException(
                            "GitHub App access-token response did not contain token.");
            var expires = Date(document.RootElement, "expires_at")
                          ?? DateTimeOffset.UtcNow.AddMinutes(50);

            _installationTokens[connection.Id] = new CachedToken(token, expires);
            return token;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private static string CreateAppJwt(long appId, string privateKeyBase64)
    {
        var now = DateTimeOffset.UtcNow;
        var header = Base64Url(Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}"""));
        var payload = Base64Url(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new
            {
                iat = now.AddSeconds(-60).ToUnixTimeSeconds(),
                exp = now.AddMinutes(9).ToUnixTimeSeconds(),
                iss = appId
            })));
        var unsigned = header + "." + payload;

        string pem;
        if (privateKeyBase64.Contains("BEGIN", StringComparison.Ordinal))
            pem = privateKeyBase64;
        else
            pem = Encoding.UTF8.GetString(
                Convert.FromBase64String(privateKeyBase64));

        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(unsigned),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return unsigned + "." + Base64Url(signature);
    }

    private static ConnectorResourceDto MapRepository(JsonElement item)
    {
        var id = item.TryGetProperty("id", out var idNode)
            ? idNode.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture)
            : String(item, "node_id") ?? Guid.NewGuid().ToString("N");
        var name = String(item, "name") ?? "repository";
        var fullName = String(item, "full_name") ?? name;

        return new ConnectorResourceDto(
            id,
            "repository",
            name,
            fullName,
            String(item, "html_url"),
            String(item, "default_branch"),
            Bool(item, "private"),
            Bool(item, "archived"),
            item.TryGetProperty("language", out var language) && language.ValueKind == JsonValueKind.String
                ? language.GetString()
                : null,
            String(item, "description"),
            Date(item, "updated_at"));
    }

    private static bool Matches(
        ConnectorResourceDto item,
        ConnectorDiscoveryQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            if (!item.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !(item.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) &&
                !(item.PrimaryLanguage?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                return false;
        }

        var visibility = query.Visibility?.Trim().ToLowerInvariant();
        return visibility switch
        {
            "private" => item.Private,
            "public" => !item.Private,
            _ => true
        };
    }

    private static string? BindingBranch(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return null;
        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            return document.RootElement.TryGetProperty("defaultBranch", out var branch) &&
                   branch.ValueKind == JsonValueKind.String
                ? branch.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<bool> IsSuccessAsync(
        HttpClient client,
        string path,
        CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync(path, ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    private static async Task<JsonDocument> ReadJsonAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    private static string? OwnerOf(JsonElement repository)
    {
        if (!repository.TryGetProperty("owner", out var owner) ||
            owner.ValueKind != JsonValueKind.Object)
            return null;
        return String(owner, "login");
    }

    private static string? String(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool Bool(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(name, out var value) &&
           value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
           value.GetBoolean();

    private static DateTimeOffset? Date(JsonElement element, string name)
    {
        var value = String(element, name);
        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);
    private static string Short(string sha) => sha[..Math.Min(12, sha.Length)];
    private static bool IsApp(IntegrationConnectionMode mode)
        => mode is IntegrationConnectionMode.GitHubApp
            or IntegrationConnectionMode.GitHubEnterpriseApp;

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string Safe(string value)
    {
        var normalized = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
        return normalized[..Math.Min(1000, normalized.Length)];
    }

    private ConnectorHealth Unauthorized(string message)
        => new(
            ConnectionHealthState.Unauthorized,
            message,
            DateTimeOffset.UtcNow,
            null,
            Array.Empty<ConnectorCapability>());

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAtUtc);
}

internal static class HttpClientEvidenceIntegrationExtensions
{
    public static T Tap<T>(this T value, Action<T> configure)
    {
        configure(value);
        return value;
    }
}
