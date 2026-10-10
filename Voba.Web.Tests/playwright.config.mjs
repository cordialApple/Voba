import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: '.',
  testMatch: 'browser*.spec.mjs',
  timeout: 600_000,
  expect: { timeout: 20_000 },
  workers: 1,
  reporter: 'list',
  outputDir: 'test-results',
  use: {
    baseURL: process.env.VOBA_WEB_BASE_URL ?? 'http://127.0.0.1:5057',
    browserName: 'chromium',
    channel: 'msedge',
    headless: true,
    actionTimeout: 20_000,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure'
  }
});
