// WJSS Stage 0.2.1A — Owner critical Pump decision: blocking modal + Mandatory Safe Return in
// installed Microsoft Edge (CRIT-A..F), plus the synthetic AutoSequence controls (SEQ-B..G).
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

// ---------------------------------------------------------------------------------------------
// Owner final closeout: Synthetic AutoSequence control (SEQ-B..G; SEQ-A = S11/S12 in
// operations.spec.ts). Controls live only in the opt-in Diagnostics drawer; the AutoSequence always
// dispatches GlobalQueue Position 1; Pump fault clear + Acknowledge never Resume; RESET CRITICAL
// SCENARIO requires an explicit START afterwards. SYNTHETIC REVIEW TOOLING — not the Production
// operator-control model.
type SeqSnap = {
  queue: { entries: { sensorId: string }[] };
  sequence: { autoSequence: string; mode: string; lastJobOutcome: null | { jobId: string; outcome: string } };
  activeJob: null | { jobId: string; targetSensorId: string; lifecycle: string; dispatch: { positionBefore: number; sensorId: string } };
};
const seqSnap = async (request: APIRequestContext): Promise<SeqSnap> => (await request.get('/api/snapshot')).json();
const queueIds = async (request: APIRequestContext) => (await seqSnap(request)).queue.entries.map((e) => e.sensorId);

/** Deterministic queue content (synthetic sources) and the drawer opened before any modal. */
async function prepareSequence(page: Page, request: APIRequestContext) {
  for (const id of ['G9', 'G8', 'I12']) await scenario(request, 'enqueue', { sensorId: id, reason: 'TIME_DUE' });
  await open(page);
  await page.getByTestId('diagnostics-toggle').click();
  await expect(page.getByTestId('asc-group')).toBeVisible();
  await expect(page.getByTestId('asc-mode')).toContainText('OFF');
}
async function startAutoSequence(page: Page, request: APIRequestContext) {
  await expect(page.getByTestId('asc-start')).toBeEnabled({ timeout: 15_000 });
  const head = (await queueIds(request))[0];
  await page.getByTestId('asc-start').click();
  await expect(page.getByTestId('asc-job')).toContainText(head, { timeout: 3000 });
  return head;
}

test('SEQ-B START AUTOSEQUENCE dispatches the queue head (never the selected Sensor); queue shifts FIFO', async ({ page, request }) => {
  await prepareSequence(page, request);
  const before = await queueIds(request);
  expect(before.length).toBeGreaterThanOrEqual(2);
  // Select a different Sensor on the map: the selection must not influence the target.
  await page.locator('[data-sensor-id="J12"]').click();
  await expect(page.getByTestId('stc-selected')).toHaveText('J12');
  await expect(page.getByTestId('asc-start-reason')).toHaveText('Available');
  const head = await startAutoSequence(page, request);
  expect(head).toBe(before[0]);
  const s = await seqSnap(request);
  expect(s.activeJob?.targetSensorId).toBe(before[0]);
  expect(s.activeJob?.dispatch.positionBefore).toBe(1);
  expect(s.queue.entries.map((e) => e.sensorId).slice(0, before.length - 1)).toEqual(before.slice(1));
  await expect(page.getByTestId('asc-mode')).toContainText('RUNNING');
  await expect(page.getByTestId('asc-start')).toBeDisabled();
  await expect(page.getByTestId('asc-start-reason')).toHaveText('An Active Job exists');
});

