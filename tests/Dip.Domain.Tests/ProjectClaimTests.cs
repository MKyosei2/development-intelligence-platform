// Purpose: Verifies expected behavior and invariants for the surrounding feature.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Xunit;

namespace Dip.Domain.Tests;

public sealed class ProjectClaimTests
{
    [Fact]
    public void ApplyVerification_ChangesOnlyClaimVerificationState()
    {
        var claim = new ProjectClaim(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "project",
            "Complete",
            100m,
            "developer",
            null,
            DateTimeOffset.UtcNow);

        claim.ApplyVerification(
            ClaimVerificationStatus.Contradicted,
            DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal(ClaimVerificationStatus.Contradicted, claim.VerificationStatus);
        Assert.Equal("developer", claim.ClaimedBy);
        Assert.Equal("Complete", claim.ClaimedStatus);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_RejectsProgressOutsideRange(decimal progress)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ProjectClaim(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "project",
            "Working",
            progress,
            "developer",
            null,
            DateTimeOffset.UtcNow));
    }
}
