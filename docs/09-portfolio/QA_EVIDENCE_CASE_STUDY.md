# QA Evidence Case Study — Critical Finding → Project Truth

## 目的

DIPのFinal portfolio scenarioは、production障害を発見した実績を装うものではありません。deterministicなCritical QA findingを意図的に投入し、Project TruthがunsupportedなComplete claimを拒否できるかを検証します。

## Scenario

```text
1. DIP projectを作成
2. Critical native/Universal-QA findingを投入
3. Collector ingestion後のQuality GateがFailedであることを確認
4. Complete / 100% claimを作成
5. Project Truth dashboardを取得
6. ClaimがContradictedであることを確認
7. Verified progress < Reported progressを確認
8. contradictionCount >= 1を確認
```

## なぜexpected failureなのか

Critical findingがあるのにQuality GateがPassedならDIP側の失敗です。

期待値は次です。

```text
Product quality gate: Failed
Scenario verifier:    PASS
```

Scenario verifierのPASSは、「DIPが失敗を隠さず、completion claimを否定した」ことを意味します。

## Revision boundary

Evidenceは必ずDIP current SHAを持ちます。

```text
dipSourceCommit = <40-char DIP SHA>
status = PASS
scenario = native-critical-contradicts-complete-claim
```

`tools/finalize_portfolio_evidence.py`はProject Truth evidenceのSHAがCI / benchmark / QA Bug Zooと一致しなければfail closedします。

## 実行

DIP runtime起動後:

```powershell
$sha = (git rev-parse HEAD).Trim()

python tools/publish_qa_scenario_evidence.py `
  --api-base http://127.0.0.1:5080 `
  --expected-dip-sha $sha `
  --output artifacts/qa-project-truth
```

Canonical artifact:

```text
artifacts/qa-project-truth/project-truth-evidence.json
```

## Reviewerが見るポイント

- synthetic fixtureであることを隠していない。
- Critical findingがQuality GateをFailedへ落とす。
- Complete / 100%というClaimとEvidenceを別modelで扱う。
- Project TruthがContradictionを記録する。
- 同一SHAでなければFinal Evidenceへ昇格しない。
- AI出力ではなくdeterministic Evidenceとassertionで検証する。
