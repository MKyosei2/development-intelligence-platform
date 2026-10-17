# マルチプレイ・リプレイ・ゲームプレイQA計画

> **文書ステータス:** Telemetry RunnerのAs-Is確認 / 実複数クライアントの実証なし  
> **調査基準:** DIP `main` `ae2a78f1c168`、将来資料ブランチ `3587122ad05e`（2026-10-16 確認）  
> **注意:** 文書は設計・検証の説明であり、実機・本番運用の成功を新たに証明するものではない。

## 1. 実装接点
`MultiplayerQaExperimentRunner`には`DIP_MP_STATE`（`dip.multiplayer-qa.v1`）のテレメトリ契約があり、2〜32クライアント、ネットワーク遅延・ジッタ・損失、同一tickのstate hash desync検出を扱う。ネットワーク障害を指定しただけで適用済みとはせず、Telemetryに適用証拠を要求する設計。

## 2. レベル別テスト
| レベル | 説明 | 期待する証拠 |
|---|---|---|
| Contract | サンプルTelemetryをパースできる | Parserの正/負テスト |
| Simulated | 設定済みの再現可能なHarness | ハッシュと仮想ネットワーク条件 |
| Real Engine | Playerを複数起動 | Player log、tick、OS、network |
| Distributed | 複数ホストで計測 | Host間時刻、RTT/packet loss、sync |
| Product | 継続検証とランニングコスト管理 | Tenant isolation、再現性、Alert |

## 3. テストケース
- 正常2/4クライアントでN tickのStateHash一致。
- 故意に別の入力を与え、一致しないTickを特定。
- Latency 50/100/200ms、Jitter、packet lossを段階的に増加させ、実際に効果があった証拠を検証。
- クライアントCrash/切断・再接続、State Reconciliation、Packet reorder、Late Join。
- Deterministic replay: 入力、seed、Build SHAを固定し、同じ終了状態が再現されるか。
- **前提:** サーバー権限型/Peer-to-peer等のゲーム設計が違えばオラクルも違う。全ゲームに万能なテストは存在しない。

## 4. 不正/不明への対応
Network Impairment Attestationが無い場合、指定した遅延条件での検証済みとはしない。Common Tick不足やPlayer不在をPassにしない。同じログの多重取り込みはRunIDで重複排除する。

## 5. Evidence
`caseId, targetSHA, dipSHA, clientCount, clientBuildSha, tickRate, commonTicks, desyncTicks, requested/observed latency, harness attestation, raw logs/digest, verdict`。実Playerを動かしていない時点ではRuntime Verifiedを付けない。

## 参照する実装・既存資料

- `src/Dip.Infrastructure/P2Intelligence/MultiplayerQaExperimentRunner.cs`
- `src/Dip.Application/Features/P2Intelligence/GameRegressionContracts.cs`
- `docs/10-future/qa/QA-003-GAMEPLAY-MULTIPLAYER.md`
