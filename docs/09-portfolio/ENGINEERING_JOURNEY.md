# Engineering Journey

DIPの成長は、機能数を増やしたことよりも、**何をVerifiedと呼べるかを厳密化したこと**にあります。初期commitには広い機能追加も残っていますが、履歴を書き換えて見栄えを整えるのではなく、現在の設計・検証プロセスがどう変わったかを明示します。

## 1. 機能実装中心からEvidence設計へ

初期段階ではDashboard、QA、analysis、management、collectorなど広い機能を実装しました。その後、Sourceが存在すること、testが通ること、実runtimeでcurrent revisionが検証されたことは別だと整理しました。

現在の主役は次の3領域です。

1. **Project Truth / Evidence**
2. **Game Development QA**
3. **Verification / Release Operations**

その他の機能は、この3領域を支える周辺機能として扱います。

## 2. Historical PASSをcurrent revisionへ継承しない

過去のCI、benchmark、runtime Evidenceはbaselineとして保持しますが、後続commitの成功へ自動昇格させません。

最終提出では、source freeze後の1つのSHAに対して次を束ねます。

- required CI 11/11 Green
- exact-revision performance / soak
- exact-revision QA Bug Zoo
- Project Truth expected-failure scenario
- final Evidence binding

## 3. Engine integrationのclaimを狭めた

Unity / Unreal integrationを実装したことと、実target projectでruntime検証したことを分離しました。

実target projectを供給していない提出revisionでは、engine-specific runtimeをFinal mandatory gateに含めず、`NOT_EVALUATED`として明示します。Hosted CIはcontract/unit behaviorを検証し、actual Editor runtimeの代替にはしません。

この変更により、「実装済み」を「runtime verified」と言い換えない境界が明確になりました。

## 4. QA EvidenceをProject Truthへ接続した

Final portfolioの中心scenarioを、再現性のあるQA -> Project Truth flowへ集約しました。

```text
Critical QA Evidence
  -> Quality Gate Failed
  -> Complete / 100% claim
  -> Contradicted
  -> Verified progress < Reported progress
```

Critical findingはdeterministic fixtureとして意図的に投入します。Scenario PASSはproduct-side failureを隠す意味ではなく、DIPがunsupported claimを正しく拒否したことを意味します。

## 5. Infrastructure FailureとProduct Failureを分離

C++ toolchain、Windows runtime、GUI、databaseなどの検証で、環境都合の失敗をproduct defectにもPASSにも変換しない方針へ揃えました。

例としてC++ QAではVisual Studio/MSVCを明示解決し、CMake generator instanceはresolverが返したexact installationだけをQA sandboxの例外として許可します。C# Bug ZooではWindows Application Controlにfixture assemblyが拒否された場合も、その事実をEvidenceへ残した上でdeterministic runtime probeへ限定fallbackし、expected failureを再現できなければPASSにしません。

## 6. CIを「毎push」から「Evidenceを作る時」へ

GitHub-hosted runner quotaを通常iterationで消費しないよう、CIを3段階に分けました。

```text
work/*
  -> automatic hosted Actions: 0

workflow_dispatch
  -> Fast / Targeted / Full

main + .portfolio-ci-trigger
  -> final required 11 workflows
```

通常開発はlocal verificationを基本とし、Hosted CIは明示的に必要な時とfinal publicationへ集中させます。

## 7. ReleaseをEvidence lineageとして扱う

`workflow success`を`production deployed`と同義にしません。Source SHA、CI、benchmark、QA Bug Zoo、Project Truth、checksumsを別artifactとして残し、finalizerでsame-SHA bindingを要求します。

この履歴により、初期の試行錯誤から、revision・evidence・fail-closed verificationを意識した開発へ変化したことを説明します。
