// Stage 0.4C-K3 - real-page contract of the read-only TC Acquisition Evidence section (group G, UI half).
// The REAL public/app.mjs runs against the REAL Preview host through the stub DOM. It proves the
// application logic and the rendered text, not CSS, layout or a real browser. Every request the page
// makes is a local GET to the loopback Preview host; no Test address is ever contacted.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { allText, bootApp, cellTexts, html, rowsOf, walk } from './helpers/fakeDom.mjs';
import { CANONICAL_TC_NODE_CONFIGURATION } from '../../../packages/commissioning/src/index.mjs';

// The two Runtime-backed routes answer 503 while the independent Runtime host is absent in these
// tests. Every other request must be a 200 local GET, so an unexpected failure stays visible.
const RUNTIME_OFFLINE_PATHS = ['/api/tc-acquisition', '/api/operations/dispatch'];


let app;
let closed = false;

before(async () => {
  app = await bootApp({});
});

after(() => { if (!closed) app.close(); });

const SECTION_START = '<section id="tc-acquisition-evidence"';
const section = () => html.slice(html.indexOf(SECTION_START), html.indexOf('</section>', html.indexOf(SECTION_START)));
const table = () => app.byId['tc-evidence-body'].childNodes.find((n) => n.tagName === 'table');
const rowFor = (label) => {
  const tbody = table().childNodes.find((n) => n.tagName === 'tbody');
  const row = tbody.childNodes.find((r) => cellTexts(r)[0] === label);
  assert.ok(row, `row '${label}' must render`);
  return cellTexts(row).slice(1);
};
const CONTAINER_IDS = ['tc-evidence-validation', 'tc-evidence-decisions', 'tc-evidence-body', 'tc-evidence-sources', 'tc-evidence-process-image', 'tc-evidence-requests', 'tc-evidence-authority'];
const containers = () => CONTAINER_IDS.map((id) => app.byId[id]);

test('G: the page boots ready; the evidence projection was requested as a local GET and every request stayed on the loopback host', () => {
  assert.equal(app.byId['app-root'].attributes['data-app-state'], 'ready');
  assert.equal(app.byId['boot-error'].hidden, true);
  const projection = app.requests.find((r) => r.path === '/api/tc-acquisition-evidence');
  assert.ok(projection, 'the evidence projection must be requested at boot');
  assert.equal(projection.status, 200);
  for (const request of app.requests) {
    assert.equal(request.method, 'GET', request.path);
    assert.equal(request.status, RUNTIME_OFFLINE_PATHS.includes(request.path) ? 503 : 200, request.path);
    assert.equal(request.path.startsWith('/'), true, 'every request is a local route');
    assert.equal(/\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}/.test(request.path), false, 'no request names an address');
  }
});

test('G: the section and its navigation anchor are in the served page with the Stage boundary text', () => {
  const s = section();
  assert.match(s, /TC Acquisition Evidence/);
  assert.match(s, /Stage 0\.4C-K3 evidence gate/);
  assert.match(s, /NO NETWORK PROBE \/ NO SOCKET \/ NO PHYSICAL ACQUISITION/);
  assert.match(s, /Preview identity\s+stays Stage 0\.4C-K2/);
  assert.match(html, /href="#tc-acquisition-evidence">TC Acquisition Evidence</);
  assert.match(html, /id="node-configuration"[\s\S]*id="tc-acquisition-evidence"[\s\S]*id="diagnostics"/, 'the section follows Node Configuration and precedes Diagnostics');
});

test('G: all six Nodes render with their exact Cabinet and Test IP', () => {
  const header = table().childNodes[0].childNodes[0];
  assert.deepEqual(cellTexts(header), ['Attribute', ...CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => n.nodeId)]);
  assert.deepEqual(rowFor('Cabinet ID'), CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => n.cabinetId));
  assert.deepEqual(rowFor('Test IP'), CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => n.ipAddress));
});

