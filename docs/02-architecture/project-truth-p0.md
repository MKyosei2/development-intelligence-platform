# Project Truth / Evidence First P0 アーキテクチャ

## 1. 目的

DIPの最上位目的はQA自動化ではなく、PM・制作進行が専門知識なしで「現在のプロジェクトの実態」を確認できることにある。

人間の報告は重要だが、誤認、確認不足、情報の陳腐化、意図的な虚偽などにより、報告と実際の状態が一致しない可能性がある。DIPは人物を評価せず、報告内容（Claim）と観測事実（Evidence）を分離し、その一致度を可視化する。

## 2. データフロー

```text
Human Claim -----------------------------┐
                                         │
GitHub --------> EvidenceRecord ----------┤
Collector ------> EvidenceRecord ----------┤
Quality Gate ---> EvidenceRecord ----------┤
Performance ----> EvidenceRecord ----------┤
Unity / Maya ---> EvidenceRecord ----------┤
                                         v
                              Claim Verification
                                         |
                      +------------------+------------------+
                      |                  |                  |
                   Progress            Risk             Blocker
                      |                  |                  |
                      +------------------+------------------+
                                         |
                                  Project Truth
                                         |
                       PM-first Dashboard / Timeline
```

QAはEvidence Providerの一つであり、Project Truthそのものではない。

## 3. Claim状態

| 状態 | 意味 |
|---|---|
| Confirmed | 複数または十分な高信頼Evidenceが報告を支持 |
| PartiallyConfirmed | 一部Evidenceのみ支持、またはWarningを含む |
| Unverified | Evidence不足 |
| Contradicted | 高信頼の失敗Evidenceが報告と矛盾 |
| Stale | 報告が古く、新しいEvidenceが不足 |

人物への「truth score」は作らない。

## 4. Evidence Confidence（証拠の信頼度）

Evidenceには`Unverified / Low / Medium / High / Verified`を持たせる。

`Verified`は「結果が必ず正しい」という意味ではなく、Evidenceの出所・取得経路が強く検証できていることを示す。例として、HMAC署名検証済みGitHub WebhookはVerified、Collector実行結果はHighとして扱う。

## 5. 進捗

P0では二つのProgressを分離する。

- `ReportedProgress`: チームが報告した進捗
- `VerifiedProgress`: ClaimのうちEvidenceで裏付けられた割合

この二つを一つの数字に混ぜない。

## 6. Risk / Blocker（リスク / 阻害要因）

RiskはHealth Scoreだけから決めない。

- 最新の高信頼Failure Evidence
- Runtime / Build / Integration / Release系Failure
- ClaimとEvidenceのContradiction
- ReportedProgressとVerifiedProgressの大きな差
- Evidence不足

を理由付きで評価する。

## 7. Dashboard（ダッシュボード）

DashboardはPM向け情報を最初に表示する。

1. Current Project Truth
2. Evidence-backed Progress / Reported Progress
3. Risk / Blocker / Mismatch
4. Top Actions
5. Report vs Reality
6. Evidence Timeline
7. Technical Evidence（Quality Gate / Pipeline / Metric / Issue）

技術情報を削除するのではなく、PMビューの下にEvidenceとして配置する。

## 8. DB互換性

現行DIPは正式なEF Core migration chainを使用する。新規Databaseは`DbInitializer`からmigrationを適用し、過去の`EnsureCreated`系Databaseについては`LegacyMigrationAdopter`が互換性をfail-closedで検証したうえでmigration baselineへ移行する。

P0開発時に使用していた`P0SchemaBootstrapper`による`CREATE TABLE IF NOT EXISTS`方式は、正式migration baselineの導入完了に伴い廃止した。Schema evolutionのtruth sourceは`src/Dip.Infrastructure/Persistence/Migrations/`とする。

## 9. P0で実装しないもの

- 人物別Trust Score
- AIによる「嘘判定」
- 言語非依存QA
- Hardware Capability Simulation
- 自動修正PR

これらはProject Truthのコア設計を壊さない形で後続フェーズとして扱う。
