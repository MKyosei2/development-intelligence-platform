# Reviewer向け概要

DIPは、C# Backend / Web / Worker、Python分析、C++ / native Collector、CI/CD、PostgreSQL-backed durable queueを一つのDevelopment Intelligence製品として統合した、**ゲーム・ソフトウェア制作向けDevelopment Infrastructure**です。

## ゲーム企業のReviewer向け60秒読み順

1. `README.md`の「60秒レビュー」と「まず見てほしい3領域」で、Project Truth / Game Development QA / Verification & Release Operationsの3本柱を確認する。
2. `README.md`の「実Project Case Study」と `REAL_PROJECT_CASE_STUDY.md` で、`MAYAtoUnity → actual Unity Editor → Collector Evidence → DIP API → Project Truth / Quality Gate` の接続を見る。
3. `README.md`の「Verification status」と「Final exact-revision gate」で、Implemented / Tested / Runtime Verifiedを分離し、current revisionのPASSをEvidenceなしに継承しない設計を確認する。
4. `ENGINEERING_JOURNEY.md` と `docs/09-portfolio/evidence/`、GitHub Actions / Release artifactで、失敗分析・再発防止・exact-revision verificationが実際の開発プロセスへ組み込まれていることを確認する。

## 何を評価してほしいProjectか

DIPの主眼は、gameplayやrendering algorithmそのものではありません。ゲーム制作現場で分散しやすいGit / CI / engine QA / runtime / performance / project dataをEvidenceへ統合し、開発者・TA/TD・QA・Lead・PMが同じ根拠を見て判断できる基盤を設計した点です。

特に評価対象として明確にしたいのは、単純な機能数ではなく、次の設計判断です。

- ClaimとEvidenceを分離し、source / revision / freshness / contradictionを追跡すること。
- Historical PASSを新revisionへ継承せず、exact-revision artifactを要求すること。
- Unity / Unreal / native toolchain固有の情報を失わず、Project Contextへ正規化すること。
- CI失敗、runtime未検証、infrastructure blockedを成功扱いせず、fail-closedに判断すること。
- 実装だけでなく、Unit / Integration / E2E / benchmark / runtime / Releaseまで同一revisionへ束ねること。

UnityではAsset、Build Settings、Scene / Reference、Animation、Runtime Performanceを同じEvidence pipelineへ接続します。Unrealやnative toolchainもprovider-specific detailを保持しつつ、共通Project Contextへ正規化します。

現在はQA結果を単に表示するだけでなく、Finding lifecycle、Flaky reliability、Regression baseline、Ownership、期限付きWaiver、QA Policy / Quality Gate、Evidence provenance、QA self-health、technical-debt trendまで接続しています。PM / ProducerはProduct FailureとQA Infrastructure Failureを分けた上で、GO / REVIEW REQUIRED / NO-GOとそのEvidenceを確認できます。

## 実Project Case Studyの境界

MAYAtoUnityは別repositoryで管理している実Git-backed Unity Projectであり、生成fixtureではありません。Case Studyは実Unity EditorからDIPまでのend-to-end integrationを示します。一方で、これは第三者企業・チームへのproduction導入実績を意味しないため、そのようなclaimは別Evidenceなしに行いません。

## 書類選考時

ローカル実行は前提にしません。READMEのGIF、Architecture、固定済みhistorical Evidence、exact-revision CI / Release artifactから主要範囲を確認できます。GIF / screenshotはsample-data previewであり、Runtime Verifiedの根拠には使いません。

最終提出revisionについては、README中の説明よりもGitHub Actions / Release artifact / final Evidence bundleを優先してください。final gate未完了のrevisionをCurrent Revision PASSとして扱いません。

## Runtimeを確認する場合

別Demo版や簡易Backendへ切り替えず、host-native runtimeのPostgreSQL / Python Analysis / Dip.Api / Dip.Worker / Dip.Webを検証します。Docker / external queue daemonは正式なNative-first runtimeの必須依存ではありません。実Unity Editorを含むengine claimはengine runtime verifierへ分離し、cross-platform E2Eだけで「Unity runtime verified」とは主張しません。

## Verification boundary

**Implemented / Tested / Runtime Verifiedを分離**しています。正式な説明では、exact revisionのverification artifactがある項目だけをruntime-verifiedとして扱います。Historical PASS、sample-data preview、実装済みsourceの存在をcurrent revisionのPASSへ読み替えません。
