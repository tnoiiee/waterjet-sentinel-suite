// WJSS Stage 0.2.1A — Owner-local soak run in installed Microsoft Edge.
// PLANNED FOR OWNER-LOCAL EXECUTION. NOT EXECUTED IN ARENA.
//
//   WJSS_SOAK_MINUTES=10  (Smoke)  | 60 (Feasibility) | 240 (optional Extended)
//   WJSS_SOAK_LABEL=smoke | feasibility | extended
//   WJSS_SOAK_SAMPLE_S=15
//
// Samples browser-side diagnostics and harness metrics into results/raw/<label>-<ts>/ as
// JSONL (git-ignored). Run measurements/summarize.mjs afterwards to produce the committed
// summary. Only HARD gates are asserted here; exploratory thresholds are set by the Owner
// after the Smoke baseline.
import fs from 'node:fs';
import path from 'node:path';
import { expect, test } from '@playwright/test';
import { diag, metrics, scenario } from './support';

const minutes = Number(process.env.WJSS_SOAK_MINUTES ?? 10);
const label = process.env.WJSS_SOAK_LABEL ?? (minutes <= 10 ? 'smoke' : minutes <= 60 ? 'feasibility' : 'extended');
const sampleS = Number(process.env.WJSS_SOAK_SAMPLE_S ?? 15);

// Synthetic scenario cycle applied during the soak (repeats).
const CYCLE: Array<[string, Record<string, unknown>?]> = [
  ['set-dirty-mode', { mode: 'dirty30' }],
  ['raise-alarm', { sensorId: 'SYN-REAR-07' }],
  ['set-dirty-mode', { mode: 'oscillate' }],
  ['clear-alarm', {}],
  ['device-timeout', { deviceId: 'SYN-TC-06', enabled: true }],
  ['set-dirty-mode', { mode: 'dirty70' }],
  ['device-timeout', { deviceId: 'SYN-TC-06', enabled: false }],
  ['ack-alarm', {}],
  ['historian-delay', { delayMs: 5000 }],
  ['drop-clients', {}],
  ['historian-delay', { delayMs: 20 }],
  ['set-dirty-mode', { mode: 'normal' }],
];

test(`soak ${label} ${minutes} min`, async ({ page, request }) => {
  test.setTimeout((minutes + 5) * 60_000);
  const runDir = path.resolve('..', 'results', 'raw', `${label}-${new Date().toISOString().replace(/[:.]/g, '-')}`);
  fs.mkdirSync(runDir, { recursive: true });
  const browserFile = path.join(runDir, 'browser-samples.jsonl');
  const harnessFile = path.join(runDir, 'harness-samples.jsonl');
  fs.writeFileSync(path.join(runDir, 'run.json'), JSON.stringify({ label, minutes, sampleS, startedAt: new Date().toISOString(), browser: 'msedge (installed)', note: 'SYNTHETIC SPIKE RUN - raw, not committed' }, null, 2));

  let crashed = false;
  page.on('crash', () => (crashed = true));
  await page.goto('/');
  await expect(page.getByTestId('conn-state')).toHaveText('LIVE', { timeout: 15_000 });
  await page.keyboard.press('d'); // open diagnostics overlay

  const end = Date.now() + minutes * 60_000;
  let step = 0;
  let nextScenario = Date.now() + 30_000;
  while (Date.now() < end) {
    await page.waitForTimeout(sampleS * 1000);
    const b = await diag(page);
    fs.appendFileSync(browserFile, JSON.stringify(b) + '\n');
    const m = await metrics(request);
    fs.appendFileSync(harnessFile, JSON.stringify({ t: Date.now(), revision: m.revision, cpu: m.cpuPercentSinceLastRead, memory: m.memory, eventLoopDelayMs: m.eventLoopDelayMs, publish: m.publish, historian: { depth: m.historian.depth, nearOverflow: m.historian.nearOverflow, rejected: m.historian.rejected }, jobs: m.jobs, invariants: m.invariants.violations, sse: m.sse }) + '\n');
    if (Date.now() >= nextScenario) {
      const [c, p] = CYCLE[step % CYCLE.length];
      await scenario(request, c, p);
      step += 1;
      nextScenario = Date.now() + 30_000;
    }
  }
  await scenario(request, 'set-dirty-mode', { mode: 'normal' });
  await page.waitForTimeout(3000);

  // Hard gates.
  const final = await diag(page);
  const m = await metrics(request);
  fs.writeFileSync(path.join(runDir, 'final.json'), JSON.stringify({ finishedAt: new Date().toISOString(), browser: final, harness: m }, null, 2));
  expect(crashed).toBe(false);
  expect(final.connection).toBe('LIVE');
  expect(m.invariants.violations).toBe(0);
  expect(m.jobs.acceptedSecondJobs).toBe(0);
  expect(Number(final.trendPoints)).toBeLessThanOrEqual(600);
});
