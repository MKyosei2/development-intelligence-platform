# Project Truth Policy（Project Truth方針）

## 目的

`ProjectTruthEvaluator`と`ProjectRiskEvaluator`はdeterministicなdecision-support componentであり、reported progressとobserved Evidenceの関係を明示し、説明可能にする。

以前は複数のnumeric threshold/penaltyをEvaluator codeへ直接埋め込んでいた。現在は`ProjectTruthPolicy`へ集約し、decision ruleを可視化・review・test・replaceできるようにしている。

## 既定Policy

Default policyはhistorical behaviorとの互換性を意図的に維持する。

| Setting | Default | 意味 |
|---|---:|---|
| Critical blocker health penalty | 25 | Critical blocker 1件あたりのHealth Score減点 |
| Blocker health penalty | 12 | Blocker 1件あたりのHealth Score減点 |
| Contradiction health penalty | 10 | Contradicted Claim 1件あたりのHealth Score減点 |
| Unverified claim penalty | 3 | Claimごとの減点。別途上限あり |
| Max unverified-claim penalty | 12 | 無制限な累積を防ぐ上限 |
| Progress-gap penalty factor | 0.25 | Reported vs Verifiedの1 percentage point差あたりの減点 |
| Max progress-gap penalty | 20 | Progress gapによるHealth impact上限 |
| High-risk contradiction threshold | 2 | Contradiction 2件以上でHigh risk |
| Medium-risk progress gap | 20% | 大きなReported vs Verified gapでMedium risk |
| Near-completion reported progress | 80% | Reported completion付近ではUnverified Claimをより重視 |
| Low-confidence risk boundary | Low | Low/Unverified Evidenceでは最低でもMedium risk |

これらは**compatibility default**であり、20%、25 pointsなどが普遍的に最適だと主張するものではない。

## MLではなくDeterministic Policyを使う理由

現在のportfolio scopeではdeterministic policyに次の利点がある。

- 各decisionをEvidenceとvisible ruleへtraceできる。
- Run間で挙動を再現できる。
- Learned modelを正当化できるだけのbiasedでないlabeled production dataがない。
- Reviewerがunit testからboundary behaviorを直接確認できる。
- Retrainingやopaque confidence claimなしにoperatorがpolicyを調整できる。

将来learned modelでriskのrank/recommendを行うことは可能だが、training data、drift monitoring、validationがない状態でEvidence/provenance boundaryを置き換えるべきではない。

## Real Production DataでのCalibration Procedure

Production deploymentではdefault valueを永久に受け入れるのではなく、次のようなdefensible calibrationを行う。

1. Immutable Evidenceと実際の後続outcomeを持つhistorical release/project snapshotを収集。
2. Tune対象policyから独立したlabel（例: `safe`、`review-required`、`release-blocking`）を定義。
3. Near-duplicate revisionがcalibrationとvalidationへ漏れないようProject/timeでsplit。
4. Missed blockerの方がunnecessary reviewより高コストならfalse negativeを重くしたcost functionでcandidate threshold/penaltyを評価。
5. Aggregate accuracyだけでなくconfusion matrix、precision/recall、false-positive rate、boundary sensitivityを報告。
6. Policyを選びversionを付与し、calibrationに使ったdataset/evidence revisionを記録。
7. Workflow/tooling変更や大きなevidence-distribution drift後に再評価。

## Test境界

`ProjectTruthPolicyTests`では次を検証する。

- default policyが既存20% progress-gap boundaryを維持。
- 別thresholdでEvaluator codeを変えずにbehaviorが変わる。
- contradiction thresholdがconfigurable。
- invalid policy valueがfail fast。

これらはimplementation behaviorを証明するが、default policyがempirically optimalであることは証明しない。その主張には上記calibration processとreal labeled datasetが必要である。

## 運用ルール

Policy changeはcode changeと同様にreviewする。

- 変更理由を記述。
- 可能ならcalibration/validation Evidenceを添付。
- 想定false-positive/false-negative impactを明示。
- 再現性のためprevious policy versionを保持。
- 新policyを過去に正しかったよう見せるためhistorical Evidenceを書き換えない。