test('SEQ-C PAUSE AFTER CURRENT JOB: PAUSE_REQUESTED, Job continues, Safe Return completes, no next dispatch, PAUSED', async ({ page, request }) => {
  await prepareSequence(page, request);
  const head = await startAutoSequence(page, request);
  const nextHead = (await queueIds(request))[0];
  await page.getByTestId('asc-pause').click();
  await expect(page.getByTestId('asc-mode')).toContainText('PAUSE_REQUESTED');
  await expect(page.getByTestId('asc-job')).toContainText(head); // the current Job continues
  await expect(page.getByTestId('asc-resume')).toBeDisabled();
  await expect(page.getByTestId('asc-sr')).toContainText('SAFE_RETURN', { timeout: 60_000 });
  await expect(page.getByTestId('asc-mode')).toContainText('PAUSED · PAUSED', { timeout: 60_000 });
  await page.waitForTimeout(2500);
  const s = await seqSnap(request);
  expect(s.activeJob).toBeNull();
  expect(s.sequence.lastJobOutcome?.outcome).toBe('COMPLETED');
  expect(s.queue.entries[0]?.sensorId).toBe(nextHead); // no next dispatch
  await expect(page.getByTestId('asc-resume')).toBeEnabled();
});

test('SEQ-D RESUME only from PAUSED dispatches the current head; disabled in CRITICAL_SUSPENDED', async ({ page, request }) => {
  await prepareSequence(page, request);
  await startAutoSequence(page, request);
  await expect(page.getByTestId('asc-resume')).toBeDisabled();
  await expect(page.getByTestId('asc-resume-reason')).toHaveText('Only available from PAUSED');
  await page.getByTestId('asc-pause').click();
  await page.getByTestId('asc-abort').click(); // faster path to PAUSED, still through Safe Return
  await expect(page.getByTestId('asc-mode')).toContainText('PAUSED · PAUSED', { timeout: 30_000 });
  const head = (await queueIds(request))[0];
  await expect(page.getByTestId('asc-resume')).toBeEnabled();
  await page.getByTestId('asc-resume').click();
  await expect(page.getByTestId('asc-job')).toContainText(head, { timeout: 3000 });
  expect((await seqSnap(request)).activeJob?.targetSensorId).toBe(head);
  // Critical: Resume is disabled and never clears the suspension.
  await scenario(request, 'pump-trip');
  await expect(page.getByRole('alertdialog')).toBeVisible({ timeout: 3000 });
  await expect(page.getByTestId('asc-mode')).toContainText('CRITICAL_SUSPENDED');
  await expect(page.getByTestId('asc-resume')).toBeDisabled();
  await expect(page.getByTestId('asc-resume-reason')).toContainText('CRITICAL_SUSPENDED');
});

test('SEQ-E Clear + Acknowledge do not enable Resume; RESET only after Safe Return; no dispatch; explicit START', async ({ page, request }) => {
  await prepareSequence(page, request);
  await startAutoSequence(page, request);
  await scenario(request, 'safe-return-config', { valveFeedbackDelayMs: 6000 });
  await scenario(request, 'pump-trip');
  await expect(page.getByRole('alertdialog')).toBeVisible({ timeout: 3000 });
  const frozen = await queueIds(request);
  await page.getByTestId('critical-ack').click();
  await scenario(request, 'pump-fault-clear');
  await expect(page.getByTestId('asc-critical')).toHaveText('CLEARED · ACKNOWLEDGED', { timeout: 3000 });
  await expect(page.getByTestId('asc-resume')).toBeDisabled();
  await expect(page.getByTestId('asc-reset')).toBeDisabled();
  await expect(page.getByTestId('asc-reset-reason')).toContainText('Safe Return not complete');
  await expect(page.getByRole('alertdialog')).toBeHidden({ timeout: 30_000 });
  await expect(page.getByTestId('asc-mode')).toContainText('CRITICAL_SUSPENDED');
  await expect(page.getByTestId('asc-resume')).toBeDisabled();
  await expect(page.getByTestId('asc-reset')).toBeEnabled();
  await page.getByTestId('asc-reset').click();
  await expect(page.getByTestId('asc-mode')).toHaveText('OFF · OFF', { timeout: 3000 });
  await page.waitForTimeout(2500);
  const s = await seqSnap(request);
  expect(s.activeJob).toBeNull(); // reset never dispatches
  expect(s.queue.entries.map((e) => e.sensorId).slice(0, frozen.length)).toEqual(frozen);
  await expect(page.getByTestId('asc-start')).toBeDisabled();
  await expect(page.getByTestId('asc-start-reason')).toHaveText('Pump not ready'); // reset does not start the Pump
  await page.getByTestId('asc-pump-start').click();
  await startAutoSequence(page, request);
  expect((await seqSnap(request)).activeJob?.targetSensorId).toBe(frozen[0]);
});

