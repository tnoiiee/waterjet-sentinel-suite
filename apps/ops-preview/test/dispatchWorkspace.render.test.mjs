// H3-A - the Cleaning Job Workspace panels A to F, rendered by the REAL public/app.mjs against the
// REAL Preview server through the stub DOM.
//
// These tests prove the rendered text and the rendered structure of the six panels. They do NOT
// prove layout, the Owner-local 1920x1080 review, or the delay arithmetic: the arithmetic lives in
// the C# planner, and the pixels are the Owner Manual Review's evidence of record.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { allText, bootApp, click, walk, waitFor } from './helpers/fakeDom.mjs';
import { dispatchWorkspaceDocument, frozenManualDecision } from './helpers/dispatchWorkspace.mjs';

const ROUTE = '/api/operations/dispatch';
const FROZEN_TEXT = 'Active countdown is frozen. Live preview does not modify execution.';
const PANELS = ['cj-workspace', 'cj-queue', 'cj-configuration', 'cj-countdown', 'cj-preview', 'cj-timeline'];

/** A Runtime transport that serves ONE workspace document and refuses everything else. */
function serving(document) {
  return async (url) => {
    if (new URL(url).pathname === '/api/v1/dispatch-workspace') return { ok: true, json: async () => document };
    throw new Error('this test serves only the Cleaning Job Workspace');
  };
}

const text = (app, id) => allText(app.byId[id]);

/** The status line is written with ``textContent``, exactly like the other status lines of this page. */
const statusText = (app) => app.byId['cleaning-job-status'].textContent;

const timelineList = (app) => app.byId['cj-timeline'].childNodes
  .find((node) => node.className === 'timeline');

test('the six panels render the published workspace, its queue and its configuration', async () => {
  const app = await bootApp({ runtimeFetch: serving(dispatchWorkspaceDocument()) });
  try {
    assert.equal(app.byId['app-root'].attributes['data-app-state'], 'ready');
    assert.equal(JSON.stringify(app.requests.filter((r) => r.path === ROUTE).map((r) => r.status)), '[200]');
    assert.match(statusText(app), /Runtime revision 12/);
    assert.match(statusText(app), /SIMULATION - no Hardware access, no device write/);

    const workspace = text(app, 'cj-workspace');
    assert.match(workspace, /Runtime revision 12/);
    assert.match(workspace, /AutoSequence READY_TO_DISPATCH/);
    assert.match(workspace, /Mode RUNNING/);
    assert.match(workspace, /No Cleaning Job is active/);
    assert.match(workspace, /Timeline stage NEXT_DISPATCH_COUNTDOWN/);

    const queue = text(app, 'cj-queue');
    assert.match(queue, /Entries 2 of 8/);
    assert.match(queue, /Queue revision 7/);
    assert.match(queue, /SYN-JOB-1 at 2026-10-08T00:00:12.000Z/);
    const rows = app.byId['cj-queue-body'].childNodes;
    assert.equal(rows.length, 2);
    assert.deepEqual(rows[0].childNodes.map((cell) => allText(cell)),
      ['1', 'SYN-QE-2', 'G+202', 'AUTO_SEQUENCE', '41']);

    const configuration = text(app, 'cj-configuration');
    assert.match(configuration, /AUTO_RH_LINEAR_FROZEN/);
    assert.match(configuration, /RH min \/ max 515 \/ 535/);
    assert.match(configuration, /VERY_CLEAN: 600 \/ 660 s/);
    assert.match(configuration, /NORMAL_CLEAN: 500 \/ 550 s/);
    assert.match(configuration, /MEDIUM_DIRTY: 340 \/ 380 s/);
    assert.match(configuration, /VERY_DIRTY: 240 \/ 280 s/);
    assert.match(configuration, /not DCS commissioning values/);
  } finally {
    app.close();
  }
});

