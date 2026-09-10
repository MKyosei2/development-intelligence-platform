# Portfolio公開Checklist

公開は段階的に行う: **source freeze → exact revision CI → performance → real engine runtime → Project Truth → release**。Historical output、runner未実行、sample previewをcurrent revision PASSへ読み替えない。

## 1. Source freeze

主要変更は`work/*`からPRでmainへ入れる。mainが安定した後、final verification専用branchを作成し、`.portfolio-ci-trigger`だけを変更する。

```powershell
git switch main
git pull --ff-only origin main
git switch -c ci/final-portfolio-verification
.\scripts\Request-FinalVerification.ps1 -Commit -CreatePullRequest
```

このPRをmergeした結果のmain SHAがpublication SHAになる。以後、Evidence取得が完了するまでsourceを変更しない。

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
10. `Unreal Collector CI`
11. `Portfolio Publication Readiness`

確認:

```powershell
$sha = (git rev-parse HEAD).Trim()
python tools/verify_final_ci_matrix.py --sha $sha --wait-minutes 60
```

`missing / cancelled / skipped / infrastructure-blocked`はPASSではない。11/11 Greenのみ次段階へ進める。

## 3. Current exact-revision performance

```powershell
$sha = (git rev-parse HEAD).Trim()
.\scripts\Run-PortfolioBenchmarks.ps1 -ExpectedSha $sha
```

必須条件:

- working tree clean
- `sourceCommit == publication SHA`
- `sourceWorkingTreeDirty == false`
- 全benchmark gate PASS

Canonical artifact:

```text
artifacts/portfolio-benchmark/portfolio-benchmark-results.json
artifacts/portfolio-benchmark/portfolio-benchmark-results.sha256
```

## 4. Real Unity Project Evidence

Default Case StudyはMAYAtoUnity。

```powershell
python tools/verify_unity_real_project.py --expected-dip-sha $sha
```

EvidenceはDIP revisionとtarget Project revisionを分離して記録する。

```text
dipSourceCommit = publication SHA
targetRepository = MAYAtoUnity
targetCommit = analyzed MAYAtoUnity SHA
runtimeVerified = true
```

Sample Unity fixtureだけではこのgateを満たさない。

## 5. Real Unreal Project Evidence

```powershell
python tools/verify_unreal_real_project.py `
  --expected-dip-sha $sha `
  --project <Git-backed Unreal Project> `
  --unreal-editor-cmd <UnrealEditor-Cmd.exe>
```

必須条件:

- actual `.uproject`
- Git-backed target revision
- actual `UnrealEditor-Cmd`
- `engineNativeAutomation.attempted == true`
- Automation reportが存在
- `dipSourceCommit == publication SHA`

生成fixtureをreal-project claimの代替にはしない。

## 6. Real Project -> DIP -> Project Truth

DIP runtimeを起動し、実Unity Collector reportをDIP APIへ投入する。

```powershell
python tools/publish_real_project_evidence.py `
  --engine-evidence artifacts/real-project-runtime/unity/unity-real-project-evidence.json `
  --report artifacts/real-project-runtime/unity/unity-real-project-report.json `
  --expected-dip-sha $sha
```

`project-truth-evidence.json`にcollector ingestion、claim verification、Project Truth、Quality Gateを保存する。

## 7. Final Evidence binding

```powershell
python tools/finalize_portfolio_evidence.py `
  --sha $sha `
  --project-truth artifacts/real-project-runtime/project-truth/project-truth-evidence.json
```

FinalizerはCI、benchmark、Unity、Unreal、Project Truthが同じDIP SHAへbindingされていなければ失敗する。

## 8. 一括実行

上記2〜7は次でまとめて実行できる。

```powershell
.\scripts\Complete-PortfolioRelease.ps1 `
  -Version <x.y.z> `
  -UnrealProjectPath <UnrealProject> `
  -UnrealEditorCmd <UnrealEditor-Cmd.exe>
```

Releaseまで進める場合:

```powershell
.\scripts\Complete-PortfolioRelease.ps1 `
  -Version <x.y.z> `
  -UnrealProjectPath <UnrealProject> `
  -UnrealEditorCmd <UnrealEditor-Cmd.exe> `
  -PublishRelease
```

`Release` workflow自身も`expected_sha`と11/11 Green matrixを再検証するため、CI未完了のSHAをreleaseできない。

## 9. Demo

README GIFはsample-data previewのまま扱う。2〜3分のreal-project demoは[`REAL_PROJECT_DEMO.md`](REAL_PROJECT_DEMO.md)の順に録画する。

動画が実在する前に「実演動画あり」と記載しない。公開時は`dip-real-project-demo.mp4`をRelease assetまたは動画ホスティングへ置き、READMEからリンクする。

## 10. Reviewer確認

Release後、Signed-out状態で次を確認する。

- READMEで3領域が最初に伝わる。
- sample GIFとreal runtime Evidenceが混同されていない。
- Real Project Case Studyへ到達できる。
- final SHAの11/11 CIへ到達できる。
- benchmark artifactのSHAがfinal SHAと一致する。
- Unity/Unreal runtime Evidenceの`dipSourceCommit`がfinal SHAと一致する。
- `Policy Verified`と`Production Deployed`が明確に別状態である。
- Engineering Journeyで初期履歴から現在の開発プロセスへの変化を説明できる。
