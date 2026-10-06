// WJSS Stage 0.2.1A — Owner-local fullscreen layout assertions in installed Microsoft Edge.
// PLANNED FOR OWNER-LOCAL EXECUTION. NOT EXECUTED IN ARENA (no browser available there).
// A Playwright viewport of 1920 x 1080 approximates Edge F11 fullscreen at 100 % zoom (no
// browser chrome). The Owner's manual F11 visual re-review remains required.
// Edge results do not verify WebView2 or kiosk-shell behaviour.
import { expect, test, type Locator, type Page } from '@playwright/test';
import { scenario } from './support';

type Box = { x: number; y: number; width: number; height: number };
const EPS = 0.5;

const PRIMARY = ['wall-overview', 'sensor-detail', 'active-job', 'queue-preview', 'pressure-trend', 'camera-placeholder'] as const;

async function box(l: Locator): Promise<Box> {
  const b = await l.boundingBox();
  expect(b, 'element has a layout box').not.toBeNull();
  return b!;
}
function inside(b: Box, vw: number, vh: number) {
  return b.x >= -EPS && b.y >= -EPS && b.x + b.width <= vw + EPS && b.y + b.height <= vh + EPS;
}
function overlaps(a: Box, b: Box) {
  return a.x < b.x + b.width - EPS && b.x < a.x + a.width - EPS && a.y < b.y + b.height - EPS && b.y < a.y + a.height - EPS;
}
async function docDims(page: Page) {
  return page.evaluate(() => {
    const d = document.documentElement;
    return { sh: d.scrollHeight, ch: d.clientHeight, sw: d.scrollWidth, cw: d.clientWidth };
  });
}
async function open(page: Page, width: number, height: number) {
  await page.setViewportSize({ width, height });
  await page.goto('/');
  await expect(page.getByTestId('conn-state')).toHaveText('LIVE', { timeout: 15_000 });
  await page.waitForTimeout(800);
}
async function cellMetrics(page: Page, sensorId: string) {
  return page.locator(`[data-sensor-id="${sensorId}"]`).evaluate((el) => {
    const r = el.getBoundingClientRect();
    const id = el.querySelector('[data-part="id"]')!;
    const value = el.querySelector('[data-part="value"]')!;
    return {
      width: r.width,
      height: r.height,
      idFont: parseFloat(getComputedStyle(id).fontSize),
      valueFont: parseFloat(getComputedStyle(value).fontSize),
      idWeight: Number(getComputedStyle(id).fontWeight),
      valueWeight: Number(getComputedStyle(value).fontWeight),
    };
  });
}

test.describe.configure({ mode: 'serial' });

test.beforeEach(async ({ request }) => {
  await scenario(request, 'quality-showcase', { enabled: false });
});

