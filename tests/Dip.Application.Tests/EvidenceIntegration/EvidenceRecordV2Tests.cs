// Purpose: Verifies Evidence Record V 2 behavior, edge cases, and repository invariants.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.EvidenceIntegration;

public sealed class EvidenceRecordV2Tests
{
    [Fact]
    public void Legacy_constructor_remains_backward_compatible()
    {
        var item = new EvidenceRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "subject",
            EvidenceSourceType.GitHub,
            "legacy.kind",
            EvidenceOutcome.Succeeded,
            "title",
            "summary",
            EvidenceConfidence.High,
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        item.EventType.Should().Be(EvidenceEventType.Unknown);
        item.IntegrityStatus.Should().Be(EvidenceIntegrityStatus.LegacyUnsigned);
        item.PrivacyClass.Should().Be(EvidencePrivacyClass.Internal);
        item.SourceEventId.Should().BeNull();
    }

    [Fact]
    public void V2_factory_keeps_normalized_and_provider_payload_separate()
    {
        var item = EvidenceRecord.CreateV2(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "repo:owner/name",
            EvidenceSourceType.GitHub,
            "connection:abc:repository:1",
            "github:commit:123",
            EvidenceEventType.SourceCommit,
            "github.commit",
            EvidenceOutcome.Changed,
            "commit",
            "summary",
            EvidenceConfidence.High,
            "https://example.test",
            """{"branch":"main"}""",
            """{"providerSpecific":true}""",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            actorId: "octocat");

        item.MetadataValue("branch").Should().Be("main");
        item.PayloadValue("providerSpecific").Should().Be("true");
        item.ActorId.Should().Be("octocat");
        item.EventType.Should().Be(EvidenceEventType.SourceCommit);
    }
}
