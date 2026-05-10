import { defineConfig } from '@playwright/test';

const browserChannel = process.env.TMR_PLAYWRIGHT_CHANNEL || undefined;

export default defineConfig({
  testDir: 'tests/browser-overlays',
  testMatch: '**/*.pw.spec.js',
  timeout: 10000,
  reporter: 'list',
  use: {
    browserName: 'chromium',
    headless: true,
    viewport: { width: 800, height: 600 },
    ...(browserChannel ? { channel: browserChannel } : {})
  }
});
