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
    const spans = el.querySelectorAll('span');
    return {
      width: r.width,
      height: r.height,
      idFont: parseFloat(getComputedStyle(spans[0]).fontSize),
      valueFont: parseFloat(getComputedStyle(spans[1]).fontSize),
      idWeight: Number(getComputedStyle(spans[0]).fontWeight),
      valueWeight: Number(getComputedStyle(spans[1]).fontWeight),
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

  // Sensor cell scale and typography (computed values, not CSS source).
  for (const id of ['G+201', 'H7', 'J18']) {
    const m = await cellMetrics(page, id);
    expect(m.width, `${id} width`).toBeGreaterThanOrEqual(48 - EPS);
    expect(m.width, `${id} width`).toBeLessThanOrEqual(52 + EPS);
    expect(m.height, `${id} height`).toBeGreaterThanOrEqual(42 - EPS);
    expect(m.height, `${id} height`).toBeLessThanOrEqual(48 + EPS);
    expect(m.idFont).toBeGreaterThanOrEqual(12);
    expect(m.valueFont).toBeGreaterThanOrEqual(14);
    expect(m.idWeight).toBeGreaterThanOrEqual(650);
    expect(m.valueWeight).toBeGreaterThanOrEqual(650);
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
  expect(m.idFont).toBeGreaterThanOrEqual(12);
  expect(m.valueFont).toBeGreaterThanOrEqual(14);
  expect(m.height).toBeGreaterThanOrEqual(42 - EPS);
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
  expect(m.width).toBeLessThanOrEqual(54 + EPS);
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
