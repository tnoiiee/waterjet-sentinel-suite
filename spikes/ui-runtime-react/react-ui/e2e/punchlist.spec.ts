// WJSS Stage 0.2.1A — Final Owner UI punchlist assertions in installed Microsoft Edge.
// PLANNED FOR OWNER-LOCAL EXECUTION. NOT EXECUTED IN ARENA (no browser available there).
// Requires the harness started with --synthetic-test-controls (playwright.config.ts webServer).
// Edge results do not verify WebView2 or kiosk-shell behaviour.
import { expect, test, type Page } from '@playwright/test';
import { scenario } from './support';

type R = { left: number; top: number; right: number; bottom: number; width: number; height: number };
const EPS = 0.5;
const PROBE_IDS = ['G+205', 'G+218', 'G+118'];

async function open(page: Page) {
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.goto('/');
  await expect(page.getByTestId('conn-state')).toHaveText('LIVE', { timeout: 15_000 });
  await page.evaluate(() => document.fonts.ready);
  await page.waitForTimeout(600);
}
const intersects = (a: R, b: R) => a.left < b.right - EPS && b.left < a.right - EPS && a.top < b.bottom - EPS && b.top < a.bottom - EPS;
const within = (inner: R, outer: R) => inner.left >= outer.left - EPS && inner.top >= outer.top - EPS && inner.right <= outer.right + EPS && inner.bottom <= outer.bottom + EPS;

/** Zone rectangles for one Sensor cell (client coordinates). */
async function zones(page: Page, sensorId: string) {
  return page.locator(`[data-sensor-id="${sensorId}"]`).evaluate((el) => {
    const r = (e: Element | null) => {
      if (!e) return null;
      const b = e.getBoundingClientRect();
      return { left: b.left, top: b.top, right: b.right, bottom: b.bottom, width: b.width, height: b.height };
    };
    const id = el.querySelector('[data-part="id"]') as HTMLElement;
    const value = el.querySelector('[data-part="value"]') as HTMLElement;
    // Text ink box of the value (the grid item stretches; the glyph run is what must be centred).
    const range = document.createRange();
    range.selectNodeContents(value);
    const ink = range.getBoundingClientRect();
    return {
      cell: r(el)!,
      id: r(id)!,
      idScroll: { sw: id.scrollWidth, cw: id.clientWidth },
      idText: id.textContent,
      value: r(value)!,
      valueInk: { left: ink.left, top: ink.top, right: ink.right, bottom: ink.bottom, width: ink.width, height: ink.height },
      zone: r(el.querySelector('[data-part="marker-zone"]'))!,
      marker: r(el.querySelector('[data-part="quality-marker"]')),
      rail: r(el.querySelector('[data-part="rail"]'))!,
      alarm: r(el.querySelector('[data-part="alarm-marker"]')),
      queue: r(el.querySelector('[data-part="queue-badge"]')),
      idFamily: getComputedStyle(id).fontFamily,
      valueFamily: getComputedStyle(value).fontFamily,
      padLeft: parseFloat(getComputedStyle(el).paddingLeft),
      padRight: parseFloat(getComputedStyle(el).paddingRight),
    };
  });
}

async function controlsToken(page: Page): Promise<string> {
  const r = await page.request.get('/api/spike/test-controls');
  expect(r.status(), 'harness must run with --synthetic-test-controls').toBe(200);
  return (await r.json()).token;
}

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ request }) => {
  await scenario(request, 'quality-showcase', { enabled: false });
});
test.afterEach(async ({ page }) => {
  const token = await controlsToken(page);
  await page.request.post('/api/spike/scenario', { headers: { 'x-spike-token': token }, data: { command: 'visual-preset', params: { preset: 'reset' } } });
});

