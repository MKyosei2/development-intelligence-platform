import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.DIP_WEB_BASE_URL || 'http://127.0.0.1:5081';

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
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure'
  },
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } }
  ]
});
