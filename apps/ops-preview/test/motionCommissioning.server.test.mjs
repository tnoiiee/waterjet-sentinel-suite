// Motion commissioning - contract of the read-only STATIC CATALOGUE route.
//
// The REAL server runs on an ephemeral loopback port with an injected Runtime transport. What is
// proved here is the route contract: the catalogue is always available, it is composed without
// reading the Runtime at all, it carries the canonical Owner-confirmed mapping with its provenance
// and the recorded Drawing evidence note, and it carries no endpoint, no address and no command
// surface. The Snapshot-coupled motion route no longer exists: the catalogue is not a Snapshot
// member and not a Delta member.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createPreviewServer, motionCataloguePayload, UI_BIND_HOST } from '../server.mjs';
import { canonicalMotionCatalogue } from '../../../packages/contracts/wjss-contracts-ts/test/helpers/motionCatalogue.mjs';
import { validMotionCommissioningCatalogue } from '../../../packages/contracts/wjss-contracts-ts/validate.mjs';
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

const CATALOGUE_ROUTE = '/api/motion-commissioning/catalogue';

test('GET the catalogue route answers the whole catalogue and never reads the Runtime', async () => {
  let snapshots = 0;
  const runtimeFetch = async () => {
    snapshots += 1;
    return { ok: true, json: async () => ({ kind: 'snapshot', schema: 'wjss.snapshot/2', apiVersion: 1, revision: 1 }) };
  };
  await withServer(runtimeFetch, async (get) => {
    const response = await get(CATALOGUE_ROUTE);
    assert.equal(response.status, 200);
    const body = await response.json();
    assert.equal(body.stageId, '0.4C-K2');
    assert.equal(body.mode, 'SIMULATION / PREVIEW');
    assert.equal(validateMotionCommissioningDesign(body.catalogue), true);
    assert.equal(snapshots, 0, 'the static catalogue must not depend on a Runtime host');
    assert.ok(body.refusalReasons.includes('MOTION_COMMISSIONING_GATE_NOT_OPEN'));
    // No Runtime half, no revision, no Snapshot coupling in the payload at all.
    assert.equal('runtime' in body, false);
    assert.equal('revision' in body, false);
    assert.equal('snapshot' in body, false);
  });
});

test('the payload states where the catalogue lives and where it does not', async () => {
  await withServer(refuse, async (get) => {
    const body = await (await get(CATALOGUE_ROUTE)).json();
    assert.equal(body.placement.route, CATALOGUE_ROUTE);
    assert.equal(body.placement.static, true);
    assert.equal(body.placement.carriedBySnapshot, false);
    assert.equal(body.placement.carriedByDelta, false);
    assert.equal(body.placement.runtimeReadRequired, false);
  });
});

test('the catalogue carries the whole confirmed topology, the canonical mapping and no endpoint', async () => {
  await withServer(refuse, async (get) => {
    const body = await (await get(CATALOGUE_ROUTE)).json();
    const catalogue = body.catalogue;
    assert.equal(catalogue.controllers.length, 4);
    assert.equal(catalogue.axes.length, 16);
    assert.equal(catalogue.waterJets.length, 8);
    assert.equal(catalogue.mappingEvidenceNotes.length, 1);
    assert.equal(catalogue.authorizationGateState, 'CLOSED');
    const text = JSON.stringify(body);
    assert.doesNotMatch(text, /\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b/, 'no address may appear in the payload');
    assert.ok(text.includes('ENDPOINT_NOT_CONFIGURED'));
    assert.ok(text.includes('MOTION_PORT_NOT_CONFIGURED'));

    // The canonical mapping, with Owner-confirmed provenance on every row.
    const horizontal = (id) => catalogue.waterJets.find((row) => row.waterJetId === id).horizontalAxisId;
    const vertical = (id) => catalogue.waterJets.find((row) => row.waterJetId === id).verticalAxisId;
    assert.equal(horizontal('WJ1'), 'A');
    assert.equal(horizontal('WJ2'), 'A');
    assert.equal(horizontal('WJ3'), 'C');
    assert.equal(horizontal('WJ4'), 'C');
    assert.equal(horizontal('WJ5'), 'A');
    assert.equal(horizontal('WJ6'), 'A');
    assert.equal(horizontal('WJ7'), 'C');
    assert.equal(horizontal('WJ8'), 'C');
    // REV2 cabinet allocation: WJ1/WJ4 on WSB2, WJ2/WJ3 on WSB1, WJ5/WJ7 on WSB3,
    // WJ6/WJ8 on WSB4 (the revoked REV1 cabinets must not reappear).
    const cabinetOf = (id) => catalogue.waterJets.find((row) => row.waterJetId === id).cabinetId;
    assert.equal(cabinetOf('WJ1'), 'WSB2');
    assert.equal(cabinetOf('WJ2'), 'WSB1');
    assert.equal(cabinetOf('WJ3'), 'WSB1');
    assert.equal(cabinetOf('WJ4'), 'WSB2');
    assert.equal(cabinetOf('WJ5'), 'WSB3');
    assert.equal(cabinetOf('WJ6'), 'WSB4');
    assert.equal(cabinetOf('WJ7'), 'WSB3');
    assert.equal(cabinetOf('WJ8'), 'WSB4');
    for (const row of catalogue.waterJets) {
      assert.equal(row.mappingStatus, 'CONFIRMED_OWNER', row.waterJetId);
      assert.equal(row.mappingBasis, 'OWNER_CONFIRMED', row.waterJetId);
      assert.notEqual(row.horizontalAxisId, row.verticalAxisId);
    }
    // The two Water Jets of the recorded Drawing note, under REV2: WJ2 = A,B and WJ4 = C,D.
    assert.equal(vertical('WJ2'), 'B');
    assert.equal(vertical('WJ4'), 'D');

    // Explicit data: no cabinet rule and no odd/even rule can produce this table.
    for (const cabinet of ['WSB1', 'WSB2', 'WSB3', 'WSB4']) {
      const [first, second] = catalogue.waterJets.filter((row) => row.cabinetId === cabinet);
      assert.notEqual(first.horizontalAxisId, second.horizontalAxisId, cabinet);
    }
    assert.notEqual(horizontal('WJ2'), horizontal('WJ4'));
    assert.notEqual(horizontal('WJ1'), horizontal('WJ3'));
    assert.notEqual(horizontal('WJ5'), horizontal('WJ7'));
    assert.notEqual(horizontal('WJ6'), horizontal('WJ8'));
  });
});

