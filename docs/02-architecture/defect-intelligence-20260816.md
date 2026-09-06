# Defect Intelligence / Universal QA 拡張 — 2026-08-16

## パイプライン

```text
RepositoryTechnologyDetector
 -> QaCapabilityPlanner
 -> specialized adapter / DeepLanguagePack / manifest adapter
 -> UniversalSyntaxTree + bounded DataFlow
 -> compiler/static/test/property/fuzz/race/runtime/game/system evidence
 -> UniversalDiagnosticNormalizer
 -> QaFinding + stable fingerprint
 -> RuntimeDefectIntelligence / Incident
 -> Project Truth / Release Readiness
 -> optional evidence-grounded Fix Proposal
 -> disposable worktree + regression tests + QA re-run
 -> Verified Fix (only after evidence passes)
```

## 言語対応方針

Python、C#、C++は深い専用adapterを維持し、UnityもEngine固有の知識を利用する。Deep Language Packは一般的な言語向けnative capability probeを追加する。未知・独自言語は`.dip/qa.json`を使用し、manifestは実行ファイルとargument配列を指定する方式で、shell commandの文字列連結は行わない。このため、DIP Domainのenumを増やさずに、独自言語へfuzz/property/race toolを拡張できる。

## Universal Semantic Layer（共通意味層）

`UniversalSyntaxTree`は依存関係を持たない構造解析fallbackであり、compiler ASTの代替ではない。`UniversalSemanticAnalyzer`は、local sourceから危険なsinkへのflow、hard-coded secret、動的SQL生成、空catch、sync-over-async、nested-lock warningなど、範囲を限定した高シグナル検査を行う。Findingにはrule IDとsource coordinateを記録する。

## Runtimeと外部Event

SentryとOpenTelemetryのpayloadは`RuntimeDefectObservation`へ正規化する。Crash、memory、timeout、race、deadlock signalには安定fingerprintを付与し、既存Incident layerへ接続する。Anonymous ingest endpointはingest keyが構成されるまで無効であり、key比較はfixed-timeで行う。

## Verified Fixの境界

Fix agentは既定で無効とする。提案時には提供されたEvidence GUIDを引用しなければならない。Proposalがsource workspaceを直接編集することはない。`VerifiedFixService`は隔離されたGit worktree/copyを作り、標準unified diffを適用し、範囲を限定したregression-test draftを書き出し、Project技術を検出してQA adapterを実行する。実行可能な検証が成功し、Critical findingが0件の場合にのみ`Verified`となる。

## ゲームQA

Unityでは、既存のBuild/Animation/Scene/Performance Evidenceに加え、serialized GUID/reference check、Addressables存在確認、shader diagnostic、Update allocation heuristic、save-version reviewを扱う。UnrealではProject/Asset/Automation/Crash Evidenceに加え、Blueprint、packaging、shader/RHI、network logを分類する。

## API / DB / 分散システムQA

`.dip/system-qa.json`はopt-inである。HTTP probeはlocalhostまたは明示的host allowlistだけを許可する。Database credentialは環境変数名で参照し、built-in PostgreSQL probeは`SELECT`のみ受け付ける。これによりsecretのcommitや、Repository構成を汎用network scannerとして悪用することを防ぐ。

## CIと計測

`universal-qa-benchmark.yml`と`tools/universal_qa_benchmark.py`は、native toolchainのbootstrapを実測したEvidenceを生成する。利用できないtoolchainはSkippedのまま扱う。正式なexact-revision .NET/Unity Evidenceには、引き続き既存verification scriptと対象SDKを備えたrunnerが必要である。
