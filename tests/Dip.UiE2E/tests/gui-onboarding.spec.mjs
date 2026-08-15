import { test, expect } from '@playwright/test';

function uniqueProjectName() {
  return `E2E Project ${Date.now()}`;
}

test.describe('DIP GUI onboarding', () => {
  test('portfolio and GitHub repository picker are reachable', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('portfolio-page')).toBeVisible();
    await expect(page.getByTestId('active-project-select')).toBeVisible();

    await page.getByTestId('import-github-cta').click();
    await expect(page.getByTestId('github-import-page')).toBeVisible();

    const connected = page.getByTestId('github-connected');
    const disconnected = page.getByTestId('github-disconnected');
    await expect(connected.or(disconnected)).toBeVisible();

    if (await connected.isVisible()) {
      const grid = page.getByTestId('repository-grid');
      const empty = page.getByText('No repositories found');
      await expect(grid.or(empty)).toBeVisible();
    } else {
      await expect(page.getByText('Connect a GitHub App installation')).toBeVisible();
    }
  });

  test('manual project can be created, selected and inspected across project-scoped GUI', async ({ page }) => {
    const name = uniqueProjectName();
    await page.goto('/projects/new');
    await expect(page.getByTestId('manual-project-page')).toBeVisible();

    await page.locator('#project-name').fill(name);
    await page.locator('#repository-url').fill('https://github.com/example/dip-e2e-fixture');
    await page.locator('#default-branch').fill('main');
    await page.getByTestId('create-project-submit').click();

    await expect(page).toHaveURL(/\/dashboard\/[0-9a-f-]{36}$/i);
    await expect(page.getByTestId('project-overview')).toBeVisible();
    await expect(page.getByRole('heading', { name })).toBeVisible();
    await expect(page.getByTestId('active-project-select')).toHaveValue(/^[0-9a-f-]{36}$/i);

    const projectUrl = page.url();
    const projectId = projectUrl.split('/').at(-1);

    await page.goto(`/dashboard/${projectId}/repository`);
    await expect(page.getByTestId('repository-page')).toBeVisible();

    await page.goto(`/dashboard/${projectId}/ci`);
    await expect(page.getByTestId('ci-page')).toBeVisible();

    await page.goto(`/dashboard/${projectId}/qa`);
    await expect(page.getByTestId('qa-page')).toBeVisible();
    await expect(page.getByText('Synchronize the repository first')).toBeVisible();

    await page.goto(`/dashboard/${projectId}/intelligence`);
    await expect(page.getByRole('heading', { name: 'Development Intelligence' })).toBeVisible();

    await page.goto(`/dashboard/${projectId}/qa-governance`);
    await expect(page.getByRole('heading', { name: 'QA Governance' })).toBeVisible();

    await page.getByTestId('active-project-select').selectOption('');
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByTestId('portfolio-page')).toBeVisible();
  });
});
