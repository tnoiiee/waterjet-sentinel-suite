// WJSS Stage 0.2.1A — Playwright config for OWNER-LOCAL Windows runs with the installed
// Microsoft Edge (channel 'msedge'). No Playwright browser download is used
// (set PLAYWRIGHT_SKIP_BROWSER_DOWNLOAD=1 before npm ci).
// Edge automation does NOT verify embedded WebView2 or kiosk-shell behaviour.
// Raw output goes to ../results/raw/ (git-ignored). Screenshots, video, and traces are off.
//
// Not type-checked by tsc: @types/node is not an approved dependency; Playwright transpiles
// this file itself. `npm run e2e:list` validates that config and specs load.
import { defineConfig } from '@playwright/test';

const port = Number(process.env.WJSS_SPIKE_PORT ?? 5181);
const token = process.env.WJSS_SPIKE_TOKEN ?? globalThis.crypto.randomUUID();
process.env.WJSS_SPIKE_TOKEN = token;
process.env.WJSS_SPIKE_PORT = String(port);

export default defineConfig({
  testDir: 'e2e',
  timeout: 5 * 60_000,
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['json', { outputFile: '../results/raw/playwright/report.json' }]],
  outputDir: '../results/raw/playwright/test-results',
  use: {
    channel: 'msedge',
    baseURL: `http://127.0.0.1:${port}`,
    viewport: { width: 1600, height: 900 },
    trace: 'off',
    screenshot: 'off',
    video: 'off',
  },
  projects: [{ name: 'msedge', use: { channel: 'msedge' } }],
  webServer: {
    command: `node ../runtime-harness/src/main.mjs --port ${port} --static ./dist`,
    url: `http://127.0.0.1:${port}/healthz`,
    reuseExistingServer: false,
    timeout: 30_000,
    env: { WJSS_SPIKE_TOKEN: token },
  },
});
