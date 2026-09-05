import { test, expect } from '@playwright/test';

async function createManualProject(page) {
  const name = `PM Branch QA ${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
  await page.goto('/projects/new');
  await page.getByTestId('manual-project-page').waitFor();
  await page.locator('#project-name').fill(name);
  await page.locator('#repository-url').fill('https://github.com/example/dip-branch-qa-fixture');
  await page.locator('#default-branch').fill('main');
  await page.getByTestId('create-project-submit').click();
  await expect(page).toHaveURL(/\/dashboard\/[0-9a-f-]{36}$/i);
  return page.url().split('/').at(-1);
}

test.describe('PM project control and branch QA GUI', () => {
  test('project control center exposes delivery-management controls', async ({ page }) => {
    const projectId = await createManualProject(page);
    await page.goto(`/dashboard/${projectId}/project-control-center`);
    await expect(page.getByTestId('project-control-center')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Project Control Center' })).toBeVisible();
    await expect(page.getByText('What requires my decision today?')).toBeVisible();
    await expect(page.getByText('Schedule & critical path')).toBeVisible();
    await expect(page.getByText('Workstreams & capacity')).toBeVisible();
    await expect(page.getByText('Blockers & risks')).toBeVisible();
    await expect(page.getByText('Decisions & actions')).toBeVisible();
    await expect(page.getByText('Scope & change requests')).toBeVisible();
    await expect(page.getByText('Dependencies, RACI & approvals')).toBeVisible();
    await expect(page.getByTestId('open-management-workspace')).toBeVisible();
  });

  test('branch quality center clearly requires a synchronized Git workspace', async ({ page }) => {
    const projectId = await createManualProject(page);
    await page.goto(`/dashboard/${projectId}/qa/branches`);
    await expect(page.getByTestId('branch-quality-center')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Branch Quality Center' })).toBeVisible();
    await expect(page.getByText('A synchronized repository workspace is required.')).toBeVisible();
    await expect(page.getByRole('link', { name: 'Open Repository' })).toBeVisible();
  });
});
