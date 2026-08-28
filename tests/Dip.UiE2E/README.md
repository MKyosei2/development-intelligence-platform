# DIP Playwright GUI E2E

The deterministic local suite verifies normal GUI onboarding without requiring external GitHub credentials.

The hosted public-browser suite verifies the production portfolio URL itself:

- HTTPS/API health is reachable;
- the Portfolio page renders without login;
- the UI clearly identifies the live read-only public view;
- project reads succeed anonymously;
- anonymous project creation is rejected with 401/403;
- a published project can be opened when one exists.

Run the hosted check against an already deployed DIP URL:

```bash
DIP_WEB_BASE_URL=https://your-host.example npm run test:public
```

`Live Browser Deployment` runs this suite automatically after deploying the exact Git commit. A failed browser check fails the deployment job and triggers the previous image-set rollback path.

The optional `@live-github` test remains separate because it exercises authenticated GitHub App integration rather than the anonymous reviewer path.
