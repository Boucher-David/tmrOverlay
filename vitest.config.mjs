import { defineConfig } from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'jsdom',
    include: ['tests/browser-overlays/**/*.test.js'],
    testTimeout: 5000
  }
});
