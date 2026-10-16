# GitHub Projects / Milestones の設定案

**Status: Proposed** | **GitHub上の実在するProjectやMilestoneを作成したわけではない。**

## Project名

`DIP Product & Engineering Roadmap`

## 推奨フィールド

| Field | Type | 候補値 |
|---|---|---|
| Status | Single select | Backlog, Ready, In progress, In review, Blocked, Done |
| Priority | Single select | P0, P1, P2, P3 |
| Pillar | Single select | Project Truth, Game QA, Verification, Cross-cutting |
| Work type | Single select | Feature, Bug, Verification, Research, Documentation, Security |
| Target milestone | GitHub Issue milestone等 | 実Milestoneと一致させる |
| Confidence | Single select | Unverified, Source reviewed, Tested, Runtime Verified |

## Views

- **Roadmap:** フェーズ・Milestoneを軸にしたタイムライン。確定していない日付は入力しない。
- **Board:** Statusで列を分ける。
- **By Pillar:** 三つの柱ごとの優先課題を表示する。
- **Verification Gaps:** 未評価・実環境不足・Security Criticalをフィルターする。

## Milestone候補

- `QA Runtime Validation`: Unity／AI QA実実行Evidenceの獲得。
- `Portfolio Evidence Hardening`: 現在SHAへ紐付くQA／Benchmark／Project Truthの強化。
- `Game QA Expansion`: GPU／Visual／Multiplayerのサンプル実証。
- `Service Foundation`: 権限分離、隔離、運用復旧の基盤。

これらの名称は提案であって、既存リリースの確定版番号とは無関係。

## 運用ルール

Issueは一つ以上の明確な受け入れ条件を持ち、PRから`Closes #番号`等で連携する。終了時にテストの実行条件、EvidenceとSHA、不足している実機検証を記録する。Milestoneを閉じる条件は、含まれるIssueのクローズ数だけでなく定義したCompletion Evidenceに基づく。
