// Purpose: Locks canonical evidence-signature claims and the fail-closed promotion boundary used by ingestion/release evidence flows.
using System.Text.Json;
using Dip.Application.Features.Evidence;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.EvidenceIntegration;

public sealed class EvidenceIntegrityVerifierTests
{
    [Fact]
    public void BuildCanonicalPayload_NormalizesArtifactHashAndUtcTime()
    {
        var request = Request();
        var payload = EvidenceIntegrityVerifier.BuildCanonicalPayload(request, new string('A', 64));
        using var json = JsonDocument.Parse(payload);

        json.RootElement.GetProperty("schema").GetString().Should().Be(EvidenceIntegrityVerifier.PayloadSchema);
        json.RootElement.GetProperty("projectId").GetString().Should().Be(request.ProjectId.ToString("D"));
        json.RootElement.GetProperty("artifactSha256").GetString().Should().Be(new string('a', 64));
        json.RootElement.GetProperty("observedAtUtc").GetString().Should().Be(request.ObservedAtUtc.ToUniversalTime().ToString("O"));
    }

    [Fact]
    public void CanPromoteToVerified_RejectsCallerMetadataWithoutTrustBoundary()
    {
        var result = new EvidenceIntegrityVerificationResult(
            EvidenceIntegrityStatus.Unverified,
            ArtifactResolved: false,
            HashVerified: false,
            ParentVerified: false,
            SourceTrusted: false,
            SignatureVerified: false,
            SigningKeyId: null,
            Reason: "caller metadata only",
            ResolvedArtifactSha256: null);

        EvidenceIntegrityVerifier.CanPromoteToVerified(result).Should().BeFalse();
    }

    [Fact]
    public void CanPromoteToVerified_AcceptsTrustedSignedEvidence()
    {
        var result = new EvidenceIntegrityVerificationResult(
            EvidenceIntegrityStatus.Verified,
            ArtifactResolved: false,
            HashVerified: false,
            ParentVerified: false,
            SourceTrusted: true,
            SignatureVerified: true,
            SigningKeyId: "test-key",
            Reason: "verified signature",
            ResolvedArtifactSha256: null);

        EvidenceIntegrityVerifier.CanPromoteToVerified(result).Should().BeTrue();
    }

    private static EvidenceIntegrityVerificationRequest Request()
        => new(
            Guid.NewGuid(),
            EvidenceSourceType.Unreal,
            "unreal-ci",
            "event-42",
            EvidenceEventType.UnrealAutomationTests,
            "game/client",
            new DateTimeOffset(2026, 10, 7, 12, 34, 56, TimeSpan.FromHours(9)));
}