test('LAYOUT-A 1920 x 1080: no page scroll, every primary surface inside the viewport, larger readable cells', async ({ page, request }) => {
  await scenario(request, 'set-dirty-mode', { mode: 'dirty70' });
  await open(page, 1920, 1080);
  await page.waitForTimeout(2500);

  const d = await docDims(page);
  expect(d.sh, 'document vertical overflow').toBeLessThanOrEqual(d.ch);
  expect(d.sw, 'document horizontal overflow').toBeLessThanOrEqual(d.cw);

  expect(inside(await box(page.getByTestId('operations-page')), 1920, 1080)).toBe(true);
  expect(inside(await box(page.getByTestId('status-bar')), 1920, 1080)).toBe(true);
  expect(inside(await box(page.getByTestId('alarm-strip')), 1920, 1080)).toBe(true);
  const boxes: Record<string, Box> = {};
  for (const id of PRIMARY) {
    boxes[id] = await box(page.getByTestId(id));
    expect(inside(boxes[id], 1920, 1080), `${id} inside viewport`).toBe(true);
  }
  for (let i = 0; i < PRIMARY.length; i += 1)
    for (let j = i + 1; j < PRIMARY.length; j += 1) expect(overlaps(boxes[PRIMARY[i]], boxes[PRIMARY[j]]), `${PRIMARY[i]} overlaps ${PRIMARY[j]}`).toBe(false);

  // Eight GlobalQueue rows fully visible inside the queue card and the viewport.
  const rows = page.getByTestId('queue-preview').locator('tbody tr');
  await expect(rows).toHaveCount(8, { timeout: 10_000 });
  for (let i = 0; i < 8; i += 1) {
    const r = await box(rows.nth(i));
    expect(inside(r, 1920, 1080), `queue row ${i + 1} inside viewport`).toBe(true);
    expect(r.y + r.height, `queue row ${i + 1} inside queue card`).toBeLessThanOrEqual(boxes['queue-preview'].y + boxes['queue-preview'].height + EPS);
  }

  // Sensor cell scale and typography (computed values, not CSS source). Readability refinement:
  // cells 52..56 x 46..50 px, ID 13 px / 700, value 16 px / 700.
  for (const id of ['G+201', 'H7', 'J18']) {
    const m = await cellMetrics(page, id);
    expect(m.width, `${id} width`).toBeGreaterThanOrEqual(52 - EPS);
    expect(m.width, `${id} width`).toBeLessThanOrEqual(56 + EPS);
    expect(m.height, `${id} height`).toBeGreaterThanOrEqual(46 - EPS);
    expect(m.height, `${id} height`).toBeLessThanOrEqual(50 + EPS);
    expect(m.idFont).toBeGreaterThanOrEqual(13);
    expect(m.valueFont).toBeGreaterThanOrEqual(16);
    expect(m.idWeight).toBeGreaterThanOrEqual(700);
    expect(m.valueWeight).toBeGreaterThanOrEqual(700);
  }
  // Cannon slots: same outer size as a Sensor cell.
  const cell = await box(page.locator('[data-sensor-id="I8"]'));
  for (const c of await page.locator('[data-slot-type="CANNON"]').all()) {
    const b = await box(c);
    expect(Math.abs(b.width - cell.width)).toBeLessThanOrEqual(1);
    expect(Math.abs(b.height - cell.height)).toBeLessThanOrEqual(1);
  }

  await expect(page.locator('[data-sensor-id]')).toHaveCount(106);
  await expect(page.locator('[data-slot-type="CANNON"]')).toHaveCount(2);
  // Trend: still one bounded uPlot instance, fully inside its card.
  await expect(page.locator('.uplot')).toHaveCount(1);
  const plot = await box(page.locator('.uplot'));
  expect(plot.y + plot.height).toBeLessThanOrEqual(boxes['pressure-trend'].y + boxes['pressure-trend'].height + EPS);
  const cam = await box(page.getByTestId('camera-placeholder').locator('svg'));
  expect(cam.y + cam.height).toBeLessThanOrEqual(boxes['camera-placeholder'].y + boxes['camera-placeholder'].height + EPS);

  await scenario(request, 'set-dirty-mode', { mode: 'normal' });
});

test('LAYOUT-B U-shaped geometry: Rear above, Left left, Right right, Front below a compact center', async ({ page }) => {
  await open(page, 1920, 1080);
  const wall = (w: string) => box(page.locator(`[data-wall="${w}"]`));
  const [rear, left, right, front] = await Promise.all(['REAR', 'LEFT', 'RIGHT', 'FRONT'].map(wall));
  const center = await box(page.getByTestId('map-center'));
  expect(rear.y + rear.height).toBeLessThanOrEqual(center.y + EPS);
  expect(left.x + left.width).toBeLessThanOrEqual(center.x + EPS);
  expect(right.x).toBeGreaterThanOrEqual(center.x + center.width - EPS);
  expect(front.y).toBeGreaterThanOrEqual(center.y + center.height - EPS);
  expect(center.width).toBeLessThanOrEqual(210 + EPS);
  expect(center.height).toBeLessThanOrEqual(170 + EPS);
  const walls = [rear, left, right, front];
  for (let i = 0; i < 4; i += 1) for (let j = i + 1; j < 4; j += 1) expect(overlaps(walls[i], walls[j]), `walls ${i}/${j} overlap`).toBe(false);
  // Side walls share Y range with the Rear / Front blocks (reduced center space).
  expect(left.y).toBeLessThan(rear.y + rear.height);
  expect(left.y + left.height).toBeGreaterThan(front.y);
  // U order and canonical first ids unchanged.
  expect(await page.locator('[data-wall]').evaluateAll((els) => els.map((e) => e.getAttribute('data-wall')))).toEqual(['REAR', 'LEFT', 'RIGHT', 'FRONT']);
  const firstIds = await page.locator('[data-wall="LEFT"] [data-wall-row]').evaluateAll((rows) => rows.map((r) => r.firstElementChild?.getAttribute('data-sensor-id')));
  expect(firstIds).toEqual(['G+201', 'G+101', 'G1', 'H1', 'I1', 'J1']);
  // Map surface is inside its card (no clipped wall at the primary target).
  const card = await box(page.getByTestId('wall-overview'));
  for (const w of walls) expect(w.x >= card.x - EPS && w.y >= card.y - EPS && w.x + w.width <= card.x + card.width + EPS && w.y + w.height <= card.y + card.height + EPS).toBe(true);
});

