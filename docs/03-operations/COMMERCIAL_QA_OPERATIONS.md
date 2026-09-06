# 商用QA / PM Quality Operations

DIP Quality Operationsは、自動QAによる観測をEvidenceに基づく運用判断へ変換する。AIやheuristic outputをSource of Truthとしては扱わない。

## Operator Entry Point（運用者の入口）

- `/dashboard/{projectId}/quality-operations` — 商用Quality Operations Command Center。
- `/dashboard/{projectId}/qa/operations` — 同じcommand centerのQA向けalias。
- `/dashboard/{projectId}/qa` — run/detail/debug view。
- `/dashboard/{projectId}/qa-governance` — lifecycle、waiver、baseline、governance control。
- `/dashboard/{projectId}/management` — PM decision、action、approval、Source of Truth運用。

## Release Gateの意味

`Blocked / DO NOT PROMOTE`
: 1件以上のP0 release blockerがopen。Mechanically reproducedされたP0はblockへのconfidenceを上げるが、Evidence source自体を置き換えない。

`Hold / HOLD FOR REVIEW`
: P0 blockerはないが、verificationがstale/non-successful、SLA breach、または累積P1 riskに明示的reviewが必要。

`Ready / READY FOR HUMAN APPROVAL`
: P0 blockerがなく、最新QA Evidenceがcurrentかつsuccessful。ただし自動release approvalではなく、通常のhuman approval processを継続する。

`Unknown / NO CURRENT VERIFICATION`
: Current QA Evidenceが存在しない。UnknownをReadyへ昇格させない。

## 既定の運用Response Window

- P0: 4時間
- P1: 24時間
- P2: 72時間
- Fresh release verification: 24時間

既定値は`CommercialQaServiceLevelPolicy`へ集約し、host固有policy registrationで置き換えられる。これは運用targetであり、業界共通standardではない。

## 商用QA Evidence Chain

`Repository / Game / Plugin / Media / DCC`
→ technology + production capability detection
→ adapter + cross-cutting production QA
→ finding classification
→ optional mechanical reproduction
→ evidence + durable quality issue
→ PM decision translation
→ Project Truth + Quality Operations release gate

Skipped/unavailable tool、runtime executableなし、media baselineなし、stale verification、QA runなしをPASSとして表現しない。

## Production Integrity Coverage（本番整合性の検証範囲）

Cross-cutting Production QA Packはbounded deterministic checkとして次を含む。

- Unity asset `.meta`存在とduplicate GUID collision。
- Unreal `.uplugin` descriptor parsing/integrity。
- Mayaのmissing referenceとmachine-dependent absolute reference。
- zero-byte production asset。
- image/frame-sequence gap。
- 既存media-regression runnerを使うconfigured visual/video/audio regression。

`.dip/production.json`はproduction domain、genre/capability hint、media baseline/current comparisonを拡張する。未構成のruntime/reference checkは明示的unverifiedのまま扱う。

## PM Workflow（PM運用手順）

1. Quality Operations Command Centerを開く。
2. 最初にRelease GateとDecision Briefを読む。
3. Low-priority triageよりP0とSLA-breached laneを優先してresolve。
4. Work assignment時はowner role、reproduction status、occurrence count、evidence ageを使用。
5. Blocked releaseでは修正後に同じEvidence-producing scenarioをrerun。Scopeを外す場合はexplicit approval、rollback、residual riskを記録。
6. Waiver/baselineはQA Governance、action/approvalはManagementを使用。Command centerからriskを暗黙waive/approveしない。

## QA Workflow（QA運用手順）

1. QA画面から適切なQA pathを実行。
2. One-off warningよりmechanically reproduced failureを先に確認。
3. Code diagnosticだけでなくgame/plugin/media/DCC integrityにはproduction-capability findingを使用。
4. ArtifactとEvidence IDを対象revisionへ紐付けたまま保持。
5. Fix後にrerun。古いsuccessful Evidenceはcurrent revisionの証明にならない。

## Quality Score（品質スコア）

Command centerの`0–100`は、open P0/P1、SLA breach、recent failed QA runから作る運用prioritization scoreである。CategoricalなRelease Gateより下位の補助指標であり、Product qualityの普遍的measureとして提示してはならない。

## 検証

Revision公開前にlocalで実行:

```powershell
python tools/validate_commercial_quality_operations.py
python tools/universal_qa_benchmark.py --output artifacts/universal-qa-benchmark

dotnet build
dotnet test
```

その後、通常のDIP環境でGUI E2E/Playwrightを実行する。Verification hostに存在しないtoolchainはSkippedのままであり、Passingと説明してはならない。
