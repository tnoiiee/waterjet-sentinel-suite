// Stage 0.4C-K3 - route boundary of GET /api/tc-acquisition-evidence (group G, route half).
// The route is exercised through the REAL Preview host on an ephemeral loopback port. The loopback
// host is the Preview itself, never a TC endpoint: no request leaves the machine.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { CANONICAL_TC_NODE_CONFIGURATION, LIVE_COMMUNICATION_STATES } from '../../../packages/commissioning/src/index.mjs';
import { STAGE_ID, UI_BIND_HOST, createPreviewServer } from '../server.mjs';

const ROUTE = '/api/tc-acquisition-evidence';
let server;
let base;

before(async () => {
  server = createPreviewServer();
  await new Promise((resolve) => server.listen(0, UI_BIND_HOST, resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  base = origin.origin;
});

after(() => server.close());

function keysOf(value, out = []) {
  if (Array.isArray(value)) value.forEach((v) => keysOf(v, out));
  else if (value && typeof value === 'object') {
    for (const [k, v] of Object.entries(value)) {
      if (v !== false) out.push(k);
      keysOf(v, out);
    }
  }
  return out;
}

test('G: GET succeeds with a JSON body, the security headers, no-store and no CORS wildcard', async () => {
  const r = await fetch(`${base}${ROUTE}`);
  assert.equal(r.status, 200);
  assert.match(r.headers.get('content-type'), /application\/json/);
  assert.match(r.headers.get('content-security-policy'), /default-src 'self'/);
  assert.equal(r.headers.get('access-control-allow-origin'), null);
  assert.equal(r.headers.get('cache-control'), 'no-store');
  const body = await r.json();
  assert.equal(body.nodes.length, 6);
  assert.equal(body.stageId, STAGE_ID, 'the Preview identity stays the committed Stage by Owner rule');
  assert.equal(body.stageId, '0.4C-K2');
  assert.equal(body.evidenceStage, '0.4C-K3');
  assert.equal(body.evidenceMode, 'EVIDENCE_AND_CONTRACT_ONLY');
});

test('G: HEAD succeeds with no body', async () => {
  const r = await fetch(`${base}${ROUTE}`, { method: 'HEAD' });
  assert.equal(r.status, 200);
  assert.equal(await r.text(), '');
});

test('G: every write-style method is refused with 405 and an Allow header that names only GET and HEAD', async () => {
  for (const method of ['POST', 'PUT', 'PATCH', 'DELETE', 'OPTIONS']) {
    const r = await fetch(`${base}${ROUTE}`, { method, body: method === 'POST' ? '{}' : undefined });
    assert.equal(r.status, 405, method);
    assert.equal(r.headers.get('allow'), 'GET, HEAD', method);
  }
});

test('G: the response is deterministic across requests and an unknown query string cannot change it', async () => {
  const a = await (await fetch(`${base}${ROUTE}`)).text();
  const b = await (await fetch(`${base}${ROUTE}`)).text();
  const c = await (await fetch(`${base}${ROUTE}?node=TC-NODE-09&port=1&protocol=x&activate=true`)).text();
  assert.equal(a, b);
  assert.equal(a, c);
});

test('G: the response carries the canonical six Nodes with unresolved port and protocol and no live communication state', async () => {
  const body = await (await fetch(`${base}${ROUTE}`)).json();
  assert.deepEqual(body.nodes.map((n) => [n.nodeId, n.cabinetId, n.ipAddress]), CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => [n.nodeId, n.cabinetId, n.ipAddress]));
  for (const n of body.nodes) {
    assert.equal(n.portStatus, 'PORT_UNRESOLVED');
    assert.equal(n.protocolStatus, 'PROTOCOL_UNRESOLVED');
    assert.equal(n.reachabilityStatus, 'REACHABILITY_NOT_TESTED');
    assert.equal(n.acquisitionStatus, 'ACQUISITION_NOT_IMPLEMENTED');
    assert.equal(n.communicationState, 'CONFIGURED_NOT_TESTED');
    assert.equal(LIVE_COMMUNICATION_STATES.includes(n.communicationState), false);
    assert.equal(n.wireBreakDecodingStatus, 'WIRE_BREAK_DECODING_BLOCKED');
  }
  assert.equal(body.decisions.protocol.status, 'PROTOCOL_UNRESOLVED');
  assert.equal(body.decisions.protocol.evidenceStatus, 'OWNER_EVIDENCE_PENDING');
  assert.equal(body.decisions.protocol.candidateStatus, 'WITHDRAWN');
  assert.equal(body.decisions.protocol.sourceCompleteness, 'SOURCE_COMPLETENESS_INSUFFICIENT');
  assert.equal(body.decisions.protocol.candidateRetained, false);
  assert.equal(body.decisions.protocol.candidateRef, null);
  assert.equal(body.decisions.protocol.approvedForTestAdapter, false);
  assert.equal(body.decisions.protocol.adapterAuthorized, false);
  assert.equal(body.decisions.protocol.acquisitionAuthorized, false);
  assert.equal(body.decisions.protocol.activated, false);
  assert.equal(body.statuses.processImage, 'PROCESS_IMAGE_DERIVATION_BLOCKED');
  assert.equal(body.statuses.wireBreakDecoding.evidenceClassification, 'CONFLICTING_EVIDENCE');
  assert.equal(body.statuses.wireBreakDecoding.sourceCompleteness, 'SOURCE_COMPLETENESS_INSUFFICIENT');
  assert.equal(body.statuses.wireBreakDecoding.hardwareObservationStatus, 'NOT_PROVIDED');
  for (const src of body.sources.filter((x) => x.kind === 'MANUFACTURER_DOCUMENT' || x.kind === 'DRAWING')) {
    assert.equal(src.availability, 'NOT_AVAILABLE_IN_SESSION');
    assert.equal(src.storageAccess, 'CONNECTED_STORAGE_NOT_AUTHORIZED');
    assert.equal(src.contentRetrieval, 'CONTENT_NOT_RETRIEVED');
    assert.equal(src.evidenceStatus, 'OWNER_EVIDENCE_PENDING');
  }
  assert.equal(body.decisions.port.status, 'PORT_UNRESOLVED');
  assert.equal(body.decisions.port.candidate, null);
});

test('G: the response exposes no port number, no protocol or vendor name, no URL, no file path, no credential and no Production endpoint', async () => {
  const text = await (await fetch(`${base}${ROUTE}`)).text();
  assert.equal(/\b502\b/.test(text), false, 'no well-known port literal');
  assert.equal(/https?:\/\//.test(text), false, 'no URL');
  assert.equal(/[A-Za-z]:\\|\/home\/|\/Users\/|\.csv|\.json|\.pdf/.test(text), false, 'no file path');
  assert.equal(/secret|token|credential|passphrase/i.test(text), false);
  assert.equal(/PRODUCTION_ADDRESS|productionIp/.test(text), false);
  // Owner ruling (PR #21): no connector-derived archive identity, no candidate reference, no word range or start.
  assert.equal(/WJSS_TC_EVIDENCE_BASELINE_REV1|17345849|17 345 849|2807f158ced9a984dea288e5ab30e2d8|\.zip|OWNER_SOURCE_LOCATED/.test(text), false, 'no archive identity');
  assert.equal(/TC-K3-PROTOCOL-CANDIDATE|PROCESS_IMAGE_CANDIDATE_UNVERIFIED|startWord|endWord|candidateWordOrdinal|scheduleConsistency/.test(text), false, 'no candidate or range');
  for (const key of keysOf(JSON.parse(text))) {
    assert.doesNotMatch(key, /secret|token|credential|username|passphrase/i, key);
    assert.doesNotMatch(key, /^(write|command|setpoint|poll|connect)/i, key);
  }
});

test('G: every authority flag is an explicit false, verification counts are zero and the candidate is not an address', async () => {
  const body = await (await fetch(`${base}${ROUTE}`)).json();
  for (const [k, v] of Object.entries(body.authority)) if (k !== 'statements') assert.equal(v, false, k);
  assert.deepEqual([body.totals.reachabilityTested, body.totals.acquisitionVerified, body.totals.hardwareVerified, body.totals.fieldVerified, body.totals.protocolsApproved, body.totals.portsApproved], [0, 0, 0, 0, 0, 0]);
  assert.equal(body.processImageCandidate.state, 'DERIVATION_BLOCKED');
  assert.equal(body.processImageCandidate.candidateStatus, null);
  assert.equal(body.processImageCandidate.addressStatus, 'ADDRESS_UNRESOLVED');
  assert.equal(body.processImageCandidate.authoritative, false);
  assert.equal(body.processImageCandidate.activationPossible, false);
  assert.equal(body.processImageCandidate.perNodeInputWords, null);
  assert.deepEqual(body.processImageCandidate.modules, []);
  assert.equal(body.totals.candidateInputWordsPerNode, null);
  assert.equal(body.recordToRecordConsistency.classification, 'RECORD_TO_RECORD_CONSISTENCY_ONLY');
  assert.equal(body.recordToRecordConsistency.compared, 240);
  assert.equal(body.recordToRecordConsistency.mismatches, 0);
  assert.equal(body.recordToRecordConsistency.meaning.length, 7);
  assert.ok(body.recordToRecordConsistency.meaning.includes('not hardware verification'));
  for (const k of ['hardwareVerified', 'establishesProcessImageStart', 'establishesModuleOffsets', 'authorizesAdapter', 'authorizesAcquisition']) assert.equal(body.recordToRecordConsistency[k], false, k);
  assert.equal(body.validation.valid, true);
  assert.equal(body.missingEvidenceRequests.length, 11);
});

test('G: the existing Node configuration route is unchanged by the evidence route (it still carries no evidence keys)', async () => {
  const text = await (await fetch(`${base}/api/tc-node-configuration`)).text();
  const keys = keysOf(JSON.parse(text)).join(' ').toLowerCase();
  for (const word of ['offset', 'byte', 'word', 'processimage', 'evidence', 'candidate', 'communicationstate']) {
    assert.equal(keys.includes(word), false, `key containing '${word}'`);
  }
  const health = await (await fetch(`${base}/api/health`)).json();
  assert.equal(health.stageId, '0.4C-K2');
});
