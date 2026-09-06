# P2 Test Plan（P2テスト計画）

P2はsource implementationとexecution Evidenceの両方が存在する場合にのみcompleteとする。

## 自動.NET Test

- Community Hardware consent/privacyとP2 recordのDomain validation
- Evidence Freshness decay
- Evidence Conflict authority rule
- Adaptive QA planning
- P0/P1 regression testがgreenのままであること
- E2E: Collector Evidence -> Project Truth -> Daily Change -> Decision Brief -> Health Change

## Local Functional Test（ローカル機能検証）

### Reliability（信頼性）

Deterministicなpass/fail targetを繰り返し実行し、pass/fail count、rate、confidenceを確認する。

### Soak / Drift（長時間 / 漂移）

`samples/p2-bug-zoo/python/soak_memory_growth/app.py`を使用する。このsampleはmemoryを保持しながら`DIP_METRIC` telemetryを出力する。Resulting P2 recordでmemory growthとperformance driftを確認する。

### Fuzz Test（ファズテスト）

Hostに適したsafe repository wrapper/executableを介して`samples/p2-bug-zoo/python/fuzz_target/app.py`を使用する。Crashを起こすmagic inputがartifactとして保持され、fingerprint化されることを確認する。

### Regression Bisect（回帰点特定）

Known good revisionとknown bad revisionを持つdisposable test repositoryまたは専用fixture branchを使用する。DIPがdetached worktreeでevaluationを実行し、active checkoutを変更せずfirst bad commitを報告することを確認する。

### Mutation Test（変異テスト）

1つのmutationがkillされ、1つが意図的にsurviveするfixtureを使用する。Mutation scoreと各case detailを確認する。

### Media Regression（メディア回帰）

Known baseline/currentのimage、video、audio pairを生成し、次を確認する。

- identical files -> Passed
- small difference -> configured thresholdに従いWarning
- large difference -> Failed
- ffmpeg/ffprobe欠損 -> InfrastructureFailure。Product Failedにはしない

## Hardware Intelligence（ハードウェア知識）

- ConsentなしのCommunity submissionはfail
- Raw hardware keyを永続化しない
- Community sampleが3未満の場合aggregate profileを作らない
- Profile confidenceはrepresentative sample count/diversityとともに増えるが上限を持つ
- Portability blockerがlimiting capability dimensionを特定する
- Minimum hardware search resultがsimulated/non-real-deviceと明示される
- Recommended minimumにsafety marginを含め、technical minimumとは分離する

## AI Grounding（AI根拠付け）

External AIはoptionalで既定無効。有効化時は次を守る。

- Requested subject向けにassembleしたEvidence bundleだけを送信
- Valid supplied Evidence IDを1件も含まないAI candidateを破棄
- AI outputだけからFailed Evidenceを作らない
- Sourceを自動変更せずPRを自動作成しない

## PM Semantics（PM意味論）

Dashboard/APIが次を区別できることを確認する。

- Claim vs Evidence
- measured vs simulated/predicted
- current vs stale evidence
- current authoritative evidence vs historical conflict
- schedule risk estimate vs promised schedule
- completion range vs exact date
- evidence stagnation vs employee misconduct

## 必須P2 Completion Evidence

Full Windows development machine上で`scripts/Verify-P2.ps1`が成功完了する必要がある。Live E2EにはさらにPostgreSQL、API、Workerが必要。P2 changeset commit後もCIをgreenに保つ必要がある。
