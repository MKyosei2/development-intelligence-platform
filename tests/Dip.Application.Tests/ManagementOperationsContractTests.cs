using Dip.Application.Features.Management;
using FluentAssertions;

namespace Dip.Application.Tests;

public sealed class ManagementOperationsContractTests
{
    [Fact]
    public void FormalApproval_EnablesSeparationOfDutiesByDefault()
    {
        var command = new CreateApprovalRequestCommand(
            Guid.NewGuid(),
            "release-go",
            "Release",
            "Release GO",
            new ManagementTarget("release", "1.0.0"),
            ["Tech Lead", "Producer"],
            "requester");

        command.AllowSameActorMultipleRoles.Should().BeFalse();
    }

    [Fact]
    public void TeamContract_CarriesExplicitCrossProjectResourcePool()
    {
        var command = new UpsertTeamCommand(
            Guid.NewGuid(),
            "graphics",
            "Graphics",
            40m,
            SharedResourceKey: "graphics-core",
            SharedPoolCapacityHoursPerWeek: 80m);

        command.SharedResourceKey.Should().Be("graphics-core");
        command.SharedPoolCapacityHoursPerWeek.Should().Be(80m);
    }

    [Fact]
    public void AlertState_PreservesOriginalFirstSeenTime()
    {
        var first = DateTimeOffset.UtcNow.AddHours(-3);
        var state = new AlertStateDto(
            "risk:r1",
            "Acknowledged",
            "actor",
            null,
            DateTimeOffset.UtcNow,
            null,
            2,
            DateTimeOffset.UtcNow,
            first);

        state.FirstSeenAtUtc.Should().Be(first);
    }

    [Fact]
    public void SyncContract_CanCarryCrossDomainDataTypeAndFieldAuthority()
    {
        var command = new ManagementSyncCommand(
            Guid.NewGuid(),
            "Spreadsheet",
            ManagementSyncDirection.Bidirectional,
            true,
            "construction.task",
            [new("Progress", "Spreadsheet", "PreferAuthority"), new("Approval", "Manual", "Manual")]);

        command.DataType.Should().Be("construction.task");
        command.FieldRules.Should().Contain(x => x.Field == "Progress" && x.Authority == "Spreadsheet");
    }

    [Fact]
    public void ExternalItem_CanPreserveProviderSpecificFieldsWithoutNewEntityType()
    {
        var item = new ManagementExternalItem(
            "Spreadsheet",
            "row-7",
            "task-7",
            "3F piping",
            "InProgress",
            "Site",
            DateTimeOffset.UtcNow.AddDays(3),
            DateTimeOffset.UtcNow,
            "rev-3",
            "file://schedule.xlsx",
            new Dictionary<string, string>
            {
                ["Inspection"] = "Pending",
                ["DomainId"] = "construction"
            });

        item.Fields.Should().ContainKey("Inspection");
        item.Fields!["DomainId"].Should().Be("construction");
    }

    [Theory]
    [InlineData("担当者", "Owner")]
    [InlineData("進捗率", "Progress")]
    [InlineData("ショット", "Title")]
    [InlineData("検査結果", "Approval")]
    [InlineData("Due Date", "DueDate")]
    public void SpreadsheetSchemaInference_RecognizesJapaneseAndEnglishAliases(
        string header,
        string expectedField)
    {
        var inferred = SpreadsheetSchemaInferenceService.InferColumn(0, header);

        inferred.Field.Should().Be(expectedField);
        inferred.Confidence.Should().BeGreaterThan(0.8m);
    }

    [Fact]
    public void SpreadsheetSchemaInference_InfersMediaProductionWithoutNewCoreEntityType()
    {
        var service = new SpreadsheetSchemaInferenceService(null!, null!, null!, null!);
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> workbook =
            new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>
            {
                ["VFX Shot Schedule"] = new List<IReadOnlyList<string>>
                {
                    new List<string> { "ショット", "担当者", "ステータス", "リテイク", "備考" },
                    new List<string> { "sh010", "Compositor A", "Review", "1", "smoke pass" }
                }
            };

        var analysis = service.AnalyzeWorkbook(Guid.NewGuid(), workbook);

        analysis.DomainId.Should().Be("media.production");
        analysis.Sheets.Should().ContainSingle();
        analysis.Sheets[0].ObjectKind.Should().Be("media.shot");
        analysis.Sheets[0].Columns.Should().Contain(x => x.Header == "ショット" && x.Field == "Title");
        analysis.Sheets[0].Columns.Should().Contain(x => x.Header == "備考" && x.Field is null);
    }

    [Fact]
    public void SpreadsheetSchemaInference_InfersConstructionInspection()
    {
        var service = new SpreadsheetSchemaInferenceService(null!, null!, null!, null!);
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> workbook =
            new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>
            {
                ["施工検査一覧"] = new List<IReadOnlyList<string>>
                {
                    new List<string> { "管理番号", "工程名", "担当者", "検査結果", "期限" },
                    new List<string> { "INSP-001", "3F piping", "Site Lead", "承認", "2026-09-01" }
                }
            };

        var analysis = service.AnalyzeWorkbook(Guid.NewGuid(), workbook);

        analysis.DomainId.Should().Be("construction");
        analysis.Sheets[0].ObjectKind.Should().Be("construction.inspection");
        analysis.Sheets[0].Columns.Should().Contain(x => x.Field == "Approval");
    }
}