test('LAYOUT-C Diagnostics drawer: no scroll change, inside viewport, closable, never over the Camera', async ({ page }) => {
  await open(page, 1920, 1080);
  const before = await docDims(page);
  await page.getByTestId('diagnostics-toggle').click();
  const drawer = page.getByTestId('diagnostics');
  await expect(drawer).toBeVisible();
  await page.waitForTimeout(1200);
  const after = await docDims(page);
  expect(after.sh).toBe(before.sh);
  expect(after.sw).toBe(before.sw);
  const db = await box(drawer);
  expect(inside(db, 1920, 1080)).toBe(true);
  expect(db.width).toBeGreaterThanOrEqual(300 - EPS);
  expect(db.width).toBeLessThanOrEqual(340 + EPS);
  expect(overlaps(db, await box(page.getByTestId('camera-placeholder')))).toBe(false);
  expect(overlaps(db, await box(page.getByTestId('pressure-trend')))).toBe(false);
  await page.getByTestId('diagnostics-close').click();
  await expect(drawer).toHaveCount(0);
  // After closing, the Active Job card is not covered.
  const job = await box(page.getByTestId('active-job'));
  const hit = await page.evaluate(([x, y]) => document.elementFromPoint(x, y)?.closest('[data-testid]')?.getAttribute('data-testid'), [job.x + job.width - 20, job.y + 20]);
  expect(hit).toBe('active-job');
  const closed = await docDims(page);
  expect(closed.sh).toBe(before.sh);
  // Keyboard toggle still works.
  await page.keyboard.press('d');
  await expect(drawer).toBeVisible();
  await page.keyboard.press('d');
  await expect(drawer).toHaveCount(0);
});

test('LAYOUT-D 1366 x 768: no horizontal page overflow, no card overlap, readable cells, controlled internal scroll', async ({ page }) => {
  await open(page, 1366, 768);
  const d = await docDims(page);
  expect(d.sw).toBeLessThanOrEqual(d.cw);
  expect(d.sh).toBeLessThanOrEqual(d.ch);
  const boxes = await Promise.all(PRIMARY.map((id) => box(page.getByTestId(id))));
  for (let i = 0; i < boxes.length; i += 1) for (let j = i + 1; j < boxes.length; j += 1) expect(overlaps(boxes[i], boxes[j]), `${PRIMARY[i]} overlaps ${PRIMARY[j]}`).toBe(false);
  for (const b of boxes) expect(b.x + b.width).toBeLessThanOrEqual(1366 + EPS);
  const m = await cellMetrics(page, 'G+201');
  expect(m.idFont).toBeGreaterThanOrEqual(13);
  expect(m.valueFont).toBeGreaterThanOrEqual(16);
  expect(m.height).toBeGreaterThanOrEqual(46 - EPS);
  await expect(page.locator('[data-sensor-id="G+205"]')).toBeInViewport();
  await expect(page.locator('[data-sensor-id]')).toHaveCount(106);
  // Any overflow is handled inside a panel with controlled scrolling, never by the page.
  const scrollers = await page.evaluate(() =>
    ['wall-overview', 'queue-preview'].map((id) => {
      const el = document.querySelector(`[data-testid="${id}"]`) as HTMLElement;
      const inner = id === 'queue-preview' ? (el.querySelector('table')!.parentElement as HTMLElement) : el;
      const cs = getComputedStyle(inner);
      return { id, overflowing: inner.scrollHeight > inner.clientHeight + 1, overflowY: cs.overflowY };
    }),
  );
  for (const s of scrollers) if (s.overflowing) expect(['auto', 'scroll'], `${s.id} scrolls internally`).toContain(s.overflowY);
});

