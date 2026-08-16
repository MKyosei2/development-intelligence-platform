# Defect Intelligence / Universal QA expansion — 2026-08-16

## Pipeline

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

## Language policy

Python, C# and C++ retain deep specialized adapters. Unity remains engine-aware. The Deep Language Pack adds native capability probes for common languages. Unknown/proprietary languages use `.dip/qa.json`; the manifest invokes an executable plus an argument array and never concatenates a shell command. Fuzz/property/race tools therefore extend to proprietary languages without adding a Domain enum.

## Universal semantic layer

`UniversalSyntaxTree` is a dependency-free structural fallback, not a replacement for a compiler AST. `UniversalSemanticAnalyzer` performs bounded high-signal checks such as local source-to-dangerous-sink flows, hard-coded secrets, dynamic SQL construction, empty catches and sync-over-async/nested-lock warnings. Findings record rule IDs and source coordinates.

## Runtime and external events

Sentry and OpenTelemetry payloads are normalized into `RuntimeDefectObservation`; crash/memory/timeout/race/deadlock signals receive a stable fingerprint and feed the existing Incident layer. The anonymous ingest endpoint is disabled until an ingest key is configured and compares the key in fixed time.

## Verified fix boundary

The fix agent is disabled by default. It must cite supplied Evidence GUIDs. A proposal does not edit the source workspace. `VerifiedFixService` creates an isolated Git worktree/copy, applies a standard unified diff, writes bounded regression-test drafts, detects the project technology and runs QA adapters. Only a successful executable validation with no Critical finding becomes `Verified`.

## Game QA

Unity: serialized GUID/reference checks, Addressables presence, shader diagnostics, Update allocation heuristics and save-version review alongside existing Build/Animation/Scene/Performance evidence. Unreal: project/asset/automation/crash evidence plus Blueprint, packaging, shader/RHI and network log classifications.

## API / DB / distributed system QA

`.dip/system-qa.json` is opt-in. HTTP probes require localhost or an explicit host allowlist. Database credentials are referenced by environment-variable name; the built-in PostgreSQL probe accepts only `SELECT`. This avoids committing secrets or turning repository configuration into a generic network scanner.

## CI and measurement

`universal-qa-benchmark.yml` and `tools/universal_qa_benchmark.py` provide measured native-toolchain bootstrap evidence. Skipped toolchains remain skipped. Formal exact-revision .NET/Unity evidence still requires the existing verification scripts and a runner containing those SDKs.