test('the frozen countdown card carries the exact Owner wording and the whole frozen decision', async () => {
  const app = await bootApp({ runtimeFetch: serving(dispatchWorkspaceDocument()) });
  try {
    const countdown = text(app, 'cj-countdown');
    assert.equal(countdown.split(FROZEN_TEXT).length - 1, 1, 'the frozen wording must appear exactly once');
    assert.match(countdown, /Lifecycle ACTIVE_FROZEN/);
    assert.match(countdown, /Generation 1/);
    assert.match(countdown, /Remaining seconds 473/);
    assert.match(countdown, /Frozen yes - the decision is immutable/);
    assert.match(countdown, /Dispatch admissible no/);
    // Every frozen identity the Owner required is visible, and the RH reading is the frozen one.
    for (const expected of [
      'SYN-TD-1', 'Queue generation 7', 'SYN-QE-2', 'G+202', 'Next Sensor wall LEFT', 'Next Sensor OrderWall 2',
      'Canonical Water Jet WJ1', 'Effective WJ provenance CANONICAL_ASSIGNMENT', 'Isolation valve IV1',
      'Canonical WSB cabinet WSB1', 'Motion controller MC-WSB1', 'Motion axis pair (Horizontal / Vertical) A / B',
      '525 · GOOD · observed 2026-10-08T00:00:18.000Z · generation 19', 'Boiler Condition VERY_CLEAN',
      'Manual delay (s) 0', 'RH min / max 515 / 535', 'Auto delay min / max (s) 600 / 660',
      'Base delay (s) 630', 'Zone UPPER', 'Zone factor 0.75', 'Applied delay (s) 473',
    ]) {
      assert.ok(countdown.includes(expected), `the frozen decision must show '${expected}'`);
    }
  } finally {
    app.close();
  }
});

test('a MANUAL_FIXED decision shows no RH input and the 1.00 factor', async () => {
  const app = await bootApp({
    runtimeFetch: serving(dispatchWorkspaceDocument({
      countdown: {
        lifecycle: 'COMPLETED_AWAITING_DISPATCH', generation: 2, remainingSeconds: 0,
        decision: frozenManualDecision(), frozen: true, dispatchAdmissible: true,
      },
    })),
  });
  try {
    const countdown = text(app, 'cj-countdown');
    assert.match(countdown, /Dispatch admissible yes - exactly one dispatch/);
    assert.match(countdown, /RH input not used \(MANUAL_FIXED\)/);
    assert.match(countdown, /Manual delay \(s\) 5/);
    assert.match(countdown, /Zone factor 1/);
    assert.match(countdown, /Applied delay \(s\) 5/);
    assert.ok(countdown.includes(FROZEN_TEXT), 'a completed frozen decision is still frozen evidence');
  } finally {
    app.close();
  }
});

test('the live preview is rendered as a preview and states that it changes nothing', async () => {
  const app = await bootApp({ runtimeFetch: serving(dispatchWorkspaceDocument()) });
  try {
    const preview = text(app, 'cj-preview');
    assert.match(preview, /Live RH temperature 517.5/);
    assert.match(preview, /Clamped RH 517.5/);
    assert.match(preview, /Mapped delay \(s\) 652.5/);
    assert.match(preview, /Base delay \(s\) 652/);
    assert.match(preview, /Applied if planned now \(s\) 489/);
    assert.match(preview, /Affects execution: no/);
    assert.match(preview, /the frozen decision above does not change/);
  } finally {
    app.close();
  }
});

test('a refused preview shows its typed refusal and never a zero delay', async () => {
  const app = await bootApp({
    runtimeFetch: serving(dispatchWorkspaceDocument({
      preview: {
        available: false, refusalCode: 'RH_STALE', rhTemperature: 520, clampedRhTemperature: null,
        mappedDelaySeconds: null, baseDelaySeconds: null, appliedDelaySeconds: null,
      },
    })),
  });
  try {
    const preview = text(app, 'cj-preview');
    assert.match(preview, /No preview is available: RH_STALE/);
    assert.match(preview, /No delay is claimed/);
    assert.match(preview, /Applied if planned now \(s\) none/);
  } finally {
    app.close();
  }
});

test('the timeline renders all thirteen steps in contract order with exactly one current step', async () => {
  const app = await bootApp({ runtimeFetch: serving(dispatchWorkspaceDocument()) });
  try {
    const list = timelineList(app);
    assert.ok(list, 'the timeline list must render');
    const steps = list.childNodes;
    assert.deepEqual(steps.map((step) => step.attributes['data-stage']), [
      'PRECHECK', 'MOVE_TO_P1', 'OPEN_IV', 'P2', 'P3', 'P4', 'P5', 'P6', 'CLOSE_IV',
      'MANDATORY_SAFE_RETURN', 'COMPLETE', 'NEXT_DISPATCH_COUNTDOWN', 'DISPATCHED',
    ]);
    const current = steps.filter((step) => step.attributes['data-state'] === 'CURRENT');
    assert.equal(current.length, 1, 'exactly one step may be current');
    assert.equal(current[0].attributes['data-stage'], 'NEXT_DISPATCH_COUNTDOWN');
    assert.equal(steps.filter((step) => step.attributes['data-state'] === 'COMPLETE').length, 11);
    assert.equal(steps.filter((step) => step.attributes['data-state'] === 'PENDING').length, 1);
    assert.match(text(app, 'cj-timeline'), /Current stage: NEXT_DISPATCH_COUNTDOWN/);
    assert.match(text(app, 'cj-timeline'), /Last dispatch: SYN-JOB-1 at 2026-10-08T00:00:12.000Z/);
  } finally {
    app.close();
  }
});

