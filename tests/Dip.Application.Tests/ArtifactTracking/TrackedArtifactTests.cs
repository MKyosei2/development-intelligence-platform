// Purpose: Defines Tracked Artifact Tests responsibilities for this repository area.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.ArtifactTracking;

public sealed class TrackedArtifactTests
{
    [Fact]
    public void Version_numbers_are_monotonic_and_deletion_keeps_logical_identity()
    {
        var now = DateTimeOffset.UtcNow;
        var item = new TrackedArtifact(
            Guid.NewGuid(), Guid.NewGuid(), "CHARACTER.MA", "character.ma", ".ma", now);

        item.RecordContent("character.ma", "CHARACTER.MA", ".ma", new string('a', 64), 100, now, now)
            .Should().Be(1);
        item.RecordContent("hero.ma", "HERO.MA", ".ma", new string('b', 64), 110, now, now.AddMinutes(1))
            .Should().Be(2);
        item.RecordDeletion(now.AddMinutes(2)).Should().Be(3);

        item.DisplayPath.Should().Be("hero.ma");
        item.LifecycleState.Should().Be(TrackedArtifactLifecycleState.Deleted);
        item.CurrentSha256.Should().BeNull();
    }
}
