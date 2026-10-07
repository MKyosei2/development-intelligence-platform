// Purpose: Verifies evidence artifact hashes, trusted source identity, parent-chain integrity, and ECDSA/RSA signatures without allowing callers to self-promote integrity.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Evidence;

public sealed record EvidenceIntegrityVerificationRequest(
    Guid ProjectId,
    EvidenceSourceType SourceType,
    string SourceInstanceId,
    string SourceEventId,
    EvidenceEventType EventType,
    string SubjectKey,
    DateTimeOffset ObservedAtUtc,
    Guid? ArtifactRecordId = null,
    string? ExpectedContentHash = null,
    Guid? ParentEvidenceId = null,
    string? SigningKeyId = null,
    string? SignatureAlgorithm = null,
    string? SignatureBase64 = null,
    string? SignedPayloadBase64 = null);

public sealed record EvidenceIntegrityVerificationResult(
    EvidenceIntegrityStatus Status,
    bool ArtifactResolved,
    bool HashVerified,
    bool ParentVerified,
    bool SourceTrusted,
    bool SignatureVerified,
    string? SigningKeyId,
    string Reason,
    string? ResolvedArtifactSha256);

public sealed class EvidenceIntegrityVerifier
{
    private const string PayloadSchema = "dip.evidence-signature.v1";
    private readonly IArtifactRepository _artifacts;
    private readonly IEvidenceV2Repository _evidence;

    public EvidenceIntegrityVerifier(IArtifactRepository artifacts, IEvidenceV2Repository evidence)
    {
        _artifacts = artifacts;
        _evidence = evidence;
    }

    public async Task<EvidenceIntegrityVerificationResult> VerifyEvidenceAsync(
        EvidenceRecord evidence,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Guid? artifactId = Guid.TryParse(evidence.ArtifactId, out var parsedArtifact) ? parsedArtifact : null;
        var signature = Metadata(evidence.MetadataJson, "signatureBase64") ?? Metadata(evidence.MetadataJson, "signature");
        var keyId = Metadata(evidence.MetadataJson, "signingKeyId");
        var algorithm = Metadata(evidence.MetadataJson, "signatureAlgorithm");
        var signedPayload = Metadata(evidence.MetadataJson, "signedPayloadBase64");
        return await VerifyAsync(
            new EvidenceIntegrityVerificationRequest(
                evidence.ProjectId,
                evidence.SourceType,
                evidence.SourceInstanceId ?? string.Empty,
                evidence.SourceEventId ?? string.Empty,
                evidence.EventType,
                evidence.SubjectKey,
                evidence.ObservedAtUtc,
                artifactId,
                evidence.ContentHash,
                evidence.ParentEvidenceId,
                keyId,
                algorithm,
                signature,
                signedPayload),
            ct);
    }

