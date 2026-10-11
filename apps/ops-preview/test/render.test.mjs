// Stage 0.4C-K1 (re-verified at 0.4C-K2) - Kiosk Preview UI tests (Stage task section 16, groups A, C and D, plus the
// visual-acceptance content of section 18).
//
// These tests run the REAL public/app.mjs against the REAL Preview server through a stub DOM. They
// prove the application logic and the rendered text. They do NOT prove layout, CSS, scaling or real
// browser behaviour: no browser was run, and none of that is claimed.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { allText, bootApp, byAction, byFilter, cellTexts, click, html, rowsOf, waitFor } from './helpers/fakeDom.mjs';

let app;
let closed = null;

before(async () => {
  closed = { called: false };
  app = await bootApp({ extraWindow: { close: () => { closed.called = true; } } });
});

after(() => {
  app.close();
  closed = null;
});

const id = (name) => app.byId[name];
const text = (name) => id(name).textContent;
const tableRows = () => rowsOf(app.byId, 'monitor-table-body');
const cells = (row) => cellTexts(row);
const allCells = () => tableRows().map(cells);
const detailText = () => [allText(id('detail-identity')), allText(id('detail-observation')), allText(id('detail-workflow'))].join(' ');
const filter = (key) => click(byFilter(id('filters'), key));
const action = (name) => byAction(id('workflow-actions'), name);

async function applyScenario(key, { node = null, tick = 0, until } = {}) {
  filter('all');
  id('scenario-select').value = key;
  if (node) id('node-select').value = node;
  id('tick-input').value = String(tick);
  click(id('scenario-apply'));
  await waitFor(() => text('scenario-summary').startsWith(key), `scenario ${key} to be applied`);
  if (until) await waitFor(until, `${key}: ${until.toString()}`);
}

const hasCell = (index, needle) => () => allCells().some((c) => c[index].includes(needle));

test('the shell shows the Stage boundary on screen', () => {
  const chips = allText(id('boundary-chips'));
  assert.match(chips, /SIMULATION \/ PREVIEW/);
  assert.match(chips, /Stage 0\.4C-K2/);
  assert.match(chips, /NO HARDWARE ACCESS/);
  assert.match(chips, /NO DEVICE WRITE/);
  assert.match(chips, /FIELD VERIFICATION PENDING/);
  // Static shell chrome (read from the served file, since the stub DOM does not parse HTML text).
  assert.match(html, /Kiosk shell Preview/);
  assert.match(html, /id="exit-button"[^>]*>Exit Preview</);
  assert.match(html, /SIMULATION \/ PREVIEW · NO HARDWARE ACCESS · NO DEVICE WRITE ·\s+FIELD VERIFICATION PENDING/);
});

test('the navigation exposes Operations, Sensor Map, Commissioning Monitor, Mapping and Diagnostics', () => {
  for (const area of ['operations', 'sensor-map', 'commissioning', 'mapping', 'diagnostics']) {
    assert.match(html, new RegExp(`href="#${area}"`), area);
    assert.notEqual(id(area), undefined, area);
  }
  assert.match(html, /Commissioning Monitor/);
  assert.match(html, /Sensor Map/);
});

test('the Overview renders all 240 physical Channels with every required column', () => {
  assert.equal(tableRows().length, 240);
  assert.equal(cells(tableRows()[0]).length, 20);
  assert.match(html, /Sensor ID/);
  assert.match(html, /Product compat\./);
  assert.match(html, /Last valid \(historical\)/);
  assert.match(html, /Value age/);
  assert.match(html, /Field verification/);
});

test('the summary counts are the evidence totals, and filtering never changes them', () => {
  const summaryBefore = allText(id('summary-body'));
  assert.match(summaryBefore, /Physical Channels 240/);
  assert.match(summaryBefore, /Mapped 216/);
  assert.match(summaryBefore, /Unassigned 24/);
  assert.match(summaryBefore, /Product-compatible measurements 212/);
  assert.match(summaryBefore, /Conflict rows 16/);
  assert.match(summaryBefore, /BLOCKED 40/);
  assert.match(summaryBefore, /NOT_TESTED 200/);
  assert.match(summaryBefore, /PASS 0/);
  filter('quality-bad');
  assert.equal(tableRows().length, 0, 'no BAD row exists in the default NORMAL_GOOD scenario');
  assert.equal(allText(id('summary-body')), summaryBefore, 'the summary is not filtered');
  filter('all');
  assert.equal(tableRows().length, 240);
});

