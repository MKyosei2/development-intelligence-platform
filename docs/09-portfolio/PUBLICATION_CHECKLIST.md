# Portfolio公開Checklist

公開は段階的に行う: **source freeze → exact revision CI → local exact-revision QA / benchmark → Project Truth scenario → final Evidence binding → release**。Historical output、runner未実行、sample previewをcurrent revision PASSへ読み替えない。

## 1. Source freeze

主要変更はreview可能なbranchからmainへ入れる。publication SHAを確定したら、Evidence取得が完了するまでsourceを変更しない。

確認:

```powershell
git switch main
git pull --ff-only origin main
git status --short
git rev-parse HEAD
```

tracked working treeがcleanで、`HEAD == origin/main`であること。

## 2. Exact-revision CI Gate

Publication SHAについて次のreal execution成功を要求する。

1. `Commit Message`
2. `.NET CI`
3. `Python CI`
4. `Native Collector CI`
5. `Integration and E2E Tests`
6. `GUI E2E`
7. `Native Package Build`
8. `Security Scan`
9. `Universal QA Benchmark`
10. `Unreal Collector Contract CI`
11. `Portfolio Publication Readiness`

確認:

```powershell
$sha = (git rev-parse HEAD).Trim()
python tools/verify_final_ci_matrix.py --sha $sha --wait-minutes 60
```

`missing / cancelled / skipped / infrastructure-blocked`はPASSではない。11/11 Greenのみ次段階へ進める。

## 3. QA Bug Zoo

CSharp / Cpp / Pythonを必須言語としてexact SHAへbindする。

```powershell
dotnet run `
  --project tools/Dip.QaBugZooVerifier/Dip.QaBugZooVerifier.csproj `
  -c Release -- `
  --root . `
  --output artifacts/qa-bug-zoo `
  --expected-sha $sha `
  --require-languages CSharp,Cpp,Python
```

必須条件:

- `sourceCommit == publication SHA`
- tracked tree clean
- `passed == true`
- `blockingInfrastructureFailures == 0`
- required language missingなし
- expected bugを検出
- clean controlでfalse positiveなし
- reproduction対象が再現可能

Windows Application Controlがfixture DLLを拒否した場合、C# runtime-only stepはverifier runtime probeへ切り替えることがある。その場合はEvidenceの`hostPolicyFallback`を確認し、fallback自体がexpected finding / timeoutを再現できなければPASSにしない。

## 4. Current exact-revision performance / Soak

```powershell
$sha = (git rev-parse HEAD).Trim()
.\scripts\Run-PortfolioBenchmarks.ps1 -ExpectedSha $sha
```

必須条件:

- working tree clean
- `sourceCommit == publication SHA`
- `sourceWorkingTreeDirty == false`
- 全benchmark / soak gate PASS

Canonical artifact:

```text
artifacts/portfolio-benchmark/portfolio-benchmark-results.json
artifacts/portfolio-benchmark/portfolio-benchmark-results.sha256
```

## 5. QA -> Project Truth expected-failure scenario

DIP runtimeを起動し、deterministic Critical QA findingを投入する。

```powershell
python tools/publish_qa_scenario_evidence.py `
  --api-base http://127.0.0.1:5080 `
  --expected-dip-sha $sha `
  --output artifacts/qa-project-truth
```

期待するproduct-side outcome:

```text
Quality Gate = Failed
Complete / 100% claim = Contradicted
Verified progress < Reported progress
contradictionCount >= 1
```

Scenario verifierの`status=PASS`は、DIPがこのexpected failureを正しく扱えたことを意味する。

## 6. Final Evidence binding

```powershell
python tools/finalize_portfolio_evidence.py `
  --sha $sha `
  --ci artifacts/final-evidence/ci-matrix.json `
  --benchmark artifacts/portfolio-benchmark/portfolio-benchmark-results.json `
  --qa-bug-zoo artifacts/qa-bug-zoo/qa-bug-zoo-results.json `
  --project-truth artifacts/qa-project-truth/project-truth-evidence.json `
  --output-root artifacts/final-evidence
```

Finalizerは4系統が同じDIP SHAでなければ失敗する。

## 7. 一括実行

```powershell
.\scripts\Complete-PortfolioRelease.ps1 -Version <x.y.z>
```

Releaseまで進める場合:

```powershell
.\scripts\Complete-PortfolioRelease.ps1 -Version <x.y.z> -PublishRelease
```

## 8. Engine-specific optional evidence

実Unity / Unreal target projectを用意できる場合のみoptional verifierを別実行する。これらの未実行はFinal mandatory gateを失敗させない代わりに、engine runtimeをVerifiedとも表現しない。

未評価時:

```text
Unity engine runtime  = NOT_EVALUATED
Unreal engine runtime = NOT_EVALUATED
```

## 9. Demo

README GIFはsample-data previewとして扱う。2〜3分のportfolio demoは[`QA_EVIDENCE_DEMO.md`](QA_EVIDENCE_DEMO.md)の順に録画する。

動画が実在する前に「実演動画あり」と記載しない。

## 10. Reviewer確認

公開後、Signed-out状態で次を確認する。

- READMEで3領域が最初に伝わる。
- sample GIFとruntime Evidenceが混同されていない。
- QA Evidence Case Studyへ到達できる。
- final SHAの11/11 CIへ到達できる。
- benchmark / QA Bug Zoo / Project Truth artifactがfinal SHAと一致する。
- optional engine runtimeの未評価状態が誤ってVerified表示されていない。
- Engineering Journeyで初期履歴から現在のEvidence-first開発への変化を説明できる。
