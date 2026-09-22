// Purpose: Infers WBS/Gantt mappings and normalizes project-plan rows into structured candidates.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Dip.Application.Features.ProjectIntelligence;

public sealed class ProjectPlanUnderstandingEngine
{
    private static readonly string[] WbsAliases =
        ["wbs", "wbs id", "id", "no", "番号", "wbs番号", "項目id", "task id", "taskid"];
    private static readonly string[] TitleAliases =
        ["task", "task name", "name", "item", "work item", "work", "作業", "作業名", "タスク", "タスク名", "項目", "項目名", "工程", "工程名"];
    private static readonly string[] ParentAliases =
        ["parent", "parent id", "parent wbs", "parent task", "親", "親wbs", "親タスク"];
    private static readonly string[] StartAliases =
        ["start", "start date", "planned start", "plannedstart", "開始", "開始日", "着手", "着手日", "予定開始", "開始予定"];
    private static readonly string[] DueAliases =
        ["end", "finish", "finish date", "due", "due date", "planned finish", "planned end", "終了", "終了日", "完了予定", "期限", "納期", "予定終了"];
    private static readonly string[] ProgressAliases =
        ["progress", "% complete", "percent complete", "completion", "completion percent", "進捗", "進捗率", "完了率", "完成率"];
    private static readonly string[] OwnerAliases =
        ["owner", "assignee", "assigned to", "担当", "担当者", "担当部署", "担当チーム"];
    private static readonly string[] StatusAliases =
        ["status", "state", "状態", "ステータス", "進行状態"];
    private static readonly string[] DependencyAliases =
        ["dependency", "dependencies", "predecessor", "predecessors", "depends on", "blocked by", "先行", "先行タスク", "依存", "依存先", "前工程"];
    private static readonly string[] MilestoneAliases =
        ["milestone", "is milestone", "マイルストーン", "節目"];

    public ProjectPlanAnalysisDto Analyze(
        string fileName,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> tables,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var warnings = new List<string>();
        var sheets = new List<PlanSheetAnalysisDto>();
        var all = new List<PlanItemCandidateDto>();

        foreach (var (sheetName, rows) in tables)
        {
            if (rows.Count == 0)
                continue;

            var mapping = DetectMapping(sheetName, rows);
            if (mapping.TitleColumn is null)
            {
                warnings.Add($"{sheetName}: task/name column could not be identified; sheet skipped.");
                continue;
            }

            var items = ParseItems(sheetName, rows, mapping, warnings);
            all.AddRange(items);
            sheets.Add(new(
                sheetName,
                mapping,
                Math.Max(0, rows.Count - mapping.HeaderRowIndex - 1),
                items.Take(20).ToArray()));
        }

        if (all.Count == 0)
            warnings.Add("No WBS/Gantt rows were recognized. Check the mapping preview before importing.");

        return new(
            fileName,
            HashTables(tables),
            sheets,
            all.Count,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToArray(),
            now);
    }

    public IReadOnlyList<PlanItemCandidateDto> ParseAll(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> tables,
        IReadOnlyDictionary<string, PlanColumnMappingDto>? overrides,
        ICollection<string> warnings)
    {
        var result = new List<PlanItemCandidateDto>();
        foreach (var (sheetName, rows) in tables)
        {
            if (rows.Count == 0)
                continue;

            var mapping = overrides is not null && overrides.TryGetValue(sheetName, out var value)
                ? value
                : DetectMapping(sheetName, rows);
            if (mapping.TitleColumn is null)
            {
                warnings.Add($"{sheetName}: task/name column could not be identified; sheet skipped.");
                continue;
            }

            result.AddRange(ParseItems(sheetName, rows, mapping, warnings));
        }

        return result;
    }