test('G: every status is written as text with its code: unresolved port and protocol, not tested, not implemented, configured-not-tested, candidate, blocked, pending', () => {
  assert.deepEqual(rowFor('Port'), Array(6).fill('UNRESOLVED (PORT_UNRESOLVED)'));
  assert.deepEqual(rowFor('Protocol'), Array(6).fill('UNRESOLVED (PROTOCOL_UNRESOLVED)'));
  assert.deepEqual(rowFor('Reachability'), Array(6).fill('NOT TESTED (REACHABILITY_NOT_TESTED)'));
  assert.deepEqual(rowFor('Acquisition'), Array(6).fill('NOT IMPLEMENTED (ACQUISITION_NOT_IMPLEMENTED)'));
  assert.deepEqual(rowFor('Communication state'), Array(6).fill('CONFIGURED, NOT TESTED (CONFIGURED_NOT_TESTED)'));
  assert.deepEqual(rowFor('Process image'), Array(6).fill('DERIVATION BLOCKED (PROCESS_IMAGE_DERIVATION_BLOCKED)'));
  assert.deepEqual(rowFor('Byte order'), Array(6).fill('UNRESOLVED (BYTE_ORDER_UNRESOLVED)'));
  assert.deepEqual(rowFor('Word order'), Array(6).fill('UNRESOLVED (WORD_ORDER_UNRESOLVED)'));
  assert.deepEqual(rowFor('Wire-break decoding'), Array(6).fill('DECODING BLOCKED (WIRE_BREAK_DECODING_BLOCKED)'));
  assert.deepEqual(rowFor('Firmware'), Array(6).fill('NOT VERIFIED (FIRMWARE_NOT_VERIFIED)'));
  assert.deepEqual(rowFor('Hardware verification'), Array(6).fill('PENDING (HARDWARE_VERIFICATION_PENDING)'));
  assert.deepEqual(rowFor('Field verification'), Array(6).fill('PENDING (FIELD_VERIFICATION_PENDING)'));
  assert.deepEqual(rowFor('Hardware access'), Array(6).fill('NOT AUTHORIZED (HARDWARE_ACCESS_NOT_AUTHORIZED)'));
  const everything = containers().map(allText).join(' ') + containers().map((c) => c.textContent ?? '').join(' ');
  assert.equal(/\b(ONLINE|ONLINE_READ_ONLY|COMMUNICATION_LOSS|RECONNECTING|CONNECTING)\b/.test(rowFor('Communication state').join(' ')), false);
  assert.equal(/\b502\b/.test(everything), false, 'no port number appears anywhere in the section');
  // Owner ruling (PR #21): no candidate reference, no word range and no connector-derived archive identity anywhere in the section.
  assert.equal(/PROCESS_IMAGE_CANDIDATE_UNVERIFIED|CANDIDATE, UNVERIFIED/.test(everything), false);
  assert.equal(/TC-K3-PROTOCOL-CANDIDATE|OWNER_SOURCE_LOCATED/.test(everything), false);
  assert.equal(/\b(0-7|8-15|16-23|24-31|32-39)\b|startWord|endWord|40 candidate input words/.test(everything), false, 'no word range or start');
  assert.equal(/WJSS_TC_EVIDENCE_BASELINE_REV1|17345849|17 345 849|2807f158ced9a984dea288e5ab30e2d8|\.zip|Drive/.test(everything), false, 'no archive identity');
});

test('G: the decision block shows PROTOCOL_UNRESOLVED, Owner evidence pending, candidate WITHDRAWN, completeness insufficient, the seven-point note, PORT_UNRESOLVED with no default, and pending freshness thresholds', () => {
  const text = allText(app.byId['tc-evidence-decisions']);
  assert.match(text, /Protocol decision PROTOCOL_UNRESOLVED/);
  assert.match(text, /Protocol evidence status OWNER_EVIDENCE_PENDING/);
  assert.match(text, /Protocol candidate status WITHDRAWN - no candidate is retained; no protocol is inferred from the device family/);
  assert.match(text, /Protocol source completeness SOURCE_COMPLETENESS_INSUFFICIENT/);
  assert.match(text, /Non-authoritative evidence note .*transcribed in Stage 0B.*NOT retained as a candidate\. Original document readable in this review: no; committed profile section truncated: yes; as-installed applicability verified: no; firmware verified: no; configuration verified: no; port resolved: no; candidate retained: no; adapter authorized: no; acquisition authorized: no\./);
  assert.equal(/named by|candidate reference|\(PROTOCOL_CANDIDATE\)/.test(text), false, 'no candidate is named; vendor and transport vocabulary is enforced by the boundary scan (S3)');
  assert.match(text, /Approved for a Test adapter no - Owner approval OWNER_INPUT_PENDING/);
  assert.match(text, /Protocol activated no/);
  assert.match(text, /Port decision PORT_UNRESOLVED/);
  assert.match(text, /Port candidate none - NO_EVIDENCE_NAMES_THE_PORT; no default is assumed/);
  assert.match(text, /9 states, contract only; current Node state CONFIGURED, NOT TESTED/);
  assert.match(text, /aging OWNER_INPUT_PENDING · stale OWNER_INPUT_PENDING · Last-Valid retention OWNER_INPUT_PENDING/);
  assert.match(app.byId['tc-evidence-validation'].textContent, /Evidence validation: 0 issues · evidence set TC-ACQUISITION-EVIDENCE-K3-REV1 · evidence Stage 0\.4C-K3 · mode EVIDENCE_AND_CONTRACT_ONLY · Preview identity Stage 0\.4C-K2/);
});

