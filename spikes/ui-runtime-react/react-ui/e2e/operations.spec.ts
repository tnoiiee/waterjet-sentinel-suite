// WJSS Stage 0.2.1A — Owner-local functional scenarios in installed Microsoft Edge.
// PLANNED FOR OWNER-LOCAL EXECUTION. NOT EXECUTED IN ARENA (no browser available there).
// Edge results do not verify WebView2 or kiosk-shell behaviour.
import { expect, test } from '@playwright/test';
import { diag, metrics, scenario, trackHosts } from './support';

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ page, request }) => {
  await scenario(request, 'quality-showcase', { enabled: false });
  await page.goto('/');
  await expect(page.getByTestId('conn-state')).toHaveText('LIVE', { timeout: 15_000 });
});

test('S01/S02 initial Snapshot renders 106 cells; one-second Deltas advance the revision', async ({ page }) => {
  await expect(page.locator('[data-sensor-id]')).toHaveCount(106);
  const a = await diag(page);
  await page.waitForTimeout(3200);
  const b = await diag(page);
  expect(Number(b.deltas) - Number(a.deltas)).toBeGreaterThanOrEqual(2);
});

test('MAP U-shaped wall map: 4 walls x 6 rows, 24/29/24/29 Sensors, Cannons at I7/I16 not selectable', async ({ page }) => {
  const walls = page.locator('[data-wall]');
  await expect(walls).toHaveCount(4);
  expect(await walls.evaluateAll((ws) => ws.map((w) => [w.getAttribute('data-wall'), w.getAttribute('data-wall-rows'), w.querySelectorAll('[data-sensor-id]').length]))).toEqual([
    ['REAR', '6', 29],
    ['LEFT', '6', 24],
    ['RIGHT', '6', 24],
    ['FRONT', '6', 29],
  ]);
  // Plan-view U geometry, relative to the compact centre summary. The side walls intentionally
  // share Y range with the Rear / Front blocks (approved fullscreen layout), so wall-to-wall
  // Y-interval ordering is NOT asserted; positions are asserted against the centre instead.
  const box = async (sel: string) => (await page.locator(sel).boundingBox())!;
  const [rear, left, right, front] = [await box('[data-wall="REAR"]'), await box('[data-wall="LEFT"]'), await box('[data-wall="RIGHT"]'), await box('[data-wall="FRONT"]')];
  const center = await box('[data-testid="map-center"]');
  const surface = await box('[data-testid="u-surface"]');
  type B = { x: number; y: number; width: number; height: number };
  const cx = (b: B) => b.x + b.width / 2;
  const cy = (b: B) => b.y + b.height / 2;
  const intersects = (a: B, b: B) => a.x < b.x + b.width - 0.5 && b.x < a.x + a.width - 0.5 && a.y < b.y + b.height - 0.5 && b.y < a.y + a.height - 0.5;
  expect(rear.y + rear.height, 'Rear above centre').toBeLessThanOrEqual(center.y + 0.5);
  expect(left.x + left.width, 'Left left of centre').toBeLessThanOrEqual(center.x + 0.5);
  expect(right.x, 'Right right of centre').toBeGreaterThanOrEqual(center.x + center.width - 0.5);
  expect(front.y, 'Front below centre').toBeGreaterThanOrEqual(center.y + center.height - 0.5);
  expect(cy(rear), 'Rear y-centre above Front y-centre').toBeLessThan(cy(front));
  expect(cx(left), 'Left x-centre left of Right x-centre').toBeLessThan(cx(right));
  expect(Math.abs(cx(rear) - cx(center)), 'Rear horizontally centred').toBeLessThanOrEqual(2);
  expect(Math.abs(cx(front) - cx(center)), 'Front horizontally centred').toBeLessThanOrEqual(2);
  const wallBoxes: [string, B][] = [
    ['REAR', rear],
    ['LEFT', left],
    ['RIGHT', right],
    ['FRONT', front],
  ];
  for (let i = 0; i < wallBoxes.length; i += 1) {
    for (let j = i + 1; j < wallBoxes.length; j += 1) expect(intersects(wallBoxes[i][1], wallBoxes[j][1]), `${wallBoxes[i][0]} / ${wallBoxes[j][0]} intersect`).toBe(false);
    expect(intersects(wallBoxes[i][1], center), `${wallBoxes[i][0]} intersects centre`).toBe(false);
    const w = wallBoxes[i][1];
    expect(w.x >= surface.x - 0.5 && w.y >= surface.y - 0.5 && w.x + w.width <= surface.x + surface.width + 0.5 && w.y + w.height <= surface.y + surface.height + 0.5, `${wallBoxes[i][0]} inside map bounds`).toBe(true);
  }
  await expect(page.locator('[data-sensor-id="I7"]')).toHaveCount(0);
  await expect(page.locator('[data-sensor-id="I16"]')).toHaveCount(0);
  const cannon = page.locator('[data-equipment-id="CANNON_REAR"]');
  await expect(cannon).toHaveAttribute('data-logical-column', '7');
  await cannon.click();
  await expect(page.locator('[aria-pressed="true"]')).toHaveCount(0);
});

