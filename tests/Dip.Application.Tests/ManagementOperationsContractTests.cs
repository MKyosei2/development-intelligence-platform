using Dip.Application.Features.Management;
using FluentAssertions;

namespace Dip.Application.Tests;

public sealed class ManagementOperationsContractTests
{
    [Fact]
    public void FormalApproval_EnablesSeparationOfDutiesByDefault()
    {
        var command = new CreateApprovalRequestCommand(
            Guid.NewGuid(), "release-go", "Release", "Release GO",
            new ManagementTarget("release", "1.0.0"), ["Tech Lead", "Producer"], "requester");

        command.AllowSameActorMultipleRoles.Should().BeFalse();
    }

    [Fact]
    public void TeamContract_CarriesExplicitCrossProjectResourcePool()
    {
        var command = new UpsertTeamCommand(
            Guid.NewGuid(), "graphics", "Graphics", 40m, SharedResourceKey: "graphics-core", SharedPoolCapacityHoursPerWeek: 80m);

        command.SharedResourceKey.Should().Be("graphics-core");
        command.SharedPoolCapacityHoursPerWeek.Should().Be(80m);
    }

    [Fact]
    public void AlertState_PreservesOriginalFirstSeenTime()
    {
        var first = DateTimeOffset.UtcNow.AddHours(-3);
        var state = new AlertStateDto("risk:r1", "Acknowledged", "actor", null, DateTimeOffset.UtcNow, null, 2, DateTimeOffset.UtcNow, first);

        state.FirstSeenAtUtc.Should().Be(first);
    }
}