test('G: the evidence-source summary lists the nine sources with their availability, the claim classes, and the unresolved fields with their model-level candidates', () => {
  const text = allText(app.byId['tc-evidence-sources']);
  assert.match(text, /24 classified claims/);
  assert.match(text, /MANUFACTURER_EVIDENCE \d+/);
  assert.match(text, /CONFLICTING_EVIDENCE 1/);
  assert.match(text, /SYNTHETIC_TEST_ONLY 1/);
  assert.match(text, /NOT_AVAILABLE_IN_SESSION CONNECTED_STORAGE_NOT_AUTHORIZED CONTENT_NOT_RETRIEVED OWNER_EVIDENCE_PENDING no/);
  assert.equal((text.match(/CONNECTED_STORAGE_NOT_AUTHORIZED CONTENT_NOT_RETRIEVED OWNER_EVIDENCE_PENDING/g) ?? []).length, 4, 'drawing and three manuals');
  assert.match(text, /COMMITTED_TRUNCATED n\/a n\/a n\/a TRUNCATED/);
  assert.match(text, /recorded \(RECORDED_IN_0B_REVIEW_NOT_RECOMPUTED_IN_K3\)/);
  assert.match(text, /Byte order UNRESOLVED \(BYTE_ORDER_UNRESOLVED\) · candidate LOW_BYTE_FIRST \[MANUFACTURER_EVIDENCE, MODEL_LEVEL\]/);
  assert.match(text, /Word order UNRESOLVED \(WORD_ORDER_UNRESOLVED\) · candidate NOT_APPLICABLE_SINGLE_WORD \[CANDIDATE_UNVERIFIED, MODEL_LEVEL\]/);
  assert.match(text, /Status location UNRESOLVED \(STATUS_LOCATION_UNRESOLVED\) · candidate NOT_ON_FIELD_NETWORK/);
  assert.match(text, /Wire-break decoding DECODING BLOCKED \(WIRE_BREAK_DECODING_BLOCKED\) - evidence classification CONFLICTING_EVIDENCE; source completeness SOURCE_COMPLETENESS_INSUFFICIENT; Hardware observation NOT_PROVIDED; physical decoder implemented: no; 0 of 8 required evidence items provided/);
  assert.match(text, /Wire-break evidence record Manufacturer document transcription \(Stage 0B\): .*as transcribed in Stage 0B.*Reviewer paraphrase: .*reviewer wording.*As-installed configuration: MISSING.*Field observation: MISSING.*Ambiguity: WIRE_BREAK_VS_DEACTIVATED_CHANNEL_INDISTINGUISHABLE_BY_DATA_WORD/);
  const sourceTable = app.byId['tc-evidence-sources'].childNodes.find((n) => n.tagName === 'table');
  const sourceRows = sourceTable.childNodes.find((n) => n.tagName === 'tbody').childNodes.filter((n) => n.tagName === 'tr');
  assert.equal(sourceRows.length, 9, 'nine source rows');
  assert.equal(cellTexts(sourceRows[0]).length, 9);
});

