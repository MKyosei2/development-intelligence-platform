// Purpose: Coordinates endpoint-agent policy, heartbeat, update, and cryptographic verification operations.
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Dip.Agent.Core;

public sealed record EnterpriseEnrollmentResponse(
    Guid Id,
    Guid OrganizationId,
    Guid ProjectId,
    string AgentId,
    string Version,
    int PolicyVersion,
    string Status);

public sealed record EnterpriseUpdateDecision(
    bool UpdateAvailable,
    string? Version,
    string? PackageUri,
    string? Sha256,
    string? Signature,
    string? MinimumServerVersion);

public sealed record SignedPolicyEnvelope(
    string AgentId,
    Guid ProjectId,
    int Version,
    string PolicyJson,
    string Sha256,
    string? SignatureBase64,
    string SignatureAlgorithm,
    DateTimeOffset IssuedAtUtc);

public sealed class EnterpriseAgentClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly AgentOptions _options;

    public EnterpriseAgentClient(AgentOptions options)
    {
        _options = AgentOptions.Validate(options);
        _http = new HttpClient
        {
            BaseAddress = _options.ApiBaseUrl,
            Timeout = TimeSpan.FromMinutes(2)
        };
        if (!string.IsNullOrWhiteSpace(_options.BearerToken))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken);
    }

    public async Task HeartbeatAsync(
        string version,
        int policyVersion,
        string healthJson,
        CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/v1/enterprise/agents/heartbeat",
            new { agentId = _options.AgentId, version, policyVersion, healthJson },
            ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<EnterpriseUpdateDecision?> GetUpdateAsync(string channel, CancellationToken ct) =>
        await _http.GetFromJsonAsync<EnterpriseUpdateDecision>(
            $"api/v1/enterprise/agents/{Uri.EscapeDataString(_options.AgentId)}/updates?channel={Uri.EscapeDataString(channel)}",
            ct);

    public async Task<SignedPolicyEnvelope?> GetPolicyAsync(CancellationToken ct) =>
        await _http.GetFromJsonAsync<SignedPolicyEnvelope>(
            $"api/v1/enterprise/agents/{Uri.EscapeDataString(_options.AgentId)}/policy",
            ct);

    public static bool VerifyPolicy(SignedPolicyEnvelope envelope, string publicKeyPem)
    {
        var bytes = Encoding.UTF8.GetBytes(envelope.PolicyJson);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!string.Equals(hash, envelope.Sha256, StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrWhiteSpace(envelope.SignatureBase64))
            return false;

        using var key = ECDsa.Create();
        key.ImportFromPem(publicKeyPem);
        return key.VerifyData(
            bytes,
            Convert.FromBase64String(envelope.SignatureBase64),
            HashAlgorithmName.SHA256);
    }

    public async Task<bool> VerifyAndStagePackageAsync(
        EnterpriseUpdateDecision decision,
        string downloadPath,
        CancellationToken ct)
    {
        if (!decision.UpdateAvailable ||
            string.IsNullOrWhiteSpace(decision.PackageUri) ||
            string.IsNullOrWhiteSpace(decision.Sha256))
            return false;

        using var response = await _http.GetAsync(
            decision.PackageUri,
            HttpCompletionOption.ResponseHeadersRead,
            ct);
        response.EnsureSuccessStatusCode();

        await using (var input = await response.Content.ReadAsStreamAsync(ct))
        await using (var output = new FileStream(
                         downloadPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         81920,
                         true))
            await input.CopyToAsync(output, ct);

        await using var verify = new FileStream(
            downloadPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(verify, ct)).ToLowerInvariant();
        return string.Equals(actual, decision.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public static (string PublicKeyPem, string PrivateKeyPem) CreateDeviceKeyPair()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return (key.ExportSubjectPublicKeyInfoPem(), key.ExportPkcs8PrivateKeyPem());
    }

    public static string Sign(string privateKeyPem, string payload)
    {
        using var key = ECDsa.Create();
        key.ImportFromPem(privateKeyPem);
        return Convert.ToBase64String(
            key.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256));
    }

    public void Dispose() => _http.Dispose();
}
