# Project Truth P0 overlay

Base repository: `MKyosei2/development-intelligence-platform`

Inspected base revision: `c6bca543db8faf814392977324d7be031960106e`

This archive preserves the repository-relative paths of every file added or
modified for Project Truth P0. Each source file in this archive contains the
full post-change file content, not a patch fragment.

## Apply

1. Start from the inspected `main` revision (or a compatible newer revision).
2. Extract the `development-intelligence-platform` directory over the repository
   root, allowing files with the same path to be replaced.
3. Review the changed files before committing.
4. Run the repository's normal verification on Windows:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-Solution.ps1"
```

The P0 archive itself must not be treated as verified completion until the
repository's build/test/CI evidence is green.

## P0 scope

- Evidence / Claim / Claim-Evidence relation
- Evidence Confidence
- Reported vs evidence-backed progress
- Risk and Blocker evaluation
- Project Truth snapshots
- PM-first dashboard
- Report vs Reality
- Evidence Timeline
- Claim API and dashboard submission form
- Collector and GitHub evidence ingestion
- Existing no-migrations PostgreSQL schema bootstrap
- Domain/Application tests and Project Truth E2E scenarios

QA remains one Evidence Provider. The Project Truth layer is the system's core
for PM / production-management decisions.