test('G: the process image is shown as DERIVATION BLOCKED with its blockers; the only arithmetic is RECORD_TO_RECORD_CONSISTENCY_ONLY with its meaning; no range table exists', () => {
  const text = allText(app.byId['tc-evidence-process-image']);
  assert.match(text, /Process image: DERIVATION BLOCKED \(DERIVATION_BLOCKED\); address UNRESOLVED \(ADDRESS_UNRESOLVED\); authoritative no; activation possible no\. Blockers: protocol: PROTOCOL_UNRESOLVED; protocol evidence: OWNER_EVIDENCE_PENDING;/);
  assert.match(text, /source SRC-MANUAL-750-498: OWNER_EVIDENCE_PENDING/);
  assert.match(text, /SOURCE_NOT_READABLE_IN_CURRENT_REVIEW/);
  assert.match(text, /RECORD_TO_RECORD_CONSISTENCY_ONLY: 240 schedule rows compared with the repository profile numbers, 0 mismatches\. Meaning: record-to-record comparison only; not hardware verification; not current-session manufacturer verification; does not establish a process-image start; does not establish module offsets; does not authorize an adapter; does not authorize physical acquisition\./);
  assert.match(text, /Verification blockers: .*REQ-K3-03.*REQ-K3-05.*REQ-K3-02.*REQ-K3-04/);
  assert.equal(app.byId['tc-evidence-process-image'].childNodes.some((n) => n.tagName === 'table'), false, 'no candidate range table');
  assert.equal(/CANDIDATE_UNVERIFIED|candidate input words|\(8 words|\b0-7\b|\b32-39\b|Schedule consistency/.test(text), false);
});

test('G: the missing-evidence list names eleven exact requests, each with what it blocks, all OWNER_INPUT_PENDING', () => {
  const items = app.byId['tc-evidence-requests'].childNodes.filter((n) => n.tagName === 'li');
  assert.equal(items.length, 11);
  const text = allText(app.byId['tc-evidence-requests']);
  for (let i = 1; i <= 11; i += 1) assert.match(text, new RegExp(`REQ-K3-${String(i).padStart(2, '0')}`));
  assert.match(text, /blocks: PORT;/);
  assert.match(text, /blocks: PROTOCOL_UNRESOLVED; APPROVED_FOR_TEST_ADAPTER/);
  assert.equal((text.match(/OWNER_INPUT_PENDING/g) ?? []).length, 11);
});

test('G: the authority line states the boundary and the section carries no button, input, select, form or control vocabulary', () => {
  assert.match(app.byId['tc-evidence-authority'].textContent, /SIMULATION \/ PREVIEW \/ EVIDENCE AND CONTRACT FOUNDATION ONLY \/ NO NETWORK PROBE \/ NO SOCKET \/ NO PHYSICAL ACQUISITION \/ NO DEVICE WRITE \/ NO TEST_HARDWARE \/ NO PRODUCTION \/ FIELD VERIFICATION PENDING/);
  assert.match(app.byId['tc-evidence-authority'].textContent, /A candidate is not an address; model-level evidence does not resolve an instance; the synthetic wire-break scenario is not physical evidence/);
  const s = section();
  for (const tag of ['<button', '<input', '<select', '<form', '<textarea']) assert.equal(s.includes(tag), false, tag);
  for (const container of containers()) {
    walk(container, (node) => {
      assert.equal(['button', 'input', 'select', 'form', 'textarea', 'a'].includes(node.tagName), false, `${node.tagName} in section`);
    });
  }
  const text = containers().map(allText).join(' ') + ' ' + containers().map((c) => c.textContent ?? '').join(' ');
  assert.equal(/\b(Connect|Test Connection|Ping|Activate|Start Polling|Apply to Device|Poll|Probe|Read Now|Start|Apply|Save|Hardware Mode|Production Mode)\b/.test(text), false);
  assert.equal(/\b(Connect|Test Connection|Ping|Activate|Start Polling|Apply to Device|Poll|Probe|Read Now|Start|Apply|Save|Hardware Mode|Production Mode)\b/.test(s), false);
});

test('G: the Node Configuration section, the Wall Map and the Commissioning Monitor still render, and the page identity stays Stage 0.4C-K2', () => {
  const nodeTable = app.byId['node-config-body'].childNodes.find((n) => n.tagName === 'table');
  assert.ok(nodeTable, 'the K2 section still renders');
  assert.equal(rowsOf(app.byId, 'monitor-table-body').length, 240);
  assert.ok(app.byId.wallmap.childNodes.length > 0, 'the U-shaped Wall Map renders');
  assert.match(html, /<p class="sub">Stage 0\.4C-K2 · /);
  assert.match(allText(app.byId['diagnostics-body']), /Stage 0\.4C-K2/);
});

test('G: when the evidence route fails, only the evidence section degrades and the rest of the page still boots ready', async () => {
  // Two live application instances would share one stub DOM, so the shared instance is closed first.
  app.close();
  closed = true;
  const degraded = await bootApp({ failPath: '/api/tc-acquisition-evidence' });
  try {
    assert.equal(degraded.byId['app-root'].attributes['data-app-state'], 'ready');
    assert.match(degraded.byId['tc-evidence-validation'].textContent, /TC acquisition evidence unavailable/);
    assert.equal(degraded.byId['tc-evidence-body'].childNodes.some((n) => n.tagName === 'table'), false);
    assert.ok(degraded.byId['node-config-body'].childNodes.find((n) => n.tagName === 'table'), 'the K2 section is unaffected');
    assert.equal(rowsOf(degraded.byId, 'monitor-table-body').length, 240);
  } finally {
    degraded.close();
  }
});
