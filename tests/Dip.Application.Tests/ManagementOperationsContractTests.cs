using Dip.Application.Features.Management;
using FluentAssertions;

namespace Dip.Application.Tests;

public sealed class ManagementOperationsContractTests
{
    [Fact]
    public void FormalApproval_EnablesSeparationOfDutiesByDefault()
    {
        var command = new CreateApprovalRequestCommand(Guid.NewGuid(), "release-go", "Release", "Release GO",new ManagementTarget("release", "1.0.0"), ["Tech Lead", "Producer"], "requester");
        command.AllowSameActorMultipleRoles.Should().BeFalse();
    }

    [Fact]
    public void TeamContract_CarriesExplicitCrossProjectResourcePool()
    {
        var command = new UpsertTeamCommand(Guid.NewGuid(), "graphics", "Graphics", 40m, SharedResourceKey: "graphics-core", SharedPoolCapacityHoursPerWeek: 80m);
        command.SharedResourceKey.Should().Be("graphics-core");command.SharedPoolCapacityHoursPerWeek.Should().Be(80m);
    }

    [Fact]
    public void AlertState_PreservesOriginalFirstSeenTime()
    {
        var first = DateTimeOffset.UtcNow.AddHours(-3);var state = new AlertStateDto("risk:r1", "Acknowledged", "actor", null, DateTimeOffset.UtcNow, null, 2, DateTimeOffset.UtcNow, first);
        state.FirstSeenAtUtc.Should().Be(first);
    }

    [Fact]
    public void SyncContract_CanCarryCrossDomainDataTypeAndFieldAuthority()
    {
        var command=new ManagementSyncCommand(
            Guid.NewGuid(),"Spreadsheet",ManagementSyncDirection.Bidirectional,true,
            "construction.task",[new("Progress","Spreadsheet","PreferAuthority"),new("Approval","Manual","Manual")]);
        command.DataType.Should().Be("construction.task");
        command.FieldRules.Should().Contain(x=>x.Field=="Progress"&&x.Authority=="Spreadsheet");
    }

    [Fact]
    public void ExternalItem_CanPreserveProviderSpecificFieldsWithoutNewEntityType()
    {
        var item=new ManagementExternalItem("Spreadsheet","row-7","task-7","3F piping","InProgress","Site",DateTimeOffset.UtcNow.AddDays(3),DateTimeOffset.UtcNow,"rev-3","file://schedule.xlsx",new Dictionary<string,string>{{"Inspection","Pending"},{"DomainId","construction"}});
        item.Fields.Should().ContainKey("Inspection");item.Fields!["DomainId"].Should().Be("construction");
    }
}
