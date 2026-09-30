# Reviewer向け概要

DIPは、C# Backend / Web / Worker、Python分析、C++ / native QA、CI/CD、PostgreSQL-backed durable queueを一つのDevelopment Intelligence製品として統合した、ゲーム・ソフトウェア制作向けDevelopment Infrastructureです。

## 60秒読み順

1. `README.md`の「まず見てほしい3領域」でProject Truth / Game Development QA / Verification & Release Operationsを確認する。
2. `QA_EVIDENCE_CASE_STUDY.md`で、Critical QA Evidenceがunsupported Complete claimをProject TruthでContradictedへ変える流れを見る。
3. `README.md`の「Verification status」と「Final exact-revision gate」で、Implemented / Tested / Runtime Verifiedを分離し、current revisionのPASSをEvidenceなしに継承しない設計を確認する。
4. `ENGINEERING_JOURNEY.md` と `docs/09-portfolio/evidence/`、GitHub Actions / Release artifactで、失敗分析・再発防止・exact-revision verificationが開発プロセスへ組み込まれていることを確認する。

## 何を評価してほしいProjectか

DIPの主眼は、gameplayやrendering algorithmそのものではありません。ゲーム制作現場で分散しやすいGit / CI / QA / runtime / performance / project dataをEvidenceへ統合し、開発者・TA/TD・QA・Lead・PMが同じ根拠を見て判断できる基盤を設計した点です。

特に評価対象として明確にしたい設計判断は次です。

- ClaimとEvidenceを分離し、source / revision / freshness / contradictionを追跡する。
- Historical PASSを新revisionへ継承せず、exact-revision artifactを要求する。
- Product FailureとQA Infrastructure Failureを分離する。
- C++ / engine / native toolchain固有の情報を失わず、Project Contextへ正規化する。
- 未実行、skipped、environment-blockedを成功扱いしない。
- Unit / Integration / E2E / benchmark / QA Bug Zoo / Project Truthを同一revisionへ束ねる。

## 中心Case Studyの境界

Portfolioの中心scenarioはdeterministic fixtureです。Critical findingは実production障害の発見結果ではなく、Project Truthのfail-closed behaviorを再現可能に検証するため意図的に投入します。

Verifier PASSの意味は「品質Gateが成功した」ではありません。期待されるproduct-side outcomeはQuality Gate `Failed`であり、その状態でComplete / 100% claimをDIPが`Contradicted`にできることを検証します。

## Engine integrationの境界

Unity / Unreal integration sourceは保持しますが、実target projectを供給していないrevisionではengine-specific runtimeをVerifiedと主張しません。Hosted workflowのcontract/unit verificationとactual Editor runtimeを分離し、target projectがある場合だけoptional verifierで別Evidenceを作ります。

## 書類選考時

ローカル実行は前提にしません。READMEのGIF、Architecture、historical baseline、exact-revision CI / Release artifact、final Evidence bundleから主要範囲を確認できます。GIF / screenshotはsample-data previewであり、Runtime Verifiedの根拠には使いません。

最終提出revisionについては、README中の説明よりGitHub Actions / Release artifact / final Evidence bundleを優先してください。final gate未完了のrevisionをCurrent Revision PASSとして扱いません。

## Verification boundary

**Implemented / Tested / Runtime Verified / NOT_EVALUATEDを分離**しています。Historical PASS、sample preview、実装済みsourceの存在をcurrent revisionのPASSへ読み替えません。
