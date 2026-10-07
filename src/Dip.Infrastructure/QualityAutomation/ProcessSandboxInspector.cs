// Purpose: Verifies signed external sandbox attestations and independently checks local filesystem/network isolation claims.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dip.Infrastructure.QualityAutomation;

public sealed record SandboxIsolationAttestation(
    string Schema,
    string AttestationId,
    string Backend,
    string RunnerId,
    string WorkspaceIdentitySha256,
    bool Ephemeral,
    bool FilesystemIsolated,
    bool NetworkIsolated,
    bool CpuLimitApplied,
    bool MemoryLimitApplied,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Nonce,
    string SignatureBase64);

public sealed record SandboxAttestationVerification(
    bool Verified,
    string Reason,
    string? AttestationId,
    string? Backend,
    bool FilesystemIsolated,
    bool NetworkIsolated,
    bool Ephemeral);

internal static class ProcessSandboxInspector
{
    private const string AttestationSchema = "dip.qa.sandbox-attestation.v1";
    private static readonly TimeSpan MaximumClockSkew = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaximumAttestationLifetime = TimeSpan.FromHours(2);

    internal static bool FilesystemIsolationVerified(string? workspace = null)
    {
        var signed = TryReadSignedAttestation(workspace);
        if (signed is { Verified: true })
            return signed.FilesystemIsolated && signed.Ephemeral;

        if (SignedAttestationRequired())
            return false;

        return string.Equals(
            Environment.GetEnvironmentVariable("DIP_QA_FILESYSTEM_ISOLATED"),
            "1",
            StringComparison.Ordinal);
    }

    internal static bool NetworkIsolationVerified(string? workspace = null)
    {
        var signed = TryReadSignedAttestation(workspace);
        if (signed is { Verified: true })
            return signed.NetworkIsolated && signed.Ephemeral;

        if (SignedAttestationRequired())
            return false;

        if (!string.Equals(
                Environment.GetEnvironmentVariable("DIP_QA_NETWORK_ISOLATED"),
                "1",
                StringComparison.Ordinal))
            return false;

        // On Linux the assertion is accepted only when the current namespace has no
        // non-loopback route. A plain environment variable inside a normally networked
        // worker is therefore insufficient to claim isolation.
        if (OperatingSystem.IsLinux())
            return LinuxHasOnlyLoopbackRoutes();

        // Windows/macOS isolation is supplied by the external sandbox/runner. When
        // signed attestations are mandatory this compatibility path is disabled.
        return true;
    }

