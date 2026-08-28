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
    await expect(page.getByTestId('qa-autonomous-debug-tab')).toBeVisible();
    await page.getByTestId('qa-autonomous-debug-tab').click();
    await expect(page.getByTestId('qa-autonomous-debug-panel')).toBeVisible();
    await expect(page.getByTestId('qa-run-autonomous-debug')).toBeDisabled();

    await page.goto(`/dashboard/${projectId}/management`);
    await expect(page.getByTestId('management-operations-page')).toBeVisible();
    await page.getByRole('button', { name: 'Sync & source of truth' }).click();
    await expect(page.getByTestId('management-spreadsheet-intelligence')).toBeVisible();
    await expect(page.getByTestId('spreadsheet-analyze')).toBeDisabled();
    await expect(page.getByTestId('spreadsheet-dry-run')).toBeDisabled();
    await expect(page.getByTestId('spreadsheet-apply')).toBeDisabled();

    await page.goto(`/dashboard/${projectId}/intelligence`);
    await expect(page.getByRole('heading', { name: 'Development Intelligence' })).toBeVisible();

    await page.goto(`/dashboard/${projectId}/qa-governance`);
    await expect(page.getByRole('heading', { name: 'QA Governance' })).toBeVisible();

    await page.getByTestId('active-project-select').selectOption('');
    await expect(page).toHaveURL(/\/$/);
    await expect(page.getByTestId('portfolio-page')).toBeVisible();
  });

  test('project lifecycle, owner safety, notifications and audit are operable from existing GUI', async ({ page }) => {
    const name = uniqueProjectName();
    await page.goto('/projects/new');
    await page.locator('#project-name').fill(name);
    await page.locator('#repository-url').fill('https://github.com/example/dip-lifecycle-fixture');
    await page.locator('#default-branch').fill('main');
    await page.getByTestId('create-project-submit').click();
    await expect(page).toHaveURL(/\/dashboard\/[0-9a-f-]{36}$/i);
    const projectId = page.url().split('/').at(-1);

    await page.goto('/settings');
    await page.locator('#managed-project').selectOption(projectId);
    await expect(page.getByText('Project settings')).toBeVisible();

    const projectNameInput = page.getByText('Project name').locator('..').locator('input');
    await projectNameInput.fill(`${name} Updated`);
    await page.getByRole('button', { name: 'Save changes' }).click();
    await expect(page.getByText('Project settings saved.')).toBeVisible();

    await page.getByRole('button', { name: 'Archive project' }).click();
    await expect(page.getByText('Project archived.')).toBeVisible();
    await page.getByRole('button', { name: 'Restore project' }).click();
    await expect(page.getByText('Project restored.')).toBeVisible();

    await page.getByRole('button', { name: 'Members' }).click();
    const ownerRow = page.locator('tbody tr').filter({ hasText: 'Owner' }).first();
    await ownerRow.locator('select').selectOption('Viewer');
    await expect(page.getByText(/last project owner cannot be demoted/i)).toBeVisible();

    await page.getByRole('button', { name: 'Audit' }).click();
    await page.getByRole('button', { name: 'Search audit history' }).click();
    await expect(page.getByText('project.archived')).toBeVisible();
    await expect(page.getByText('project.restored')).toBeVisible();

    await page.getByRole('button', { name: /Notifications/ }).click();
    await expect(page.getByTestId('notification-drawer')).toBeVisible();
  });
});
