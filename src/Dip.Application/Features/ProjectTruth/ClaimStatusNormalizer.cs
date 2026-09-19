// Purpose: Normalizes free-form claim status text before evidence-based verification.
namespace Dip.Application.Features.ProjectTruth;

public static class ClaimStatusNormalizer
{
    private static readonly string[] CompleteTokens =
    [
        "complete", "completed", "done", "finished", "approved", "published", "delivered", "inspected",
        "完了", "完成", "対応済", "終了", "承認済", "公開済", "納品済", "検査済", "レビュー済", "レンダー完了"
    ];

    private static readonly string[] NegativeCompletionTokens =
    [
        "not complete", "not completed", "incomplete", "not finished", "not approved",
        "未完了", "未完成", "未対応", "未承認", "未検査", "未納品"
    ];

    private static readonly string[] ReadyTokens =
    [
        "ready", "release ready", "ship ready", "準備完了", "リリース可能", "出荷可能"
    ];

    private static readonly string[] BlockedTokens =
    [
        "blocked", "blocking", "on hold", "stopped", "ブロック", "停止", "保留", "進行不能"
    ];

    private static readonly string[] FailedTokens =
    [
        "failed", "failure", "rejected", "broken", "失敗", "不合格", "否認", "差戻"
    ];

    private static readonly string[] NotStartedTokens =
    [
        "not started", "todo", "planned", "未着手", "未開始", "計画"
    ];

    private static readonly string[] InProgressTokens =
    [
        "in progress", "working", "under review", "進行中", "作業中", "対応中", "レビュー中"
    ];

    public static ClaimSemanticState Classify(string? value)
    {
        var status = value?.Trim() ?? string.Empty;

        // Negative expressions must be checked before positive completion tokens.
        if (ContainsAny(status, NegativeCompletionTokens))
            return ClaimSemanticState.InProgress;
        if (ContainsAny(status, BlockedTokens))
            return ClaimSemanticState.Blocked;
        if (ContainsAny(status, FailedTokens))
            return ClaimSemanticState.Failed;
        if (ContainsAny(status, NotStartedTokens))
            return ClaimSemanticState.NotStarted;
        if (ContainsAny(status, ReadyTokens))
            return ClaimSemanticState.Ready;
        if (ContainsAny(status, CompleteTokens))
            return ClaimSemanticState.Complete;
        if (ContainsAny(status, InProgressTokens))
            return ClaimSemanticState.InProgress;

        return ClaimSemanticState.Unknown;
    }

    private static bool ContainsAny(string value, IEnumerable<string> tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
}