test('FONT-A Google Sans is loaded and used by app text, Sensor IDs, values, legend, controls; no reflow after load', async ({ page }) => {
  await open(page);
  const f = await page.evaluate(() => ({
    check700: document.fonts.check('700 13px "Google Sans"'),
    check400: document.fonts.check('400 13px "Google Sans"'),
    loaded: [...document.fonts].filter((x) => x.family.replace(/"/g, '') === 'Google Sans' && x.status === 'loaded').length,
  }));
  expect(f.check700).toBe(true);
  expect(f.check400).toBe(true);
  expect(f.loaded).toBeGreaterThan(0);
  const families = await page.evaluate(() =>
    ['[data-testid="app-name"]', '[data-testid="app-subtitle"]', '[data-testid="synthetic-badge"]', '[data-part="id"]', '[data-part="value"]', '[data-testid="map-legend"] [data-legend]', '[data-testid="diagnostics-toggle"]', '[data-testid="queue-preview"]', '[data-testid="sensor-detail"]', '[data-testid="wj-slot-rear"]'].map((s) => [s, getComputedStyle(document.querySelector(s)!).fontFamily]),
  );
  for (const [sel, fam] of families) expect(fam, sel).toMatch(/^"?Google Sans"?,/);
  // No layout change after the font is ready (first render waited for it).
  const snap = () => page.evaluate((ids) => ids.map((id) => document.querySelector(`[data-sensor-id="${id}"] [data-part="id"]`)!.getBoundingClientRect().width).concat([document.querySelector('[data-testid="app-name"]')!.getBoundingClientRect().width]), PROBE_IDS);
  const a = await snap();
  await page.waitForTimeout(1200);
  expect(await snap()).toEqual(a);
});

test('CELL-A every Sensor ID is unclipped; ID / marker / value / rail zones are separate; value centred', async ({ page, request }) => {
  await scenario(request, 'quality-showcase', { enabled: true });
  await open(page);
  const ids = await page.locator('[data-sensor-id]').evaluateAll((els) => els.map((e) => e.getAttribute('data-sensor-id')!));
  expect(ids).toHaveLength(106);
  for (const id of ids) {
    const z = await zones(page, id);
    expect(z.idScroll.sw, `${id} ID clipped`).toBeLessThanOrEqual(z.idScroll.cw);
    expect(z.idText).toBe(id);
    for (const part of [z.id, z.value, z.zone, z.rail]) expect(within(part, z.cell), `${id} part inside cell`).toBe(true);
    expect(intersects(z.id, z.zone), `${id} ID vs marker zone`).toBe(false);
    if (z.marker) {
      expect(within(z.marker, z.zone), `${id} marker inside its zone`).toBe(true);
      expect(intersects(z.marker, z.valueInk), `${id} marker vs value`).toBe(false);
      expect(intersects(z.marker, z.id), `${id} marker vs ID`).toBe(false);
    }
    // Value centred on the cell content box (symmetric spacer | value | marker columns).
    const contentCentre = (z.cell.left + z.padLeft + (z.cell.right - z.padRight)) / 2;
    expect(Math.abs((z.valueInk.left + z.valueInk.right) / 2 - contentCentre), `${id} value centred`).toBeLessThanOrEqual(1.5);
  }
  for (const id of PROBE_IDS) {
    const z = await zones(page, id);
    expect(z.idFamily).toMatch(/^"?Google Sans"?,/);
    expect(z.valueFamily).toMatch(/^"?Google Sans"?,/);
    expect(z.cell.width).toBeGreaterThanOrEqual(52 - EPS);
    expect(z.cell.width).toBeLessThanOrEqual(56 + EPS);
    expect(z.cell.height).toBeGreaterThanOrEqual(46 - EPS);
    expect(z.cell.height).toBeLessThanOrEqual(50 + EPS);
  }
});

test('IDENT-A application identity: 19 px name + 11 px subtitle, separate badge, single row, no overflow', async ({ page }) => {
  await open(page);
  const m = await page.evaluate(() => {
    const q = (s: string) => document.querySelector(`[data-testid="${s}"]`) as HTMLElement;
    const r = (s: string) => q(s).getBoundingClientRect();
    const cs = (s: string) => getComputedStyle(q(s));
    return {
      nameSize: parseFloat(cs('app-name').fontSize),
      nameWeight: Number(cs('app-name').fontWeight),
      subSize: parseFloat(cs('app-subtitle').fontSize),
      subWeight: Number(cs('app-subtitle').fontWeight),
      subSpacing: parseFloat(cs('app-subtitle').letterSpacing) / parseFloat(cs('app-subtitle').fontSize),
      nameOverflow: q('app-name').scrollWidth - q('app-name').clientWidth,
      subOverflow: q('app-subtitle').scrollWidth - q('app-subtitle').clientWidth,
      badgeOverflow: q('synthetic-badge').scrollWidth - q('synthetic-badge').clientWidth,
      identity: r('app-identity'),
      badge: r('synthetic-badge'),
      bar: r('status-bar'),
      label: q('app-identity').getAttribute('aria-label'),
    };
  });
  expect(m.nameSize).toBe(19);
  expect(m.nameWeight).toBe(700);
  expect(m.subSize).toBe(11);
  expect(m.subWeight).toBeGreaterThanOrEqual(600);
  expect(m.subSpacing).toBeGreaterThanOrEqual(0.05 - 0.005);
  expect(m.subSpacing).toBeLessThanOrEqual(0.07 + 0.005);
  expect(m.nameOverflow).toBeLessThanOrEqual(0);
  expect(m.subOverflow).toBeLessThanOrEqual(0);
  expect(m.badgeOverflow).toBeLessThanOrEqual(0);
  expect(intersects(m.identity, m.badge)).toBe(false);
  expect(within(m.identity, m.bar)).toBe(true);
  expect(within(m.badge, m.bar)).toBe(true);
  expect(m.label).toBe('WaterJet Sentinel Suite — Operations Console');
});

test('LEGEND-A eight entries; swatches and labels fully inside the legend; no clipping at any edge', async ({ page }) => {
  await open(page);
  const m = await page.evaluate(() => {
    const legend = document.querySelector('[data-testid="map-legend"]') as HTMLElement;
    const centre = document.querySelector('[data-testid="map-center"]') as HTMLElement;
    const rr = (e: Element) => {
      const b = e.getBoundingClientRect();
      return { left: b.left, top: b.top, right: b.right, bottom: b.bottom, width: b.width, height: b.height };
    };
    return {
      legend: rr(legend),
      centre: rr(centre),
      centreOverflow: [centre.scrollWidth - centre.clientWidth, centre.scrollHeight - centre.clientHeight],
      items: [...legend.querySelectorAll('[data-legend]')].map((i) => {
        const label = i.lastElementChild as HTMLElement;
        return { kind: i.getAttribute('data-legend'), text: label.textContent, item: rr(i), svg: rr(i.querySelector('svg')!), label: rr(label), labelOverflow: label.scrollWidth - label.clientWidth };
      }),
    };
  });
  expect(m.items.map((i) => i.text)).toEqual(['Dirty', 'Cleaner', 'Not classified', 'Uncertain', 'Alarm', 'Selected', 'Active Job', 'Water Jet']);
  expect(m.centreOverflow).toEqual([0, 0]);
  expect(within(m.legend, m.centre)).toBe(true);
  for (const i of m.items) {
    expect(i.svg.width, i.kind!).toBeCloseTo(16, 0);
    expect(i.svg.height, i.kind!).toBeCloseTo(16, 0);
    expect(within(i.svg, i.item), `${i.kind} swatch inside item`).toBe(true);
    expect(within(i.label, i.item), `${i.kind} label inside item`).toBe(true);
    expect(within(i.item, m.legend), `${i.kind} item inside legend`).toBe(true);
    expect(intersects(i.svg, i.label), `${i.kind} swatch vs label`).toBe(false);
    expect(i.labelOverflow, `${i.kind} label clipped`).toBeLessThanOrEqual(0);
  }
});

test('WJ-A Water Jet reference slots read WJ / REAR and WJ / FRONT and are not selectable Sensors', async ({ page }) => {
  // Owner-local Edge correction: a Water Jet slot is not selectable, so clicking it must not create
  // or change a selection. The previous assertion waited for detail-id after clicking a slot with
  // no prior selection; detail-id correctly never appears in that state.
  await open(page);
  const slots = [
    ['wj-slot-rear', 'REAR'],
    ['wj-slot-front', 'FRONT'],
  ] as const;
  for (const [tid, side] of slots) {
    const slot = page.getByTestId(tid);
    await expect(slot).toBeVisible();
    await expect(slot).toContainText('WJ');
    await expect(slot).toContainText(side);
    await expect(slot).toHaveAttribute('data-slot-type', 'CANNON');
    await expect(slot).not.toHaveAttribute('data-sensor-id', /.*/);
    await expect(slot).not.toHaveAttribute('aria-pressed', /.*/);
    await expect(slot).not.toHaveAttribute('tabindex', /.*/);
  }
  // Water Jet slots are the logical I7 / I16 equipment positions: no I7 / I16 Sensor cells exist.
  await expect(page.locator('[data-slot-type="SENSOR"][data-sensor-id="I7"], [data-slot-type="SENSOR"][data-sensor-id="I16"]')).toHaveCount(0);

  // Record every spike command / scenario request issued while clicking the slots.
  const commands: string[] = [];
  page.on('request', (r) => {
    if (r.method() !== 'GET' && r.url().includes('/api/spike/')) commands.push(`${r.method()} ${r.url()}`);
  });
  const selectedCount = () => page.locator('[data-slot-type="SENSOR"][aria-pressed="true"]').count();
  const queueText = async () => (await page.getByTestId('queue-preview').textContent()) ?? '';

  // Case A — no current Sensor selection.
  await expect(page.getByTestId('detail-id')).toHaveCount(0);
  expect(await selectedCount()).toBe(0);
  for (const [tid] of slots) {
    await page.getByTestId(tid).click({ force: true });
    await page.waitForTimeout(250);
    await expect(page.getByTestId('detail-id'), `${tid}: no Sensor detail appears`).toHaveCount(0);
    expect(await selectedCount(), `${tid}: no selection state created`).toBe(0);
    await expect(page.getByTestId(tid)).not.toHaveClass(/selected/);
  }
  expect(commands, 'Case A: no command / queue / job request sent').toEqual([]);

  // Case B — an existing Sensor selection is retained when Water Jet slots are clicked.
  await page.locator('[data-sensor-id="G+205"]').click();
  await expect(page.getByTestId('detail-id')).toHaveText('G+205');
  const queueBefore = await queueText();
  for (const [tid] of [slots[0], slots[1]]) {
    await page.getByTestId(tid).click({ force: true });
    await page.waitForTimeout(250);
    await expect(page.getByTestId('detail-id'), `${tid}: Sensor detail unchanged`).toHaveText('G+205');
    await expect(page.locator('[data-sensor-id="G+205"]')).toHaveAttribute('aria-pressed', 'true');
    expect(await selectedCount(), `${tid}: exactly the original selection`).toBe(1);
    await expect(page.getByTestId(tid)).not.toHaveClass(/selected/);
    await expect(page.getByTestId('detail-id')).not.toHaveText(/CANNON|WJ/);
  }
  expect(commands, 'Case B: no command / queue / job request sent').toEqual([]);
  // A Water Jet slot never becomes a queue entry or a Job target.
  expect(await queueText()).not.toMatch(/\bWJ\b|CANNON/);
  expect(queueBefore).not.toMatch(/\bWJ\b|CANNON/);

  await expect(page.getByTestId('map-cannon-count')).toHaveText('2 Water Jet reference slots · synthetic');
  await expect(page.locator('body')).not.toContainText(/\b2 Water Jets\b|Cannon/);
});

test('QUEUE-A mixed-source GlobalQueue: bounded to 8, at least 3 source labels, FIFO positions', async ({ page }) => {
  await open(page);
  const token = await controlsToken(page);
  const res = await (await page.request.post('/api/spike/scenario', { headers: { 'x-spike-token': token }, data: { command: 'queue-mixed-sources', params: {} } })).json();
  expect(res.accepted).toBe(true);
  expect(res.detail.totalQueued).toBeLessThanOrEqual(8);
  await page.waitForTimeout(1500);
  const labels = await page.getByTestId('queue-reason').allTextContents();
  expect(labels.length).toBeGreaterThanOrEqual(5);
  expect(labels.length).toBeLessThanOrEqual(8);
  expect(labels.slice(0, 4)).toEqual(['TIME DUE', 'TEMP + TIME', 'OPERATOR', 'TEMP']);
  expect(new Set(labels).size).toBeGreaterThanOrEqual(3);
  expect(labels).toContain('DIRTY SCORE');
  await expect(page.getByTestId('queue-count')).toHaveText(`${labels.length} / 8 queued · FIFO · not Production scheduling`);
});

test('QUEUE-B GlobalQueue is synthetic, bounded (0..8 of 8), status-free, and the Active Job links to the dispatched head', async ({ page, request }) => {
  await scenario(request, 'set-dirty-mode', { mode: 'dirty70' });
  await open(page);
  await expect(page.getByTestId('queue-title')).toHaveText('GlobalQueue · synthetic');
  const q = page.getByTestId('queue-preview');
  for (let i = 0; i < 4; i += 1) {
    const n = await q.locator('tbody tr').count();
    expect(n).toBeLessThanOrEqual(8);
    await expect(page.getByTestId('queue-count')).toHaveText(/^[0-8] \/ 8 queued · FIFO · not Production scheduling$/);
    await page.waitForTimeout(1000);
  }
  await expect(q.locator('th')).toHaveText(['Pos', 'Sensor', 'Source reason', 'Score', 'Since clean']);
  await expect(q.locator('[data-status]')).toHaveCount(0);
  expect(await q.textContent()).not.toMatch(/\b(READY|BLOCKED|HELD|EXCLUDED|WAITING)\b/);
  expect(await page.locator('body').textContent()).not.toMatch(/\b7[67] queued\b/);
  // Active Job target == the Sensor of its Position 1 dispatch record (Diagnostics evidence).
  await expect(page.getByTestId('active-job')).toContainText('SYN-JOB-', { timeout: 10_000 });
  await page.getByTestId('diagnostics-toggle').click();
  const summary = page.getByTestId('diag-dispatch-summary');
  await expect(summary).toHaveText(/^Position 1 · \S+ · Queue revision \d+→\d+ · SYN-DSP-\d{4}$/);
  const dispatched = (await summary.textContent())!.split(' · ')[1];
  const snap = await (await request.get('/api/snapshot')).json();
  if (snap.activeJob) {
    expect(snap.activeJob.dispatch.sensorId).toBe(snap.activeJob.targetSensorId);
    expect(snap.activeJob.dispatch.positionBefore).toBe(1);
    expect(snap.queue.entries.map((e: { sensorId: string }) => e.sensorId)).not.toContain(snap.activeJob.targetSensorId);
  }
  expect(dispatched).toMatch(/^[GHIJ]\+?\d+$/);
  await scenario(request, 'set-dirty-mode', { mode: 'normal' });
});

test('CTRL-A synthetic test control: disabled without selection; preset 1 queues the DIRTY Sensor at Position 1; alarm + badge zones separate', async ({ page }) => {
  await open(page);
  await page.getByTestId('diagnostics-toggle').click();
  const panel = page.getByTestId('synthetic-test-control');
  await expect(panel).toHaveAttribute('data-availability', 'enabled');
  await expect(page.getByTestId('stc-preset-queued-dirty')).toBeDisabled();
  await page.locator('[data-sensor-id="G+205"]').click();
  await expect(page.getByTestId('stc-selected')).toHaveText('G+205');
  await page.getByTestId('stc-preset-queued-dirty').click();
  await expect(page.getByTestId('stc-result')).toHaveText('visual-preset: accepted');
  const cell = page.locator('[data-sensor-id="G+205"]');
  await expect(cell.locator('[data-part="queue-badge"]')).toHaveText('Q', { timeout: 5000 });
  await expect(page.locator('[data-testid="queue-preview"] tbody tr').first().locator('td').nth(1)).toHaveText('G+205');
  // Preset 5: Alarm on the Active Job Sensor -> alarm icon (left) and Job badge (right) on the rail.
  await page.getByTestId('stc-preset-alarm-active-job').click();
  await expect(page.getByTestId('stc-result')).toHaveText('visual-preset: accepted');
  await expect(cell.locator('[data-part="alarm-marker"]')).toBeVisible({ timeout: 5000 });
  await expect(cell.locator('[data-part="queue-badge"]')).toHaveText('J');
  const z = await zones(page, 'G+205');
  expect(z.alarm && z.queue).toBeTruthy();
  expect(intersects(z.alarm!, z.queue!), 'alarm vs badge').toBe(false);
  expect(within(z.alarm!, z.rail) && within(z.queue!, z.rail)).toBe(true);
  expect(z.alarm!.left).toBeLessThan(z.queue!.left); // alarm left, badge right
  expect(z.idScroll.sw).toBeLessThanOrEqual(z.idScroll.cw);
  if (z.marker) {
    expect(intersects(z.marker, z.alarm!)).toBe(false);
    expect(intersects(z.marker, z.queue!)).toBe(false);
  }
});

test('CTRL-B presets 2..7 give valid queue / job states (head-only dispatch, no retarget); preset 8 resets; never two jobs', async ({ page, request }) => {
  await open(page);
  await page.getByTestId('diagnostics-toggle').click();
  const id = 'G+218';
  await page.locator(`[data-sensor-id="${id}"]`).click();
  const rows = page.locator('[data-testid="queue-preview"] tbody tr');
  const rowSensor = (i: number) => rows.nth(i).locator('td').nth(1);
  const badge = page.locator(`[data-sensor-id="${id}"] [data-part="queue-badge"]`);
  const apply = async (p: string) => {
    await page.getByTestId(`stc-${p}`).click();
    await expect(page.getByTestId('stc-result')).toHaveText('visual-preset: accepted');
    await page.waitForTimeout(600);
    expect(await rows.count(), p).toBeLessThanOrEqual(8);
    await expect(page.locator('[data-testid="queue-preview"] [data-status]')).toHaveCount(0);
    const z = await zones(page, id);
    expect(z.idScroll.sw, p).toBeLessThanOrEqual(z.idScroll.cw);
  };
  await apply('preset-queued-cleaner');
  await expect(rowSensor(0)).toHaveText(id);
  await expect(rows.first().getByTestId('queue-reason')).toHaveText('OPERATOR');
  await apply('preset-selected-queued');
  await expect(rowSensor(1)).toHaveText(id);
  await expect(badge).toHaveText('Q');
  await apply('preset-dispatched-head');
  await expect(page.getByTestId('active-job')).toContainText(id);
  await expect(badge).toHaveText('J');
  await expect(page.getByTestId('diag-dispatch-summary')).toHaveText(new RegExp(`^Position 1 · ${id.replace('+', '\\+')} · `));
  expect(await rows.allTextContents()).not.toContainEqual(expect.stringContaining(id));
  await apply('preset-alarm-not-admitted');
  await expect(page.locator(`[data-sensor-id="${id}"] [data-part="alarm-marker"]`)).toBeVisible();
  await expect(badge).toHaveCount(0);
  await expect(page.getByTestId('diag-eligibility')).toContainText(`${id} · NOT ADMITTED · Reason pending Owner-approved eligibility policy`);
  await apply('preset-head-to-job');
  await expect(page.getByTestId('active-job')).toContainText(id);
  await expect(page.getByTestId('diag-eligibility')).toHaveCount(0);
  await page.getByTestId('stc-preset-reset').click();
  await expect(page.getByTestId('stc-result')).toHaveText('visual-preset: accepted');
  await expect(page.getByTestId('queue-count')).toHaveText(/^[0-8] \/ 8 queued/);
  const m = await (await request.get('/api/spike/metrics')).json();
  expect(m.jobs.acceptedSecondJobs).toBe(0);
  expect(m.invariants.violations).toBe(0);
  expect(m.queue.length).toBeLessThanOrEqual(8);
});

test('CTRL-C drawer scrolls internally: opening the controls never changes document dimensions', async ({ page }) => {
  await open(page);
  const dims = () => page.evaluate(() => [document.documentElement.scrollWidth, document.documentElement.scrollHeight, document.documentElement.clientWidth, document.documentElement.clientHeight]);
  const before = await dims();
  await page.getByTestId('diagnostics-toggle').click();
  await expect(page.getByTestId('synthetic-test-control')).toBeVisible();
  expect(await dims()).toEqual(before);
  expect(before[0]).toBe(before[2]);
  expect(before[1]).toBe(before[3]);
});
