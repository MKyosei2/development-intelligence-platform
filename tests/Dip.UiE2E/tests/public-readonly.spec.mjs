import { test, expect } from '@playwright/test';

test('hosted portfolio is browser-accessible, read-only, and keeps internal read surfaces private', async ({ page, request }) => {
  const health = await request.get('/healthz');
  expect(health.ok()).toBeTruthy();

  const response = await page.goto('/');
  expect(response?.ok()).toBeTruthy();

  await expect(page.getByTestId('portfolio-page')).toBeVisible();
  await expect(page.getByTestId('public-readonly-banner')).toBeVisible();
  await expect(page.getByTestId('public-readonly-sidebar')).toBeVisible();
  await expect(page.getByTestId('import-github-cta')).toHaveCount(0);

  const projectsResponse = await request.get('/api/projects');
  expect(projectsResponse.ok()).toBeTruthy();
  const projects = await projectsResponse.json();
  expect(Array.isArray(projects)).toBeTruthy();

  const writeResponse = await request.post('/api/projects', {
    data: {
      name: 'unauthorized-public-write',
      repositoryUrl: 'https://github.com/example/blocked',
      defaultBranch: 'main',
      releaseDateUtc: null
    }
  });
  expect([401, 403]).toContain(writeResponse.status());

  // ProjectRead is public only for GET/HEAD. A read-policy POST must not become
  // anonymously executable, because it can consume CPU, parse uploads, or touch integrations.
  if (projects.length > 0) {
    const analyzeResponse = await request.post(
      `/api/v1/project-control/projects/${projects[0].id}/management.xlsx/analyze`
    );
    expect([401, 403]).toContain(analyzeResponse.status());
  }

  // Repository credentials/capabilities and detailed infrastructure status are
  // internal surfaces even when the portfolio read model is public.
  for (const internalPath of [
    '/api/v1/integrations/github/status',
    '/api/v1/system/status'
  ]) {
    const internalResponse = await request.get(internalPath);
    expect([401, 403]).toContain(internalResponse.status());
  }

  const cards = page.getByTestId('project-card');
  if (await cards.count() > 0) {
    await cards.first().click();
    await expect(page).toHaveURL(/\/dashboard\/[0-9a-f-]{36}(?:\/.*)?$/i);
    await expect(page.getByTestId('public-readonly-banner')).toBeVisible();
  }
});
