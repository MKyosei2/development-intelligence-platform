// Purpose: Verifies P ro je ct Pl an Un de rs ta nd in gE ng in eT es ts behavior.
using Dip.Application.Features.ProjectIntelligence;
using FluentAssertions;

namespace Dip.Application.Tests.ProjectIntelligence;

public sealed class ProjectPlanUnderstandingEngineTests
{
    private readonly ProjectPlanUnderstandingEngine _engine = new();
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void JapaneseWbs_DetectsPlanningColumnsAndHierarchy()
    {
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> tables =
            new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>
            {
                ["開発WBS"] = new IReadOnlyList<string>[]
                {
                    new[] { "WBS番号", "工程名", "担当者", "開始日", "終了日", "進捗率", "依存先" },
                    new[] { "1", "バトル", "Lead A", "2026-08-01", "2026-08-20", "60%", "" },
                    new[] { "1.1", "AI実装", "Dev B", "2026-08-02", "2026-08-10", "80%", "" },
                    new[] { "1.2", "結合テスト", "QA C", "2026-08-11", "2026-08-20", "20%", "1.1FS+2d" }
                }
            };

        var result = _engine.Analyze("project.xlsx", tables, Now);
        var sheet = result.Sheets.Should().ContainSingle().Subject;

        sheet.Mapping.TitleColumn.Should().Be(1);
        sheet.Mapping.WbsColumn.Should().Be(0);
        sheet.Mapping.OwnerColumn.Should().Be(2);
        sheet.Mapping.StartColumn.Should().Be(3);
        sheet.Mapping.DueColumn.Should().Be(4);
        sheet.Mapping.ProgressColumn.Should().Be(5);
        sheet.Mapping.DependencyColumn.Should().Be(6);
        sheet.Mapping.Confidence.Should().BeGreaterThanOrEqualTo(80m);
        sheet.Preview.Single(x => x.Key == "1.1").ParentKey.Should().Be("1");
        sheet.Preview.Single(x => x.Key == "1.2").DependencyKeys.Should().ContainSingle("1.1");
        sheet.Preview.Single(x => x.Key == "1.1").DeclaredProgressPercent.Should().Be(80m);
    }

    [Fact]
    public void EnglishGantt_FindsHeaderBelowTitleRows()
    {
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> tables =
            new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>
            {
                ["Schedule"] = new IReadOnlyList<string>[]
                {
                    new[] { "Project Aurora", "", "", "" },
                    new[] { "Task ID", "Task Name", "Planned Start", "Due Date", "Percent Complete", "Assignee" },
                    new[] { "GAME-10", "Gameplay prototype", "2026-08-01", "2026-08-31", "0.5", "Team A" }
                }
            };

        var result = _engine.Analyze("schedule.xlsx", tables, Now);
        var sheet = result.Sheets.Should().ContainSingle().Subject;

        sheet.Mapping.HeaderRowIndex.Should().Be(1);
        sheet.Preview.Should().ContainSingle();
        sheet.Preview[0].Key.Should().Be("GAME-10");
        sheet.Preview[0].DeclaredProgressPercent.Should().Be(50m);
    }

    [Fact]
    public void MissingWbs_UsesStableRowKeyAndDoesNotInventCompletionEvidence()
    {
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> tables =
            new Dictionary<string, IReadOnlyList<IReadOnlyList<string>>>
            {
                ["Tasks"] = new IReadOnlyList<string>[]
                {
                    new[] { "Task", "Owner", "Due" },
                    new[] { "Server integration", "Backend", "2026-09-01" }
                }
            };

        var result = _engine.Analyze("tasks.csv", tables, Now);
        var item = result.Sheets.Single().Preview.Single();

        item.Key.Should().Be("ROW-2");
        item.DeclaredProgressPercent.Should().BeNull();
        result.Sheets.Single().Mapping.Notes.Should().Contain(x => x.Contains("WBS key", StringComparison.OrdinalIgnoreCase));
    }
}