test('S03/S04/S05 30 % / 70 % Dirty and threshold oscillation', async ({ page, request }) => {
  for (const [mode, lo, hi] of [
    ['dirty30', 20, 45],
    ['dirty70', 60, 85],
  ] as const) {
    await scenario(request, 'set-dirty-mode', { mode });
    await page.waitForTimeout(2500);
    const n = await page.locator('[data-process="DIRTY"]').count();
    expect(n).toBeGreaterThanOrEqual(lo);
    expect(n).toBeLessThanOrEqual(hi);
  }
  await scenario(request, 'set-dirty-mode', { mode: 'oscillate' });
  await page.waitForTimeout(4000);
  await scenario(request, 'set-dirty-mode', { mode: 'normal' });
});

test('S06 all quality states; BAD/STALE/DISABLED neutral; UNCERTAIN detail note', async ({ page, request }) => {
  await scenario(request, 'quality-showcase', { enabled: true });
  await page.waitForTimeout(1500);
  for (const [id, q] of [
    ['G+202', 'BAD'],
    ['G+203', 'STALE'],
    ['G+204', 'DISABLED'],
  ]) {
    const cell = page.locator(`[data-sensor-id="${id}"]`);
    await expect(cell).toHaveAttribute('data-quality', q);
    await expect(cell).toHaveAttribute('data-process', 'NEUTRAL');
  }
  await page.locator('[data-sensor-id="G+201"]').click();
  await expect(page.getByTestId('detail-quality')).toContainText('UNCERTAIN');
  await expect(page.getByTestId('detail-note')).toContainText(/last validated classification/i);
  await scenario(request, 'quality-showcase', { enabled: false });
});

test('S07 selection persists during updates and reconnect', async ({ page, request }) => {
  const cell = page.locator('[data-sensor-id="H8"]');
  await cell.click();
  await page.waitForTimeout(3000);
  await expect(cell).toHaveAttribute('aria-pressed', 'true');
  await scenario(request, 'drop-clients');
  await expect(page.getByTestId('conn-state')).toHaveText('LIVE', { timeout: 10_000 });
  await expect(cell).toHaveAttribute('aria-pressed', 'true');
});

test('S08/S09/S10 queue badges, GlobalQueue preview, single job progression', async ({ page, request }) => {
  await scenario(request, 'set-dirty-mode', { mode: 'dirty30' });
  await page.waitForTimeout(2500);
  const rows = page.getByTestId('queue-preview').locator('tbody tr');
  expect(await rows.count()).toBeGreaterThan(0);
  expect(await rows.count()).toBeLessThanOrEqual(8);
  expect(await page.locator('[data-queue]').count()).toBeGreaterThan(0);
  await expect(page.getByTestId('active-job')).toContainText('SYN-JOB-', { timeout: 10_000 });
  await scenario(request, 'set-dirty-mode', { mode: 'normal' });
});

