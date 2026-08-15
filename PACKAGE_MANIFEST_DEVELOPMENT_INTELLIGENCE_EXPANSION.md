# Development Intelligence Expansion package manifest

- Repository: `MKyosei2/development-intelligence-platform`
- Base branch: `main`
- Observed base: `3e43dcce11dc5bf67a7918acd2d55d912e41c97a` (`Upgrade8`)
- Requested expansion features: 12 / 12 implemented in source
- Files in working tree before package metadata: 540
- Added versus prior durable package: 69
- Modified versus prior durable package: 36
- Deleted versus prior durable package: 0

## Verification performed in packaging environment

- Static integration validation: PASS (43/43 checks).
- Structured files: JSON/XML/YAML parse PASS.
- Solution references: 13/13 present.
- EF `DbSet` and ModelSnapshot entities: 46/46 equal.
- Unreal collector: 3/3 tests PASS.
- Python compileall: PASS.
- Native collector CMake build + CTest: PASS (1/1).

## Runtime evidence not available in packaging environment

The packaging environment has no .NET 8 SDK, PowerShell, Docker, Unity Editor/Player, Unreal Engine, or real-device hardware harness. It therefore does **not** claim a .NET build/test pass, live EF migration, Redis/MinIO/OTLP round trip, Unity replay run, Unreal runtime/Insights run, or hardware backtest measurement. Run `scripts/Verify-DevelopmentIntelligenceExpansion.ps1` and engine/device-specific verification on the target environment.

Universal/unknown-language QA remains intentionally deferred to the final architectural phase.
