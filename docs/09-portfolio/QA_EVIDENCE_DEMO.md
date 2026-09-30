# QA Evidence Demo — 2〜3分構成

## 0:00–0:25 — 課題

ゲーム制作ではGit、CI、QA、runtime、performance、進捗情報が分散します。DIPはそれらをrevision-bound Evidenceへ集約し、ClaimとEvidenceを分離します。

## 0:25–0:55 — Exact revision

Terminalで次を表示します。

```powershell
git status --short
git rev-parse HEAD
```

clean treeとcurrent SHAを見せ、Historical PASSを現在revisionへ流用しないことを説明します。

## 0:55–1:25 — QA Evidence

QA Bug Zoo / QA画面を示し、Finding、severity、reproduction、infrastructure failureを分離していることを説明します。

Critical scenarioはdeterministic fixtureであることを明示します。

## 1:25–2:05 — Project Truth

Critical findingを投入したProjectで次を見せます。

```text
Quality Gate = Failed
Reported progress = 100
Verified progress < 100
Complete claim = Contradicted
Contradiction recorded
```

「Scenario PASS = product quality PASS」ではないことを説明します。

## 2:05–2:35 — Final Evidence

`artifacts/final-evidence/<SHA>/FINAL_EVIDENCE_SUMMARY.json`を開きます。

確認項目:

```text
sourceCommit             = current SHA
Required CI              = 11/11 Green
Benchmark                 = PASS
QA Bug Zoo                = PASS
Project Truth Scenario    = PASS
Unity engine runtime      = NOT_EVALUATED またはoptional evidence
Unreal engine runtime     = NOT_EVALUATED またはoptional evidence
```

## 2:35–3:00 — まとめ

DIPの価値を「機能数」ではなく次で説明します。

- exact-revision verification
- Claim / Evidence separation
- fail-closed QA
- Product Failure / Infrastructure Failure separation
- Project Truth contradiction handling
- reproducible release Evidence
