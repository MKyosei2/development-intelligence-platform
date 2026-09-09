# Game Development Scenario

この文書は、DIPをゲーム制作向けポートフォリオとして読む際の代表scenarioを、**Unity / Unreal → QA → Evidence → Project Truth → Quality Gate**の一本の経路で説明します。

## 1. Scenario

Release candidateのUnity Projectで、Scene内にmissing referenceが残ったとします。制作側では「実装完了」と報告されていても、そのbuildをそのままrelease-readyと扱うべきではありません。

```text
Unity Project
  ↓
Unity Collector / Engine-aware QA
  ↓
Critical Finding + provenance
  ↓
DIP Collector ingestion
  ↓
Project Evidence
  ↓
Project Truth / Contradiction
  ↓
Quality Gate: Failed / Review Required
  ↓
Engineer / TA / QA / Lead / PMが同じEvidenceを確認
```

## 2. Unity側で収集するEvidence

Unity CollectorはUnity 2022.3+向けUPM integrationとして、主に次を同一Reportへ統合します。

- Texture / Mesh / MaterialなどのAsset QA
- Build Settings
- Scene / Reference
- Animation
- Runtime Performance

Engine固有detailは失わず、DIPへ送るときにsource / rule / severity / revisionへ結び付けます。

## 3. DIP側の扱い

DIPはCollectorのFindingを「真実そのもの」としてhard-codeしません。Project Scopeとprovenanceを保持したEvidenceとして永続化し、Claim、Quality Gate、Evidence Confidence、Risk、Verified Progressなどのdecision modelへ入力します。

Critical Evidenceが完了Claimと矛盾する場合、Project Truthはそのcontradictionを説明可能な形で表示します。Lead / PMは単なる赤いstatusではなく、どのprovider / rule / revisionが判断根拠になったかを追跡できます。

## 4. Release Gate

Quality Gateはcritical findingを無視してGOへ進めるのではなく、Failed / Review Requiredとして理由を残します。Infrastructure FailureとProduct Failureを区別し、runner unavailableや未実行testを「製品品質PASS」へ変換しません。

## 5. Unreal / Native toolchain

UnrealやC++ / native toolchainはprovider-specific payloadを持ちます。DIPはそれをUnity schemaへ変換するのではなく、stableなEvidence contractへ必要項目だけをnormalizationします。これによりProject Truth / Quality Gateはengine固有実装へ直接依存せず、新しいproviderを追加できます。

## 6. 自動E2Eで検証する境界

`tests/Dip.EndToEndTests/Scenarios/GameDevelopmentEvidenceScenario.cs`は、実Unity EditorをGitHub-hosted runnerへ追加せず、**Unity Collector後に相当するnormalized critical Evidence**を既存Collector ingestionへ送り、次を自動確認します。

- Critical Unity Evidenceがingestされる
- Quality GateがFailedになる
- 完了ClaimがContradictedになる
- Verified Progressが100%のまま残らない
- Project TruthがAtRisk / Blockedへ反映される

このE2Eは「実Unity Editorを起動できた」証拠ではありません。実Unity runtime claimは`Verify-UnityCollector.ps1`を使うformal verificationへ分離します。この境界を明示することで、CIを軽量・cross-platformに保ちながらEvidence pipeline全体を検証します。

## 7. Reviewerが見るべき設計点

1. Engine integrationをProject Truthへ直接結合していない。
2. ClaimとEvidenceを別modelとして扱う。
3. Quality Gateの理由をEvidenceへtraceできる。
4. Runtime未検証をVerifiedと表示しない。
5. Unity / Unreal / native toolchainを同じProject Contextへ接続できる。

このscenarioは、DIPが「機能数の多いDashboard」ではなく、ゲーム制作の品質判断を再現可能なEvidenceへ結び付けるDevelopment Infrastructureであることを示すための中心scenarioです。
