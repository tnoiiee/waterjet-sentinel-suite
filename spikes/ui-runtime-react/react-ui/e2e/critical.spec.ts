// WJSS Stage 0.2.1A — Owner critical Pump decision: blocking modal + Mandatory Safe Return in
// installed Microsoft Edge (CRIT-A..F).
// PLANNED FOR OWNER-LOCAL EXECUTION. NOT EXECUTED IN ARENA (no browser available there).
// Requires the harness started with --synthetic-test-controls (playwright.config.ts webServer).
// SYNTHETIC PROOF ONLY — not a physical Pump / relay / VFD / Isolation Valve / axis / interlock.
// Edge results do not verify WebView2 or kiosk-shell behaviour. No safety certification.
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import { scenario } from './support';

test.describe.configure({ mode: 'serial' });

type Snap = {
  queue: { entries: { sensorId: string }[]; revision: number };
  sequence: { autoSequence: string; critical: null | { modalOpen: boolean; acknowledged: boolean; conditionActive: boolean } };
  activeJob: null | { jobId: string; lifecycle: string };
};
const snapshot = async (request: APIRequestContext): Promise<Snap> => (await request.get('/api/snapshot')).json();

async function open(page: Page, width = 1920, height = 1080) {
  await page.setViewportSize({ width, height });
  await page.goto('/');
  await expect(page.getByTestId('conn-state')).toHaveText('LIVE', { timeout: 15_000 });
  await page.evaluate(() => document.fonts.ready);
}
/** Synthetic Pump running and in band (no Job is ever created while the Pump is not ready). */
async function pumpReady(request: APIRequestContext) {
  await expect.poll(async () => ((await request.get('/api/snapshot')).json() as Promise<{ pump: { ready: boolean } }>).then((s) => s.pump.ready), { timeout: 15_000 }).toBe(true);
}

test.beforeEach(async ({ request }) => {
  await scenario(request, 'critical-reset');
  await scenario(request, 'auto-jobs', { enabled: false });
  await pumpReady(request);
});
test.afterEach(async ({ request }) => {
  await scenario(request, 'critical-reset');
  await scenario(request, 'visual-preset', { preset: 'reset' });
});

test('CRIT-A Pump trip during an Active Job: modal immediately, Safe Return progresses live, queue frozen, AutoSequence CRITICAL_SUSPENDED', async ({ page, request }) => {
  await open(page);
  const r = await scenario(request, 'critical-scenario', { scenario: 'pump-trip-p4' });
  expect(r.accepted, JSON.stringify(r)).toBe(true);
  const modal = page.getByRole('alertdialog');
  await expect(modal).toBeVisible({ timeout: 3000 });
  await expect(page.getByTestId('critical-title')).toHaveText('CRITICAL ALARM — MAIN PUMP TRIPPED');
  await expect(page.getByTestId('critical-job')).toContainText('phase P4');
  await expect(page.getByTestId('critical-autosequence')).toContainText('CRITICAL SUSPENDED');
  const before = await snapshot(request);
  // Live Safe Return progression: valve close commanded, then confirmed, then axis / Standby.
  await expect(modal).toHaveAttribute('data-safe-return', /SR[2-5]/, { timeout: 5000 });
  await expect(page.getByTestId('critical-valve')).toContainText('CLOSED CONFIRMED', { timeout: 10_000 });
  await expect(page.getByTestId('critical-sr')).toContainText('COMPLETE', { timeout: 15_000 });
  await expect(modal).toBeVisible(); // still open: not cleared, not acknowledged
  const after = await snapshot(request);
  expect(after.queue.entries.map((e) => e.sensorId)).toEqual(before.queue.entries.map((e) => e.sensorId));
  expect(after.queue.revision).toBe(before.queue.revision);
  expect(after.activeJob).toBeNull();
  expect(after.sequence.autoSequence).toBe('CRITICAL_SUSPENDED');
});

test('CRIT-B unexpected Pump stop with no Job: modal, Safe Return not required, no Job created', async ({ page, request }) => {
  await open(page);
  expect((await scenario(request, 'critical-scenario', { scenario: 'pump-stop-no-job' })).accepted).toBe(true);
  await expect(page.getByRole('alertdialog')).toBeVisible({ timeout: 3000 });
  await expect(page.getByTestId('critical-title')).toHaveText('CRITICAL ALARM — MAIN PUMP STOPPED UNEXPECTEDLY');
  await expect(page.getByTestId('critical-job')).toHaveText('No Active Job at the event');
  await expect(page.getByTestId('critical-sr')).toContainText('Not required');
  await page.waitForTimeout(2500);
  expect((await snapshot(request)).activeJob).toBeNull();
});

test('CRIT-C Acknowledge: one click, state ACKNOWLEDGED, modal remains while the condition is active', async ({ page, request }) => {
  await open(page);
  await scenario(request, 'critical-scenario', { scenario: 'pump-trip-no-job' });
  const ack = page.getByTestId('critical-ack');
  await expect(ack).toBeFocused({ timeout: 3000 });
  await ack.click();
  await expect(page.getByTestId('critical-ack-state')).toContainText('ACKNOWLEDGED at', { timeout: 3000 });
  await expect(ack).toBeDisabled();
  await expect(page.getByTestId('critical-condition')).toHaveText('ACTIVE');
  await page.waitForTimeout(1500);
  await expect(page.getByRole('alertdialog')).toBeVisible();
});

