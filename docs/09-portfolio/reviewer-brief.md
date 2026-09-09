# Reviewer向け概要

DIPは、C# Backend / Web / Worker、Python分析、C++ / native Collector、CI/CD、PostgreSQL-backed durable queueを一つのDevelopment Intelligence製品として統合した、**ゲーム・ソフトウェア制作向けDevelopment Infrastructure**です。

## ゲーム企業のReviewer向け60秒読み順

1. `README.md`のGIFと「ゲーム開発での最短ユースケース」で製品像を確認する。
2. `GAME_DEVELOPMENT_SCENARIO.md`で `Unity / Unreal → QA → Evidence → Project Truth → Quality Gate` の接続を見る。
3. `ENGINEERING_CASE_STUDY.md`でprovider-neutral Evidence、PostgreSQL coordination、exact-revision verification、failure modelの設計理由を見る。
4. `docs/09-portfolio/evidence/`とGitHub Actionsで、説明と検証Evidenceが分離されていることを確認する。

## 何を評価してほしいProjectか

DIPの主眼は、gameplayやrendering algorithmそのものではありません。ゲーム制作現場で分散しやすいGit / CI / engine QA / runtime / performance / project dataをEvidenceへ統合し、開発者・TA/TD・QA・Lead・PMが同じ根拠を見て判断できる基盤を設計した点です。

UnityではAsset、Build Settings、Scene / Reference、Animation、Runtime Performanceを同じEvidence pipelineへ接続します。Unrealやnative toolchainもprovider-specific detailを保持しつつ、共通Project Contextへ正規化します。

現在はQA結果を単に表示するだけでなく、Finding lifecycle、Flaky reliability、Regression baseline、Ownership、期限付きWaiver、QA Policy / Quality Gate、Evidence provenance、QA self-health、technical-debt trendまで接続しています。PM / ProducerはProduct FailureとQA Infrastructure Failureを分けた上で、GO / REVIEW REQUIRED / NO-GOとそのEvidenceを確認できます。

## 書類選考時

ローカル実行は前提にしません。READMEのGIF、Screenshots、Architecture、固定済みhistorical Evidence、exact-revision CI artifactから主要範囲を確認できます。GIF / screenshotはsample-data previewであり、Runtime Verifiedの根拠には使いません。

## Runtimeを確認する場合

別Demo版や簡易Backendへ切り替えず、host-native runtimeのPostgreSQL / Python Analysis / Dip.Api / Dip.Worker / Dip.Webを検証します。Docker / external queue daemonは正式なNative-first runtimeの必須依存ではありません。実Unity Editorを含むengine claimはengine runtime verifierへ分離し、cross-platform E2Eだけで「Unity runtime verified」とは主張しません。

## Verification boundary

**Implemented / Tested / Runtime Verifiedを分離**しています。正式な説明では、exact revisionのverification artifactがある項目だけをruntime-verifiedとして扱います。Historical PASS、sample-data preview、実装済みsourceの存在をcurrent revisionのPASSへ読み替えません。