test('LAYOUT-E 2560 x 1440: capped cell size, bounded center, centered dense layout', async ({ page }) => {
  await open(page, 2560, 1440);
  const d = await docDims(page);
  expect(d.sw).toBeLessThanOrEqual(d.cw);
  expect(d.sh).toBeLessThanOrEqual(d.ch);
  const m = await cellMetrics(page, 'H7');
  expect(m.width).toBeLessThanOrEqual(56 + EPS);
  expect(m.height).toBeLessThanOrEqual(50 + EPS);
  const center = await box(page.getByTestId('map-center'));
  expect(center.width).toBeLessThanOrEqual(210 + EPS);
  expect(center.height).toBeLessThanOrEqual(170 + EPS);
  const left = await box(page.locator('[data-wall="LEFT"]'));
  const right = await box(page.locator('[data-wall="RIGHT"]'));
  const rear = await box(page.locator('[data-wall="REAR"]'));
  // Horizontal space between the side walls stays bounded (Rear block plus modest margins).
  expect(right.x - (left.x + left.width)).toBeLessThanOrEqual(rear.width + 2 * 160);
  const main = await box(page.locator('main'));
  expect(Math.abs(main.x - (2560 - (main.x + main.width)))).toBeLessThanOrEqual(2);
  for (const id of PRIMARY) expect(inside(await box(page.getByTestId(id)), 2560, 1440), `${id} inside viewport`).toBe(true);
});

// ---------------------------------------------------------------------------------------------
// Readability refinement (READ-*). Computed geometry and styles in Owner-local Microsoft Edge.
// ---------------------------------------------------------------------------------------------

type Rect = { l: number; t: number; r: number; b: number };

/** Per-cell zone geometry for every Sensor cell (one evaluate call). */
async function cellZones(page: Page) {
  return page.evaluate(() => {
    const R = (el: Element | null) => {
      if (!el) return null;
      const b = el.getBoundingClientRect();
      return { l: b.left, t: b.top, r: b.right, b: b.bottom };
    };
    return [...document.querySelectorAll<HTMLElement>('[data-slot-type="SENSOR"]')].map((cell) => {
      const id = cell.querySelector<HTMLElement>('[data-part="id"]')!;
      const value = cell.querySelector<HTMLElement>('[data-part="value"]')!;
      const cs = getComputedStyle(cell);
      const bw = parseFloat(cs.borderLeftWidth);
      const r = cell.getBoundingClientRect();
      return {
        sensorId: cell.dataset.sensorId!,
        cell: { l: r.left + bw, t: r.top + parseFloat(cs.borderTopWidth), r: r.right - parseFloat(cs.borderRightWidth), b: r.bottom - parseFloat(cs.borderBottomWidth) },
        id: R(id)!,
        idClipped: id.scrollWidth > id.clientWidth + 0.5,
        idText: id.textContent,
        markerZone: R(cell.querySelector('[data-part="marker-zone"]'))!,
        qualityMarker: R(cell.querySelector('[data-part="quality-marker"]')),
        value: R(value)!,
        valueClipped: value.scrollWidth > value.clientWidth + 0.5 || value.scrollHeight > value.clientHeight + 0.5,
        rail: R(cell.querySelector('[data-part="rail"]'))!,
        badge: R(cell.querySelector('[data-part="queue-badge"]')),
        alarm: R(cell.querySelector('[data-part="alarm-marker"]')),
        idFont: parseFloat(getComputedStyle(id).fontSize),
        valueFont: parseFloat(getComputedStyle(value).fontSize),
      };
    });
  });
}
const hit = (a: Rect, b: Rect) => a.l < b.r - EPS && b.l < a.r - EPS && a.t < b.b - EPS && b.t < a.b - EPS;
const within = (a: Rect, outer: Rect) => a.l >= outer.l - EPS && a.t >= outer.t - EPS && a.r <= outer.r + EPS && a.b <= outer.b + EPS;