test('the Drawing discrepancy is recorded as evidence only and is never an open decision', async () => {
  await withServer(refuse, async (get) => {
    const catalogue = (await (await get(CATALOGUE_ROUTE)).json()).catalogue;
    const note = catalogue.mappingEvidenceNotes[0];
    assert.equal(note.evidenceId, 'MOTION-EVIDENCE-01');
    assert.deepEqual([...note.waterJetIds], ['WJ2', 'WJ4']);
    assert.deepEqual([...note.cabinetIds], ['WSB1', 'WSB2']);
    assert.equal(note.status, 'DRAWING_REV1_CONFLICT_RECORDED');
    assert.equal(note.classification, 'EVIDENCE_ONLY_NOT_CANONICAL');
    assert.equal(note.physicalResolution, 'NOT_PHYSICALLY_RESOLVED');
    assert.ok(note.statement.length > 0);
    assert.ok(note.effect.length > 0);
    const text = JSON.stringify(catalogue);
    assert.doesNotMatch(text, /OWNER_DECISION_REQUIRED/);
    assert.doesNotMatch(text, /MOTION-CONFLICT-01/);
  });
});

test('the catalogue is the static payload and no Snapshot route carries it', async () => {
  await withServer(refuse, async (get) => {
    const catalogue = await get(CATALOGUE_ROUTE);
    assert.equal(catalogue.status, 200);
    // The Snapshot-coupled motion route is gone: the catalogue is not a Snapshot member.
    const legacy = await get('/api/motion-commissioning');
    assert.equal(legacy.status, 404, 'the Runtime-coupled motion route no longer exists');
  });
});

test('every non-GET method is refused on the catalogue route: it is read-only', async () => {
  await withServer(refuse, async (get, base) => {
    for (const method of ['POST', 'PUT', 'PATCH', 'DELETE']) {
      const response = await fetch(`${base}${CATALOGUE_ROUTE}`, { method, body: method === 'DELETE' ? undefined : 'x' });
      assert.equal(response.status, 405, method);
      assert.equal(response.headers.get('allow'), 'GET, HEAD');
    }
  });
});

test('the catalogue route is not cached, so a stale static payload can never be shown', async () => {
  await withServer(refuse, async (get) => {
    const response = await get(CATALOGUE_ROUTE);
    assert.match(response.headers.get('cache-control') ?? '', /no-store/);
  });
});

test('the Product-side catalogue mirror of the same Owner-confirmed data validates identically', () => {
  // The C#-shaped mirror and the Node catalogue are the same Owner-confirmed facts: both carry all
  // eight canonical rows with OWNER_CONFIRMED provenance and the one evidence note.
  const mirrored = canonicalMotionCatalogue();
  assert.equal(validMotionCommissioningCatalogue(mirrored), true);
  assert.deepEqual(mirrored.waterJets.map((row) => [row.waterJetId, row.horizontalAxisId, row.verticalAxisId]),
    [['WJ1', 'A', 'B'], ['WJ2', 'A', 'B'], ['WJ3', 'C', 'D'], ['WJ4', 'C', 'D'],
      ['WJ5', 'A', 'B'], ['WJ6', 'A', 'B'], ['WJ7', 'C', 'D'], ['WJ8', 'C', 'D']]);
  for (const row of mirrored.waterJets) {
    assert.equal(row.mappingStatus, 'CONFIRMED_OWNER', row.waterJetId);
    assert.equal(row.mappingBasis, 'OWNER_CONFIRMED', row.waterJetId);
  }
  const note = mirrored.mappingEvidenceNotes[0];
  assert.equal(note.status, 'DRAWING_REV1_CONFLICT_RECORDED');
  assert.equal(note.classification, 'EVIDENCE_ONLY_NOT_CANONICAL');
  assert.equal(note.physicalResolution, 'NOT_PHYSICALLY_RESOLVED');
});

test('the catalogue payload is served as one static document, not per revision', async () => {
  const { body } = motionCataloguePayload();
  const bytes = Buffer.byteLength(JSON.stringify(body), 'utf8');
  // The separation exists because this payload is large and unchanging: it is served once by the
  // catalogue route instead of being re-sent with every Snapshot revision.
  assert.ok(bytes > 10000, `the static catalogue is a substantial payload (${bytes} bytes)`);
});
