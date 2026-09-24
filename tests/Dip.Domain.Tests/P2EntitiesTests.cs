// Purpose: Verifies P2 En ti ti es behavior, edge cases, and invariants for this repository area.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Domain.Tests;

public sealed class P2EntitiesTests
{
    [Fact]
    public void CommunityHardware_RequiresExplicitConsent()
    {
        var act = () => new HardwareCommunitySample(
            Guid.NewGuid(), "Mobile Mid", "Android", new string('a', 64), "Android", "driver",
            1, 1, 1, 1, 1, 80, 4096, true, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CommunityHardware_DoesNotNeedRawIdentity()
    {
        var sample = new HardwareCommunitySample(
            Guid.NewGuid(), "Mobile Mid", "Android", new string('b', 64), "Android", "driver-family",
            10, 20, 30, 40, 50, 75, 3072, true, true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        sample.ConsentGranted.Should().BeTrue();
        sample.HardwareKeyHash.Should().HaveLength(64);
    }

    [Fact]
    public void P2Record_RejectsScoreOutsideZeroToHundred()
    {
        var act = () => new P2IntelligenceRecord(Guid.NewGuid(), Guid.NewGuid(), "project",
            P2IntelligenceKind.ScheduleRisk, P2IntelligenceStatus.Warning, 101,
            EvidenceConfidence.Medium, "summary", "{}", null, DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
