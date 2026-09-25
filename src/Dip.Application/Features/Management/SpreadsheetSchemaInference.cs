// Purpose: Infers management workbook schemas and imports normalized project objects and evidence.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Management;

public sealed record SpreadsheetColumnInferenceDto(
    int ColumnIndex,
    string Header,
    string? Field,
    decimal Confidence,
    string Reason);

public sealed record SpreadsheetSheetInferenceDto(
    string SheetName,
    string DomainId,
    string ObjectKind,
    int HeaderRow,
    int DataRows,
    IReadOnlyList<SpreadsheetColumnInferenceDto> Columns,
    IReadOnlyList<string> Warnings);

public sealed record SpreadsheetSchemaAnalysisDto(
    Guid ProjectId,
    string DomainId,
    IReadOnlyList<SpreadsheetSheetInferenceDto> Sheets,
    IReadOnlyList<string> Warnings);

public sealed record FlexibleSpreadsheetImportResult(
    Guid ProjectId,
    string DomainId,
    int Imported,
    int Skipped,
    int DependencyLinks,
    bool DryRun,
    SpreadsheetSchemaAnalysisDto Analysis,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Infers common project-management semantics from arbitrary XLSX headers and
/// imports them as universal graph objects. This is intentionally isolated from
/// ManagementSpreadsheetService because inference over user-controlled workbook
/// schemas is a separate, higher-risk responsibility.
/// </summary>
public sealed class SpreadsheetSchemaInferenceService(
    DevelopmentGraphService graph,
    IEvidenceRepository evidenceRepository,
    IUnitOfWork unitOfWork,
    ISystemClock clock)
{
    private static readonly IReadOnlyDictionary<string, string[]> FieldAliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Key"] = ["key", "id", "taskid", "itemid", "管理番号", "番号", "キー", "識別子", "_diprowid"],
            ["ExternalId"] = ["externalid", "external id", "外部id", "外部番号", "管理id"],
            ["Title"] = ["title", "name", "task", "item", "work", "subject", "作業", "作業名", "タスク", "タスク名", "項目", "項目名", "件名", "名称", "工程", "工程名", "カット", "ショット"],
            ["Description"] = ["description", "detail", "details", "summary", "説明", "詳細", "内容", "概要"],
            ["Owner"] = ["owner", "assignee", "responsible", "担当", "担当者", "責任者", "作業者", "担当部署", "ownerrole"],
            ["Status"] = ["status", "state", "状態", "ステータス", "進行状態", "状況"],
            ["Progress"] = ["progress", "completion", "completionpercent", "percent", "進捗", "進捗率", "完了率", "達成率"],
            ["StartDate"] = ["start", "startdate", "plannedstart", "開始", "開始日", "着手日", "予定開始", "開始予定"],
            ["DueDate"] = ["due", "duedate", "deadline", "finish", "end", "終了", "終了日", "締切", "期限", "完了予定", "終了予定", "納期"],
            ["Priority"] = ["priority", "優先度", "重要度"],
            ["Dependency"] = ["dependency", "dependencies", "dependson", "predecessor", "blockedby", "依存", "依存先", "先行", "前工程", "前提"],
            ["Milestone"] = ["milestone", "マイルストーン", "節目"],
            ["Cost"] = ["cost", "budget", "amount", "費用", "コスト", "予算", "金額"],
            ["Risk"] = ["risk", "risklevel", "リスク", "危険度"],
            ["Approval"] = ["approval", "approved", "review", "承認", "承認状態", "レビュー", "検査結果"],
        };

    private static readonly string[] ConstructionSignals =
        ["construction", "architecture", "bim", "ifc", "rfi", "施工", "建築", "工事", "現場", "検査", "図面", "躯体", "配管", "設備", "工程"];

    private static readonly string[] MediaSignals =
        ["media", "vfx", "shot", "cut", "render", "retake", "composite", "animation", "映像", "カット", "ショット", "レンダー", "リテイク", "アニメーション", "コンポ", "撮影"];

    private static readonly string[] SoftwareSignals =
        ["software", "build", "commit", "branch", "pull request", "bug", "test", "ci", "コード", "ビルド", "コミット", "バグ", "テスト"];

    public Task<SpreadsheetSchemaAnalysisDto> AnalyzeXlsxAsync(
        Guid projectId,
        Stream stream,
        CancellationToken ct)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        ArgumentNullException.ThrowIfNull(stream);
        ct.ThrowIfCancellationRequested();

        var workbook = ManagementXlsxCodec.Read(stream);
        if (workbook.Count == 0)
            throw new InvalidDataException(
                "Workbook contains no readable worksheets.");

        return Task.FromResult(AnalyzeWorkbook(projectId, workbook));
    }

    public SpreadsheetSchemaAnalysisDto AnalyzeWorkbook(
        Guid projectId,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>> workbook)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        ArgumentNullException.ThrowIfNull(workbook);

        var sheets = new List<SpreadsheetSheetInferenceDto>();
        var warnings = new List<string>();
        foreach (var pair in workbook)
        {
            var rows = pair.Value;
            var headerIndex = FindHeaderRow(rows);
            if (headerIndex < 0)
            {
                warnings.Add($"{pair.Key}: no non-empty header row was found.");
                continue;
            }

            var headers = rows[headerIndex];
            var columns = new List<SpreadsheetColumnInferenceDto>();
            for (var i = 0; i < headers.Count; i++)
                columns.Add(InferColumn(i, headers[i]));

            var domain = InferDomain(pair.Key, headers);
            var kind = InferObjectKind(domain, pair.Key, headers);
            var sheetWarnings = new List<string>();
            if (!columns.Any(x => x.Field == "Title"))
                sheetWarnings.Add("No high-confidence title/name column was found; the first non-empty cell will be used as the object name.");
            if (!columns.Any(x => x.Field is "Key" or "ExternalId"))
                sheetWarnings.Add("No stable key column was found. DIP will derive a deterministic row identity from semantic values; add _DipRowId or an external ID for stronger round-trip stability.");
            if (columns.Count(x => x.Field is not null) == 0)
                sheetWarnings.Add("No known management columns were recognized. The sheet can still be preserved as generic project observations.");

            var dataRows = Math.Max(0, rows.Count - headerIndex - 1);
            sheets.Add(new(pair.Key, domain, kind, headerIndex + 1, dataRows, columns, sheetWarnings));
        }

        var dominant = sheets
            .GroupBy(x => x.DomainId, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .ThenByDescending(x => x.Key != "generic.project")
            .Select(x => x.Key)
            .FirstOrDefault() ?? "generic.project";
        return new(projectId, dominant, sheets, warnings);
    }

    public async Task<FlexibleSpreadsheetImportResult> ImportFlexibleXlsxAsync(
        Guid projectId,
        Stream stream,
        string? domainOverride,
        bool dryRun,
        CancellationToken ct)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        ArgumentNullException.ThrowIfNull(stream);

        var workbook = ManagementXlsxCodec.Read(stream);
        var analysis = AnalyzeWorkbook(projectId, workbook);
        var imported = 0;
        var skipped = 0;
        var links = 0;
        var warnings = new List<string>(analysis.Warnings);
        var evidenceToAdd = new List<EvidenceRecord>();
        var importedByLogicalKey = new Dictionary<string, DevelopmentGraphNodeDto>(StringComparer.OrdinalIgnoreCase);
        var pendingDependencies = new List<(DevelopmentGraphNodeDto Node, string Dependency)>();

        foreach (var sheet in analysis.Sheets)
        {
            ct.ThrowIfCancellationRequested();
            if (!workbook.TryGetValue(sheet.SheetName, out var rows))
                continue;

            var headerIndex = sheet.HeaderRow - 1;
            if (headerIndex < 0 || headerIndex >= rows.Count)
                continue;

            var domain = string.IsNullOrWhiteSpace(domainOverride)
                ? sheet.DomainId
                : NormalizeDomain(domainOverride!);
            var mapped = sheet.Columns
                .Where(x => x.Field is not null)
                .ToDictionary(x => x.Field!, x => x.ColumnIndex, StringComparer.OrdinalIgnoreCase);
            var duplicateKeys = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var rowIndex = headerIndex + 1; rowIndex < rows.Count; rowIndex++)
            {
                ct.ThrowIfCancellationRequested();
                var row = rows[rowIndex];
                if (row.All(string.IsNullOrWhiteSpace))
                {
                    skipped++;
                    continue;
                }

                var title = Mapped(row, mapped, "Title")
                    ?? row.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    skipped++;
                    continue;
                }

                var suppliedKey = Mapped(row, mapped, "Key") ?? Mapped(row, mapped, "ExternalId");
                var baseKey = string.IsNullOrWhiteSpace(suppliedKey)
                    ? StableRowKey(sheet.SheetName, title!, mapped, row)
                    : SafeKey(suppliedKey!);
                duplicateKeys.TryGetValue(baseKey, out var occurrence);
                duplicateKeys[baseKey] = occurrence + 1;
                var logicalKey = occurrence == 0 ? baseKey : $"{baseKey}-{occurrence + 1}";

                var status = Mapped(row, mapped, "Status") ?? "Observed";
                var sourceObjectId = Mapped(row, mapped, "ExternalId")
                    ?? Mapped(row, mapped, "Key")
                    ?? logicalKey;
                var raw = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var headers = rows[headerIndex];
                for (var i = 0; i < Math.Min(headers.Count, 100); i++)
                {
                    var header = string.IsNullOrWhiteSpace(headers[i])
                        ? $"Column{i + 1}"
                        : Limit(headers[i].Trim(), 200);
                    raw[header] = Limit(Cell(row, i), 1000);
                }

                var semantics = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in mapped)
                {
                    var value = Cell(row, entry.Value);
                    if (!string.IsNullOrWhiteSpace(value))
                        semantics[entry.Key] = Limit(value, 2000);
                }

                var metadata = BoundMetadata(new
                {
                    source = "spreadsheet",
                    sheet = Limit(sheet.SheetName, 200),
                    row = rowIndex + 1,
                    semantics,
                    raw,
                    rowIdentity = logicalKey
                });
                var fallback = FallbackType(sheet.ObjectKind);

                DevelopmentGraphNodeDto node;
                if (dryRun)
                {
                    node = new(
                        Guid.Empty,
                        fallback.ToString(),
                        $"sheet:{SafeKey(sheet.SheetName)}:{logicalKey}",
                        title!,
                        status,
                        "Spreadsheet",
                        null,
                        metadata,
                        DateTimeOffset.MinValue);
                }
                else
                {
                    node = (await graph.UpsertUniversalNodeAsync(
                        new(
                            projectId,
                            domain,
                            sheet.ObjectKind,
                            $"sheet:{SafeKey(sheet.SheetName)}:{logicalKey}",
                            title!,
                            status,
                            "Spreadsheet",
                            null,
                            sourceObjectId,
                            "1",
                            fallback,
                            metadata),
                        ct)).Node;
                }

                imported++;
                importedByLogicalKey[logicalKey] = node;
                if (!string.IsNullOrWhiteSpace(suppliedKey))
                    importedByLogicalKey[SafeKey(suppliedKey!)] = node;

                var dependency = Mapped(row, mapped, "Dependency");
                if (!string.IsNullOrWhiteSpace(dependency))
                    pendingDependencies.Add((node, dependency!));

                if (!dryRun && ShouldEmitEvidence(sheet.ObjectKind))
                {
                    var subject = SafeSubject(sourceObjectId);
                    var outcome = Outcome(
                        status,
                        semantics.TryGetValue("Approval", out var approval) ? approval : null);
                    var payload = metadata.Length <= 15000
                        ? metadata
                        : JsonSerializer.Serialize(new
                        {
                            source = "spreadsheet",
                            sheet = Limit(sheet.SheetName, 200),
                            row = rowIndex + 1,
                            rowIdentity = logicalKey,
                            kind = sheet.ObjectKind
                        });
                    evidenceToAdd.Add(new EvidenceRecord(
                        Guid.NewGuid(),
                        projectId,
                        null,
                        subject,
                        EvidenceSourceType.Spreadsheet,
                        EvidenceKind(sheet.ObjectKind),
                        outcome,
                        Limit($"{sheet.SheetName}: {title}", 200),
                        Limit($"Structured spreadsheet observation: {status}.", 4000),
                        EvidenceConfidence.Medium,
                        Limit($"spreadsheet:{sheet.SheetName}:{rowIndex + 1}", 1000),
                        payload,
                        clock.UtcNow,
                        clock.UtcNow));
                }
            }
        }

        if (!dryRun)
        {
            if (evidenceToAdd.Count > 0)
            {
                await evidenceRepository.AddRangeAsync(evidenceToAdd, ct);
                await unitOfWork.SaveChangesAsync(ct);
            }

            foreach (var pending in pendingDependencies)
            {
                foreach (var key in SplitDependencies(pending.Dependency))
                {
                    if (!importedByLogicalKey.TryGetValue(SafeKey(key), out var target) ||
                        target.Id == Guid.Empty ||
                        pending.Node.Id == Guid.Empty ||
                        target.Id == pending.Node.Id)
                        continue;

                    await graph.LinkUniversalAsync(
                        new(
                            projectId,
                            pending.Node.Id,
                            target.Id,
                            "depends-on",
                            70m,
                            false,
                            DevelopmentGraphEdgeType.DependsOn,
                            JsonSerializer.Serialize(new { source = "spreadsheet", raw = pending.Dependency })),
                        ct);
                    links++;
                }
            }
        }

        if (imported == 0)
            warnings.Add("No importable rows were found.");
        return new(
            projectId,
            string.IsNullOrWhiteSpace(domainOverride) ? analysis.DomainId : NormalizeDomain(domainOverride!),
            imported,
            skipped,
            links,
            dryRun,
            analysis,
            warnings);
    }

    public static SpreadsheetColumnInferenceDto InferColumn(int index, string header)
    {
        var original = header?.Trim() ?? string.Empty;
        var normalized = Normalize(original);
        if (normalized.Length == 0)
            return new(index, original, null, 0m, "Empty header.");

        foreach (var pair in FieldAliases)
        {
            foreach (var alias in pair.Value)
            {
                var normalizedAlias = Normalize(alias);
                if (normalized == normalizedAlias)
                    return new(index, original, pair.Key, 0.99m, $"Exact semantic alias match: {alias}.");
            }
        }

        SpreadsheetColumnInferenceDto? best = null;
        foreach (var pair in FieldAliases)
        {
            foreach (var alias in pair.Value)
            {
                var normalizedAlias = Normalize(alias);
                if (normalizedAlias.Length < 2)
                    continue;
                if (normalized.Contains(normalizedAlias, StringComparison.OrdinalIgnoreCase) ||
                    normalizedAlias.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    const decimal score = 0.82m;
                    if (best is null || score > best.Confidence)
                        best = new(index, original, pair.Key, score, $"Header contains semantic alias: {alias}.");
                }
            }
        }

        return best ?? new(index, original, null, 0.15m, "No known semantic alias matched; preserve as a custom field.");
    }

    private static string InferDomain(string sheetName, IReadOnlyList<string> headers)
    {
        var sheet = sheetName.ToLowerInvariant();
        var headerText = string.Join(' ', headers).ToLowerInvariant();

        // The worksheet name describes the managed collection and therefore has
        // higher authority than incidental columns such as Review or Retake.
        var construction =
            Score(sheet, ConstructionSignals) * 3
            + Score(headerText, ConstructionSignals);
        var media =
            Score(sheet, MediaSignals) * 3
            + Score(headerText, MediaSignals);
        var software =
            Score(sheet, SoftwareSignals) * 3
            + Score(headerText, SoftwareSignals);

        var max = Math.Max(construction, Math.Max(media, software));
        if (max == 0)
            return "generic.project";

        if (media == max && Score(sheet, MediaSignals) > 0)
            return "media.production";
        if (construction == max && Score(sheet, ConstructionSignals) > 0)
            return "construction";
        if (software == max && Score(sheet, SoftwareSignals) > 0)
            return "software";

        if (construction == max)
            return "construction";
        if (media == max)
            return "media.production";
        return "software";
    }

    private static string InferObjectKind(string domain, string sheetName, IReadOnlyList<string> headers)
    {
        var text = (sheetName + " " + string.Join(' ', headers)).ToLowerInvariant();
        if (domain == "construction")
        {
            if (ContainsAny(text, ["inspection", "検査"])) return "construction.inspection";
            if (ContainsAny(text, ["drawing", "図面"])) return "construction.drawing";
            if (ContainsAny(text, ["rfi", "質疑", "照会"])) return "construction.rfi";
            if (ContainsAny(text, ["approval", "承認"])) return "construction.approval";
            return "construction.task";
        }

        if (domain == "media.production")
        {
            // Prefer the sheet name when it expresses the managed object. A shot
            // schedule often carries Review/Retake columns, but those columns are
            // attributes of the shot and must not reclassify the whole sheet as
            // a media.review object collection.
            var sheet = sheetName.ToLowerInvariant();
            if (ContainsAny(sheet, ["render", "レンダー"])) return "media.render-job";
            if (ContainsAny(sheet, ["review", "retake", "レビュー", "リテイク"])) return "media.review";
            if (ContainsAny(sheet, ["shot", "cut", "ショット", "カット"])) return "media.shot";
            if (ContainsAny(text, ["render", "レンダー"])) return "media.render-job";
            if (ContainsAny(text, ["review", "retake", "レビュー", "リテイク"])) return "media.review";
            if (ContainsAny(text, ["shot", "cut", "ショット", "カット"])) return "media.shot";
            return "media.task";
        }

        if (domain == "software")
        {
            if (ContainsAny(text, ["build", "ビルド"])) return "software.build";
            if (ContainsAny(text, ["bug", "issue", "バグ", "不具合"])) return "software.issue";
            return "software.task";
        }

        return "generic.task";
    }

    private static DevelopmentGraphNodeType FallbackType(string kind) => kind switch
    {
        "construction.inspection" or "construction.approval" => DevelopmentGraphNodeType.Evidence,
        "construction.drawing" or "media.render-job" => DevelopmentGraphNodeType.Asset,
        "construction.rfi" or "media.review" or "media.shot" or "software.issue" => DevelopmentGraphNodeType.WorkItem,
        "software.build" => DevelopmentGraphNodeType.Build,
        _ => DevelopmentGraphNodeType.Task
    };

    private static bool ShouldEmitEvidence(string kind) =>
        kind is "construction.inspection" or "construction.approval" or "media.review" or "media.render-job" or "software.build";

    private static string EvidenceKind(string kind) => kind switch
    {
        "construction.inspection" => "ConstructionInspection",
        "construction.approval" => "ConstructionApproval",
        "media.review" => "MediaReview",
        "media.render-job" => "MediaRender",
        "software.build" => "SoftwareBuild",
        _ => "SpreadsheetObservation"
    };

    private static string SafeSubject(string value)
    {
        var result = value.Trim();
        if (result.Length == 0)
            result = "spreadsheet-item";
        return result[..Math.Min(200, result.Length)];
    }

    private static EvidenceOutcome Outcome(string status, string? approval)
    {
        var text = (status + " " + (approval ?? string.Empty)).ToLowerInvariant();
        if (ContainsAny(text, ["failed", "failure", "rejected", "blocked", "error", "不合格", "否認", "差戻", "失敗", "中止"]))
            return EvidenceOutcome.Failed;
        if (ContainsAny(text, ["warning", "pending", "in progress", "open", "not complete", "incomplete", "unapproved", "保留", "進行中", "確認中", "未完了", "未承認", "未検査"]))
            return EvidenceOutcome.Warning;
        if (ContainsAny(text, ["passed", "success", "succeeded", "approved", "completed", "done", "ready", "合格", "承認済", "完了", "完成", "納品済", "検査済", "レビュー済"]))
            return EvidenceOutcome.Succeeded;
        return EvidenceOutcome.Informational;
    }

    private static int FindHeaderRow(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var best = -1;
        var bestScore = 0m;
        for (var i = 0; i < Math.Min(rows.Count, 25); i++)
        {
            var row = rows[i];
            if (row.Count(x => !string.IsNullOrWhiteSpace(x)) == 0)
                continue;

            var recognized = row
                .Select((header, index) => InferColumn(index, header))
                .Count(x => x.Field is not null);
            var density = Math.Min(10, row.Count(x => !string.IsNullOrWhiteSpace(x))) * 0.05m;
            var score = recognized + density;
            if (score > bestScore)
            {
                best = i;
                bestScore = score;
            }
        }
        return best;
    }

    private static string? Mapped(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> map,
        string field) =>
        map.TryGetValue(field, out var index) ? NullIfEmpty(Cell(row, index)) : null;

    private static string Cell(IReadOnlyList<string> row, int index) =>
        index >= 0 && index < row.Count ? row[index]?.Trim() ?? string.Empty : string.Empty;

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string StableRowKey(
        string sheet,
        string title,
        IReadOnlyDictionary<string, int> mapped,
        IReadOnlyList<string> row)
    {
        var semantic = string.Join('|', new[]
        {
            title,
            Mapped(row, mapped, "Owner") ?? string.Empty,
            Mapped(row, mapped, "DueDate") ?? string.Empty,
            Mapped(row, mapped, "Milestone") ?? string.Empty
        });
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sheet + "|" + semantic));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..20];
    }

    private static string SafeKey(string value)
    {
        var normalized = new string(value.Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.'
                ? char.ToLowerInvariant(ch)
                : '-')
            .ToArray());
        while (normalized.Contains("--", StringComparison.Ordinal))
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        var trimmed = normalized.Trim('-');
        if (trimmed.Length == 0)
            return "item";
        return trimmed[..Math.Min(120, trimmed.Length)];
    }

    private static string BoundMetadata(object value)
    {
        var json = JsonSerializer.Serialize(value);
        if (json.Length <= 60000)
            return json;
        return JsonSerializer.Serialize(new
        {
            source = "spreadsheet",
            truncated = true,
            reason = "Workbook row metadata exceeded the graph payload bound."
        });
    }

    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];

    private static IEnumerable<string> SplitDependencies(string value) =>
        value.Split([';', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0);

    private static int Score(string text, IEnumerable<string> signals) =>
        signals.Count(x => text.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string text, IEnumerable<string> values) =>
        values.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase));

    private static string Normalize(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static string NormalizeDomain(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length is < 1 or > 120 ||
            normalized.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' or ':' or '/')))
            throw new ArgumentException("DomainId contains unsupported characters.", nameof(value));
        return normalized;
    }
}
