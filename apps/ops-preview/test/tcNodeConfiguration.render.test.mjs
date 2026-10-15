// Stage 0.4C-K2 - real-page contract of the read-only TC Node Configuration section (group G).
// The REAL public/app.mjs runs against the REAL Preview host through the stub DOM. This proves the
// application logic and the rendered text; it does not prove CSS, layout or a real browser.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { allText, bootApp, cellTexts, html, rowsOf, walk } from './helpers/fakeDom.mjs';
import { CANONICAL_TC_NODE_CONFIGURATION } from '../../../packages/commissioning/src/index.mjs';

let app;
let closed = false;

before(async () => {
  app = await bootApp({});
});

after(() => { if (!closed) app.close(); });

const section = () => html.slice(html.indexOf('<section id="node-configuration"'),
  html.indexOf('</section>', html.indexOf('<section id="node-configuration"')));
const table = () => app.byId['node-config-body'].childNodes.find((n) => n.tagName === 'table');
const rowFor = (label) => {
  const tbody = table().childNodes.find((n) => n.tagName === 'tbody');
  const row = tbody.childNodes.find((r) => cellTexts(r)[0] === label);
  assert.ok(row, `row '${label}' must render`);
  return cellTexts(row).slice(1);
};
const containers = () => ['node-config-validation', 'node-config-totals', 'node-config-body', 'node-config-legend', 'node-config-authority']
  .map((id) => app.byId[id]);

test('G: the page boots ready; Node configuration succeeds and the absent Runtime is reported separately', () => {
  assert.equal(app.byId['app-root'].attributes['data-app-state'], 'ready');
  assert.equal(app.byId['boot-error'].hidden, true);
  const projection = app.requests.find((r) => r.path === '/api/tc-node-configuration');
  assert.ok(projection, 'the projection must be requested at boot');
  assert.equal(projection.status, 200);
  for (const request of app.requests) {
    assert.equal(request.method, 'GET', request.path);
    assert.equal(request.status, request.path === '/api/tc-acquisition' ? 503 : 200, request.path);
    assert.equal(request.path.startsWith('/'), true, 'every request is a local route');
  }
});

test('G: the Node Configuration section is in the served page, with the approved Stage boundary text', () => {
  const s = section();
  assert.match(s, /Node Configuration/);
  assert.match(s, /TEST_CONFIGURATION/);
  assert.match(s, /NO HARDWARE ACCESS \/ NO DEVICE WRITE/);
  assert.match(s, /id="node-config-body"/);
  assert.match(html, /href="#node-configuration">Node Configuration</);
});

test('G: all six Nodes render, each with its exact Cabinet and its exact Test IP', () => {
  const header = table().childNodes[0].childNodes[0];
  assert.deepEqual(cellTexts(header), ['Attribute', ...CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => n.nodeId)]);
  assert.deepEqual(rowFor('Cabinet ID'), CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => n.cabinetId));
  assert.deepEqual(rowFor('Test IP'), CANONICAL_TC_NODE_CONFIGURATION.nodes.map((n) => n.ipAddress));
  assert.equal(new Set(rowFor('Test IP')).size, 6, 'no Test IP is duplicated on the screen');
});

test('G: the exact topology renders per Node: Coupler, TC modules, Channels, end module', () => {
  assert.deepEqual(rowFor('Head station (expected)'), Array(6).fill('750-362 x 1'));
  assert.deepEqual(rowFor('TC input modules (expected)'), Array(6).fill('5 x 750-498'));
  assert.deepEqual(rowFor('Physical TC Channels (expected)'), Array(6).fill('40'));
  assert.deepEqual(rowFor('End module (expected)'), Array(6).fill('750-600 x 1'));
  assert.deepEqual(rowFor('Environment'), Array(6).fill('TEST'));
  assert.deepEqual(rowFor('Configuration mode'), Array(6).fill('TEST_CONFIGURATION'));
});

test('G: Port and Protocol are visibly UNRESOLVED and never blank or zero', () => {
  assert.deepEqual(rowFor('Port'), Array(6).fill('UNRESOLVED'));
  assert.deepEqual(rowFor('Protocol'), Array(6).fill('UNRESOLVED'));
});