test('CRIT-D condition cleared before Safe Return is complete: modal stays open until Standby is confirmed', async ({ page, request }) => {
  await open(page);
  await scenario(request, 'safe-return-config', { valveFeedbackDelayMs: 6000 });
  await scenario(request, 'critical-scenario', { scenario: 'pump-trip-p1' });
  await expect(page.getByRole('alertdialog')).toBeVisible({ timeout: 3000 });
  await page.getByTestId('critical-ack').click();
  await scenario(request, 'pump-fault-clear');
  await expect(page.getByTestId('critical-condition')).toContainText('CLEARED', { timeout: 3000 });
  await expect(page.getByTestId('critical-sr')).toContainText('IN PROGRESS');
  await expect(page.getByRole('alertdialog')).toBeVisible();
  await expect(page.getByTestId('critical-axis')).toContainText('NOT COMMANDED'); // axis waits for valve confirmation
  await expect(page.getByRole('alertdialog')).toBeHidden({ timeout: 25_000 });
});

test('CRIT-E cleared + Safe Return complete + acknowledged closes the modal; still CRITICAL_SUSPENDED; no Resume control; no new Job', async ({ page, request }) => {
  await open(page);
  await scenario(request, 'critical-scenario', { scenario: 'pump-trip-p4' });
  const before = await snapshot(request);
  await scenario(request, 'pump-fault-clear');
  await expect(page.getByTestId('critical-sr')).toContainText('COMPLETE', { timeout: 15_000 });
  await expect(page.getByRole('alertdialog')).toBeVisible(); // not acknowledged yet
  await page.getByTestId('critical-ack').click();
  await expect(page.getByRole('alertdialog')).toBeHidden({ timeout: 3000 });
  await expect(page.getByTestId('operations-scope')).not.toHaveAttribute('inert', /.*/);
  const s = await snapshot(request);
  expect(s.sequence.autoSequence).toBe('CRITICAL_SUSPENDED');
  expect(s.sequence.critical?.modalOpen).toBe(false);
  await expect(page.getByRole('button', { name: /resume/i })).toHaveCount(0);
  await scenario(request, 'auto-jobs', { enabled: true });
  await page.waitForTimeout(3000);
  const later = await snapshot(request);
  expect(later.activeJob).toBeNull();
  expect(later.queue.entries.map((e) => e.sensorId)).toEqual(before.queue.entries.map((e) => e.sensorId));
  await page.getByTestId('diagnostics-toggle').click();
  await expect(page.getByTestId('diag-sequence-state')).toHaveText('CRITICAL_SUSPENDED');
});

for (const [w, h] of [
  [1920, 1080],
  [1366, 768],
]) {
  test(`CRIT-F ${w} x ${h} geometry, blocking, focus trap, Escape ignored, no close button`, async ({ page, request }) => {
    await open(page, w, h);
    await scenario(request, 'critical-scenario', { scenario: 'pump-trip-p4' });
    const modal = page.getByTestId('critical-modal');
    await expect(modal).toBeVisible({ timeout: 3000 });
    const box = (await modal.boundingBox())!;
    expect(box.x).toBeGreaterThanOrEqual(0);
    expect(box.y).toBeGreaterThanOrEqual(0);
    expect(box.x + box.width).toBeLessThanOrEqual(w + 0.5);
    expect(box.y + box.height).toBeLessThanOrEqual(h + 0.5);
    expect(Math.abs(box.x + box.width / 2 - w / 2)).toBeLessThanOrEqual(2); // horizontally centred
    expect(Math.abs(box.y + box.height / 2 - h / 2)).toBeLessThanOrEqual(2); // vertically centred
    const bd = (await page.getByTestId('critical-backdrop').boundingBox())!;
    expect(bd.width).toBeGreaterThanOrEqual(w - 0.5);
    expect(bd.height).toBeGreaterThanOrEqual(h - 0.5);
    // Interaction blocked: a click on a Sensor cell behind the backdrop does not select it.
    const cell = page.locator('[data-sensor-id="G+205"]');
    const cb = (await cell.boundingBox())!;
    await page.mouse.click(cb.x + cb.width / 2, cb.y + cb.height / 2);
    await expect(page.getByTestId('operations-scope')).toHaveAttribute('inert', /.*/);
    expect(await page.getByTestId('detail-id').count()).toBe(0);
    // Escape never dismisses; Tab / Shift+Tab stay inside the dialog.
    await page.keyboard.press('Escape');
    await expect(modal).toBeVisible();
    for (const k of ['Tab', 'Shift+Tab', 'Tab', 'Tab']) {
      await page.keyboard.press(k);
      expect(await modal.evaluate((el) => el.contains(document.activeElement))).toBe(true);
    }
    // No close (X) button: Acknowledge is the only button.
    await expect(modal.locator('button')).toHaveCount(1);
    await expect(modal.locator('button')).toHaveText(/Acknowledge/);
    // No flashing / animation on the dialog.
    const anim = await modal.evaluate((el) => getComputedStyle(el).animationName);
    expect(anim === 'none' || anim === '').toBe(true);
    // No document scroll introduced.
    const dims = await page.evaluate(() => ({ sw: document.documentElement.scrollWidth, sh: document.documentElement.scrollHeight }));
    expect(dims.sw).toBeLessThanOrEqual(w);
    expect(dims.sh).toBeLessThanOrEqual(h);
  });
}
