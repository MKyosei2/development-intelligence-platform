# P2 Decision Intelligence アーキテクチャ

## 目的

P2はDIPを「現在状態の可視化」からEvidenceに基づく意思決定支援へ拡張する。Project Truthを置き換えず、QAをProduct coreへ置き換えるものでもない。

```text
Community hardware / experiments / regression / reliability / media checks
                              |
                              v
                    P2 Intelligence Records
                              |
             +----------------+----------------+
             |                                 |
      Objective Evidence                Prediction / Context
             |                                 |
             v                                 v
        Project Truth                  PM Decision Intelligence
             |                                 |
             +----------------+----------------+
                              v
                         PM Dashboard
```

実測FailureはProject Truth Evidenceになり得る。一方、forecast、AI candidate、community aggregate、simulationはcontextual evidenceであり、実測されたreal-device factへ昇格させない。

## 実装済みP2機能群

### Hardware Intelligence（ハードウェア知識）

- 明示的opt-inのCommunity Hardware sample
- SHA-256 pseudonymous hardware identity。raw keyは保存しない
- sample count、OS/driver diversity、real-device ratio、P10/P50/P90に基づくconfidence
- community aggregate Hardware Profile
- CPU/GPU/memory/storage/thermal dimensionを横断するportability analysis
- CPU/GPU/memory-bandwidth/storage/memory-budgetごとの独立minimum boundary search
- estimated technical minimumとは別のsafety-margin recommended profile

### 適応型Verification Planning

- changed-file/tag classification
- historical failure risk weighting
- fixed expected QA cost
- budget-aware plan selection
- docs-only short path
- rendering/mobile/audio/network specialization

このplannerは既存のhardcoded Evidence Providerを選択する。Language-neutral QA adapterを追加するものではない。

### Regression / Reliability（回帰 / 信頼性）

- repeated flaky-test execution
- repeated runtime reliability execution
- memory/CPU/FPS/frame-time telemetry付きsoak test
- performance drift detection
- isolated Git worktree regression bisect
- historical metric pointに対する明示的regression-origin analysis
- failure corpus、deduplication、bounded minimizationを備えたdeterministic fuzz seed/boundary corpus
- mandatory passing baselineを持つisolated workspace内mutation testing
- project-reference/path/tokenに基づくTest Impact Analysis
- stable-fingerprint QA crash clustering
- 利用可能時のllvm-symbolizer/addr2lineによるnative address symbolication
- target FPS frame budgetを使うgame performance/network regression gate

### Media Regression（メディア回帰）

- visual image comparisonとdifference image生成
- sampled video comparisonと最初の差分時刻
- audio RMS/silence/clipping/sample-difference analysis
- ffmpeg/ffprobe dependency failureはProduct failureではなくInfrastructureFailure

### Evidenceに基づくRoot Cause分析

- deterministic evidence clusteringは常時利用可能
- 任意の外部OpenAI-compatible endpointは既定で無効
- 明示的に有効化した場合のみ、選択したEvidence bundleを送信
- 未知Evidence IDを参照するAI candidateは破棄
- AI outputからProject Truth blockerを直接作成しない
- suggested actionはhuman review向け助言であり、auto-fix/auto-PRは行わない
- similar-incident lookupでは過去のP2 Evidenceを使用

### PM Decision Intelligence（意思決定支援）

- Evidence Freshnessとconfidence decay
- authority-weighted Evidence Conflict Resolution
- Historical Risk
- Schedule Risk estimate
- Completion Forecast range
- Work Stagnation detection
- Dependency Risk
- What Changed Today
- Why Health Changed
- ranked PM Decision Brief
- Project Truth transitionのDecision Trace

## Human-safety原則

P2はemployee trustworthiness、lying probability、個人performance scoreを計算しない。Stagnationは、Claimに対してobjective Evidenceが古いことを意味する。Contradictionは、Claimとcurrent Evidenceが一致しないことを意味する。どちらも人物への非難ではない。

## Predictionの意味

Forecastは実測factではない。P2はprediction confidenceを別に保存し、確約completion dateではなくdate rangeを使用する。SimulationやCommunity Hardware aggregationは、sample数が多くてもreal-device `Verified` statusを得ない。

## 実行安全性

Fuzzing、mutation、soak、repeated execution、bisectは構成済み`Qa:WorkspaceRoot`内に制限する。Executable/media pathはrelative pathでなければならず、そのroot内に留める。Mutationとbisectは可能な限りisolated Git worktreeを使用し、developerのactive checkoutを変更しない。

Shared deploymentではauthenticationを有効にし、ProjectWrite accessを制限する。P2 execution endpointを信頼できないnetworkへanonymous公開してはならない。
