# Latest revision evidence status

## 目的

このファイルは「Sourceが存在する」「過去RevisionがPASSした」「現在Revisionが実際に検証された」を混同しないための境界を定義します。

## Patch preparation baseline

- GitHub repository: `MKyosei2/development-intelligence-platform`
- source baseline used for this repair package: `77c65ea9e434f90da4f24824af29fff5d95e77d7`
- baseline date: 2026-08-28

> このSHAは**修正Packageを作るために読み取った基準Revision**です。修正を適用してCommitすると新しいSHAになります。その新SHAをこのファイル内へ自己参照的に固定することはしません。

## Exact revision evidenceの生成方法

Tracked Markdownへ「自分自身を含むCommit SHA」をハードコードすると、ファイル変更によってCommit SHA自体が変わるため循環します。

そのためDIPでは、Exact Revision EvidenceをGitHub Actions実行時に生成します。

`Portfolio Publication Readiness` workflowは実行中の`GITHUB_SHA`を使い、次をArtifactへ出力します。

```text
artifacts/portfolio-readiness/exact-revision.json
artifacts/portfolio-readiness/exact-revision.md
```

`GUI E2E`は以下へRuntime Evidenceを出力します。

```text
artifacts/gui-runtime-verification/<timestamp>/GUI_RUNTIME_VERIFICATION_PASS.txt
artifacts/gui-runtime-verification/<timestamp>/gui-runtime-verification.json
```

このため、README・スクリーンショット・Historical Evidenceだけを見て最新RevisionをVerifiedとは判断しません。

## 現在の判定ルール

| 状態 | 意味 |
|---|---|
| Implemented | Source Codeが存在する |
| Tested | 対象RevisionでTestが実行された |
| Runtime Verified | 対象Revisionを想定Runtimeで実行しEvidenceが生成された |
| Infrastructure Blocked | Runner / account / platform都合でVerification自体が開始できていない |

`Infrastructure Blocked`を`Failed Source`にも`PASS`にも変換しません。

## GitHub-hosted runnerについて

このPackageの基準Revisionを確認した時点では、最新`main`に対する複数Workflowが`failure`表示でした。一方、Repositoryに残っている履歴ではGitHub-hosted runner / account availabilityの問題により、checkout/build/testへ入る前にJobが終了したケースが確認されています。

したがって次のどちらも禁止します。

- zero-step runを「Sourceが壊れている証拠」と断定する
- zero-step runを無視して「最新RevisionはGreen」と記載する

Runnerが利用可能になった後、同じ新SHAに対して実Stepが走った結果を採用します。

## 修正適用後に必要なVerification

1. `dotnet restore DevelopmentIntelligencePlatform.sln`
2. `dotnet build DevelopmentIntelligencePlatform.sln -c Release --no-restore`
3. Domain / Application / Infrastructure tests
4. Integration tests
5. `scripts/Verify-GuiRuntime.ps1 -RunPlaywright`
6. `python tools/verify_publication_safety.py --root .`
7. `python tools/validate_portfolio_readiness.py --root .`
8. Conventional Commitでpush
9. 新SHAの`.NET CI / Integration and E2E / Docker Build / Security Scan / GUI E2E / Publication Readiness`が実際にStepを実行したことを確認
10. 全ての必要Evidenceが揃った後にのみRelease / Portfolio Verifiedとする

## Evidence inheritance policy

Historical Evidenceは比較・回帰確認のBaselineとして利用できます。ただし、新しいCommitへVerification Stateを自動継承しません。