    public async Task<EvidenceIntegrityVerificationResult> VerifyAsync(
        EvidenceIntegrityVerificationRequest request,
        CancellationToken ct)
    {
        ValidateRequest(request);
        var keyId = request.SigningKeyId?.Trim();
        var artifactResolved = false;
        var hashVerified = false;
        string? artifactSha = null;

        if (request.ArtifactRecordId is Guid artifactId)
        {
            var record = await _artifacts.GetRecordAsync(artifactId, ct);
            if (record is null)
                return Invalid("Referenced artifact record does not exist.");
            if (record.ProjectId != request.ProjectId)
                return Invalid("Referenced artifact belongs to another project.");
            var blob = await _artifacts.GetBlobAsync(record.ArtifactBlobId, ct);
            if (blob is null)
                return Invalid("Referenced artifact blob does not exist.");
            artifactResolved = true;
            artifactSha = blob.Sha256;
            if (!string.IsNullOrWhiteSpace(request.ExpectedContentHash))
            {
                if (!IsSha256(request.ExpectedContentHash))
                    return Invalid("Expected content hash is not a SHA-256 digest.", artifactResolved, false, artifactSha);
                if (!FixedHashEquals(request.ExpectedContentHash, blob.Sha256))
                    return Invalid("Evidence content hash does not match the durable artifact blob.", artifactResolved, false, artifactSha);
            }
            hashVerified = true;
        }
        else if (!string.IsNullOrWhiteSpace(request.ExpectedContentHash))
        {
            // A caller-supplied hash without a resolvable immutable artifact is not proof.
            if (!IsSha256(request.ExpectedContentHash))
                return Invalid("Expected content hash is not a SHA-256 digest.");
        }

        var parentVerified = false;
        if (request.ParentEvidenceId is Guid parentId)
        {
            var parent = await _evidence.GetAsync(request.ProjectId, parentId, ct);
            if (parent is null)
                return Invalid("Parent evidence does not exist in this project.", artifactResolved, hashVerified, artifactSha);
            if (parent.IntegrityStatus == EvidenceIntegrityStatus.Invalid)
                return Invalid("Parent evidence has invalid integrity.", artifactResolved, hashVerified, artifactSha);
            parentVerified = parent.IntegrityStatus == EvidenceIntegrityStatus.Verified;
        }

        var policies = LoadSourcePolicies();
        policies.TryGetValue(request.SourceInstanceId, out var policy);
        var sourceTrusted = policy is not null;
        if (policy is not null && !EnumAllowed(policy.SourceType, request.SourceType))
            return Invalid("Source policy does not permit this evidence source type.", artifactResolved, hashVerified, artifactSha, parentVerified, true);

        var signaturePresent = !string.IsNullOrWhiteSpace(request.SignatureBase64);
        var signatureVerified = false;
        if (signaturePresent)
        {
            if (string.IsNullOrWhiteSpace(keyId))
                return Invalid("Evidence signature is present but SigningKeyId is missing.", artifactResolved, hashVerified, artifactSha, parentVerified, sourceTrusted);
            if (policy is not null
                && !string.IsNullOrWhiteSpace(policy.KeyId)
                && !string.Equals(policy.KeyId, keyId, StringComparison.Ordinal))
                return Invalid("Evidence signing key does not match the trusted source policy.", artifactResolved, hashVerified, artifactSha, parentVerified, sourceTrusted);

            var keys = LoadTrustedKeys();
            if (!keys.TryGetValue(keyId, out var publicKeyPem))
                return Invalid("Evidence signing key is not trusted.", artifactResolved, hashVerified, artifactSha, parentVerified, sourceTrusted);

            byte[] signature;
            try { signature = Convert.FromBase64String(request.SignatureBase64!.Trim()); }
            catch (FormatException) { return Invalid("Evidence signature is not valid base64.", artifactResolved, hashVerified, artifactSha, parentVerified, sourceTrusted); }

            var canonical = BuildCanonicalPayload(request, artifactSha ?? request.ExpectedContentHash);
            var payload = canonical;
            if (!string.IsNullOrWhiteSpace(request.SignedPayloadBase64))
            {
                try { payload = Convert.FromBase64String(request.SignedPayloadBase64.Trim()); }
                catch (FormatException) { return Invalid("Signed evidence payload is not valid base64.", artifactResolved, hashVerified, artifactSha, parentVerified, sourceTrusted); }
                if (!SignedPayloadMatchesClaims(payload, request, artifactSha ?? request.ExpectedContentHash))
                    return Invalid("Signed evidence payload claims do not match the evidence record.", artifactResolved, hashVerified, artifactSha, parentVerified, sourceTrusted);
            }

            signatureVerified = VerifySignature(publicKeyPem, request.SignatureAlgorithm, payload, signature);
            if (!signatureVerified)
                return Invalid("Evidence signature verification failed.", artifactResolved, hashVerified, artifactSha, parentVerified, sourceTrusted);
        }

        if (policy?.RequireSignature == true && !signatureVerified)
            return new(
                EvidenceIntegrityStatus.Unverified,
                artifactResolved,
                hashVerified,
                parentVerified,
                sourceTrusted,
                false,
                keyId,
                "Trusted source policy requires a valid signature.",
                artifactSha);

        var unsignedArtifactAllowed = policy?.AllowUnsignedArtifact == true && artifactResolved && hashVerified;
        var chainTrusted = parentVerified;
        var fullyVerified = sourceTrusted
                            && (signatureVerified
                                || chainTrusted
                                || artifactResolved && hashVerified && unsignedArtifactAllowed);
        if (fullyVerified)
        {
            return new(
                EvidenceIntegrityStatus.Verified,
                artifactResolved,
                hashVerified,
                parentVerified,
                sourceTrusted,
                signatureVerified,
                keyId,
                signatureVerified
                    ? artifactResolved
                        ? "Artifact hash, source policy and cryptographic signature verified."
                        : "Trusted source policy and cryptographic signature verified for non-artifact evidence."
                    : parentVerified
                        ? "Trusted source policy and verified parent evidence chain established integrity."
                        : "Artifact hash and trusted unsigned-artifact source policy established integrity.",
                artifactSha);
        }

        return new(
            EvidenceIntegrityStatus.Unverified,
            artifactResolved,
            hashVerified,
            parentVerified,
            sourceTrusted,
            signatureVerified,
            keyId,
            !artifactResolved
                ? "No immutable artifact was resolved; caller-supplied metadata cannot self-verify integrity."
                : !sourceTrusted
                    ? "Artifact hash is valid but source identity is not covered by a trusted source policy."
                    : "Integrity evidence is incomplete.",
            artifactSha);

        EvidenceIntegrityVerificationResult Invalid(
            string reason,
            bool resolved = false,
            bool hash = false,
            string? sha = null,
            bool parent = false,
            bool trusted = false)
            => new(EvidenceIntegrityStatus.Invalid, resolved, hash, parent, trusted, false, keyId, reason, sha);
    }