    public PlanColumnMappingDto DetectMapping(
        string sheetName,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var bestRow = 0;
        var bestScore = -1m;
        Dictionary<string, int?> best = new();
        IReadOnlyList<string> headers = [];

        for (var rowIndex = 0; rowIndex < Math.Min(rows.Count, 20); rowIndex++)
        {
            var normalizedHeaders = rows[rowIndex].Select(NormalizeHeader).ToArray();
            var map = new Dictionary<string, int?>
            {
                ["wbs"] = Find(normalizedHeaders, WbsAliases),
                ["title"] = Find(normalizedHeaders, TitleAliases),
                ["parent"] = Find(normalizedHeaders, ParentAliases),
                ["start"] = Find(normalizedHeaders, StartAliases),
                ["due"] = Find(normalizedHeaders, DueAliases),
                ["progress"] = Find(normalizedHeaders, ProgressAliases),
                ["owner"] = Find(normalizedHeaders, OwnerAliases),
                ["status"] = Find(normalizedHeaders, StatusAliases),
                ["dependency"] = Find(normalizedHeaders, DependencyAliases),
                ["milestone"] = Find(normalizedHeaders, MilestoneAliases)
            };

            var score =
                (map["title"] is not null ? 4m : 0m) +
                (map["wbs"] is not null ? 2m : 0m) +
                (map["start"] is not null ? 1.5m : 0m) +
                (map["due"] is not null ? 1.5m : 0m) +
                (map["progress"] is not null ? 1m : 0m) +
                (map["owner"] is not null ? 1m : 0m) +
                (map["status"] is not null ? 0.5m : 0m) +
                (map["dependency"] is not null ? 0.5m : 0m);

            if (score > bestScore)
            {
                bestScore = score;
                bestRow = rowIndex;
                best = map;
                headers = rows[rowIndex].Select(x => x?.Trim() ?? string.Empty).ToArray();
            }
        }

        var notes = new List<string>();
        if (best.GetValueOrDefault("wbs") is null)
            notes.Add("WBS key will be inferred from row order/title indentation.");
        if (best.GetValueOrDefault("parent") is null)
            notes.Add("Parent hierarchy will be inferred from dotted WBS keys/title indentation when possible.");
        if (best.GetValueOrDefault("progress") is null)
            notes.Add("Declared progress column was not found; readiness will rely on external evidence.");

        var recognized = best.Values.Count(x => x is not null);
        var confidence = Math.Clamp((bestScore / 12m) * 100m, 0m, 100m);
        if (best.GetValueOrDefault("title") is null)
            confidence = Math.Min(confidence, 35m);
        else if (recognized >= 5)
            confidence = Math.Max(confidence, 80m);

        return new(
            sheetName,
            bestRow,
            best.GetValueOrDefault("wbs"),
            best.GetValueOrDefault("title"),
            best.GetValueOrDefault("parent"),
            best.GetValueOrDefault("start"),
            best.GetValueOrDefault("due"),
            best.GetValueOrDefault("progress"),
            best.GetValueOrDefault("owner"),
            best.GetValueOrDefault("status"),
            best.GetValueOrDefault("dependency"),
            best.GetValueOrDefault("milestone"),
            Math.Round(confidence, 1),
            headers,
            notes);
    }

    private static IReadOnlyList<PlanItemCandidateDto> ParseItems(
        string sheetName,
        IReadOnlyList<IReadOnlyList<string>> rows,
        PlanColumnMappingDto mapping,
        ICollection<string> warnings)
    {
        var result = new List<PlanItemCandidateDto>();
        var indentStack = new Dictionary<int, string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var rowIndex = mapping.HeaderRowIndex + 1; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var rawTitle = Cell(row, mapping.TitleColumn);
            if (string.IsNullOrWhiteSpace(rawTitle))
                continue;

            var title = rawTitle.Trim();
            var rawWbs = Cell(row, mapping.WbsColumn).Trim();
            var key = string.IsNullOrWhiteSpace(rawWbs) ? $"ROW-{rowIndex + 1}" : SanitizeKey(rawWbs);
            if (!used.Add(key))
            {
                var original = key;
                key = $"{key}-R{rowIndex + 1}";
                warnings.Add($"{sheetName} row {rowIndex + 1}: duplicate WBS key '{original}' was disambiguated as '{key}'.");
                used.Add(key);
            }

            var parent = Cell(row, mapping.ParentColumn).Trim();
            if (string.IsNullOrWhiteSpace(parent) && !string.IsNullOrWhiteSpace(rawWbs))
                parent = InferDottedParent(rawWbs) ?? string.Empty;

            var indent = LeadingIndent(rawTitle);
            if (string.IsNullOrWhiteSpace(parent) && indent > 0)
            {
                var parentDepth = indentStack.Keys.Where(x => x < indent).DefaultIfEmpty(-1).Max();
                if (parentDepth >= 0)
                    parent = indentStack[parentDepth];
            }

            indentStack[indent] = key;
            foreach (var depth in indentStack.Keys.Where(x => x > indent).ToArray())
                indentStack.Remove(depth);

            var start = ParseDate(Cell(row, mapping.StartColumn), false);
            var due = ParseDate(Cell(row, mapping.DueColumn), true);
            if (start.HasValue && due.HasValue && due < start)
                warnings.Add($"{sheetName} row {rowIndex + 1}: finish date is before start date for '{title}'.");

            var progress = ParsePercent(Cell(row, mapping.ProgressColumn));
            var status = Null(Cell(row, mapping.StatusColumn));
            var owner = Null(Cell(row, mapping.OwnerColumn));
            var dependencies = SplitKeys(Cell(row, mapping.DependencyColumn));
            var milestone = ParseBoolean(Cell(row, mapping.MilestoneColumn)) || IsMilestoneStatus(status, title);

            result.Add(new(
                key,
                title,
                Null(parent),
                start,
                due,
                progress,
                owner,
                status,
                dependencies,
                milestone,
                sheetName,
                rowIndex + 1,
                $"{sheetName}!R{rowIndex + 1}"));
        }

