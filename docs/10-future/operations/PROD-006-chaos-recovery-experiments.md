# 故障注入・復旧訓練・本番移行判定計画

> **文書ステータス:** Proposed / 実施実績なし  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 実施目的
24時間365日のサービスは「停止しない」と主張するのではなく、停止・切断・部分故障を**正確に検出し安全に復旧できること**で信頼性を説明する。

## 2. 訓練表
| ID | 故障 | 観測点 | 完了条件 |
|---|---|---|---|
| CHAOS-01 | API停止 | 5xx、Probe、User impact | Restart後に整合、誤PASSなし |
| CHAOS-02 | Worker kill | Lease、Retry、Job status | 1回だけ結果確定 |
| CHAOS-03 | PostgreSQL切断 | Error/Queue/Audit | 再接続、データ欠損なし |
| CHAOS-04 | Artifact write denied | Hash、Run status | 完了未証明として拒否 |
| CHAOS-05 | LLM API timeout | AI stage/Cost | 候補を未検証に保つ |
| CHAOS-06 | QA sandbox失効 | Attestation/Process | 非信頼コードを実行させない |
| CHAOS-07 | GPU device reset | Telemetry/Driver | GPU QA未評価、再試験可能 |
| CHAOS-08 | Backup corruption | Restore/Hash | 破損を検出し誤Restore成功なし |
| CHAOS-09 | Tenant IDOR | Auth/Audit | 拒否、情報非漏えい |

## 3. 実施上の注意
- 本番の顧客システムで無許諾の障害注入を行わない。
- 最初は隔離staging、専用DB、サンプルデータで実施する。
- テスト開始前のデータ/Job/Artifact Snapshotを用意する。
- 時間上限とAbort手順を定義し、失敗しても安全に環境を戻せるようにする。

## 4. 結果の記録
`experiment ID, fault injection, target SHA, test env, expected symptoms, actual detection, recovery time, data consistency, artifact digests, incidents, follow-up issues`。RTO/RPOは測定値から提示し、設計上の希望値を達成値と混同しない。

## 参照する実装・既存資料

- `docs/10-future/operations/INCIDENT-AND-RECOVERY.md`
- `docs/10-future/operations/SLO-RUNBOOK.md`
- `docs/06-operations/incident-runbook-catalog.md`