test('the required filters are present with their counts', () => {
  const filters = allText(id('filters'));
  for (const label of ['All (240)', 'Product-compatible (212)', 'Drawing verified (200)',
    'Drawing internal conflict (12)', 'Product-topology conflict (4)', 'Unassigned (24)',
    // GOOD is 216 because the 24 unassigned physical Channels carry no measurement at all.
    'GOOD (216)', 'UNCERTAIN (0)', 'BAD (0)', 'STALE (0)', 'COMMUNICATION_LOSS (0)', 'Simulated wire break (0)',
    'NOT_TESTED (200)', 'IN_PROGRESS (0)', 'PASS (0)', 'FAIL (0)', 'PARTIAL (0)', 'BLOCKED (40)']) {
    assert.match(filters, new RegExp(label.replace(/[()]/g, '\\$&')), label);
  }
});

test('filtering is presentation only and the counts stay identical', () => {
  for (const key of ['product-compatible', 'drawing-internal-conflict', 'product-topology-conflict', 'unassigned', 'commissioning-blocked']) {
    filter(key);
    assert.equal(tableRows().length > 0, true, key);
  }
  filter('all');
  assert.match(text('monitor-count'), /Showing 240 of 240 physical Channels/);
  assert.match(text('monitor-count'), /changes the view only/);
});

test('a BAD observation never shows a numeric current value', async () => {
  await applyScenario('OUT_OF_RANGE', { until: hasCell(12, 'BAD') });
  const row = tableRows().find((r) => cells(r)[12].includes('BAD'));
  assert.ok(row, 'a BAD row is rendered');
  const c = cells(row);
  assert.match(c[10], /UNAVAILABLE/);
  assert.equal(/[0-9]/.test(c[10]), false, 'no digits in an unavailable current value');
  assert.equal(c[11], '-', 'no unit for an unavailable value');
  assert.equal(c[14], 'no');
  assert.match(c[13], /Out of range/);
  assert.match(c[15], /not the current value/);
  assert.match(allText(id('summary-body')), /Current value unavailable [1-9]/);
  assert.match(allText(id('summary-body')), /BAD [1-9]/);
  filter('all');
});

test('a STALE observation is distinct from communication loss and from a Channel fault', async () => {
  await applyScenario('STALE', { until: hasCell(12, 'STALE') });
  const row = tableRows().find((r) => cells(r)[12].includes('STALE'));
  assert.ok(row, 'a STALE row is rendered');
  const c = cells(row);
  assert.equal(c[12].includes('BAD'), false);
  assert.match(c[13], /Module fault/);
  assert.match(c[17], /stale \(Preview threshold\)/i);
  assert.match(allText(id('summary-body')), /STALE [1-9]/);
  filter('all');
});

test('communication loss is scoped to one node and the other five stay online', async () => {
  await applyScenario('COMMUNICATION_LOSS_NODE', { node: 'TC-NODE-04', until: hasCell(3, 'TC-NODE-04') });
  const nodeText = allText(id('node-summary-body'));
  assert.match(nodeText, /TC-NODE-04/);
  assert.match(nodeText, /COMMUNICATION_LOSS/);
  assert.equal((nodeText.match(/ONLINE/g) ?? []).length, 5);
  // 36 of the node's 40 Channels carry a measurement and report the loss; the node's 4 unassigned
  // positions carry no measurement at all, and the node itself is presented as COMMUNICATION_LOSS.
  assert.match(allText(id('summary-body')), /Communication loss 36/);
  assert.match(allText(id('summary-body')), /BAD 36/);
  assert.match(allText(id('summary-body')), /Current value unavailable 60/);
  assert.match(nodeText, /TC-NODE-04 CAB4 40 40 COMMUNICATION_LOSS/);
  filter('communication-loss');
  assert.equal(tableRows().length, 36);
  for (const row of tableRows()) assert.equal(cells(row)[3], 'TC-NODE-04');
  filter('all');
});

test('the simulated wire break is labelled as a simulation in the row and in the detail panel', async () => {
  await applyScenario('SIMULATED_WIRE_BREAK', { until: hasCell(13, 'Wire break') });
  const row = tableRows().find((r) => cells(r)[13].includes('Wire break'));
  assert.ok(row, 'a wire-break row is rendered');
  assert.match(cells(row)[13], /SIMULATED - NOT HARDWARE DETECTED/);
  assert.equal(cells(row)[10], 'UNAVAILABLE');
  assert.equal(cells(row)[14], 'no');
  click(row);
  const detail = detailText();
  assert.match(detail, /SIMULATED DIAGNOSTIC SCENARIO - NOT HARDWARE DETECTED/);
  assert.match(detail, /IsCurrentValueValid false/);
  assert.match(detail, /SIMULATED_WIRE_BREAK/);
  filter('all');
});

