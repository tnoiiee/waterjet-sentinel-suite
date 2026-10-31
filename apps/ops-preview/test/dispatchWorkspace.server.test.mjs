// H3-A - contract of the read-only Cleaning Job Workspace route.
//
// The REAL Preview server runs on an ephemeral loopback port with an injected Runtime transport.
// What is proved here is the transport contract: one GET, no retry, no polling, no write; a Runtime
// that cannot answer or that answers a document this Preview cannot render truthfully is reported as
// an explicit refusal state, never repaired and never filled with invented values. The delay
// arithmetic is NOT proved here: it lives in the C# planner and its own tests.

import { test } from 'node:test';
import assert from 'node:assert/strict';

import { createPreviewServer, dispatchWorkspacePayload, UI_BIND_HOST, validDispatchWorkspace } from '../server.mjs';
import { dispatchWorkspaceDocument, frozenManualDecision } from './helpers/dispatchWorkspace.mjs';

const ROUTE = '/api/operations/dispatch';
const RUNTIME_ROUTE = '/api/v1/dispatch-workspace';

async function withServer(runtimeFetch, fn) {
  const server = createPreviewServer({ runtimeFetch });
  await new Promise((resolve) => server.listen(0, UI_BIND_HOST, resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  try {
    return await fn((path, options) => fetch(`${origin.origin}${path}`, options), origin.origin);
  } finally {
    server.close();
  }
}

const refuse = async () => { throw new Error('no Runtime host in this test'); };
const serving = (document) => async () => ({ ok: true, json: async () => document });

test('the route answers one PUBLISHED workspace with the Runtime revision', async () => {
  const document = dispatchWorkspaceDocument();
  const calls = [];
  await withServer(async (url, options) => {
    calls.push({ url, options });
    return { ok: true, json: async () => document };
  }, async (get) => {
    const response = await get(ROUTE);
    assert.equal(response.status, 200);
    const body = await response.json();
    assert.equal(body.stageId, '0.4C-K2');
    assert.equal(body.mode, 'SIMULATION / PREVIEW');
    assert.equal(body.status, 'PUBLISHED');
    assert.equal(body.revision, 12);
    assert.equal(body.workspace.countdown.lifecycle, 'ACTIVE_FROZEN');
    assert.equal(body.workspace.countdown.decision.appliedDelaySeconds, 473);
    assert.equal(calls.length, 1, 'exactly one Runtime read per request');
    assert.equal(calls[0].url, `http://127.0.0.1:5181` + RUNTIME_ROUTE);
    assert.equal(calls[0].options.method, 'GET');
  });
});

test('an absent Runtime is an explicit unavailable state, never a fabricated countdown', async () => {
  await withServer(refuse, async (get) => {
    const response = await get(ROUTE);
    assert.equal(response.status, 503);
    const body = await response.json();
    assert.equal(body.status, 'RUNTIME_UNAVAILABLE');
    assert.equal(body.workspace, null);
    assert.equal(body.revision, null);
  });
});

test('a non-OK Runtime answer is unavailable, not a stale or partial document', async () => {
  await withServer(async () => ({ ok: false, status: 503, json: async () => ({}) }), async (get) => {
    const response = await get(ROUTE);
    assert.equal(response.status, 503);
    assert.equal((await response.json()).status, 'RUNTIME_UNAVAILABLE');
  });
});

test('a malformed Runtime answer is refused as INVALID_RUNTIME_RESPONSE', async () => {
  await withServer(async () => ({ ok: true, json: async () => { throw new Error('not json'); } }), async (get) => {
    const response = await get(ROUTE);
    assert.equal(response.status, 502);
    assert.equal((await response.json()).status, 'INVALID_RUNTIME_RESPONSE');
  });
});

test('the route is GET only, like every other route of this Preview', async () => {
  await withServer(serving(dispatchWorkspaceDocument()), async (get, origin) => {
    for (const method of ['POST', 'PUT', 'PATCH', 'DELETE']) {
      const response = await fetch(`${origin}${ROUTE}`, { method });
      assert.equal(response.status, 405, method);
    }
  });
});

test('a valid published document is accepted by the strict validator', () => {
  assert.equal(validDispatchWorkspace(dispatchWorkspaceDocument()), true);
  const manual = dispatchWorkspaceDocument({
    countdown: {
      lifecycle: 'COMPLETED_AWAITING_DISPATCH', generation: 2, remainingSeconds: 0,
      decision: frozenManualDecision(), frozen: true, dispatchAdmissible: true,
    },
  });
  assert.equal(validDispatchWorkspace(manual), true);
  const none = dispatchWorkspaceDocument({
    countdown: { lifecycle: 'NONE', generation: 0, remainingSeconds: 0, decision: null, frozen: false, dispatchAdmissible: false },
  });
  assert.equal(validDispatchWorkspace(none), true);
});

test('every unrenderable document is refused and never repaired', () => {
  const cases = [
    ['wrong kind', { ...dispatchWorkspaceDocument(), kind: 'snapshot' }],
    ['wrong schema', { ...dispatchWorkspaceDocument(), schema: 'wjss.snapshot/2' }],
    ['wrong API version', { ...dispatchWorkspaceDocument(), apiVersion: 2 }],
    ['no revision', { ...dispatchWorkspaceDocument(), revision: 0 }],
    ['unknown lifecycle', dispatchWorkspaceDocument({ countdown: { lifecycle: 'COUNTING' } })],
    ['negative remaining', dispatchWorkspaceDocument({ countdown: { remainingSeconds: -1 } })],
    ['frozen without a decision', dispatchWorkspaceDocument({ countdown: { decision: null } })],
    ['admissible with time left', dispatchWorkspaceDocument({ countdown: { dispatchAdmissible: true, remainingSeconds: 4 } })],
    ['manual with an RH reading', dispatchWorkspaceDocument({
      countdown: { decision: frozenManualDecision({ rhInput: { rhTemperature: 520, quality: 'GOOD', observedAt: '2026-10-08T00:00:18.000Z', generation: 1 } }) },
    })],
    ['manual with a zone factor', dispatchWorkspaceDocument({ countdown: { decision: frozenManualDecision({ zoneFactor: 0.75 }) } })],
    ['a decision with an empty identity', dispatchWorkspaceDocument({ countdown: { decision: frozenManualDecision({ isolationValveId: '' }) } })],
    ['a preview that claims to affect execution', dispatchWorkspaceDocument({ preview: { affectsExecution: true } })],
    ['a preview with an unknown mode', dispatchWorkspaceDocument({ preview: { mode: 'LIVE_LEGACY' } })],
    ['a configuration with an inverted RH range', dispatchWorkspaceDocument({
      configuration: { ...dispatchWorkspaceDocument().configuration, rhTempMin: 535, rhTempMax: 515 },
    })],
    ['a configuration with a missing range list', dispatchWorkspaceDocument({ configuration: { ...dispatchWorkspaceDocument().configuration, boilerRanges: [] } })],
    ['a queue that reports more than it holds', dispatchWorkspaceDocument({
      entries: [{ position: 1, entryId: 'SYN-QE-2', sensorId: 'G+202', sourceReason: 'AUTO_SEQUENCE', secondsSinceLastClean: 1 }],
      queue: { totalQueued: 2 },
    })],
    ['a queue whose positions are not one-based FIFO', dispatchWorkspaceDocument({
      entries: [{ position: 2, entryId: 'SYN-QE-2', sensorId: 'G+202', sourceReason: 'AUTO_SEQUENCE', secondsSinceLastClean: 1 }],
    })],
  ];
  for (const [label, document] of cases) {
    assert.equal(validDispatchWorkspace(document), false, label);
  }
});

test('the timeline contract is order, ordinal and exactly one current step', () => {
  const reordered = dispatchWorkspaceDocument();
  const [first, second] = reordered.timeline.steps;
  reordered.timeline.steps = [second, first, ...reordered.timeline.steps.slice(2)];
  assert.equal(validDispatchWorkspace(reordered), false, 'a reordered timeline must be refused');

  const noCurrent = dispatchWorkspaceDocument();
  noCurrent.timeline.steps = noCurrent.timeline.steps.map((step) => ({ ...step, state: 'PENDING' }));
  assert.equal(validDispatchWorkspace(noCurrent), false, 'a timeline without a current step must be refused');

  const twoCurrent = dispatchWorkspaceDocument();
  twoCurrent.timeline.steps = twoCurrent.timeline.steps.map((step, index) => ({ ...step, state: index < 2 ? 'CURRENT' : 'PENDING' }));
  assert.equal(validDispatchWorkspace(twoCurrent), false, 'a timeline with two current steps must be refused');
});

test('a Runtime document missing a whole panel section is refused', () => {
  for (const key of ['queue', 'countdown', 'timeline', 'preview']) {
    const document = dispatchWorkspaceDocument();
    delete document[key];
    assert.equal(validDispatchWorkspace(document), false, key);
  }
});

test('the payload builder refuses an out-of-range Runtime port before any request', async () => {
  for (const port of ['0', '65536', 'abc', '5181;']) {
    await assert.rejects(
      () => dispatchWorkspacePayload({ runtimeFetch: refuse, runtimePort: port }),
      RangeError,
      port,
    );
  }
});