test('S11/S12 active alarm and cleared-ack-required, independent of Dirty colour', async ({ page, request }) => {
  // Deterministic (Owner closeout): the Dirty Score is fixed and held through the synthetic
  // scenario API, so every background sample comes from the SAME applied score. The previous
  // version compared samples from different revisions while the score legitimately drifted.
  type Rec = { sensorId: string; dirtyScore: number | null; classification: string; alarmState: string };
  const id = 'G16';
  const cell = page.locator(`[data-sensor-id="${id}"]`);
  const rec = async () => {
    const s = (await (await request.get('/api/snapshot')).json()) as { revision: number; sensors: Rec[] };
    return { revision: s.revision, r: s.sensors.find((x) => x.sensorId === id)! };
  };
  const visual = () => cell.evaluate((e) => ({ bg: getComputedStyle(e).backgroundColor, inline: (e as HTMLElement).style.background, process: e.getAttribute('data-process') }));
  await scenario(request, 'auto-jobs', { enabled: false }); // the Sensor must not become a Job target mid-test
  try {
    const fixed = await scenario(request, 'set-sensor-score', { sensorId: id, classification: 'DIRTY', hold: true });
    expect(fixed.accepted, JSON.stringify(fixed)).toBe(true);
    expect(fixed.detail.held).toBe(true);
    const score = fixed.detail.score as number;
    // Wait for the runtime revision that carries the fixed score, then for the UI to render it.
    await expect.poll(async () => (await rec()).r.dirtyScore, { timeout: 10_000 }).toBe(score);
    const applied = await rec();
    expect(applied.r.classification).toBe('DIRTY');
    await expect(cell).toHaveAttribute('data-process', 'DIRTY');
    await expect(cell.locator('[data-part="value"]')).toHaveText(String(Math.round(score)));
    await expect(cell).toHaveAttribute('data-alarm', 'NONE');
    const before = await visual();
    expect(before.inline).toContain('color-mix'); // Dirty shade derived from the fixed score

    const r = await scenario(request, 'raise-alarm', { sensorId: id });
    await expect(cell).toHaveAttribute('data-alarm', 'ACTIVE_UNACK');
    const raised = await rec();
    expect(raised.revision).toBeGreaterThan(applied.revision);
    expect(raised.r.dirtyScore).toBe(score);
    expect(raised.r.classification).toBe('DIRTY');
    expect(raised.r.alarmState).toBe('ACTIVE_UNACK');
    const during = await visual();
    expect(during.process).toBe('DIRTY');
    expect(during.inline).toBe(before.inline); // same process shade expression for the same score
    expect(during.bg).toBe(before.bg); // the Alarm does not recolour the Sensor
    await expect(cell.locator('[data-part="alarm-marker"]')).toBeVisible(); // separate warning channel

    await scenario(request, 'clear-alarm', { alarmId: r.detail.alarmId });
    await expect(cell).toHaveAttribute('data-alarm', 'CLEARED_UNACK');
    await expect(page.getByTestId('alarm-strip')).toContainText('acknowledgement required');
    const cleared = await rec();
    expect(cleared.r.dirtyScore).toBe(score);
    expect(cleared.r.classification).toBe('DIRTY');
    expect((await visual()).bg).toBe(before.bg);

    await scenario(request, 'ack-alarm', { alarmId: r.detail.alarmId });
    await expect(cell).toHaveAttribute('data-alarm', 'NONE');
  } finally {
    await scenario(request, 'reset-sensor', { sensorId: id }); // releases the fixed-score hold
    await scenario(request, 'auto-jobs', { enabled: true });
  }
});

test('S13/S14/S15 device timeout, other devices continue, recovery', async ({ page, request }) => {
  await scenario(request, 'device-timeout', { deviceId: 'SYN-TC-02', enabled: true });
  await page.waitForTimeout(4500);
  const affected = page.locator('[data-sensor-id="G+105"]');
  await expect(affected).toHaveAttribute('data-quality', 'BAD');
  await expect(page.locator('[data-sensor-id="J12"]')).toHaveAttribute('data-quality', 'GOOD');
  await scenario(request, 'device-timeout', { deviceId: 'SYN-TC-02', enabled: false });
  await expect(affected).toHaveAttribute('data-quality', 'GOOD', { timeout: 10_000 });
});