    public static byte[] BuildCanonicalPayload(
        EvidenceIntegrityVerificationRequest request,
        string? artifactSha256)
    {
        var payload = new
        {
            schema = PayloadSchema,
            projectId = request.ProjectId.ToString("D"),
            sourceType = request.SourceType.ToString(),
            sourceInstanceId = request.SourceInstanceId.Trim(),
            sourceEventId = request.SourceEventId.Trim(),
            eventType = request.EventType.ToString(),
            subjectKey = request.SubjectKey.Trim(),
            observedAtUtc = request.ObservedAtUtc.ToUniversalTime().ToString("O"),
            artifactSha256 = artifactSha256?.Trim().ToLowerInvariant()
        };
        return JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static bool SignedPayloadMatchesClaims(
        byte[] payload,
        EvidenceIntegrityVerificationRequest request,
        string? artifactSha256)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            return Value(root, "schema") == PayloadSchema
                   && Value(root, "projectId") == request.ProjectId.ToString("D")
                   && Value(root, "sourceType") == request.SourceType.ToString()
                   && Value(root, "sourceInstanceId") == request.SourceInstanceId.Trim()
                   && Value(root, "sourceEventId") == request.SourceEventId.Trim()
                   && Value(root, "eventType") == request.EventType.ToString()
                   && Value(root, "subjectKey") == request.SubjectKey.Trim()
                   && Value(root, "observedAtUtc") == request.ObservedAtUtc.ToUniversalTime().ToString("O")
                   && string.Equals(
                       Value(root, "artifactSha256"),
                       artifactSha256?.Trim().ToLowerInvariant(),
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool VerifySignature(
        string publicKeyPem,
        string? algorithm,
        byte[] payload,
        byte[] signature)
    {
        var normalized = algorithm?.Trim().ToLowerInvariant();
        if (normalized is null or "ecdsa-sha256")
        {
            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(publicKeyPem);
                if (ecdsa.VerifyData(payload, signature, HashAlgorithmName.SHA256)) return true;
            }
            catch (Exception ex) when (normalized is null && (ex is CryptographicException or ArgumentException))
            {
            }
            catch (Exception ex) when (ex is CryptographicException or ArgumentException)
            {
                return false;
            }
        }
        if (normalized is null or "rsa-pss-sha256")
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(publicKeyPem);
                return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
            }
            catch (Exception ex) when (ex is CryptographicException or ArgumentException)
            {
                return false;
            }
        }
        return false;
    }

    private static IReadOnlyDictionary<string, string> LoadTrustedKeys()
    {
        var json = Environment.GetEnvironmentVariable("DIP_EVIDENCE_TRUSTED_KEYS_JSON");
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>(StringComparer.Ordinal);
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, string>(StringComparer.Ordinal);
            return document.RootElement.EnumerateObject()
                .Where(x => x.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.Value.GetString()))
                .ToDictionary(x => x.Name, x => x.Value.GetString()!, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static IReadOnlyDictionary<string, SourceTrustPolicy> LoadSourcePolicies()
    {
        var json = Environment.GetEnvironmentVariable("DIP_EVIDENCE_SOURCE_POLICY_JSON");
        if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, SourceTrustPolicy>(StringComparer.Ordinal);
        try
        {
            var value = JsonSerializer.Deserialize<Dictionary<string, SourceTrustPolicy>>(
                json,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
            return value is null
                ? new Dictionary<string, SourceTrustPolicy>(StringComparer.Ordinal)
                : new Dictionary<string, SourceTrustPolicy>(value, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, SourceTrustPolicy>(StringComparer.Ordinal);
        }
    }

    private static bool EnumAllowed(string? configured, EvidenceSourceType actual)
        => string.IsNullOrWhiteSpace(configured)
           || configured.Equals(actual.ToString(), StringComparison.OrdinalIgnoreCase);

    private static void ValidateRequest(EvidenceIntegrityVerificationRequest request)
    {
        if (request.ProjectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(request));
        if (!Enum.IsDefined(request.SourceType)) throw new ArgumentOutOfRangeException(nameof(request));
        if (!Enum.IsDefined(request.EventType)) throw new ArgumentOutOfRangeException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.SourceInstanceId)) throw new ArgumentException("SourceInstanceId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SourceEventId)) throw new ArgumentException("SourceEventId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.SubjectKey)) throw new ArgumentException("SubjectKey is required.", nameof(request));
    }

    private static string? Metadata(string? json, string name)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            return Value(document.RootElement, name);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Value(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;
        foreach (var property in root.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.GetRawText();
        return null;
    }

    private static bool IsSha256(string value)
        => value.Trim().Length == 64 && value.Trim().All(Uri.IsHexDigit);

    private static bool FixedHashEquals(string a, string b)
    {
        var left = Encoding.ASCII.GetBytes(a.Trim().ToLowerInvariant());
        var right = Encoding.ASCII.GetBytes(b.Trim().ToLowerInvariant());
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    public sealed record SourceTrustPolicy(
        string? KeyId = null,
        bool RequireSignature = true,
        bool AllowUnsignedArtifact = false,
        string? SourceType = null);
}
