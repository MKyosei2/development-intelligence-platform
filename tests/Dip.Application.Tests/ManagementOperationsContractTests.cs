// Purpose: Verifies M an ag em en tO pe ra ti on sC on tr ac t behavior, edge cases, and invariants for this repository area.
using System.IO.Compression;
using System.Text;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.Management;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
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
        analysis.Sheets[0].Columns.Should().Contain(x => x.Header == "備考" && x.Field == null);
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


    [Fact]
    public void SpreadsheetSchemaInference_PrefersSheetIdentityOverRetakeAttribute()
    {
        var service = new SpreadsheetSchemaInferenceService(null!, null!, null!, null!);
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> workbook =
            new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>
            {
                ["VFX Shot Schedule"] = new List<IReadOnlyList<string>>
                {
                    new List<string> { "管理番号", "ショット", "担当者", "ステータス", "リテイク" },
                    new List<string> { "sh010", "sh010", "Compositor A", "Review", "1" }
                },
                ["VFX Review Log"] = new List<IReadOnlyList<string>>
                {
                    new List<string> { "管理番号", "ショット", "レビュー", "担当者" },
                    new List<string> { "rv010", "sh010", "NeedsFix", "Supervisor" }
                }
            };

        var analysis = service.AnalyzeWorkbook(Guid.NewGuid(), workbook);

        analysis.Sheets.Single(x => x.SheetName == "VFX Shot Schedule").ObjectKind.Should().Be("media.shot");
        analysis.Sheets.Single(x => x.SheetName == "VFX Review Log").ObjectKind.Should().Be("media.review");
    }

    [Fact]
    public async Task FlexibleSpreadsheetImport_RealXlsx_DryRunDoesNotMutate_ApplyWritesMediaGraphAndDependency()
    {
        var projectId = Guid.NewGuid();
        var graphRepository = new InMemoryGraphRepository();
        var unitOfWork = new TrackingUnitOfWork();
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 28, 0, 0, 0, TimeSpan.Zero));
        var graph = new DevelopmentGraphService(graphRepository, unitOfWork, clock);
        var evidence = new InMemoryEvidenceRepository();
        var service = new SpreadsheetSchemaInferenceService(graph, evidence, unitOfWork, clock);
        var xlsx = BuildXlsx(
            "VFX Shot Schedule",
            ["管理番号", "ショット", "担当者", "ステータス", "依存", "備考"],
            [
                ["sh010", "sh010", "Compositor A", "InProgress", "", "smoke pass"],
                ["sh020", "sh020", "Compositor B", "Review", "sh010", "fire pass"]
            ]);

        FlexibleSpreadsheetImportResult dryRun;
        await using (var dryStream = new MemoryStream(xlsx, writable: false))
            dryRun = await service.ImportFlexibleXlsxAsync(projectId, dryStream, null, true, CancellationToken.None);

        dryRun.DryRun.Should().BeTrue();
        dryRun.DomainId.Should().Be("media.production");
        dryRun.Imported.Should().Be(2);
        dryRun.DependencyLinks.Should().Be(0);
        dryRun.Analysis.Sheets.Should().ContainSingle(x => x.ObjectKind == "media.shot");
        graphRepository.Nodes.Should().BeEmpty("dry-run must not persist graph nodes");
        graphRepository.Edges.Should().BeEmpty("dry-run must not persist dependencies");
        evidence.Items.Should().BeEmpty();
        unitOfWork.SaveCount.Should().Be(0);

        FlexibleSpreadsheetImportResult applied;
        await using (var applyStream = new MemoryStream(xlsx, writable: false))
            applied = await service.ImportFlexibleXlsxAsync(projectId, applyStream, null, false, CancellationToken.None);

        applied.DryRun.Should().BeFalse();
        applied.Imported.Should().Be(2);
        applied.DependencyLinks.Should().Be(1);
        graphRepository.Nodes.Should().HaveCount(2);
        graphRepository.Nodes.Select(x => x.MetadataValue("kind")).Should().OnlyContain(x => x == "media.shot");
        graphRepository.Edges.Should().ContainSingle(x => x.Type == DevelopmentGraphEdgeType.DependsOn);
        unitOfWork.SaveCount.Should().BeGreaterThan(0);
    }

    private static byte[] BuildXlsx(
        string sheetName,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipEntry(zip, "[Content_Types].xml", """
<?xml version="1.0" encoding="UTF-8"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
</Types>
""");
            AddZipEntry(zip, "_rels/.rels", """
<?xml version="1.0" encoding="UTF-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
</Relationships>
""");
            AddZipEntry(zip, "xl/workbook.xml", $"""
<?xml version="1.0" encoding="UTF-8"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets><sheet name="{Xml(sheetName)}" sheetId="1" r:id="rId1"/></sheets>
</workbook>
""");
            AddZipEntry(zip, "xl/_rels/workbook.xml.rels", """
<?xml version="1.0" encoding="UTF-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
</Relationships>
""");

            var allRows = new List<IReadOnlyList<string>> { headers };
            allRows.AddRange(rows);
            var xml = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            for (var r = 0; r < allRows.Count; r++)
            {
                xml.Append($"<row r=\"{r + 1}\">");
                for (var c = 0; c < allRows[r].Count; c++)
                {
                    var column = (char)('A' + c);
                    xml.Append($"<c r=\"{column}{r + 1}\" t=\"inlineStr\"><is><t>{Xml(allRows[r][c])}</t></is></c>");
                }
                xml.Append("</row>");
            }
            xml.Append("</sheetData></worksheet>");
            AddZipEntry(zip, "xl/worksheets/sheet1.xml", xml.ToString());
        }
        return memory.ToArray();
    }

    private static void AddZipEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Xml(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&apos;", StringComparison.Ordinal);

    private sealed class FixedClock(DateTimeOffset now) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = now;
    }

    private sealed class TrackingUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCount++;
            return Task.FromResult(1);
        }
    }

    private sealed class InMemoryEvidenceRepository : IEvidenceRepository
    {
        public List<EvidenceRecord> Items { get; } = [];
        public Task AddAsync(EvidenceRecord evidence, CancellationToken ct)
        {
            Items.Add(evidence);
            return Task.CompletedTask;
        }
        public Task AddRangeAsync(IEnumerable<EvidenceRecord> evidence, CancellationToken ct)
        {
            Items.AddRange(evidence);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<EvidenceRecord>> ListForProjectAsync(Guid projectId, DateTimeOffset? observedSince, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<EvidenceRecord>>(
                Items.Where(x => x.ProjectId == projectId && (!observedSince.HasValue || x.ObservedAtUtc >= observedSince.Value)).ToArray());
    }

    private sealed class InMemoryGraphRepository : IDevelopmentGraphRepository
    {
        public List<DevelopmentGraphNode> Nodes { get; } = [];
        public List<DevelopmentGraphEdge> Edges { get; } = [];

        public Task<DevelopmentGraphNode?> GetNodeAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Nodes.FirstOrDefault(x => x.Id == id));
        public Task<DevelopmentGraphNode?> FindNodeAsync(Guid projectId, DevelopmentGraphNodeType type, string key, CancellationToken ct)
            => Task.FromResult(Nodes.FirstOrDefault(x => x.ProjectId == projectId && x.Type == type && x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)));
        public Task<IReadOnlyList<DevelopmentGraphNode>> ListNodesAsync(Guid projectId, DevelopmentGraphNodeType? type, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DevelopmentGraphNode>>(Nodes.Where(x => x.ProjectId == projectId && (!type.HasValue || x.Type == type.Value)).ToArray());
        public Task AddNodeAsync(DevelopmentGraphNode node, CancellationToken ct)
        {
            Nodes.Add(node);
            return Task.CompletedTask;
        }
        public Task<DevelopmentGraphEdge?> FindEdgeAsync(Guid projectId, Guid sourceNodeId, Guid targetNodeId, DevelopmentGraphEdgeType type, CancellationToken ct)
            => Task.FromResult(Edges.FirstOrDefault(x => x.ProjectId == projectId && x.SourceNodeId == sourceNodeId && x.TargetNodeId == targetNodeId && x.Type == type));
        public Task<IReadOnlyList<DevelopmentGraphEdge>> ListEdgesAsync(Guid projectId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<DevelopmentGraphEdge>>(Edges.Where(x => x.ProjectId == projectId).ToArray());
        public Task AddEdgeAsync(DevelopmentGraphEdge edge, CancellationToken ct)
        {
            Edges.Add(edge);
            return Task.CompletedTask;
        }
    }
}