test('conflict and unassigned rows are BLOCKED and cannot be marked PASS', async () => {
  filter('all');
  const blockedIndex = allCells().findIndex((c) => c[18] === 'BLOCKED');
  assert.equal(blockedIndex >= 0, true, 'a BLOCKED row is rendered');
  click(tableRows()[blockedIndex]);
  assert.equal(action('verdict-PASS').disabled, true, 'PASS is disabled on a BLOCKED row');
  assert.match(String(action('verdict-PASS').attributes.title ?? ''), /Not recordable/);
  assert.equal(action('verdict-FAIL').disabled, false, 'FAIL is still recordable on a mapped but blocked row');
  assert.match(detailText(), /Evidence blocking .*_BLOCKED/);
});

test('the two conflict scenarios present their own blocking evidence and cannot PASS', async () => {
  await applyScenario('DRAWING_CONFLICT', { until: hasCell(18, 'BLOCKED') });
  filter('drawing-internal-conflict');
  assert.equal(tableRows().length, 12);
  click(tableRows()[0]);
  assert.match(detailText(), /DRAWING_INTERNAL_CONFLICT/);
  assert.match(detailText(), /sheath and conductor disagree/);
  assert.equal(action('verdict-PASS').disabled, true);
  click(action('begin-BASELINE'));
  assert.match(detailText(), /DRAWING_INTERNAL_CONFLICT_BLOCKED|sheath and conductor disagree/);

  await applyScenario('PRODUCT_TOPOLOGY_CONFLICT', { until: hasCell(13, 'Not mapped') });
  filter('product-topology-conflict');
  assert.equal(tableRows().length, 4);
  click(tableRows()[0]);
  const detail = detailText();
  assert.match(detail, /never a Sensor, never a Water Jet/);
  assert.match(detail, /PRODUCT_TOPOLOGY_CONFLICT_BLOCKED/);
  assert.equal(action('verdict-PASS').disabled, true);
  filter('all');
});

test('an unassigned physical Channel offers no verdict at all', () => {
  filter('unassigned');
  click(tableRows()[0]);
  const c = cells(tableRows()[0]);
  assert.equal(c[0], '-', 'no Sensor identity');
  assert.equal(c[2], '-', 'no measurement identity');
  assert.match(c[10], /UNAVAILABLE/);
  assert.match(c[17], /no measurement exists/);
  assert.equal(action('verdict-PASS').disabled, true);
  assert.equal(action('verdict-FAIL').disabled, true);
  filter('all');
});

test('a preview verdict is recorded, shown in the row, and approves nothing', () => {
  filter('all');
  click(tableRows()[0]);
  const before = allText(id('summary-body'));
  click(action('begin-BASELINE'));
  assert.match(detailText(), /Baseline observation/);
  const passButton = action('verdict-PASS');
  assert.equal(passButton.disabled, false, 'the first schedule row is an eligible Product-compatible measurement');
  click(passButton);
  assert.equal(cells(tableRows()[0])[18], 'PASS');
  assert.match(text('alert-line'), /approves nothing/);
  assert.notEqual(allText(id('summary-body')), before, 'the summary reflects the Preview record');
  const detail = detailText();
  assert.match(detail, /PREVIEW_ONLY - NOT APPROVED/);
  assert.match(detail, /PREVIEW-SESSION-0000/);
  assert.match(detail, /not a person record/);
});

test('a Preview remark is recorded and a Preview result can be cleared', () => {
  id('remark-input').value = 'preview remark only';
  click(id('btn-record-remark'));
  assert.match(detailText(), /preview remark only/);
  click(id('btn-clear-result'));
  assert.match(text('alert-line'), /cleared/);
  assert.match(detailText(), /none recorded/);
});

test('next and previous measurement navigation moves the selection without changing any verdict', () => {
  filter('all');
  click(tableRows()[0]);
  const firstKey = tableRows()[0].attributes['data-channel-key'];
  click(id('btn-next'));
  const selected = tableRows().find((row) => row.className.includes('selected'));
  assert.notEqual(selected.attributes['data-channel-key'], firstKey);
  click(id('btn-prev'));
  const back = tableRows().find((row) => row.className.includes('selected'));
  assert.equal(back.attributes['data-channel-key'], firstKey);
});