test('S16/S17/S18 UI disconnect, reconnect, authoritative re-snapshot; S28 revision gap', async ({ page, request }) => {
  const a = await diag(page);
  await scenario(request, 'drop-clients');
  await expect(page.getByTestId('conn-state')).toHaveText('LIVE', { timeout: 10_000 });
  const b = await diag(page);
  expect(Number(b.snapshots)).toBeGreaterThan(Number(a.snapshots));
  await scenario(request, 'inject-revision-gap');
  await page.waitForTimeout(3000);
  const c = await diag(page);
  expect(Number(c.gaps)).toBeGreaterThanOrEqual(1);
  await expect(page.getByTestId('conn-state')).toHaveText('LIVE');
});

test('S19/S20 Historian slowdown and near-overflow do not stall the UI or pump stop', async ({ page, request }) => {
  await scenario(request, 'historian-delay', { delayMs: 600000 });
  await scenario(request, 'historian-burst', { count: 45000 });
  const a = await diag(page);
  await page.waitForTimeout(3200);
  const b = await diag(page);
  expect(Number(b.deltas) - Number(a.deltas)).toBeGreaterThanOrEqual(2);
  const m = await metrics(request);
  expect(m.historian.nearOverflow).toBe(true);
  const stop = await scenario(request, 'pump-stop');
  expect(stop.accepted).toBe(true);
  await expect(page.getByTestId('pump-state')).toContainText(/STOPPING|STOPPED/, { timeout: 5000 });
  await scenario(request, 'historian-delay', { delayMs: 20 });
  await scenario(request, 'pump-start');
});

test('S21 bounded trend: one uPlot instance, points <= capacity', async ({ page }) => {
  await page.waitForTimeout(3000);
  await expect(page.locator('.uplot')).toHaveCount(1);
  const d = await diag(page);
  expect(Number(d.trendPoints)).toBeLessThanOrEqual(600);
});

test('S22 camera placeholder is local; the page only talks to loopback', async ({ page }) => {
  const hosts = trackHosts(page);
  await page.reload();
  await expect(page.getByTestId('camera-placeholder').locator('svg')).toBeVisible();
  await page.waitForTimeout(2000);
  for (const h of hosts) expect(h.startsWith('127.0.0.1')).toBe(true);
});

test('S23/S24 synthetic close guard: Active Job and Pump running', async ({ page, request }) => {
  await scenario(request, 'auto-jobs', { enabled: false });
  await scenario(request, 'abort-job', { immediate: true });
  await page.waitForTimeout(500);
  await page.getByTestId('close-request').click();
  await expect(page.getByTestId('close-refused')).toContainText('Pump running');
  await page.getByRole('button', { name: 'Dismiss' }).click();
  await scenario(request, 'second-job-attempt');
  await page.waitForTimeout(500);
  await page.getByTestId('close-request').click();
  await expect(page.getByTestId('close-refused')).toContainText('Active Cleaning Job');
  await page.getByRole('button', { name: 'Dismiss' }).click();
  await scenario(request, 'auto-jobs', { enabled: true });
});

test('S25 viewport resize keeps the page usable', async ({ page }) => {
  for (const size of [
    { width: 1920, height: 1080 },
    { width: 1280, height: 800 },
    { width: 1024, height: 768 },
  ]) {
    await page.setViewportSize(size);
    await page.waitForTimeout(700);
    await expect(page.locator('[data-sensor-id]')).toHaveCount(106);
    await expect(page.locator('[data-slot-type="CANNON"]')).toHaveCount(2);
    await expect(page.getByTestId('wall-overview')).toBeVisible();
    await expect(page.locator('.uplot')).toHaveCount(1);
  }
});

test('S26 synthetic configuration revision reclassifies', async ({ page, request }) => {
  await scenario(request, 'publish-config', { dirtyThreshold: 99 });
  await page.waitForTimeout(1500);
  await expect(page.locator('[data-process="DIRTY"]')).toHaveCount(0);
  await scenario(request, 'publish-config', { dirtyThreshold: 50 });
});

test('S27 refused second Job; invariants clean', async ({ request }) => {
  const r = await scenario(request, 'second-job-attempt');
  expect(r.accepted).toBe(false);
  expect(r.reason).toBe('ACTIVE_JOB_EXISTS');
  const m = await metrics(request);
  expect(m.jobs.acceptedSecondJobs).toBe(0);
  expect(m.invariants.violations).toBe(0);
});
