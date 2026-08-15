# Project Truth P0 + Technical Evidence P1 cumulative overlay

Base repository: `MKyosei2/development-intelligence-platform`

Inspected base revision: `c6bca543db8faf814392977324d7be031960106e`

This archive is a **cumulative full-file overlay**. It contains every full file
added or modified by the previous Project Truth P0 work plus every full file
added or modified for P1. It does not contain patch hunks.

Because the source repository is private and this execution environment cannot
perform a network checkout, unchanged repository files are not duplicated in
this archive. Apply this archive over an existing checkout of the repository.

## Apply

1. Start from the inspected base revision or a compatible checkout.
2. Make sure local work is committed or backed up.
3. Extract the `development-intelligence-platform` directory over the
   repository root and allow same-path files to be replaced.
4. Review `P1_CHANGESET.md`.
5. On Windows, run:

```powershell
powershell -ExecutionPolicy Bypass -File ".\scripts\Build-P1-NativeTools.ps1"
powershell -ExecutionPolicy Bypass -File ".\scripts\Verify-P1.ps1"
```

6. Run the existing live E2E path with PostgreSQL, Redis, API and Worker active.

## Security

P1 can execute repository programs for QA and performance simulation. The API
only accepts workspaces and executable paths inside `Qa:WorkspaceRoot`, but a
remote/shared deployment must also enable the repository's authentication
policy (`Authentication:RequireAuthenticatedUser=true`) and restrict project
write access. Do not expose an unauthenticated local-development configuration
to an untrusted network.

## Evidence semantics

- QA is an Evidence Provider; Project Truth remains the final PM-facing layer.
- A person is never scored as truthful/untruthful. Claims are compared with
  evidence and can become Confirmed, Partial, Unverified, Contradicted or Stale.
- Hardware simulation is always marked simulated/non-real-device and is never
  promoted to `Verified` real-device evidence.
- Screen recording uses DXGI Desktop Duplication and creates H.264 MP4.
- System audio uses WASAPI loopback and is stored as a separate WAV artifact.
- Microphone capture is not automatically enabled.
- Source files existing in this archive are not proof of completion. Windows
  native execution, .NET build/tests and live E2E still have to be green.
