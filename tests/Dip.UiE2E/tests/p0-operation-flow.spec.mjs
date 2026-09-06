import { test, expect } from '@playwright/test';

function uniqueProjectName(prefix = 'P0 Operation Flow') {
  return `${prefix} ${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
}

async function createManualProject(page) {
  const name = uniqueProjectName();
  await page.goto('/projects/new');
  await expect(page.getByTestId('manual-project-page')).toBeVisible();
  await page.locator('#project-name').fill(name);
  await page.locator('#repository-url').fill('https://github.com/example/dip-p0-fixture');
  await page.locator('#default-branch').fill('main');
  await page.getByTestId('create-project-submit').click();
  await expect(page).toHaveURL(/\/dashboard\/[0-9a-f-]{36}$/i);
  return { name, projectId: page.url().split('/').at(-1) };
}

test.describe('P0 guided project operation flow', () => {
  test('portfolio explains the six-step DIP operation model without requiring README context', async ({ page }) => {
    await page.goto('/');

    const howItWorks = page.getByTestId('how-dip-works');
    await expect(howItWorks).toBeVisible();
    await expect(howItWorks.getByRole('listitem')).toHaveCount(6);
    await expect(page.getByTestId('home-flow-connect')).toContainText('Connect');
    await expect(page.getByTestId('home-flow-collect')).toContainText('Collect');
    await expect(page.getByTestId('home-flow-verify')).toContainText('Verify');
    await expect(page.getByTestId('home-flow-understand')).toContainText('Understand');
    await expect(page.getByTestId('home-flow-decide')).toContainText('Decide');
    await expect(page.getByTestId('home-flow-monitor')).toContainText('Monitor');
    await expect(howItWorks).toContainText('Current Runtime');
    await expect(howItWorks).toContainText('Production Design Target');
  });

  test('manual project receives deterministic current state, reason and one primary next action', async ({ page }) => {
    const { projectId } = await createManualProject(page);

    const flow = page.getByTestId('operation-flow');
    await expect(flow).toBeVisible();
    await expect(flow.getByRole('listitem')).toHaveCount(6);

    const currentState = page.getByTestId('operation-flow-current-state');
    await expect(currentState).toContainText(/Source connection required|Repository synchronization required/);

    const why = page.getByTestId('operation-flow-why');
    await expect(why).not.toHaveText('');

    const nextAction = page.getByTestId('operation-flow-next-action');
    await expect(nextAction).toHaveCount(1);
    await expect(nextAction).toHaveAttribute('href', `/dashboard/${projectId}/repository`);
  });
});
