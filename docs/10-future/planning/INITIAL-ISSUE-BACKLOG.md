# 初期Issue候補（未登録）

**Status: Planning Draft.** この一覧はGitHub Issueを自動作成せず、対応状況の確定情報でもない。

## 優先候補

| ID | 仮タイトル | Pillar | Priority | 関連文書 | 完了判定の中心 |
|---|---|---|---|---|---|
| FUT-001 | Unity実Editor E2E検証 | Game QA | P0 | RFC-001 | 対象SHA付き実実行Evidence |
| FUT-002 | Unreal実Editor検証と実行重複の確認 | Game QA | P1 | RFC-001 | 実行経路・対象SHAが一意に記録 |
| FUT-003 | AI API opt-in実証 | Game QA | P0 | RFC-002 | 実応答から隔離検証・レビューまで |
| FUT-004 | AI QAのPrecision/Recall比較 | Game QA | P0 | QA-004 | 母数・正常対照群・指標 |
| FUT-005 | GPUフレームとCaptureの実証 | Game QA | P1 | RFC-003 | 実GPU・対象SHA・artifact検証 |
| FUT-006 | Production verification設定サンプル | Game QA | P1 | RFC-004 | 外部Toolの出力・拒否ケース |
| FUT-007 | Multiplayer実2クライアント試験 | Game QA | P2 | RFC-005 | loss/desyncの再現と記録 |
| FUT-008 | Untrusted QA隔離のfail-closed検証 | Verification | P0 | ARCH-003 | hostへの不正アクセス阻止 |
| FUT-009 | Release EvidenceのSHA整合性強化 | Verification | P0 | ARCH-004 | 旧SHA・欠損Artifactを拒否 |
| FUT-010 | Evidence信頼・署名・重複ソース監査 | Project Truth | P1 | ARCH-004 | 不正な高信頼昇格を防ぐ |
| FUT-011 | 組織・権限分離の脅威モデル | Product | P1 | ARCH-002 | クロステナント負例が定義される |
| FUT-012 | DB/Worker/Artifact障害回復演習 | Verification | P1 | ARCH-001 | 再実行と復元の正しさを確認 |
| FUT-013 | QAの検査範囲と未評価を表示 | Game QA | P1 | QA-001 | 実装と実機検証の差が可視化 |
| FUT-014 | Visual QAの正常・異常評価セット | Game QA | P1 | QA-002 | 許容変動と誤検知率を測定 |
| FUT-015 | 利用者導入フローの価値検証 | Product | P2 | PRD-001 | 再現可能な導入手順・フィードバック |

## 登録の手順

1. 影響と重複を確認し、取り組むIssueのみ登録する。
2. 主担当、Labels、Pillar、Priority、Milestone、受け入れ条件を付ける。
3. GitHub Projectに追加し、StatusをBacklog/Ready/In progress/Review/Doneで管理する。
4. 一つのIssueで複数の実装目的が混ざる場合は親Issueと子Issueに分割する。
5. 実装の完了と対象SHAのRuntime Verifiedを別項目で追跡する。

## 推奨ラベル（作成が必要）

`area:qa` / `area:project-truth` / `area:release` / `area:platform` / `type:feature` / `type:docs` / `type:verification` / `priority:p0` / `priority:p1` / `priority:p2` / `needs:decision` / `needs:real-runtime`。

ラベルが実際に存在しない状態で自動付与できるとは想定しない。
