// Motion commissioning - contract of the read-only Preview route.
//
// The REAL server runs on an ephemeral loopback port with an injected Runtime transport. What is
// proved here is the route contract: the design projection is always available, the Runtime Snapshot
// is read ONLY when the caller asks, every Runtime state is explicit (never a substituted value), and
// the payload carries no endpoint, no address and no command surface.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createPreviewServer, motionCommissioningPayload, UI_BIND_HOST } from '../server.mjs';
import { canonicalMotionProjection } from '../../../packages/contracts/wjss-contracts-ts/test/helpers/motionProjection.mjs';
import { validMotionCommissioning } from '../../../packages/contracts/wjss-contracts-ts/validate.mjs';
import { validateMotionCommissioningDesign } from '../../../packages/commissioning/src/index.mjs';

/** Runs the real server on an ephemeral loopback port and returns a fetch helper. */
async function withServer(runtimeFetch, fn) {
  const server = createPreviewServer({ runtimeFetch });
  await new Promise((resolve) => server.listen(0, UI_BIND_HOST, resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  try {
    return await fn((path) => fetch(`${origin.origin}${path}`), origin.origin);
  } finally {
    server.close();
  }
}

const refuse = async () => { throw new Error('no Runtime host in this test'); };

test('GET /api/motion-commissioning answers the design projection and never reads the Runtime', async () => {
  let snapshots = 0;
  const runtimeFetch = async () => {
    snapshots += 1;
    return { ok: true, json: async () => ({ kind: 'snapshot', schema: 'wjss.snapshot/2', apiVersion: 1, revision: 1 }) };
  };
  await withServer(runtimeFetch, async (get) => {
    const response = await get('/api/motion-commissioning');
    assert.equal(response.status, 200);
    const body = await response.json();
    assert.equal(body.stageId, '0.4C-K2');
    assert.equal(body.mode, 'SIMULATION / PREVIEW');
    assert.equal(validateMotionCommissioningDesign(body.design), true);
    assert.equal(snapshots, 0, 'the design projection must not depend on a Runtime host');
    assert.equal(body.runtime.status, 'NOT_READ');
    assert.equal(body.runtime.motion, null);
    assert.ok(body.runtime.refusalReasons.includes('MOTION_COMMISSIONING_GATE_NOT_OPEN'));
  });
});

test('the design projection carries the whole confirmed topology and no endpoint', async () => {
  await withServer(refuse, async (get) => {
    const body = await (await get('/api/motion-commissioning')).json();
    const design = body.design;
    assert.equal(design.controllers.length, 4);
    assert.equal(design.axes.length, 16);
    assert.equal(design.waterJets.length, 8);
    assert.equal(design.mappingConflicts.length, 1);
    assert.equal(design.authorizationGateState, 'CLOSED');
    const text = JSON.stringify(body);
    assert.doesNotMatch(text, /\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b/, 'no address may appear in the payload');
    assert.ok(text.includes('ENDPOINT_NOT_CONFIGURED'));
    assert.ok(text.includes('MOTION_PORT_NOT_CONFIGURED'));
  });
});

test('runtime=1 with a published motion section reports PUBLISHED with the Runtime projection', async () => {
  const motion = canonicalMotionProjection();
  const runtimeFetch = async (url) => {
    if (new URL(url).pathname === '/api/v1/snapshot') {
      return { ok: true, json: async () => ({
        kind: 'snapshot', schema: 'wjss.snapshot/2', apiVersion: 1, revision: 7, motion,
      }) };
    }
    return { ok: true, json: async () => ({ profile: 'SIMULATOR', currentRevision: 7, ready: false }) };
  };
  await withServer(runtimeFetch, async (get) => {
    const body = await (await get('/api/motion-commissioning?runtime=1')).json();
    assert.equal(body.runtime.status, 'PUBLISHED');
    assert.equal(body.runtime.revision, 7);
    assert.equal(validMotionCommissioning(body.runtime.motion), true);
    assert.equal(body.runtime.motion.axes.length, 16);
    assert.equal(body.runtime.motion.authorizationGateState, 'CLOSED');
    // The design projection is still present: the two halves are never merged into one claim.
    assert.equal(validateMotionCommissioningDesign(body.design), true);
  });
});

test('runtime=1 with no motion section reports NOT_PUBLISHED, never an invented projection', async () => {
  const runtimeFetch = async () => ({ ok: true, json: async () => ({
    kind: 'snapshot', schema: 'wjss.snapshot/2', apiVersion: 1, revision: 3,
  }) });
  await withServer(runtimeFetch, async (get) => {
    const body = await (await get('/api/motion-commissioning?runtime=1')).json();
    assert.equal(body.runtime.status, 'NOT_PUBLISHED');
    assert.equal(body.runtime.revision, 3);
    assert.equal(body.runtime.motion, null);
  });

  const nullMotion = async () => ({ ok: true, json: async () => ({
    kind: 'snapshot', schema: 'wjss.snapshot/2', apiVersion: 1, revision: 4, motion: null,
  }) });
  await withServer(nullMotion, async (get) => {
    const body = await (await get('/api/motion-commissioning?runtime=1')).json();
    assert.equal(body.runtime.status, 'NOT_PUBLISHED');
    assert.equal(body.runtime.motion, null);
  });
});

test('runtime=1 refuses a malformed or unreadable Runtime answer instead of substituting values', async () => {
  const malformed = async () => ({ ok: true, json: async () => ({ kind: 'snapshot', revision: 1 }) });
  await withServer(malformed, async (get) => {
    const body = await (await get('/api/motion-commissioning?runtime=1')).json();
    assert.equal(body.runtime.status, 'INVALID_RUNTIME_RESPONSE');
    assert.equal(body.runtime.motion, null);
  });

  const invalidMotion = async () => ({ ok: true, json: async () => ({
    kind: 'snapshot', schema: 'wjss.snapshot/2', apiVersion: 1, revision: 5,
    motion: { sectionId: 'MOTION_COMMISSIONING_FOUNDATION', authorizationGateState: 'OPEN' },
  }) });
  await withServer(invalidMotion, async (get) => {
    const body = await (await get('/api/motion-commissioning?runtime=1')).json();
    assert.equal(body.runtime.status, 'INVALID_RUNTIME_RESPONSE');
    assert.equal(body.runtime.motion, null);
  });

  await withServer(refuse, async (get) => {
    const body = await (await get('/api/motion-commissioning?runtime=1')).json();
    assert.equal(body.runtime.status, 'RUNTIME_UNAVAILABLE');
    assert.equal(body.runtime.motion, null);
  });

  const notOk = async () => ({ ok: false, status: 503, json: async () => ({}) });
  await withServer(notOk, async (get) => {
    const body = await (await get('/api/motion-commissioning?runtime=1')).json();
    assert.equal(body.runtime.status, 'RUNTIME_UNAVAILABLE');
  });
});

test('the payload builder refuses an out-of-range Runtime port', async () => {
  await assert.rejects(() => motionCommissioningPayload({ runtimeFetch: refuse, runtimePort: 70000, readRuntime: true }),
    RangeError);
});

test('the motion route is GET-only: it carries no write path and no command surface', async () => {
  await withServer(refuse, async (get, base) => {
    for (const method of ['POST', 'PUT', 'PATCH', 'DELETE']) {
      const response = await fetch(`${base}/api/motion-commissioning`, { method });
      assert.equal(response.status, 405, `${method} must be refused`);
      assert.equal(response.headers.get('allow'), 'GET, HEAD');
    }
    const allowed = await get('/api/motion-commissioning');
    assert.equal(allowed.status, 200);
    assert.equal(allowed.headers.get('cache-control'), 'no-store');
    const csp = allowed.headers.get('content-security-policy') ?? '';
    assert.match(csp, /connect-src 'self'/);
  });
});