test('resetting the Preview clears workflow results but leaves evidence BLOCKED intact', () => {
  click(id('scenario-reset'));
  assert.match(text('alert-line'), /cleared/);
  assert.match(allText(id('summary-body')), /BLOCKED 40/);
  assert.match(allText(id('summary-body')), /PASS 0/);
  assert.match(allText(id('summary-body')), /NOT_TESTED 200/);
});

test('the exit action opens a close guard that explains what closing does', () => {
  assert.equal(id('exit-guard').hidden, true);
  click(id('exit-button'));
  assert.equal(id('exit-guard').hidden, false);
  assert.match(html, /Exit Preview\?/);
  assert.match(html, /closes only the Preview shell window/);
  assert.match(html, /does not stop, start or command any equipment/);
  assert.match(html, /not Production evidence/);
  assert.match(html, /id="exit-cancel"/);
  click(id('exit-cancel'));
  assert.equal(id('exit-guard').hidden, true);
  assert.equal(closed.called, false, 'cancelling the guard closes nothing');
});

test('the confirmed exit path asks the shell to close and states that the shell confirms again', () => {
  click(id('exit-button'));
  click(id('exit-confirm'));
  assert.equal(closed.called, true);
  assert.equal(id('exit-guard').hidden, true);
  assert.match(text('alert-line'), /shell asks for its own confirmation/);
  closed.called = false;
});

test('the Mapping and Diagnostics areas state the evidence position and the missing authority', () => {
  const mapping = allText(id('mapping-body'));
  assert.match(mapping, /BASELINE_REV1/);
  assert.match(mapping, /NOT field-loop-test verified/);
  assert.match(mapping, /never navigates to another origin/);
  const diagnostics = allText(id('diagnostics-body'));
  assert.match(diagnostics, /no device is contacted/);
  assert.match(diagnostics, /BLOCKED in the baseline/);
  assert.match(diagnostics, /FIELD VERIFICATION PENDING/);
});

test('the Operations area states that no control or command path exists', () => {
  const operations = allText(id('operations-body'));
  assert.match(operations, /read-only/);
  assert.match(operations, /no Pump, Valve, motion or output control/);
  assert.match(operations, /no Production command path/);
  assert.match(operations, /Hardware connection none/);
  assert.match(operations, /Device write authority none/);
});

test('the Sensor Map area shows 108 positions, 106 Sensors and the gap positions as never-Sensors', () => {
  const sensorMap = allText(id('sensor-map-body'));
  assert.match(sensorMap, /Logical positions 108/);
  assert.match(sensorMap, /Product Sensors 106/);
  assert.match(sensorMap, /Thermocouple channels 212/);
  assert.match(sensorMap, /NON_SENSOR_GAP positions 2/);
  assert.match(sensorMap, /never Sensors and never Water Jets/);
});

test('the app requested only loopback GET routes and issued no write of any kind', () => {
  assert.equal(app.requests.length > 5, true);
  for (const request of app.requests) {
    assert.equal(request.method, 'GET', request.path);
    assert.match(request.path, /^\/(api|pkg)/, request.path);
  }
});

test('density is presentation only and changes neither the rows nor the counts', () => {
  const before = allText(id('summary-body'));
  click(id('density-compact'));
  assert.equal(id('density-compact').attributes['aria-pressed'], 'true');
  assert.equal(tableRows().length, 240);
  assert.equal(allText(id('summary-body')), before);
  click(id('density-comfortable'));
  assert.equal(id('density-comfortable').attributes['aria-pressed'], 'true');
});

test('the Preview tick is deterministic: the same tick renders the same sample', async () => {
  const atTick = (tick) => () => allCells().some((c) => c[16].endsWith(`00:00:0${tick}.000Z`));
  await applyScenario('NORMAL_GOOD', { tick: 5, until: atTick(5) });
  const first = allCells().map((c) => c[10]).join('|');
  await applyScenario('NORMAL_GOOD', { tick: 5, until: atTick(5) });
  assert.equal(allCells().map((c) => c[10]).join('|'), first);
  assert.match(allCells()[0][16], /2026-01-01T00:00:05\.000Z/);
  // The sample VALUE is a pure function of the physical identity (so it is stable across ticks),
  // while the observation timestamp and the value age move with the deterministic preview clock.
  await applyScenario('NORMAL_GOOD', { tick: 6, until: atTick(6) });
  assert.match(allCells()[0][16], /2026-01-01T00:00:06\.000Z/);
  assert.equal(allCells().map((c) => c[10]).join('|'), first);
  assert.match(allCells()[0][15], /HISTORICAL - LAST VALID \(not the current value\)/);
});
