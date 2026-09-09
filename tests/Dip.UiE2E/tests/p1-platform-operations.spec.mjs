import { test, expect } from '@playwright/test';

const apiBase = process.env.DIP_API_BASE_URL ?? 'http://127.0.0.1:5080';

function uniqueProjectName(prefix = 'P1 Platform Operations') {
  return `${prefix} ${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
}

async function createManualProject(page) {
  const name = uniqueProjectName();
  await page.goto('/projects/new');
  await expect(page.getByTestId('manual-project-page')).toBeVisible();
  await page.locator('#project-name').fill(name);
  await page.locator('#repository-url').fill('https://github.com/example/dip-p1-fixture');
  await page.locator('#default-branch').fill('main');
  await page.getByTestId('create-project-submit').click();
  await expect(page).toHaveURL(/\/dashboard\/[0-9a-f-]{36}$/i);
  return { name, projectId: page.url().split('/').at(-1) };
}

test.describe('P1 platform operations and production design', () => {
  test('system page separates live runtime evidence from the 24/7 production design target', async ({ page }) => {
    await page.goto('/system');

    await expect(page.getByTestId('platform-operations-page')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Platform Operations' })).toBeVisible();
    await expect(page.getByTestId('current-runtime-context')).toContainText('Current Runtime');
    await expect(page.getByTestId('current-runtime-context')).toContainText('Production Design Target');

    await expect(page.getByTestId('continuous-operations')).toBeVisible();
    await expect(page.getByTestId('production-design-target')).toBeVisible();
    await expect(page.getByTestId('production-architecture')).toContainText('Caddy / HTTPS');
    await expect(page.getByTestId('production-architecture')).toContainText('PostgreSQL');
    await expect(page.getByTestId('production-architecture')).toContainText('Dip.Worker');

    await expect(page.getByTestId('production-design-target')).toContainText(
      'not evidence that the portfolio demonstration is currently operated 24/7'
    );
  });

  test('system status API exposes operational telemetry without inventing missing values', async ({ request }) => {
    const response = await request.get(`${apiBase}/api/v1/system/status`);
    expect(response.ok(), await response.text()).toBeTruthy();

    const status = await response.json();
    expect(status).toHaveProperty('overall');
    expect(status).toHaveProperty('components');
    expect(status).toHaveProperty('operations');
    expect(status.operations).toHaveProperty('available');
    expect(status.operations).toHaveProperty('queue');
    expect(status.operations.queue).toHaveProperty('available');

    if (status.operations.available) {
      expect(status.operations.queuedJobCount).not.toBeNull();
      expect(status.operations.runningJobCount).not.toBeNull();
      expect(status.operations.failedOrTimedOutJobCount).not.toBeNull();
    }

    if (status.operations.queue.available) {
      expect(status.operations.queue.storedEnvelopeCount).not.toBeNull();
      expect(status.operations.queue.activeLeaseCount).not.toBeNull();
      expect(status.operations.queue.retryEnvelopeCount).not.toBeNull();
      expect(status.operations.queue.deadLetterEnvelopeCount).not.toBeNull();
    }
  });

  test('sidebar follows the project lifecycle instead of feature-centric grouping', async ({ page }) => {
    const { projectId } = await createManualProject(page);

    const nav = page.getByTestId('primary-navigation');
    await expect(page.getByTestId('nav-section-start')).toHaveText('Start');
    await expect(page.getByTestId('nav-section-observe')).toHaveText('Observe');
    await expect(page.getByTestId('nav-section-understand')).toHaveText('Understand');
    await expect(page.getByTestId('nav-section-operate')).toHaveText('Operate');
    await expect(page.getByTestId('nav-section-platform')).toHaveText('Platform');

    await expect(nav.getByText('Operation Flow', { exact: true })).toBeVisible();
    await expect(nav.getByText('Project Overview', { exact: true })).toBeVisible();
    await expect(nav.getByText('Repository', { exact: true })).toBeVisible();
    await expect(nav.getByText('CI / Builds', { exact: true })).toBeVisible();
    await expect(nav.getByText('QA', { exact: true })).toBeVisible();
    await expect(nav.getByText('Intelligence', { exact: true })).toBeVisible();
    await expect(nav.getByText('Management', { exact: true })).toBeVisible();
    await expect(nav.getByText('QA Governance', { exact: true })).toBeVisible();
    await expect(nav.getByText('Platform Operations', { exact: true })).toBeVisible();

    const operationFlowLink = page.getByTestId('operation-flow-nav');
    await expect(operationFlowLink).toHaveAttribute('href', `/dashboard/${projectId}#operation-flow`);
  });
});