test('READ-A 1920 x 1080: Sensor ID, quality marker, value, alarm icon and queue badge never intersect; nothing clipped', async ({ page, request }) => {
  await scenario(request, 'set-dirty-mode', { mode: 'dirty70' });
  await scenario(request, 'quality-showcase', { enabled: true });
  const raised: string[] = [];
  for (const id of ['G+205', 'H12', 'J18']) raised.push((await scenario(request, 'raise-alarm', { sensorId: id })).detail.alarmId);
  await open(page, 1920, 1080);
  await page.waitForTimeout(2500);
  const zones = await cellZones(page);
  expect(zones).toHaveLength(106);
  expect(zones.filter((z) => z.qualityMarker).length, 'quality markers rendered').toBeGreaterThanOrEqual(4);
  expect(zones.filter((z) => z.alarm).length, 'alarm icons rendered').toBeGreaterThanOrEqual(3);
  expect(zones.filter((z) => z.badge).length, 'queue badges rendered').toBeGreaterThan(0);
  for (const z of zones) {
    const tag = z.sensorId;
    expect(z.idText).toBe(z.sensorId);
    expect(z.idClipped, `${tag} ID clipped`).toBe(false);
    expect(z.valueClipped, `${tag} value clipped`).toBe(false);
    expect(hit(z.id, z.markerZone), `${tag} ID under the marker zone`).toBe(false);
    expect(z.markerZone.l - z.id.r, `${tag} ID-to-marker gap`).toBeGreaterThanOrEqual(1);
    expect(hit(z.id, z.rail), `${tag} ID / rail`).toBe(false);
    expect(hit(z.value, z.rail), `${tag} value / rail`).toBe(false);
    for (const [name, r] of [
      ['ID', z.id],
      ['marker zone', z.markerZone],
      ['value', z.value],
      ['rail', z.rail],
    ] as const)
      expect(within(r, z.cell), `${tag} ${name} inside the cell`).toBe(true);
    if (z.qualityMarker) {
      expect(within(z.qualityMarker, z.markerZone), `${tag} quality marker inside its zone`).toBe(true);
      expect(hit(z.qualityMarker, z.id), `${tag} quality marker / ID`).toBe(false);
    }
    if (z.badge) {
      expect(within(z.badge, z.cell), `${tag} queue badge inside the cell`).toBe(true);
      expect(hit(z.badge, z.id) || hit(z.badge, z.value), `${tag} queue badge / text`).toBe(false);
    }
    if (z.alarm) {
      expect(within(z.alarm, z.cell), `${tag} alarm icon inside the cell`).toBe(true);
      expect(hit(z.alarm, z.id) || hit(z.alarm, z.value), `${tag} alarm icon / text`).toBe(false);
      if (z.qualityMarker) expect(hit(z.alarm, z.qualityMarker), `${tag} alarm obscures quality`).toBe(false);
      if (z.badge) expect(hit(z.alarm, z.badge), `${tag} alarm / badge`).toBe(false);
    }
    expect(z.idFont).toBeGreaterThanOrEqual(13);
    expect(z.valueFont).toBeGreaterThanOrEqual(16);
  }
  // The long logical IDs explicitly (Owner finding: G+205).
  for (const id of ['G+201', 'G+205', 'G+105', 'J18', 'H12']) expect(zones.find((z) => z.sensorId === id)!.idClipped, id).toBe(false);
  // Cannon slots keep the Sensor cell size.
  const cell = zones.find((z) => z.sensorId === 'I8')!.cell;
  for (const c of await page.locator('[data-slot-type="CANNON"]').all()) {
    const b = await box(c);
    expect(Math.abs(b.width - (cell.r - cell.l))).toBeLessThanOrEqual(5);
  }
  const d = await docDims(page);
  expect(d.sh).toBeLessThanOrEqual(d.ch);
  expect(d.sw).toBeLessThanOrEqual(d.cw);
  for (const a of raised) {
    await scenario(request, 'ack-alarm', { alarmId: a });
    await scenario(request, 'clear-alarm', { alarmId: a });
  }
  await scenario(request, 'quality-showcase', { enabled: false });
  await scenario(request, 'set-dirty-mode', { mode: 'normal' });
});

