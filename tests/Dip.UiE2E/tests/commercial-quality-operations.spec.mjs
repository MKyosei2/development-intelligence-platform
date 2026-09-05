import { test, expect } from '@playwright/test';

async function createManualProject(page) {
  const name = `Commercial QA ${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
  await page.goto('/projects/new');
  await expect(page.getByTestId('manual-project-page')).toBeVisible();
  await page.locator('#project-name').fill(name);
  await page.locator('#repository-url').fill('https://github.com/example/dip-commercial-qa-fixture');
  await page.locator('#default-branch').fill('main');
  await page.getByTestId('create-project-submit').click();
  await expect(page).toHaveURL(/\/dashboard\/[0-9a-f-]{36}$/i);
  return page.url().split('/').at(-1);
}

test.describe('Commercial QA / PM command center', () => {
  test('new project renders release gate, PM brief, operations lanes and run history', async ({ page }) => {
    const projectId = await createManualProject(page);
    await page.goto(`/dashboard/${projectId}/quality-operations`);

    await expect(page.getByTestId('quality-operations-page')).toBeVisible();
    await expect(page.getByTestId('commercial-release-gate')).toBeVisible();
    await expect(page.getByTestId('pm-release-decision-brief')).toBeVisible();
    await expect(page.getByTestId('verification-coverage')).toBeVisible();
    await expect(page.getByTestId('quality-operation-lanes')).toBeVisible();
    await expect(page.getByTestId('commercial-triage-queue')).toBeVisible();
    await expect(page.getByTestId('commercial-run-history')).toBeVisible();
    await expect(page.getByText('NO CURRENT VERIFICATION')).toBeVisible();
  });

  test('management cockpit exposes the quality command center entry point', async ({ page }) => {
    const projectId = await createManualProject(page);
    await page.goto(`/dashboard/${projectId}/management`);
    await expect(page.getByTestId('management-operations-page')).toBeVisible();
    const link = page.getByRole('link', { name: 'Open Quality Command Center' });
    await expect(link).toBeVisible();
    await link.click();
    await expect(page).toHaveURL(new RegExp(`/dashboard/${projectId}/quality-operations$`));
    await expect(page.getByTestId('quality-operations-page')).toBeVisible();
  });
});
