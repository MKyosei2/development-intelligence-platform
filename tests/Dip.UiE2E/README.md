# DIP Playwright GUI E2E

The deterministic suite verifies the GUI without requiring external GitHub credentials:

- portfolio loads;
- GitHub repository picker renders a connected or explicitly disconnected state;
- a manual project can be created;
- project selection state follows the active project;
- Project Overview, Repository, CI and QA routes render;
- switching back to the portfolio works.

The optional `@live-github` test uses `DIP_E2E_GITHUB_REPOSITORY=owner/repo` to verify a real GitHub App installation. It is intentionally separated from normal CI.

Run against an already-started DIP stack:

```powershell
cd tests/Dip.UiE2E
npm install
npx playwright install chromium
$env:DIP_WEB_BASE_URL = "http://127.0.0.1:5081"
npm test
```