test('READ-B alarm, selection and Active Job are distinct channels on both Dirty and Cleaner cells', async ({ page, request }) => {
  await scenario(request, 'auto-jobs', { enabled: false });
  await scenario(request, 'abort-job', { immediate: true });
  await scenario(request, 'set-dirty-mode', { mode: 'dirty30' });
  await scenario(request, 'pump-start'); // ALREADY_RUNNING is fine
  await open(page, 1920, 1080);
  await expect(page.getByTestId('pump-state')).toContainText('ready', { timeout: 20_000 });
  await page.waitForTimeout(2500);
  const pick = (process: string) =>
    page.evaluate(
      (p) => [...document.querySelectorAll<HTMLElement>(`[data-slot-type="SENSOR"][data-process="${p}"][data-quality="GOOD"]`)].map((e) => e.dataset.sensorId!),
      process,
    );
  const dirty = await pick('DIRTY');
  const cleaner = await pick('CLEANER');
  expect(dirty.length).toBeGreaterThanOrEqual(2);
  expect(cleaner.length).toBeGreaterThanOrEqual(2);
  const raised: string[] = [];
  for (const id of [dirty[0], cleaner[0]]) raised.push((await scenario(request, 'raise-alarm', { sensorId: id })).detail.alarmId);
  const job = await scenario(request, 'start-job', { sensorId: cleaner[1] });
  expect(job.accepted, 'synthetic job start').toBe(true);
  await page.locator(`[data-sensor-id="${dirty[1]}"]`).click();
  await page.waitForTimeout(1500);

  const style = (id: string) =>
    page.locator(`[data-sensor-id="${id}"]`).evaluate((el) => {
      const cs = getComputedStyle(el);
      return { bg: cs.backgroundColor, border: cs.borderTopColor, borderW: parseFloat(cs.borderTopWidth), shadow: cs.boxShadow, outline: cs.outlineStyle, alarmIcon: !!el.querySelector('[data-part="alarm-marker"]') };
    });
  const alarmTok = await page.evaluate(() => {
    const p = document.createElement('div');
    p.style.color = 'var(--alarm)';
    document.body.appendChild(p);
    const c = getComputedStyle(p).color;
    p.remove();
    return c;
  });
  const dirtyAlarm = await style(dirty[0]);
  const cleanerAlarm = await style(cleaner[0]);
  const dirtyPlain = await style(dirty[2] ?? dirty[0]);
  for (const s of [dirtyAlarm, cleanerAlarm]) {
    expect(s.border, 'alarm border uses the alarm token').toBe(alarmTok);
    expect(s.borderW).toBeGreaterThanOrEqual(2);
    expect(s.alarmIcon, 'alarm icon present (not colour alone)').toBe(true);
  }
  // Alarm never changes the process background; alarm yellow is not the Dirty background.
  expect(dirtyAlarm.bg).not.toBe(alarmTok);
  expect(cleanerAlarm.bg).not.toBe(dirtyAlarm.bg);
  if (dirty[2]) expect(dirtyPlain.border).not.toBe(alarmTok);
  const selected = await style(dirty[1]);
  expect(selected.shadow, 'selection ring').toMatch(/0px 0px 0px 3px/);
  expect(selected.border).not.toBe(alarmTok);
  await expect(page.locator(`[data-sensor-id="${cleaner[1]}"]`)).toHaveAttribute('aria-label', /active job target/, { timeout: 5000 });
  const jobCell = await style(cleaner[1]);
  expect(jobCell.outline, 'Active Job double outline').toBe('double');
  expect(jobCell.shadow).not.toMatch(/0px 0px 0px 3px/);
  // Job card: exactly one current phase.
  await expect(page.getByTestId('active-job').locator('[aria-current="step"]')).toHaveCount(1);

  for (const a of raised) {
    await scenario(request, 'ack-alarm', { alarmId: a });
    await scenario(request, 'clear-alarm', { alarmId: a });
  }
  await scenario(request, 'abort-job', { immediate: true });
  await scenario(request, 'set-dirty-mode', { mode: 'normal' });
  await scenario(request, 'auto-jobs', { enabled: true });
});

