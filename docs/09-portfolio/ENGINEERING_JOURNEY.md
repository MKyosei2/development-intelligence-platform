# Engineering Journey

DIPの成長は、機能数を増やしたことよりも、**何をVerifiedと呼べるかを厳密化したこと**にあります。初期commitには「改善」「整理」のような粗い履歴も残っていますが、履歴を書き換えて見栄えを整えるのではなく、現在の設計・検証プロセスがどう変わったかを明示します。

## 1. 機能実装中心からEvidence設計へ

初期段階ではDashboard、QA、analysis、management、collectorなど広い機能を実装しました。その後、Sourceが存在することと、testが通ることと、実runtimeでcurrent revisionが検証されたことは別だと整理しました。

現在の主役は次の3領域に固定しています。

1. **Project Truth / Evidence**
2. **Game Development QA**
3. **Verification / Release Operations**

その他の機能は、この3領域を支える周辺機能として扱います。

## 2. Historical PASSをcurrent revisionへ継承しない

過去のCI、benchmark、runtime Evidenceはbaselineとして保持しますが、後続commitの成功へ自動昇格させません。

そのため最終提出では、source freeze後の1つのSHAに対して次を束ねます。

- required CI 11/11 Green
- exact-revision performance benchmark
- real Unity Project runtime evidence
- real Unreal Project runtime evidence
- Project Truth / Quality Gate evidence

## 3. Sample previewとRuntime Evidenceを分離

READMEのGIFはUIと製品範囲を短時間で理解するためのsample-data previewです。実Projectで動いた証拠としては使用しません。

実Project EvidenceはEngine process、target project revision、DIP source revision、collector report、Project Truth結果を別artifactとして保存します。

## 4. CIを「毎push」から「Evidenceを作る時」へ

GitHub-hosted runner quotaを消費するだけの自動実行を避け、CIを3段階に分けました。

```text
work/*
  -> automatic hosted Actions: 0

workflow_dispatch
  -> Fast / Targeted / Full

main + .portfolio-ci-trigger
  -> final required 11 workflows
```

通常開発はlocal verificationを基本とし、Hosted CIは明示的に必要な時とfinal publicationへ集中させます。

## 5. Production policyと実Deployを分離

`workflow success`を`production deployed`と同義にしません。

- **Policy Verified**: deploy条件・設定の検査が成功した。
- **Production Deployed**: production mutationを実際に実行した。
- **Production Mutation Disabled**: production変更を意図的に実行していない。

Live Native Deploymentはmanual-onlyで、`deploy=false`を既定にしています。

## 6. PRを設計上のEvidenceにする

Actions quota-controlを導入するbootstrapだけは、旧PR triggerを踏まないためmainへfast-forwardする例外としました。それ以降の主要変更は`work/*`からPRを作り、final freezeも`.portfolio-ci-trigger`だけのPRとして実施します。

この履歴を残すことで、初期の試行錯誤から、revision・evidence・release lineageを意識した開発へ変化したことを説明します。
