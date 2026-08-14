# GUI / Startup Fullization — 2026-08-15

This source tree integrates the GUI/startup fixes into the existing Development Intelligence Platform repository structure.

## Completed

- Working `Start-DIP.cmd`, `Stop-DIP.cmd`, `Status-DIP.cmd`, and `Verify-GUI.cmd` launchers.
- Dockerfiles for Analysis, API, Worker, and Web with a single Compose startup path.
- Blazor Interactive Server mode so `@onclick`, forms, refresh, approvals, and Governance actions are interactive.
- Full application shell: sidebar, top navigation, project picker, portfolio, project creation, settings, and System Status.
- Offline local CSS design system; no Bootstrap CDN dependency is required.
- GUI Project creation flow.
- System Status API/UI for API, PostgreSQL, Redis, Analysis, Worker, artifact storage, QA sandbox, and GitHub configuration.
- GitHub OAuth -> API JWT -> protected HttpOnly Web session handoff.
- Authenticated artifact and management workbook downloads through a Web-side streaming proxy; API tokens are not placed in browser URLs/localStorage.
- Consistent default local ports: Web 5081, API 5080, Analysis 8100, Worker metrics 9091.
- Restored current Python Analysis application modules, tests, requirements, development helpers, and standalone Docker support.
- Isolated `Verify-GuiRuntime.ps1` real Docker E2E test; it does not reuse or mutate the normal DIP database volumes.
- Formal portfolio verification now requires the GUI runtime verification phase.

## Verification boundary

See `artifacts/gui-generation-validation/GUI_GENERATION_VALIDATION.md`. Generation-environment checks are not a substitute for the real Windows/Docker/.NET/Unity exact-revision verification.
