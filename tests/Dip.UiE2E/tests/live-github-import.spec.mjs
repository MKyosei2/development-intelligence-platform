import { test, expect } from '@playwright/test';

// Opt-in only: set DIP_E2E_GITHUB_REPOSITORY to an installation-visible owner/repo.
// This is intentionally excluded from normal CI so a missing external credential cannot
// make the deterministic GUI smoke test flaky.
test('@live-github repository picker can locate a real installation repository', async ({ page }) => {
  const fullName = process.env.DIP_E2E_GITHUB_REPOSITORY;
  test.skip(!fullName, 'DIP_E2E_GITHUB_REPOSITORY is not configured.');

  await page.goto('/projects/import/github');
  await expect(page.getByTestId('github-connected')).toBeVisible();
  await page.locator('#repo-search').fill(fullName);
  await page.getByRole('button', { name: 'Search' }).click();
  await expect(page.locator(`article.repo-card[data-repository="${fullName}"]`)).toBeVisible();
});