test('G: every honest status is visible as text: CONFIGURED, NOT TESTED, NOT IMPLEMENTED, PENDING', () => {
  assert.deepEqual(rowFor('Configuration'), Array(6).fill('CONFIGURED'));
  assert.deepEqual(rowFor('Reachability'), Array(6).fill('NOT TESTED'));
  assert.deepEqual(rowFor('Acquisition'), Array(6).fill('NOT IMPLEMENTED'));
  assert.deepEqual(rowFor('Hardware verification'), Array(6).fill('PENDING'));
  assert.deepEqual(rowFor('Field verification'), Array(6).fill('PENDING'));
  assert.deepEqual(rowFor('Hardware access'), Array(6).fill('NOT AUTHORIZED'));
});

test('G: the aggregate totals are 6 Nodes, 6 Cabinets, 30 TC Modules, 240 Channels, 6 Test IPs and zero verification', () => {
  const totals = allText(app.byId['node-config-totals']);
  assert.match(totals, /Nodes 6 Cabinets 6 TC Modules 30 Physical TC Channels 240 Configured Test IPs 6 Reachability tested 0 Acquisition verified 0 Field verified 0/);
  // The validation line is written with textContent, so it is read from the property, not the children.
  assert.match(app.byId['node-config-validation'].textContent, /Configuration validation: PASSED \(0 issues\) · Configuration status: CONFIGURED/);
});

test('G: the section carries no button, input, select or form, in the served markup or the rendered output', () => {
  const s = section();
  for (const tag of ['<button', '<input', '<select', '<form', '<textarea']) assert.equal(s.includes(tag), false, tag);
  for (const container of containers()) {
    walk(container, (node) => {
      assert.equal(['button', 'input', 'select', 'form', 'textarea'].includes(node.tagName), false, `${node.tagName} in section`);
    });
  }
});

test('G: no Connect, Test Connection, Activate, Start Polling or Apply to Device control text exists in the section', () => {
  const text = containers().map(allText).join(' ');
  assert.equal(/\b(Connect|Test Connection|Activate|Start Polling|Apply to Device|Poll)\b/.test(text), false);
  assert.equal(/\b(Connect|Test Connection|Activate|Start Polling|Apply to Device|Poll)\b/.test(section()), false);
});

test('G: the Wall Map, the Overview and the inherited Preview still render (no regression)', () => {
  assert.equal(rowsOf(app.byId, 'monitor-table-body').length, 240);
  assert.ok(app.byId.wallmap.childNodes.length > 0, 'the U-shaped Wall Map renders');
  assert.match(html, /id="wallmap-h"/);
  assert.match(html, /id="monitor-wrap"/);
});

test('G: the embedded header and Diagnostics identify Stage 0.4C-K2, with no current K1 identity in the served page', () => {
  assert.match(html, /<p class="sub">Stage 0\.4C-K2 · /);
  // The only K1 reference left in the served page is the documentation link to the historical K1 checkpoint.
  const withoutHistoricalLink = html.split('docs/STAGE_0.4C-K1_CHECKPOINT.md').join('');
  assert.equal(/0\.4C-K1/.test(withoutHistoricalLink), false, 'no current K1 identity in the served page');
  assert.match(allText(app.byId['diagnostics-body']), /Stage 0\.4C-K2/);
  assert.equal(/0\.4C-K1/.test(allText(app.byId['diagnostics-body'])), false);
});

test('G: the section is wrapped in the bounded table frame, so it cannot widen the page', () => {
  assert.match(section(), /<div class="table-wrap" id="node-config-wrap">/);
});

test('G: a failed projection degrades only this section; the rest of the page stays live', async () => {
  // Two live application instances would share one stub DOM, so the shared instance is closed first.
  app.close();
  closed = true;
  const degraded = await bootApp({ failPath: '/api/tc-node-configuration' });
  try {
    assert.equal(degraded.byId['app-root'].attributes['data-app-state'], 'ready');
    assert.match(degraded.byId['node-config-validation'].textContent, /unavailable/);
    assert.equal(rowsOf(degraded.byId, 'monitor-table-body').length, 240);
  } finally {
    degraded.close();
  }
});
