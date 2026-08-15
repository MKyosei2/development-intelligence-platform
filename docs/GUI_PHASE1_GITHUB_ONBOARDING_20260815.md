# GUI Phase 1 / GitHub Repository Onboarding — 2026-08-15

This phase completes the user-facing path from repository selection to project-scoped development views. It intentionally prioritizes GUI usability and project selection before additional language-specific QA expansion.

## User flow

```text
DIP
  -> Portfolio
  -> Import from GitHub
  -> GitHub App installation repositories
  -> Select repository
  -> Create DIP project
  -> Optional protected workspace sync
  -> Project Overview
       -> Repository
       -> CI / Builds
       -> QA
       -> Intelligence / Governance
```

## S1–S11 implementation

| Priority | Implementation |
|---|---|
| S1 | Removed stale zero-byte Web state source and kept one canonical project dashboard/navigation implementation. |
| S2 | Added scoped `ProjectState` for portfolio list, active project, refresh/error state and route synchronization. |
| S3 | Added server-side GitHub App installation authentication. App private keys never enter browser state. Read-token fallback remains available for local compatibility. |
| S4 | Added `/projects/import/github` repository picker with search, visibility filter and connection/empty/error states. |
| S5 | Added GitHub -> DIP project import using authoritative repository URL/default branch, duplicate protection and optional workspace synchronization. Initial sync failure is non-fatal and is surfaced as a warning. |
| S6 | Reworked Project Overview around project state, evidence progress, risk, attention, Repository/CI/QA pulse and recent activity. |
| S7 | Reworked project-scoped sidebar/header navigation and shared active-project selection. |
| S8 | Added Repository, CI / Builds and QA pages. QA reuses the existing QA engine against the synchronized workspace. |
| S9 | Added reusable loading/error/empty/status components. |
| S10 | Added SignalR project events for repository sync and QA lifecycle, with manual refresh fallback. |
| S11 | Added Playwright GUI E2E plus GitHub Actions workflow and Windows verification runner. |

## GitHub App configuration

Create and install a GitHub App on only the repositories DIP should inspect. Recommended read permissions for the current GUI phase:

- Repository metadata: Read
- Contents: Read
- Actions: Read
- Commit statuses: Read when required by your installation policy
- Pull requests / Issues: Read if those existing DIP integrations are used

Do not grant Contents write for repository browsing/synchronization.

Set these values in `.env` or a secret store:

```text
DIP_GITHUB_APP_ID=<app id>
DIP_GITHUB_APP_INSTALLATION_ID=<installation id>
DIP_GITHUB_APP_SLUG=<app slug>
DIP_GITHUB_APP_PRIVATE_KEY_BASE64=<base64 encoded PEM private key>
```

The API creates short-lived installation tokens. The private key remains server-side. Repository source is extracted under `Qa:WorkspaceRoot/github/<project-id>` with archive-size, extracted-size, file-count and path traversal limits.

`DIP_GITHUB_TOKEN` remains a compatibility fallback. GitHub OAuth login and GitHub App repository access are intentionally separate concerns: OAuth identifies the DIP user; the App determines which repositories DIP may inspect.

## Realtime behavior

SignalR endpoint: `/hubs/projects`

Project-scoped clients subscribe to `project:<project-id>` only after access validation when authentication is enabled. Current events include:

- `project.imported`
- `project.imported.unsynced`
- `repository.synced`
- `qa.started`
- `qa.completed`
- `qa.failed`

Realtime failure does not make the GUI unusable. Every affected page retains explicit refresh controls.

## Verification

Static/package validation:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-GuiPhase1Static.ps1
```

Runtime GUI E2E on a Windows machine with Docker Desktop and Node.js:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Verify-GuiPhase1.ps1
```

The normal Playwright suite does not require external GitHub credentials. A real App-installation lookup can be enabled separately:

```powershell
$env:DIP_E2E_GITHUB_REPOSITORY = "owner/repository"
cd tests\Dip.UiE2E
npm run test:live-github
```

## Verification boundary

The source tree contains the S1–S11 implementation and deterministic Playwright test definition. A generated ZIP is **not** proof that Docker, .NET, PostgreSQL, Redis, SignalR and a real GitHub App all passed on the target machine. Runtime PASS should only be claimed after `Verify-GuiPhase1.ps1` succeeds on the exact revision.