        return result;
    }

    private static int? Find(IReadOnlyList<string> headers, IReadOnlyList<string> aliases)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            if (aliases.Any(alias => headers[i].Equals(NormalizeHeader(alias), StringComparison.OrdinalIgnoreCase)))
                return i;
        }

        for (var i = 0; i < headers.Count; i++)
        {
            if (headers[i].Length > 1 && aliases.Any(alias => headers[i].Contains(NormalizeHeader(alias), StringComparison.OrdinalIgnoreCase)))
                return i;
        }

        return null;
    }

    private static string NormalizeHeader(string? value) =>
        new string((value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Where(c => !char.IsWhiteSpace(c) && c != '_' && c != '-' && c != '/' && c != '(' && c != ')' && c != '％' && c != '%')
            .ToArray());

    private static string Cell(IReadOnlyList<string> row, int? index) =>
        index is not null && index.Value >= 0 && index.Value < row.Count
            ? row[index.Value] ?? string.Empty
            : string.Empty;

    private static string SanitizeKey(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= 160 ? normalized : normalized[..160];
    }

    private static string? InferDottedParent(string value)
    {
        var normalized = value.Trim().TrimEnd('.');
        var separator = Math.Max(normalized.LastIndexOf('.'), normalized.LastIndexOf('-'));
        return separator > 0 ? normalized[..separator] : null;
    }

    private static int LeadingIndent(string value)
    {
        var count = 0;
        foreach (var character in value)
        {
            if (character == ' ')
                count++;
            else if (character == '\t')
                count += 4;
            else
                break;
        }
        return count / 2;
    }

    private static DateTimeOffset? ParseDate(string value, bool endOfDay)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTimeOffset.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var date) ||
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date))
        {
            if (endOfDay && !HasExplicitTime(value))
                return new DateTimeOffset(date.Date.AddDays(1).AddTicks(-1), date.Offset).ToUniversalTime();
            return date.ToUniversalTime();
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) &&
            serial >= 1 && serial < 300000)
        {
            try
            {
                var local = DateTime.FromOADate(serial);
                var offset = TimeZoneInfo.Local.GetUtcOffset(local);
                if (endOfDay && Math.Abs(serial - Math.Truncate(serial)) < 0.0000001d)
                    local = local.Date.AddDays(1).AddTicks(-1);
                return new DateTimeOffset(local, offset).ToUniversalTime();
            }
            catch
            {
                // Intentionally ignored to preserve the existing best-effort or fallback behavior.
            }
        }

        return null;
    }

    private static bool HasExplicitTime(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(
            value,
            @"(?:T|\s)\d{1,2}:\d{2}|\d{1,2}:\d{2}",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static decimal? ParsePercent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim()
            .Replace("%", string.Empty, StringComparison.Ordinal)
            .Replace("％", string.Empty, StringComparison.Ordinal);
        if (!decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var number) &&
            !decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.CurrentCulture, out number))
            return null;

        if (!value.Contains('%') && !value.Contains('％') && number is >= 0m and <= 1m && value.Contains('.'))
            number *= 100m;
        return Math.Clamp(number, 0m, 100m);
    }

    private static IReadOnlyList<string> SplitKeys(string value) =>
        value.Split([',', ';', '、', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeDependencyKey)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToArray();

    private static string NormalizeDependencyKey(string value)
    {
        var normalized = value.Trim();
        var match = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"^(?<key>.+?)(?:FS|SS|FF|SF)(?:[+-].+)?$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return (match.Success ? match.Groups["key"].Value : normalized).Trim();
    }

    private static bool ParseBoolean(string value) =>
        value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase) ||
        value.Trim().Equals("○", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("milestone", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("マイルストーン", StringComparison.OrdinalIgnoreCase);

    private static bool IsMilestoneStatus(string? status, string title) =>
        status?.Contains("milestone", StringComparison.OrdinalIgnoreCase) == true ||
        status?.Contains("マイルストーン", StringComparison.OrdinalIgnoreCase) == true ||
        title.EndsWith(" milestone", StringComparison.OrdinalIgnoreCase);

    private static string? Null(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string HashTables(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> tables)
    {
        var builder = new StringBuilder();
        foreach (var (sheet, rows) in tables.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(sheet).Append('\n');
            foreach (var row in rows)
            {
                foreach (var cell in row)
                    builder.Append(cell).Append('\u001f');
                builder.Append('\n');
            }
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }
}