    internal static SandboxAttestationVerification VerifyAttestation(
        SandboxIsolationAttestation? attestation,
        string workspace,
        string? sharedKeyBase64 = null,
        DateTimeOffset? now = null)
    {
        if (attestation is null)
            return Invalid("Sandbox attestation is missing.");
        if (!string.Equals(attestation.Schema, AttestationSchema, StringComparison.Ordinal))
            return Invalid($"Unsupported sandbox attestation schema '{attestation.Schema}'.", attestation);
        if (string.IsNullOrWhiteSpace(attestation.AttestationId)
            || string.IsNullOrWhiteSpace(attestation.Backend)
            || string.IsNullOrWhiteSpace(attestation.RunnerId)
            || string.IsNullOrWhiteSpace(attestation.Nonce))
            return Invalid("Sandbox attestation identity fields are incomplete.", attestation);

        var observedAt = now ?? DateTimeOffset.UtcNow;
        if (attestation.IssuedAtUtc > observedAt + MaximumClockSkew)
            return Invalid("Sandbox attestation was issued in the future.", attestation);
        if (attestation.ExpiresAtUtc <= observedAt - MaximumClockSkew)
            return Invalid("Sandbox attestation is expired.", attestation);
        if (attestation.ExpiresAtUtc <= attestation.IssuedAtUtc
            || attestation.ExpiresAtUtc - attestation.IssuedAtUtc > MaximumAttestationLifetime)
            return Invalid("Sandbox attestation lifetime is invalid.", attestation);

        var expectedWorkspace = WorkspaceIdentity(workspace);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(expectedWorkspace),
                Encoding.ASCII.GetBytes(attestation.WorkspaceIdentitySha256 ?? string.Empty)))
            return Invalid("Sandbox attestation is bound to a different workspace.", attestation);

        if (!attestation.Ephemeral)
            return Invalid("Sandbox runner did not attest an ephemeral execution environment.", attestation);
        if (!attestation.FilesystemIsolated)
            return Invalid("Sandbox runner did not attest filesystem isolation.", attestation);
        if (!attestation.NetworkIsolated)
            return Invalid("Sandbox runner did not attest network isolation.", attestation);

        var keyText = sharedKeyBase64
            ?? Environment.GetEnvironmentVariable("DIP_QA_SANDBOX_ATTESTATION_KEY_BASE64");
        if (string.IsNullOrWhiteSpace(keyText))
            return Invalid("Sandbox attestation key is not configured.", attestation);

        byte[] key;
        byte[] signature;
        try
        {
            key = Convert.FromBase64String(keyText.Trim());
            signature = Convert.FromBase64String(attestation.SignatureBase64 ?? string.Empty);
        }
        catch (FormatException)
        {
            return Invalid("Sandbox attestation key or signature is not valid base64.", attestation);
        }
        if (key.Length < 32)
            return Invalid("Sandbox attestation key must contain at least 256 bits.", attestation);

        var payload = CanonicalAttestationPayload(attestation);
        var expected = HMACSHA256.HashData(key, payload);
        if (!CryptographicOperations.FixedTimeEquals(expected, signature))
            return Invalid("Sandbox attestation signature verification failed.", attestation);

        return new(
            true,
            "Signed sandbox attestation verified.",
            attestation.AttestationId,
            attestation.Backend,
            attestation.FilesystemIsolated,
            attestation.NetworkIsolated,
            attestation.Ephemeral);
    }

    internal static SandboxIsolationAttestation SignAttestation(
        string backend,
        string runnerId,
        string workspace,
        bool filesystemIsolated,
        bool networkIsolated,
        bool cpuLimitApplied,
        bool memoryLimitApplied,
        string sharedKeyBase64,
        DateTimeOffset? issuedAt = null,
        TimeSpan? lifetime = null)
    {
        var key = Convert.FromBase64String(sharedKeyBase64);
        if (key.Length < 32)
            throw new InvalidOperationException("Sandbox attestation key must contain at least 256 bits.");
        var now = issuedAt ?? DateTimeOffset.UtcNow;
        var unsigned = new SandboxIsolationAttestation(
            AttestationSchema,
            Guid.NewGuid().ToString("N"),
            Required(backend, nameof(backend)),
            Required(runnerId, nameof(runnerId)),
            WorkspaceIdentity(workspace),
            true,
            filesystemIsolated,
            networkIsolated,
            cpuLimitApplied,
            memoryLimitApplied,
            now,
            now + (lifetime ?? TimeSpan.FromMinutes(30)),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            string.Empty);
        var signature = HMACSHA256.HashData(key, CanonicalAttestationPayload(unsigned));
        return unsigned with { SignatureBase64 = Convert.ToBase64String(signature) };
    }

    internal static string WorkspaceIdentity(string workspace)
    {
        var canonical = ProcessCommandValidator.CanonicalDirectory(workspace)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (OperatingSystem.IsWindows())
            canonical = canonical.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    internal static bool LinuxHasOnlyLoopbackRoutes()
    {
        try
        {
            var observedRouteState = false;
            if (File.Exists("/proc/net/route"))
            {
                observedRouteState = true;
                foreach (var line in File.ReadLines("/proc/net/route").Skip(1))
                {
                    var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length > 0 && !columns[0].Equals("lo", StringComparison.Ordinal))
                        return false;
                }
            }

            if (File.Exists("/proc/net/ipv6_route"))
            {
                observedRouteState = true;
                foreach (var line in File.ReadLines("/proc/net/ipv6_route"))
                {
                    var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (columns.Length > 0
                        && !columns[^1].Equals("lo", StringComparison.Ordinal))
                        return false;
                }
            }
            return observedRouteState;
        }
        catch
        {
            // An unverifiable namespace is not an isolated namespace.
            return false;
        }
    }

    internal static string ClassifySandboxDenial(string message)
        => message.Contains("attestation", StringComparison.OrdinalIgnoreCase)
            ? "sandbox-attestation"
            : message.Contains("network isolation", StringComparison.OrdinalIgnoreCase)
                ? "network-isolation"
                : message.Contains("filesystem isolation", StringComparison.OrdinalIgnoreCase)
                    ? "filesystem-isolation"
                    : message.Contains("workspace", StringComparison.OrdinalIgnoreCase)
                        ? "workspace-boundary"
                        : message.Contains("allowlist", StringComparison.OrdinalIgnoreCase)
                          || message.Contains("trusted PATH", StringComparison.OrdinalIgnoreCase)
                            ? "executable-policy"
                            : "sandbox-policy";

    private static SandboxAttestationVerification? TryReadSignedAttestation(string? workspace)
    {
        if (string.IsNullOrWhiteSpace(workspace))
            return null;
        var path = Environment.GetEnvironmentVariable("DIP_QA_SANDBOX_ATTESTATION_FILE");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        try
        {
            var json = File.ReadAllText(path);
            var attestation = JsonSerializer.Deserialize<SandboxIsolationAttestation>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
            return VerifyAttestation(attestation, workspace);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or CryptographicException)
        {
            return new(false, $"Sandbox attestation could not be verified: {ex.Message}", null, null, false, false, false);
        }
    }

    private static byte[] CanonicalAttestationPayload(SandboxIsolationAttestation value)
    {
        var payload = string.Join("\n", new[]
        {
            value.Schema,
            value.AttestationId,
            value.Backend,
            value.RunnerId,
            value.WorkspaceIdentitySha256,
            value.Ephemeral ? "1" : "0",
            value.FilesystemIsolated ? "1" : "0",
            value.NetworkIsolated ? "1" : "0",
            value.CpuLimitApplied ? "1" : "0",
            value.MemoryLimitApplied ? "1" : "0",
            value.IssuedAtUtc.ToUniversalTime().ToString("O"),
            value.ExpiresAtUtc.ToUniversalTime().ToString("O"),
            value.Nonce
        });
        return Encoding.UTF8.GetBytes(payload);
    }

    private static SandboxAttestationVerification Invalid(
        string reason,
        SandboxIsolationAttestation? value = null)
        => new(false, reason, value?.AttestationId, value?.Backend, false, false, false);

    internal static bool SignedAttestationRequired()
        => string.Equals(
            Environment.GetEnvironmentVariable("DIP_QA_REQUIRE_SIGNED_ATTESTATION"),
            "1",
            StringComparison.Ordinal);

    private static string Required(string value, string name)
        => !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new ArgumentException($"{name} is required.", name);
}
