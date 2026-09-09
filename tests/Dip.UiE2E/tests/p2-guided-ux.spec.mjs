import { test, expect } from '@playwright/test';

function uniqueProjectName(prefix = 'P2 Guided UX') {
  return `${prefix} ${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
}

async function disableAutomaticPrompt(page) {
  await page.addInitScript(() => window.localStorage.setItem('dip.onboarding.completed.v1', 'true'));
}

async function createManualProject(page) {
  const name = uniqueProjectName();
  await page.goto('/projects/new');
  await expect(page.getByTestId('manual-project-page')).toBeVisible();
  await page.locator('#project-name').fill(name);
  await page.locator('#repository-url').fill('https://github.com/example/dip-p2-fixture');
  await page.locator('#default-branch').fill('main');
  await page.getByTestId('create-project-submit').click();
  await expect(page).toHaveURL(/\/dashboard\/[0-9a-f-]{36}$/i);
  return { name, projectId: page.url().split('/').at(-1) };
}

test.describe('P2 guided UX', () => {
  test('first visit shows a non-blocking onboarding prompt and completion persists in localStorage', async ({ page }) => {
    await page.addInitScript(() => window.localStorage.removeItem('dip.onboarding.completed.v1'));
    await page.goto('/');

    const welcome = page.getByTestId('guided-tour-welcome');
    await expect(welcome).toBeVisible();
    await expect(welcome).toContainText(/Welcome to DIP|Review DIP in about 5 minutes/);

    await page.getByTestId('guided-tour-start').click();
    await expect(page.getByTestId('guided-tour-overlay')).toBeVisible();
    await expect(page.getByTestId('guided-tour-open-surface')).toBeVisible();

    while (await page.getByTestId('guided-tour-next').count()) {
      await page.getByTestId('guided-tour-next').click();
    }
    await page.getByTestId('guided-tour-finish').click();
    await expect(page.getByTestId('guided-tour-overlay')).toHaveCount(0);

    const completed = await page.evaluate(() => window.localStorage.getItem('dip.onboarding.completed.v1'));
    expect(completed).toBe('true');

    await page.reload();
    await expect(page.getByTestId('guided-tour-welcome')).toHaveCount(0);
  });

  test('help button reopens the product tour after onboarding has already been completed', async ({ page }) => {
    await disableAutomaticPrompt(page);
    await page.goto('/');

    await page.getByTestId('guided-tour-help').click();
    await expect(page.getByTestId('guided-tour-overlay')).toBeVisible();
    await expect(page.getByRole('heading', { name: '1. Understand DIP' })).toBeVisible();
  });

  test('reviewer guide follows the four evidence-review surfaces without creating reviewer-only data', async ({ page }) => {
    await disableAutomaticPrompt(page);
    const { projectId } = await createManualProject(page);
    await page.goto('/');

    const reviewerGuide = page.getByTestId('reviewer-guide');
    await expect(reviewerGuide).toBeVisible();
    await expect(reviewerGuide).toContainText('Project Overview');
    await expect(reviewerGuide).toContainText('QA Evidence');
    await expect(reviewerGuide).toContainText('Intelligence');
    await expect(reviewerGuide).toContainText('Platform Operations');

    await page.getByTestId('start-reviewer-tour').click();
    await expect(page.getByTestId('guided-tour-overlay')).toBeVisible();
    await expect(page.getByRole('heading', { name: '1. Project Overview' })).toBeVisible();
    await expect(page.getByTestId('guided-tour-open-surface')).toBeVisible();

    await page.getByTestId('guided-tour-open-surface').click();
    await expect(page).toHaveURL(new RegExp(`/dashboard/${projectId}`));
    await expect(page.getByTestId('project-overview')).toBeVisible();
  });

  test('project overview provides contextual definitions and an actionable evidence empty state', async ({ page }) => {
    await disableAutomaticPrompt(page);
    await createManualProject(page);

    const truthHelp = page.getByTestId('concept-help-projecttruth');
    await expect(truthHelp).toBeVisible();
    await truthHelp.locator('summary').click();
    await expect(truthHelp).toContainText('reported claims separate');

    const confidenceHelp = page.getByTestId('concept-help-evidenceconfidence');
    await expect(confidenceHelp).toBeVisible();

    const progressHelp = page.getByTestId('concept-help-evidencebackedprogress');
    await expect(progressHelp).toBeVisible();

    const gateHelp = page.getByTestId('concept-help-qualitygate');
    await expect(gateHelp).toBeVisible();

    const noEvidence = page.getByTestId('dashboard-no-evidence');
    if (await noEvidence.count()) {
      await expect(noEvidence).toContainText('This enables');
      await expect(noEvidence).toContainText('Project Truth refresh');
      await expect(noEvidence.getByRole('link', { name: 'Open QA' })).toBeVisible();
    }
  });
});