test('SEQ-F ABORT ACTIVE JOB: only with a Job, Safe Return steps visible, not released early, no next Job when paused', async ({ page, request }) => {
  await prepareSequence(page, request);
  await expect(page.getByTestId('asc-abort')).toBeDisabled();
  await expect(page.getByTestId('asc-abort-reason')).toHaveText('No Active Job');
  const head = await startAutoSequence(page, request);
  await page.getByTestId('asc-pause').click();
  await page.getByTestId('asc-abort').click();
  await expect(page.getByTestId('asc-sr')).toContainText(/ABORTING|SAFE_RETURN/, { timeout: 3000 });
  expect((await seqSnap(request)).activeJob?.targetSensorId).toBe(head); // not released early
  await expect(page.getByTestId('asc-abort')).toBeDisabled();
  await expect(page.getByTestId('asc-sr')).toContainText('SAFE_RETURN_TO_STANDBY', { timeout: 15_000 });
  expect((await seqSnap(request)).activeJob?.targetSensorId).toBe(head);
  await expect(page.getByTestId('asc-mode')).toContainText('PAUSED · PAUSED', { timeout: 20_000 });
  await page.waitForTimeout(2500);
  const s = await seqSnap(request);
  expect(s.activeJob).toBeNull();
  expect(s.sequence.lastJobOutcome?.outcome).toBe('ABORTED');
});

test('SEQ-G controls only in Diagnostics, Synthetic labels, readable reasons, no page overflow, keyboard usable, modal unchanged', async ({ page, request }) => {
  await scenario(request, 'enqueue', { sensorId: 'G9', reason: 'TIME_DUE' });
  await open(page);
  await expect(page.getByTestId('asc-group')).toHaveCount(0); // not on the Operations surface
  await page.getByTestId('diagnostics-toggle').click();
  const group = page.getByTestId('asc-group');
  await expect(group).toBeVisible();
  const labels = await group.locator('button').allTextContents();
  expect(labels).toHaveLength(6);
  for (const l of labels) expect(l.startsWith('SYN · ')).toBe(true);
  for (const id of ['pause', 'resume', 'abort', 'reset']) {
    const t = (await page.getByTestId(`asc-${id}-reason`).textContent()) ?? '';
    expect(t.length).toBeGreaterThan(5);
    await expect(page.getByTestId(`asc-${id}-reason`)).toBeVisible();
  }
  const body = page.getByTestId('diagnostics').locator('> div').nth(1);
  expect(await body.evaluate((e) => getComputedStyle(e).overflowY)).toMatch(/auto|scroll/);
  const dims = await page.evaluate(() => ({ sw: document.documentElement.scrollWidth, sh: document.documentElement.scrollHeight, w: innerWidth, h: innerHeight }));
  expect(dims.sw).toBeLessThanOrEqual(dims.w);
  expect(dims.sh).toBeLessThanOrEqual(dims.h);
  // Keyboard: focus START and activate it with the keyboard.
  await expect(page.getByTestId('asc-start')).toBeEnabled({ timeout: 15_000 });
  await page.getByTestId('asc-start').focus();
  await expect(page.getByTestId('asc-start')).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByTestId('asc-job')).not.toHaveText('None', { timeout: 3000 });
  // Critical modal behaviour unchanged with the drawer open.
  await scenario(request, 'pump-trip');
  const modal = page.getByTestId('critical-modal');
  await expect(modal).toBeVisible({ timeout: 3000 });
  await page.keyboard.press('Escape');
  await expect(modal).toBeVisible();
  await expect(modal.locator('button')).toHaveCount(1);
  await expect(page.getByTestId('operations-scope')).toHaveAttribute('inert', /.*/);
});
