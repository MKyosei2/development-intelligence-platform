# 3分Demo

目的は「機能数」ではなく、**ゲーム開発のEvidenceがProject TruthとQuality Gateまでどうつながるか**を3分で示すことです。sample-data画面は製品説明用であり、Runtime Verifiedの証拠とは分離します。

## 0:00–0:25 — 課題

ゲーム制作ではGit、CI、Unity / Unreal、QA、runtime、performance、進捗情報が別々の場所に分散します。DIPはそれらをProject Contextへ集約し、単なるstatusではなくprovenanceを持つEvidenceとして扱います。

## 0:25–0:50 — Project / Evidence

Projectを選択し、最新Evidence、Claim、revision、freshnessを表示します。ここで「Implemented」「Tested」「Runtime Verified」を同一視しないことを説明します。

## 0:50–1:20 — Game Development QA

Unity / Unreal / native toolchainから入るQA Evidenceを示します。Unity例ではAsset、Build Settings、Scene / Reference、Animation、Runtime Performanceが同一pipelineへ入り、critical findingはsource / rule / revisionとともに保持されます。

## 1:20–1:50 — Project Truth

完了Claimに対してcriticalなengine / runtime Evidenceが存在するケースを示します。DIPはClaimをそのまま真実として扱わず、Evidence Confidence、Contradiction、Risk、Verified Progressへ反映します。

## 1:50–2:15 — Quality / Release Gate

Quality Gateを開き、GO / REVIEW REQUIRED / NO-GOの判断理由をEvidenceへたどれることを示します。Infrastructure failureや未実行checkをProduct PASSへ変換しない点も説明します。

## 2:15–2:40 — Architecture

Blazor Web / .NET API / Worker / PostgreSQL / Python Analysis / Engine Collectorの境界をArchitecture図で示します。PostgreSQLをdurable truth + coordination boundaryにし、external providerはadapter越しにEvidenceへ正規化します。

## 2:40–3:00 — Verification

最後にexact-revision Evidenceを示します。Historical benchmarkや過去のGreenをcurrent revisionへ継承せず、final SHA自身のCI / runtime verificationだけを公開時の根拠にする方針で締めます。

## Demoで言わないこと

- sample screenshot / GIFをruntime proofと呼ばない。
- GitHub-hosted E2Eだけで実Unity / Unreal runtimeをVerifiedと呼ばない。
- Historical benchmarkをcurrent revisionのperformance値として提示しない。
- CI runner unavailable / cancelledをProduct PASSと呼ばない。
