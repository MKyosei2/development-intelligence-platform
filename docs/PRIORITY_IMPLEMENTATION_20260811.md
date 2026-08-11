# Priority implementation package — 2026-08-11

Base repository inspected: `MKyosei2/development-intelligence-platform` (`main`, observed SHA `5f1dc31c94f58497bb51fbe250b6e0374e98b45e`).

## Scope

This package deliberately does **not** introduce the final Universal/unknown-language QA adapter layer. The priority work completed here is:

1. fail-closed P0/P1/P2 verification orchestration;
2. QA Bug Zoo evidence and measurable QA effectiveness;
3. completion of the previously empty Unity QA analyzers/runtime probe;
4. preservation/reconstruction of the existing GitHub -> Redis/Outbox -> Worker -> Quality Gate execution path needed by the verification flow.

## QA Bug Zoo

`tools/Dip.QaBugZooVerifier` invokes the real `PythonQaEngine`, `CSharpQaEngine`, and `CppQaEngine` against cases under `samples/qa-bug-zoo`.

Evidence produced:

- detection recall;
- clean-control false-positive rate;
- repeated fingerprint reproduction rate;
- infrastructure failures / platform skips;
- per-case tool results and durations.

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Invoke-QaBugZoo.ps1
```

## Unity QA 0.2.0

Implemented in the existing Unity package:

- Build Settings validation;
- AnimationClip validation;
- Scene missing-script / serialized-reference checks;
- runtime PerformanceProbe with frame-time percentiles, managed memory and GC evidence;
- integration into the existing `UnityCollectorCli` report;
- EditMode/Runtime test assemblies and tests.

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-UnityCollector.ps1 -UnityPath "<Unity.exe>"
```

## Completion gate

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-PortfolioReadiness.ps1 -UnityPath "<Unity.exe>"
```

The readiness script is fail-closed. A complete PASS requires the P0 completion artifact, P1 local evidence + Bug Zoo evidence, P2 PASS evidence, and Unity verification evidence.

## Verification limitation of this generated package

The generation environment used for this ZIP does not provide the .NET SDK or Unity Editor. Therefore this package is **source-implemented and statically validated**, but it is not presented as Windows/.NET/Unity runtime-verified. Run the commands above on the target Windows development machine before declaring the portfolio state verified.