test('READ-C typography minimums across the Operations page (no text below 10 px; approved sizes)', async ({ page }) => {
  await open(page, 1920, 1080);
  await page.locator('[data-sensor-id="H7"]').click();
  await page.waitForTimeout(800);
  const small = await page.evaluate(() => {
    const out: string[] = [];
    const walker = document.createTreeWalker(document.querySelector('[data-testid="operations-page"]')!, NodeFilter.SHOW_TEXT);
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      if (!n.textContent?.trim()) continue;
      const el = n.parentElement!;
      if (!el.getClientRects().length) continue;
      const fs = parseFloat(getComputedStyle(el).fontSize);
      if (fs < 10) out.push(`${fs}px: ${n.textContent.trim().slice(0, 30)}`);
    }
    return out;
  });
  expect(small, 'text below 10 px').toEqual([]);
  const fs = (sel: string) => page.locator(sel).first().evaluate((el) => ({ size: parseFloat(getComputedStyle(el).fontSize), weight: Number(getComputedStyle(el).fontWeight), num: getComputedStyle(el).fontVariantNumeric }));
  const checks: [string, number, number][] = [
    ['[data-testid="status-bar"] h1, [data-testid="status-bar"] > div > span:first-child', 15, 700],
    ['[data-testid="conn-state"]', 12.5, 600],
    ['[data-testid="alarm-strip"] > span', 12, 0],
    ['[data-testid="sensor-detail"] h2', 15, 700],
    ['[data-wall="REAR"] header span span', 14, 700],
    ['[data-testid="detail-id"]', 18, 700],
    ['[data-testid="detail-group-process"] dt', 12, 0],
    ['[data-testid="detail-class"]', 13, 600],
    ['[data-testid="queue-preview"] th', 12, 600],
    ['[data-testid="pressure-trend"] h2', 14, 700],
    ['[data-testid="camera-placeholder"] h2', 14, 700],
    ['[data-testid="camera-state"]', 12, 700],
  ];
  for (const [sel, size, weight] of checks) {
    const m = await fs(sel);
    expect(m.size, `${sel} size`).toBeGreaterThanOrEqual(size - 0.01);
    expect(m.weight, `${sel} weight`).toBeGreaterThanOrEqual(weight);
  }
  // Tabular numerals for Sensor values and queue numbers.
  expect((await fs('[data-sensor-id="H7"] [data-part="value"]')).num).toContain('tabular-nums');
  expect((await fs('[data-testid="queue-preview"] table')).num).toContain('tabular-nums');
  // No page scroll after the typography increase.
  const d = await docDims(page);
  expect(d.sh).toBeLessThanOrEqual(d.ch);
  expect(d.sw).toBeLessThanOrEqual(d.cw);
});

test('READ-D GlobalQueue numeric alignment, status bar grouping, trend and camera readability', async ({ page, request }) => {
  await scenario(request, 'set-dirty-mode', { mode: 'dirty70' });
  await open(page, 1920, 1080);
  await page.waitForTimeout(2500);
  const rows = page.getByTestId('queue-preview').locator('tbody tr');
  await expect(rows).toHaveCount(8, { timeout: 10_000 });
  // Score (col 4) and Since clean (col 5): right-aligned text with a common right edge.
  for (const col of [4, 5]) {
    const rights = await rows.evaluateAll(
      (trs, c) =>
        trs.map((tr) => {
          const td = tr.children[c - 1] as HTMLElement;
          const range = document.createRange();
          range.selectNodeContents(td);
          return { align: getComputedStyle(td).textAlign, right: range.getBoundingClientRect().right };
        }),
      col,
    );
    for (const r of rights) expect(r.align).toBe('right');
    expect(Math.max(...rights.map((r) => r.right)) - Math.min(...rights.map((r) => r.right)), `col ${col} right edges`).toBeLessThanOrEqual(1);
  }
  // GlobalQueue rows carry no status chips and never use red row backgrounds.
  const rowBgs = await rows.evaluateAll((trs) => trs.map((tr) => getComputedStyle(tr).backgroundColor));
  for (const bg of rowBgs) expect(bg).toMatch(/rgba\(0, 0, 0, 0\)|transparent/);
  // Status bar priority groups, single row at the primary target.
  const groups = await page.locator('[data-status-group]').evaluateAll((els) => els.map((e) => e.getAttribute('data-status-group')));
  expect(groups).toEqual(['app', 'alarm', 'process', 'system', 'technical']);
  const bar = await box(page.getByTestId('status-bar'));
  expect(bar.height).toBeLessThanOrEqual(42 + EPS);
  // Trend: one uPlot instance, legend text 12 px, current-state summary present.
  await expect(page.locator('.uplot')).toHaveCount(1);
  const legendFs = await page.locator('.u-legend').evaluate((el) => parseFloat(getComputedStyle(el).fontSize));
  expect(legendFs).toBeGreaterThanOrEqual(12);
  await expect(page.getByTestId('trend-summary')).toContainText('setpoint');
  // Camera: icon at least 40 px, state text readable, no media element.
  const icon = await box(page.getByTestId('camera-placeholder').locator('svg'));
  expect(icon.width).toBeGreaterThanOrEqual(40);
  await expect(page.getByTestId('camera-placeholder').locator('video, img, iframe')).toHaveCount(0);
  await scenario(request, 'set-dirty-mode', { mode: 'normal' });
});