test('a failed Safe Return renders the mandatory step as FAILED, never as complete', async () => {
  const document = dispatchWorkspaceDocument({
    activeJob: {
      jobId: 'SYN-JOB-1', targetSensorId: 'G+201', jetId: 'WJ1', valveId: 'IV1',
      phase: 'P6', phaseLabel: 'P6', phaseIndex: 6, lifecycle: 'SAFE_RETURN_FAILED',
      safeReturn: { step: 'SR_FAILED' }, dispatchId: 'SYN-DP-1',
    },
    stage: 'MANDATORY_SAFE_RETURN',
  });
  document.timeline.steps = document.timeline.steps.map((step) => (step.stage === 'MANDATORY_SAFE_RETURN'
    ? { ...step, state: 'FAILED' }
    : step));
  const app = await bootApp({ runtimeFetch: serving(document) });
  try {
    const failed = timelineList(app).childNodes.filter((step) => step.attributes['data-state'] === 'FAILED');
    assert.equal(failed.length, 1);
    assert.equal(failed[0].attributes['data-stage'], 'MANDATORY_SAFE_RETURN');
    assert.match(text(app, 'cj-workspace'), /Safe Return step SR_FAILED/);
  } finally {
    app.close();
  }
});

test('an absent Runtime leaves every panel explicitly unavailable with no invented value', async () => {
  const app = await bootApp({}); // the default transport refuses: no Runtime host exists
  try {
    assert.equal(app.byId['app-root'].attributes['data-app-state'], 'ready');
    assert.match(statusText(app), /unavailable \(RUNTIME_UNAVAILABLE\)|could not be read/);
    for (const id of PANELS) {
      assert.match(text(app, id), /Unavailable\. No value is claimed\./, id);
    }
    assert.doesNotMatch(text(app, 'cj-countdown'), /frozen|remaining/i);
    assert.equal(app.byId['cj-queue-body'].childNodes.length, 0);
  } finally {
    app.close();
  }
});

test('the refresh control re-reads once, and a later failure clears the stale countdown', async () => {
  let available = true;
  let reads = 0;
  const runtimeFetch = async (url) => {
    if (new URL(url).pathname !== '/api/v1/dispatch-workspace') throw new Error('not this route');
    reads += 1;
    if (!available) throw new Error('Runtime stopped');
    return { ok: true, json: async () => dispatchWorkspaceDocument() };
  };
  const app = await bootApp({ runtimeFetch });
  try {
    assert.equal(reads, 1);
    assert.match(text(app, 'cj-countdown'), /Remaining seconds 473/);
    click(app.byId['cleaning-job-refresh']);
    await waitFor(() => reads === 2, 'the refresh to read the workspace once');
    assert.equal(app.requests.filter((r) => r.path === ROUTE).length, 2);
    available = false;
    click(app.byId['cleaning-job-refresh']);
    await waitFor(() => reads === 3 && /Unavailable/.test(text(app, 'cj-countdown')),
      'a later Runtime failure to clear the stale countdown');
    assert.doesNotMatch(text(app, 'cj-countdown'), /473|Frozen/);
    assert.equal(app.requests.every((r) => r.method === 'GET'), true, 'the section may never write');
  } finally {
    app.close();
  }
});

test('the section carries one read-only control and no queue, job or countdown command', async () => {
  const app = await bootApp({ runtimeFetch: serving(dispatchWorkspaceDocument()) });
  try {
    const buttons = [];
    const root = app.byId['cleaning-job'];
    walk(root, (node) => { if (node.tagName === 'button') buttons.push(node); });
    // The served markup is the contract: exactly one button (the read-only refresh) exists in the
    // Cleaning Job section, and it carries no data-action that could command anything.
    assert.equal(buttons.length, 0, 'the section markup carries no button inside the rendered panels');
    assert.ok(app.byId['cleaning-job-refresh'], 'the single refresh control exists');
    assert.equal(app.byId['cleaning-job-refresh'].attributes['data-action'], undefined);
    const source = readFileSync(join(dirname(fileURLToPath(import.meta.url)), '..', 'public', 'app.mjs'), 'utf8');
    assert.equal(/method:\s*['"](POST|PUT|PATCH|DELETE)/.test(source), false);
  } finally {
    app.close();
  }
});
