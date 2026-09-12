import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.DIP_WEB_BASE_URL || 'http://127.0.0.1:5081';
const baseOrigin = new URL(baseURL).origin;

// Most GUI E2E cases exercise application behavior after onboarding.
// Seed a completed onboarding state before the first document is created so
// Blazor's asynchronous first-render initialization cannot race with the
// first click/form interaction. The dedicated first-visit test explicitly
// clears this key and still verifies the real onboarding flow.
const defaultStorageState = {
  cookies: [],
  origins: [
    {
      origin: baseOrigin,
      localStorage: [
        { name: 'dip.onboarding.completed.v1', value: 'true' }
      ]
    }
  ]
};

export default defineConfig({
  testDir: './tests',
  timeout: 60_000,
  expect: { timeout: 12_000 },
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [
    ['list'],
    ['html', { outputFolder: 'artifacts/playwright-report', open: 'never' }]
  ],
  outputDir: 'artifacts/test-results',
  use: {
    baseURL,
    storageState: defaultStorageState,
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } }
  ]
});
