# Portfolio hardening package changelog

This package starts from GitHub `main` base SHA `bc3bd304fd9389f1ccd10a35d133924a3e7ceefb` / the latest full source snapshot and changes only portfolio-readiness concerns.

## P0

- CI: preserved fail-closed checks, added `Portfolio Publication Readiness`, and documented that zero-step runner-start failures are infrastructure-unavailable rather than source PASS.
- Publication security: added local source scanner + current-SHA workflow gate + guarded `gh repo edit --visibility public`.
- README: replaced the feature-log-first README with a concise reviewer-first overview.
- Demo: added three deterministic sample-data screenshots and an animated GIF, clearly separated from runtime evidence.
- Evidence: added immutable historical benchmark/QA/Windows artifacts with commit/artifact provenance and a latest-revision status document.

## P1

- Empty Razor pages: none remain in the package.
- Management Operations: split the monolithic page into `.razor`, `.razor.cs`, `ManagementCockpitOverview`, and `ManagementSectionTabs`.
- Release: added guarded `v1.0.0-portfolio` release notes, publication scripts and a release workflow that builds/tests, hashes artifacts and publishes only after safety gates.
- Benchmark/QA: preserved measured values from actual GitHub Actions artifacts instead of placeholders/TBD claims.

## P2

- Commit history quality: added `.gitmessage`, PR-time Conventional Commit validation, and contributing guidance. Historical commits are not rewritten.
- Architecture: added a reviewer-facing SVG and architecture overview.

## Validation performed while packaging

- publication safety source scan: PASS (0 high-severity findings)
- portfolio static validator: PASS
- Management Operations static contract: 86/86 PASS
- Project Intelligence static contract: 89/89 PASS
- Python analysis service: 6/6 tests PASS
- Native collector Linux build + CTest: 1/1 PASS
- workflow YAML parsing: PASS
- screenshots/GIF/SVG asset validation: PASS

.NET, Docker and PowerShell runtime verification could not be executed in the packaging environment because those runtimes are not installed. The package intentionally leaves those as exact-revision GitHub/Windows gates rather than fabricating green evidence.
