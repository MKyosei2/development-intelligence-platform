# QA Worker容量・スケジューリング・課金連動の設計

> **文書ステータス:** Proposed / 実負荷に基づく数値は未決定  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 問題
ソース解析・Unity/Unreal Player・GPU Capture・LLM呼び出しは処理時間と料金が大きく異なる。同一共有Queueだけで無制限実行すると、重いQAが短時間タスクや他顧客の業務を妨害する。

## 2. 推奨ジョブ分類
| Class | Resource | 例 | 限界 |
|---|---|---|---|
| Lightweight | CPU/低メモリ | Lint、Manifest QA | 高優先度でも無制限にしない |
| Build | CPU/IO/Storage | C++ Build、Unity Batch | Tool license、cache汚染 |
| Runtime | CPU/Memory/Player | Soak、Crash Repro | Duration、プロセス残留 |
| GPU | Dedicated GPU/VRAM | Render、GPU capture | Driver isolation、時間単価 |
| External AI | Network/Tokens | Hypothesis/patch提案 | API budget、外部データ送信 |

## 3. スケジューリングの候補規則
- Tenant/Projectごとに同時実行数、CPU/GPU予算、Queue Time、Storage、LLM Tokenを制限。
- User requested priorityと管理者が設定する上限を分ける。
- JobごとにLease、retry budget、Timeout、Cancellationを持たせ、無限再試行を許さない。
- Short JobとLong Jobを分割、starvationが起きないようweighted fairnessを検討。
- Artifact作成に失敗したジョブのCompute費用と判定結果を分けて記録する。

## 4. レイテンシ指標
`queue waiting p95`, `job duration p95`, `resource utilization`, `retry ratio`, `drop/cancel rate`, `cost per successful verified run`など。測定対象を細かく分類して比較する。

## 5. 受け入れ試験
1. 2テナント×軽いJob100件＋重いJob10件を混在させる。
2. GPU jobを飽和させても通常APIと別テナントのLightweight QAが許容遅延に収まる。
3. Worker kill/lease expiry時の二重課金・二重Evidenceを防ぐ。
4. 利用量の上限超過時にはHTTP/Jobの理由を明確に返す。
5. 負荷閾値は実測に基づき後から採用し、設計時の仮数値を成果として掲載しない。

## 参照する実装・既存資料

- `docs/10-future/architecture/ARCH-001-24X365-OPERATIONS.md`
- `docs/02-architecture/qa-job-lifecycle.md`
- `docs/08-testing/performance-load-soak-test-plan.md`
