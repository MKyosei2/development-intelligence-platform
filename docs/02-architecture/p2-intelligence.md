# P2 — 予測型Development Intelligence

P2はProject Truthを拡張するものであり、**置き換えるものではない**。QA、hardware、regression、reliability、visual/media comparison、root-cause analysisはEvidence Providerである。Project Truthは、最終的なstatus/risk/progress判断の基準として維持する。

## P2の機能

- 明示的opt-inとpseudonymizeを行うCommunity Hardware calibration sampleとconfidence aggregate。
- 明示的safety margin付きのEstimated Minimum Hardware境界探索。P2はP1の制約付きperformance-simulation runnerを再利用し、simulationを実機Evidenceとして扱わない。
- 変更path、過去のfailure Evidence、時間budgetからAdaptive QA planを作成。
- Disposableなdetached Git worktree内でGit commit範囲を探索する自動regression origin search。元のcheckoutは変更しない。
- Target processのfailure rate、timeout、memory growth、出力されたFPS/frame-time drift Evidenceを記録するflaky/reliability/soak execution。
- Visual/video/audio regression artifact comparisonとEvidence保存。精度の低いbinary fallbackをhigh-confidence Evidenceへ昇格させない。
- Evidence-grounded root-cause candidate。提供Evidenceで結論を支持できない場合は`Insufficient Evidence`を返す。
- Confidenceと根拠付きのrelease-risk/completion-window forecast。Completion velocityはProject Truthのverified-progress historyから算出し、履歴不足では架空の日付を作らずcompletion dateを出さない。
- PM Decision Brief: Situation → Why → Next Actionに加え、daily changeとProject Truth change driverを提示。

## 実行安全性

P2 regression/mutation runnerは、隔離されたdetached Git worktreeを使用する。元のcheckoutを過去commitへ切り替えず、mutation candidateを利用者のactive working treeへ書き込まない。

Verification commandは次のいずれかに制限する。

- isolated worktree内へ解決されるworkspace-relative executable/script
- allowlistされたdevelopment tool（`dotnet`、Python/pytest、CMake/CTest/Ninja/MSBuild、PowerShell、Node package tool）

任意のabsolute host commandとworkspace path traversalは拒否する。

## Hardware Evidenceの境界

Minimum-hardware estimateは、制約付きsynthetic conditionと観測されたapplication resultから生成する。CPU/GPU/memory/storage boundary probeは、要求した制約が実際に適用されなかった場合、またはtargetが明示的pass/fail product resultを報告しない場合にfail-closedとする。

Resultは`simulated=true`、`realDevice=false`のままであり、認証済みminimum specificationを公開するには実機Evidenceが必要である。

## Reliability / Soakの意味

Soak runはtimeoutではなく観測windowである。Targetが要求soak時間を通して稼働していれば、DIPは観測成功を記録してprocessを意図的に停止する。途中終了はfailureとして扱う。Memory growthはDIP自身のmanaged heapではなく**対象process**からsampleする。Performance driftは、targetが解析可能なFPSまたはframe-time metricを出力した場合だけ計算し、欠損metricを無関係なhost measurementで置き換えない。

## 安全性 / Governance

P2はdeveloper/artistのtrust scoreを計算しない。評価対象はClaimとEvidenceである。Community Hardware submissionは明示的opt-inを必要とし、生のconsent tokenではなくone-way pseudonymous source fingerprintを保存する。Root-cause suggestionがsource codeを自動変更することはない。Predictionはconfidenceとsupporting evidenceを伴うrange/riskとして扱う。
