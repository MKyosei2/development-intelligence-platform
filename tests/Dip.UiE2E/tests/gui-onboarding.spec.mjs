// Purpose: Exercises native DIP GUI onboarding, project workflows, autonomous debugging, and spreadsheet intelligence.
import { test, expect } from '@playwright/test';

const apiBase = process.env.DIP_API_BASE_URL ?? 'http://127.0.0.1:5080';
const xlsxMime = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet';
const vfxWorkbookBase64 = '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';

function uniqueProjectName(prefix = 'E2E Project') {
  return `${prefix} ${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
}

async function createManualProject(page, prefix = 'E2E Project') {
  const name = uniqueProjectName(prefix);
  await page.goto('/projects/new');
  await expect(page.getByTestId('manual-project-page')).toBeVisible();
  await page.locator('#project-name').fill(name);
  await page.locator('#repository-url').fill('https://github.com/example/dip-e2e-fixture');
  await page.locator('#default-branch').fill('main');
  await page.getByTestId('create-project-submit').click();
  await expect(page).toHaveURL(/\/dashboard\/[0-9a-f-]{36}$/i, { timeout: 30_000 });
  return { name, projectId: page.url().split('/').at(-1) };
}

async function getUniversalGraph(request, projectId, domainId = null) {
  const suffix = domainId ? `?domainId=${encodeURIComponent(domainId)}` : '';
  const response = await request.get(`${apiBase}/api/v1/project-control/projects/${projectId}/graph/universal${suffix}`);
  expect(response.ok(), await response.text()).toBeTruthy();
  return response.json();
}

async function waitForSpreadsheetOutcome(page, timeout = 30_000) {
  const result = page.getByTestId('spreadsheet-analysis-result');
  const error = page.getByTestId('spreadsheet-error');
  await expect(result.or(error)).toBeVisible({ timeout });

  if (await error.isVisible()) {
    throw new Error(
      'Spreadsheet UI reported an error: ' + (await error.innerText()).trim());
  }
  return result;
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
    }
  });

  test('manual project can be created, selected and inspected across project-scoped GUI', async ({ page }) => {
    const { name, projectId } = await createManualProject(page);
    await expect(page.getByTestId('project-overview')).toBeVisible();
    await expect(page.getByRole('heading', { name })).toBeVisible();
    await expect(page.getByTestId('active-project-select')).toHaveValue(/^[0-9a-f-]{36}$/i);

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
    await expect(page.getByTestId('qa-debug-history')).toBeVisible();
    await expect(page.getByTestId('qa-run-autonomous-debug')).toBeDisabled();

    await page.goto(`/dashboard/${projectId}/management`);
    await expect(page.getByTestId('management-operations-page')).toBeVisible();
    await page.getByRole('tab', { name: 'Sync & source of truth' }).click();
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

  test('autonomous debug backend persists deterministic history and GUI restores it after refresh', async ({ page, request }) => {
    const { projectId } = await createManualProject(page, 'Debug History');
    const response = await request.post(`${apiBase}/api/v1/defect-intelligence/projects/${projectId}/autonomous-debug`, {
      data: {
        subjectKey: 'deterministic-no-evidence',
        question: 'Return only evidence-grounded hypotheses.',
        evidenceJson: '{}',
        workspaceRelativePath: '.',
        repositoryContext: null,
        baseCommit: null,
        language: 'auto',
        domainId: 'software',
        budget: {
          maxHypotheses: 2,
          maxExperiments: 1,
          maxAiCalls: 2,
          timeoutSecondsPerExperiment: 30,
          maxWallClockMinutes: 1
        }
      }
    });
    expect(response.ok(), await response.text()).toBeTruthy();
    const session = await response.json();
    expect(session.status).toBe('NoGroundedHypothesis');

    await page.goto(`/dashboard/${projectId}/qa`);
    await page.getByTestId('qa-autonomous-debug-tab').click();
    await expect(page.getByTestId('qa-debug-history')).toContainText('deterministic-no-evidence');
    await expect(page.getByTestId('qa-debug-history')).toContainText('NoGroundedHypothesis');
    await page.reload();
    await page.getByTestId('qa-autonomous-debug-tab').click();
    await expect(page.getByTestId('qa-debug-history')).toContainText('deterministic-no-evidence');
    await expect(page.getByTestId('qa-autonomous-debug-result')).toContainText('NoGroundedHypothesis');
  });

  test('flexible spreadsheet GUI performs analyze, dry-run and apply for a media/VFX workbook', async ({ page, request }) => {
    const { projectId } = await createManualProject(page, 'VFX Spreadsheet');
    await page.goto(`/dashboard/${projectId}/management`);
    await page.getByRole('tab', { name: 'Sync & source of truth' }).click();
    await expect(page.getByTestId('management-spreadsheet-intelligence')).toBeVisible();

    const before = await getUniversalGraph(request, projectId, 'media.production');
    expect(before.nodes).toHaveLength(0);

    await page.getByTestId('spreadsheet-file').setInputFiles({
      name: 'vfx-shot-schedule.xlsx',
      mimeType: xlsxMime,
      buffer: Buffer.from(vfxWorkbookBase64, 'base64')
    });
    await expect(page.getByTestId('spreadsheet-analyze')).toBeEnabled();
    await expect(page.getByTestId('spreadsheet-dry-run')).toBeEnabled();
    await expect(page.getByTestId('spreadsheet-apply')).toBeEnabled();

    await page.getByTestId('spreadsheet-analyze').click();

    const busy = page.getByTestId('spreadsheet-busy');
    if (await busy.isVisible())
      await expect(busy).toContainText('Analyze');

    // The Blazor server component calls DIP API from server-side ApiClient.
    // Browser page.waitForResponse() cannot observe that HTTP request.
    const analysisResult = await waitForSpreadsheetOutcome(page);
    await expect(analysisResult).toContainText('media.production');
    await expect(analysisResult).toContainText('media.shot');

    await page.getByTestId('spreadsheet-dry-run').click();
    await expect(page.getByTestId('spreadsheet-import-result')).toContainText('Dry-run result');
    await expect(page.getByTestId('spreadsheet-import-result')).toContainText('2');
    const afterDryRun = await getUniversalGraph(request, projectId, 'media.production');
    expect(afterDryRun.nodes).toHaveLength(0);

    await page.getByTestId('spreadsheet-apply').click();
    await expect(page.getByTestId('spreadsheet-import-result')).toContainText('Import result');
    const afterApply = await getUniversalGraph(request, projectId, 'media.production');
    expect(afterApply.nodes).toHaveLength(2);
    expect(afterApply.nodes.every((entry) => entry.kind === 'media.shot')).toBeTruthy();
    expect(afterApply.edges.some((entry) => entry.relationKind === 'depends-on')).toBeTruthy();
  });

  test('project lifecycle, owner safety, notifications and audit are operable from existing GUI', async ({ page }) => {
    const { name, projectId } = await createManualProject(page, 'Lifecycle');

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
