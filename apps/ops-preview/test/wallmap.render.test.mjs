// Stage 0.4C-K1 (re-verified at 0.4C-K2) - Commissioning Wall Map rendering, selection synchronisation, Front/Rear
// separation, Preview workflow isolation, filters and Wall-order navigation.
//
// These tests run the REAL public/app.mjs through the REAL Preview server against a stub DOM. They
// prove the application logic and the rendered text/structure. They do NOT lay anything out and they
// do not run a browser: no pixel position, no computed overflow and no visual result is claimed here.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { WALL_MAP_ORDER_TEXT, WALL_MAP_EXPECTED } from '../../../packages/commissioning/src/index.mjs';
import { allText, bootApp, byAction, byFilter, cellTexts, click, columnIndex, html, rowsOf, waitFor, walk } from './helpers/fakeDom.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const css = readFileSync(join(HERE, '..', 'public', 'styles.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');

/** Boots the real application, runs the body, and always closes the ephemeral server. */
async function withApp(body) {
  const app = await bootApp({});
  try {
    return await body(app);
  } finally {
    app.close();
  }
}

const nodesWith = (root, attribute) => {
  const found = [];
  walk(root, (node) => {
    if (node.attributes && node.attributes[attribute] !== undefined) found.push(node);
  });
  return found;
};

/** Nodes carrying a CSS class (the stub DOM keeps classes in `className`, not in an attribute). */
const nodesByClass = (root, cls) => {
  const found = [];
  walk(root, (node) => {
    if (String(node.className ?? '').split(/\s+/).includes(cls)) found.push(node);
  });
  return found;
};
// `textContent` of a stub container stays empty: text lives in child text nodes, so read the subtree.
const valueText = (block) => allText(nodesByClass(block, 'wj-val')[0] ?? { childNodes: [] });
const hasUnit = (block) => nodesByClass(block, 'wj-unit').length > 0;

const id = (app, name) => app.byId[name];
const cells = (app) => nodesWith(id(app, 'wallmap'), 'data-slot-id');
const sensorCells = (app) => cells(app).filter((cell) => cell.attributes['data-sensor-id'] !== undefined);
const nonSensorCells = (app) => cells(app).filter((cell) => cell.attributes['data-position-kind'] !== 'SENSOR');
const blocksOf = (cell) => nodesWith(cell, 'data-side');
const blockOf = (cell, side) => blocksOf(cell).find((block) => block.attributes['data-side'] === side);
const cellFor = (app, sensorId) => sensorCells(app).find((cell) => cell.attributes['data-sensor-id'] === sensorId);
const bands = (app) => nodesWith(id(app, 'wallmap'), 'data-wall-columns');
const focus = (app) => id(app, 'wallmap-frame').attributes['data-wallmap-focus'];
const selectionState = (app) => id(app, 'wallmap-frame').attributes['data-wallmap-selection'];
const selectedRow = (app) => rowsOf(app.byId, 'monitor-table-body').find((row) => String(row.className ?? '').includes('selected')) ?? null;
const tableRows = (app) => rowsOf(app.byId, 'monitor-table-body');
const rowForMeasurement = (app, measurement) => tableRows(app).find((row) => cellTexts(row)[2] === measurement);
const detailText = (app) => [allText(id(app, 'detail-identity')), allText(id(app, 'detail-observation')), allText(id(app, 'detail-workflow'))].join(' ');
const workflowAction = (app, name) => byAction(id(app, 'workflow-actions'), name);

async function applyScenario(app, key, { node = null, tick = 0 } = {}) {
  click(byFilter(id(app, 'filters'), 'all'));
  id(app, 'scenario-select').value = key;
  if (node) id(app, 'node-select').value = node;
  id(app, 'tick-input').value = String(tick);
  click(id(app, 'scenario-apply'));
  await waitFor(() => id(app, 'scenario-summary').textContent.startsWith(key), `scenario ${key} to be applied`);
}

test('the served page carries the Wall Map, its view control, its Wall-order navigation and the Loop-Test summary', () => {
  for (const required of ['wallmap-frame', 'wallmap', 'wallmap-selection-note', 'wallmap-filter-note', 'wallmap-legend', 'loop-progress-body']) {
    assert.ok(html.includes(`id="${required}"`), `the served page must carry #${required}`);
  }
  for (const control of ['view-both', 'view-front', 'view-rear', 'wall-prev', 'wall-next']) {
    assert.ok(html.includes(`id="${control}"`), `the served page must carry #${control}`);
  }
  assert.match(html, /Measurements/);
  assert.match(html, /Both \(Front and Rear\)/);
  assert.match(html, /Front only/);
  assert.match(html, /Rear only/);
  // The Wall-order definition on screen is the module's own text: one definition, no drift.
  assert.ok(html.replace(/\s+/g, ' ').includes(WALL_MAP_ORDER_TEXT.replace(/\s+/g, ' ')), 'the served Wall-order note must state the documented order');
  // The two orders stay distinguishable: the physical schedule navigation is labelled as such.
  assert.match(html, /Physical schedule order/);
  assert.match(html, /Previous \(schedule order\)/);
  // The map is inside its own bounded frame, and the frame declares its read-only state.
  assert.match(html, /<div class="wallmap-frame" id="wallmap-frame" data-wallmap-selection="NONE" data-measurement-view="BOTH">/);
  assert.match(html, /SIMULATION \/ PREVIEW · NO HARDWARE ACCESS · NO DEVICE WRITE ·\s+FIELD VERIFICATION PENDING/);
});

test('the Wall Map renders 108 logical positions, all of them Product Sensor cells (I7 and I16 included)', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length > 0, 'the Wall Map to render');
    assert.equal(cells(app).length, 108, 'exactly 108 logical positions');
    assert.equal(sensorCells(app).length, 108, 'exactly 108 Product Sensor cells');
    assert.equal(nonSensorCells(app).length, 0, 'no non-Sensor cell exists');
    const sensorIds = sensorCells(app).map((c) => c.attributes['data-sensor-id']);
    assert.equal(new Set(sensorIds).size, 108, 'every Product Sensor appears exactly once');
    assert.equal(sensorIds.filter((id) => id === 'I7').length, 1);
    assert.equal(sensorIds.filter((id) => id === 'I16').length, 1);
    for (const sensorId of ['I7', 'I16']) {
      const cell = cellFor(app, sensorId);
      assert.equal(cell.attributes['data-position-kind'], 'SENSOR');
      assert.equal(cell.attributes['data-logical-label'], sensorId);
      assert.doesNotMatch(allText(cell), /NON_SENSOR_GAP/);
      assert.equal(blocksOf(cell).length, 2, `${sensorId} renders Front and Rear`);
      for (const block of blocksOf(cell)) assert.equal(block.tagName, 'button', `${sensorId} blocks are selectable`);
    }
    assert.doesNotMatch(allText(id(app, 'wallmap')), /NON_SENSOR_GAP/);
    // The protected totals the served matrix must reproduce.
    assert.equal(WALL_MAP_EXPECTED.productSensors, 108);
    assert.equal(WALL_MAP_EXPECTED.productCompatibleMeasurements, 216);
  });
});

test('the four wall bands keep the approved shape and the approved wall order', async () => {
  await withApp(async (app) => {
    await waitFor(() => bands(app).length === 4, 'four wall bands');
    assert.deepEqual(bands(app).map((b) => b.attributes['data-wall']), ['REAR', 'LEFT', 'RIGHT', 'FRONT']);
    assert.deepEqual(bands(app).map((b) => b.attributes['data-wall-columns']), ['5', '4', '4', '5']);
    assert.deepEqual(bands(app).map((b) => b.attributes['data-wall-rows']), ['6', '6', '6', '6']);
    assert.deepEqual(bands(app).map((b) => b.attributes['data-sensor-count']), ['30', '24', '24', '30']);
    assert.equal(bands(app).every((b) => b.attributes['data-gap-count'] === undefined), true);
    // The approved first identities of each wall, in wall-column order (no rotation, no reversal).
    const firstRow = (wall) => nodesWith(id(app, 'wallmap'), 'data-wall-row').filter((cell) => cell.attributes['data-wall'] === wall && cell.attributes['data-wall-row'] === '1').map((cell) => cell.attributes['data-logical-label']);
    assert.deepEqual(firstRow('LEFT'), ['G+201', 'G+202', 'G+203', 'G+204']);
    assert.deepEqual(firstRow('REAR'), ['G+205', 'G+206', 'G+207', 'G+208', 'G+209']);
    assert.deepEqual(firstRow('RIGHT'), ['G+210', 'G+211', 'G+212', 'G+213']);
    assert.deepEqual(firstRow('FRONT'), ['G+214', 'G+215', 'G+216', 'G+217', 'G+218']);
    // The hollow centre states the protected counts and the Stage boundary.
    const hollow = allText(id(app, 'wallmap'));
    assert.match(hollow, /108 logical positions/);
    assert.match(hollow, /108 Product Sensors/);
    assert.doesNotMatch(hollow, /NON_SENSOR_GAP/);
    assert.match(hollow, /216 measurements/);
    assert.match(hollow, /SIMULATION \/ PREVIEW/);
  });
});

test('every Sensor cell shows its identity, both measurements, a quality marker and a commissioning code', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    for (const cell of sensorCells(app)) {
      const sensorId = cell.attributes['data-sensor-id'];
      assert.match(allText(cell), new RegExp(sensorId.replace('+', '\\+')), `${sensorId} must be visible on its cell`);
      const classMarker = nodesByClass(cell, 'wj-class')[0];
      assert.ok(classMarker, `${sensorId} must render its evidence class marker`);
      assert.match(allText(classMarker), /\[(PC|IC)\]/, `${sensorId} must show [PC] or [IC] as text`);
      const front = blockOf(cell, 'F');
      const rear = blockOf(cell, 'R');
      assert.ok(front, `${sensorId} must render its Front measurement`);
      assert.ok(rear, `${sensorId} must render its Rear measurement`);
      assert.equal(front.attributes['data-measurement-key'], `${sensorId}:F`);
      assert.equal(rear.attributes['data-measurement-key'], `${sensorId}:R`);
      // Quality is never colour alone: every block carries its text marker and its Quality value.
      for (const block of [front, rear]) {
        const marker = nodesByClass(block, 'wj-q')[0];
        assert.ok(marker && allText(marker).length > 0, 'a quality marker must be rendered as text');
        assert.match(String(block.attributes['data-quality']), /^(GOOD|UNCERTAIN|BAD|STALE|DISABLED)$/);
        assert.match(String(block.attributes['data-commissioning']), /^(NOT_TESTED|IN_PROGRESS|PASS|FAIL|PARTIAL|BLOCKED)$/);
        assert.match(String(block.attributes.title), /synthetic Preview observation/);
        assert.match(String(block.attributes.title), /FIELD VERIFICATION PENDING/);
      }
    }
  });
});

test('a valid current value shows with its unit, and an unavailable value is never a number and never carries a unit', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    // NORMAL_GOOD: valid values with the unit.
    const healthy = blocksOf(cellFor(app, 'G+205'));
    for (const block of healthy) {
      assert.equal(block.attributes['data-invalid'], 'false');
      assert.match(String(block.attributes['data-quality']), /^(GOOD|UNCERTAIN)$/);
      assert.match(valueText(block), /^[0-9]+\.[0-9]$/);
      assert.equal(allText(nodesByClass(block, 'wj-unit')[0]), 'degC');
    }
    // OUT_OF_RANGE (Quality BAD): the current value is unavailable - no digits and no unit anywhere.
    await applyScenario(app, 'OUT_OF_RANGE');
    const bad = blocksOf(cells(app).find((cell) => blocksOf(cell).some((b) => b.attributes['data-quality'] === 'BAD' && b.attributes['data-side'] === 'F')));
    for (const block of bad) {
      if (block.attributes['data-quality'] !== 'BAD') continue;
      assert.equal(block.attributes['data-invalid'], 'true');
      const value = valueText(block);
      assert.equal(/[0-9]/.test(value), false, 'a BAD measurement must never render a number as its current value');
      assert.equal(hasUnit(block), false, 'an unavailable value must not carry a unit');
      assert.match(String(block.attributes.title), /no current value is presented/);
    }
  });
});

test('STALE is presented as history: it is never the current value', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    await applyScenario(app, 'STALE');
    const staleCell = cells(app).find((cell) => blocksOf(cell).some((b) => b.attributes['data-quality'] === 'STALE'));
    assert.ok(staleCell, 'the STALE scenario must mark at least one position');
    const staleBlock = blocksOf(staleCell).find((b) => b.attributes['data-quality'] === 'STALE');
    assert.equal(staleBlock.attributes['data-invalid'], 'true');
    assert.equal(valueText(staleBlock), '--');
    assert.match(String(staleBlock.attributes.title), /no current value is presented/);
    click(blockOf(staleCell, staleBlock.attributes['data-side']));
    await waitFor(() => selectionState(app) === 'POSITION', 'the stale measurement to be selected');
    // Sensor detail states the same thing in words: the current value is unavailable and the stale
    // sample is not it.
    const observation = allText(id(app, 'detail-observation'));
    assert.match(observation, /IsCurrentValueValid false/);
    assert.match(observation, /Engineering value UNAVAILABLE/);
    assert.match(observation, /Value age 45 s \(exceeds Preview threshold\)/);
    // The Overview keeps the last valid sample in a column explicitly labelled as history.
    const headers = [...html.matchAll(/<th scope="col">([^<]+)<\/th>/g)].map((m) => m[1]);
    const staleRow = selectedRow(app);
    const staleCells = cellTexts(staleRow);
    assert.match(staleCells[columnIndex(headers, 'Last valid (historical)')], /HISTORICAL - LAST VALID/);
    assert.equal(/[0-9]/.test(staleCells[columnIndex(headers, 'Engineering value')]), false, 'the current-value column must never show a number for a stale measurement');
  });
});

test('the I7 cell is an ordinary Sensor cell: selectable Front and Rear blocks, not blocked, never labelled a gap', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    const cell = cellFor(app, 'I7');
    assert.doesNotMatch(allText(cell), /NON_SENSOR_GAP/);
    assert.equal(String(cell.title ?? ''), '');
    for (const block of blocksOf(cell)) {
      assert.equal(block.tagName, 'button');
      assert.equal(block.attributes['data-inert-block'], undefined);
      assert.equal(block.attributes['data-blocked'], 'false');
      assert.match(String(block.attributes['data-measurement-key']), /^I7:[FR]$/);
    }
    assert.equal(selectionState(app), 'NONE');
    click(blocksOf(cell)[0]);
    await waitFor(() => selectionState(app) === 'POSITION', 'the I7 selection');
    assert.equal(focus(app), 'SENSOR:I7');
    assert.equal(blockOf(cellFor(app, 'I7'), 'F').attributes['data-selected'], 'true');
    assert.equal(blockOf(cellFor(app, 'I7'), 'R').attributes['data-selected'], 'false');
    assert.ok(selectedRow(app), 'the Overview row of I7 Front is selected too');
    assert.match(allText(selectedRow(app)), /\bI7\b/);
  });
});

test('selecting a Wall Map block selects exactly that measurement and fills Sensor detail', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    // Every render replaces the cells, so the cell is always re-queried: a held node would only ever
    // describe the previous render.
    const cell = () => cellFor(app, 'H7');
    const frontKey = blockOf(cell(), 'F').attributes['data-channel-key'];
    const rearKey = blockOf(cell(), 'R').attributes['data-channel-key'];
    click(blockOf(cell(), 'F'));
    await waitFor(() => selectionState(app) === 'POSITION' && selectedRow(app), 'the Front measurement to be selected');
    assert.equal(focus(app), 'SENSOR:H7');
    assert.equal(selectedRow(app).attributes['data-channel-key'], frontKey);
    assert.match(allText(id(app, 'detail-identity')), /H7/);
    assert.match(allText(id(app, 'detail-identity')), /Product measurement identity H7:F/);
    assert.match(allText(id(app, 'detail-identity')), /Drawing side F/);
    assert.equal(blockOf(cell(), 'F').attributes['data-selected'], 'true');
    assert.equal(blockOf(cell(), 'R').attributes['data-selected'], 'false');

    click(blockOf(cell(), 'R'));
    await waitFor(() => selectedRow(app).attributes['data-channel-key'] === rearKey, 'the Rear measurement to be selected');
    assert.equal(focus(app), 'SENSOR:H7');
    assert.equal(blockOf(cell(), 'R').attributes['data-selected'], 'true');
    assert.equal(blockOf(cell(), 'F').attributes['data-selected'], 'false');
    assert.match(allText(id(app, 'detail-identity')), /Product measurement identity H7:R/);
    assert.match(allText(id(app, 'detail-identity')), /Drawing side R/);
  });
});

test('an Overview selection highlights the same Wall Map cell and the same measurement', async () => {
  await withApp(async (app) => {
    await waitFor(() => tableRows(app).length === 240, 'the Overview to render');
    const cleanRow = rowForMeasurement(app, 'H7:F');
    click(cleanRow);
    await waitFor(() => selectionState(app) === 'POSITION', 'the Overview selection to reach the Wall Map');
    assert.equal(focus(app), 'SENSOR:H7');
    const cell = cellFor(app, 'H7');
    assert.equal(cell.attributes['data-selected'], 'true');
    assert.equal(blockOf(cell, 'F').attributes['data-selected'], 'true');
    assert.equal(blockOf(cell, 'R').attributes['data-selected'], 'false');
    // An internal-conflict row highlights its own Sensor and carries the blocking reason.
    const conflictRow = rowForMeasurement(app, 'H7:R');
    assert.ok(conflictRow, 'the internal-conflict row must exist');
    click(conflictRow);
    await waitFor(() => blockOf(cellFor(app, 'H7'), 'R').attributes['data-selected'] === 'true', 'the conflict measurement to be selected');
    const conflicted = blockOf(cellFor(app, 'H7'), 'R');
    assert.equal(conflicted.attributes['data-blocked'], 'true');
    assert.match(String(conflicted.attributes.title), /DRAWING_INTERNAL_CONFLICT_BLOCKED/);
    assert.match(detailText(app), /DRAWING_INTERNAL_CONFLICT_BLOCKED/);
    assert.equal(workflowAction(app, 'verdict-PASS').disabled, true, 'an internal conflict can never record a PASS');
  });
});

test('an unassigned physical Channel marks the Wall Map selection as NOT_APPLICABLE', async () => {
  await withApp(async (app) => {
    await waitFor(() => tableRows(app).length === 240, 'the Overview to render');
    click(byFilter(id(app, 'filters'), 'unassigned'));
    await waitFor(() => selectedRow(app) === null && tableRows(app).length === 24, 'the unassigned rows to be shown');
    click(tableRows(app)[0]);
    await waitFor(() => selectionState(app) === 'NOT_APPLICABLE', 'the not-applicable state');
    assert.equal(focus(app), 'NONE');
    assert.equal(sensorCells(app).some((cell) => cell.attributes['data-selected'] === 'true'), false, 'no Sensor may be selected for an unassigned Channel');
    assert.match(id(app, 'wallmap-selection-note').textContent, /unassigned physical Channel/);
    // Selecting it changed nothing about the evidence: the counts are unchanged.
    assert.match(allText(id(app, 'summary-body')), /Product-compatible measurements 216/);
  });
});

test('an I16 Overview row selects the I16 Sensor cell exactly like any other Sensor', async () => {
  await withApp(async (app) => {
    await waitFor(() => tableRows(app).length === 240, 'the Overview to render');
    const row = rowForMeasurement(app, 'I16:R');
    assert.ok(row, 'the I16 Rear row must exist as a Product-compatible row');
    click(row);
    await waitFor(() => selectionState(app) === 'POSITION', 'the I16 selection');
    assert.equal(focus(app), 'SENSOR:I16');
    assert.equal(cellFor(app, 'I16').attributes['data-selected'], 'true');
    assert.equal(blockOf(cellFor(app, 'I16'), 'R').attributes['data-selected'], 'true');
    assert.equal(blockOf(cellFor(app, 'I16'), 'F').attributes['data-selected'], 'false', 'Front and Rear stay separate');
    assert.match(id(app, 'wallmap-selection-note').textContent, /Selected: I16 Rear/);
    assert.doesNotMatch(id(app, 'wallmap-selection-note').textContent, /NON_SENSOR_GAP/);
  });
});

test('the Front/Rear view control changes what is shown without merging or copying the measurements', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    const frontValue = () => valueText(blockOf(cellFor(app, 'G+205'), 'F'));
    const both = frontValue();
    assert.equal(blocksOf(cellFor(app, 'G+205')).length, 2);

    click(id(app, 'view-front'));
    await waitFor(() => id(app, 'wallmap-frame').attributes['data-measurement-view'] === 'FRONT', 'Front only');
    assert.equal(id(app, 'view-front').attributes['aria-pressed'], 'true');
    assert.equal(id(app, 'view-both').attributes['aria-pressed'], 'false');
    assert.equal(cells(app).every((cell) => blocksOf(cell).length === 1), true, 'Front only renders one block per cell');
    assert.equal(nodesWith(id(app, 'wallmap'), 'data-side').every((b) => b.attributes['data-side'] === 'F'), true, 'no Rear block may be rendered in Front-only view');
    assert.equal(frontValue(), both, 'the Front value is not changed, copied or recomputed by the view control');

    click(id(app, 'view-rear'));
    await waitFor(() => id(app, 'wallmap-frame').attributes['data-measurement-view'] === 'REAR', 'Rear only');
    assert.equal(nodesWith(id(app, 'wallmap'), 'data-side').every((b) => b.attributes['data-side'] === 'R'), true, 'no Front block may be rendered in Rear-only view');
    assert.match(id(app, 'alert-line').textContent, /Presentation only/);

    click(id(app, 'view-both'));
    await waitFor(() => id(app, 'wallmap-frame').attributes['data-measurement-view'] === 'BOTH', 'Both');
    assert.equal(blocksOf(cellFor(app, 'G+205')).length, 2);
  });
});

test('the Preview workflow applies to the selected measurement only: Front never alters Rear', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    const cell = () => cellFor(app, 'G+205');
    const stateOf = (side) => blockOf(cell(), side).attributes['data-commissioning'];

    click(blockOf(cell(), 'F'));
    await waitFor(() => focus(app) === 'SENSOR:G+205' && blockOf(cell(), 'F').attributes['data-selected'] === 'true', 'the Front measurement selected');
    click(workflowAction(app, 'begin-BASELINE'));
    await waitFor(() => stateOf('F') === 'IN_PROGRESS', 'the Front observation to start');
    assert.equal(stateOf('R'), 'NOT_TESTED', 'starting a Front observation must not touch the Rear measurement');
    click(workflowAction(app, 'verdict-PASS'));
    await waitFor(() => stateOf('F') === 'PASS', 'the Front PASS');
    assert.equal(stateOf('R'), 'NOT_TESTED');

    click(blockOf(cell(), 'R'));
    await waitFor(() => blockOf(cell(), 'R').attributes['data-selected'] === 'true', 'the Rear measurement selected');
    click(workflowAction(app, 'verdict-FAIL'));
    await waitFor(() => stateOf('R') === 'FAIL', 'the Rear FAIL');
    assert.equal(stateOf('F'), 'PASS', 'recording a Rear verdict must never change the Front measurement');

    // Clearing the Preview result affects the selected measurement only.
    click(id(app, 'btn-clear-result'));
    await waitFor(() => stateOf('R') === 'NOT_TESTED', 'the Rear result cleared');
    assert.equal(stateOf('F'), 'PASS', 'clearing the Rear result must not clear the Front result');
    // And the Loop-Test progress follows the Preview session, not a cached total.
    assert.match(allText(id(app, 'loop-progress-body')), /PASS 1/);
    assert.match(allText(id(app, 'loop-progress-body')), /Product Sensors 108/);
    assert.match(allText(id(app, 'loop-progress-body')), /Product-compatible measurements 216/);
  });
});

test('filters dim positions as presentation only: no evidence, no verdict and no selection changes', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    await applyScenario(app, 'OUT_OF_RANGE');
    click(blockOf(cellFor(app, 'G+205'), 'F'));
    await waitFor(() => selectionState(app) === 'POSITION', 'a selection to hold');
    const summaryBefore = allText(id(app, 'summary-body'));
    const detailBefore = detailText(app);

    click(byFilter(id(app, 'filters'), 'quality-bad'));
    await waitFor(() => id(app, 'wallmap-filter-note').textContent.includes('presentation only'), 'the filter note');
    const dimmed = cells(app).filter((cell) => cell.attributes['data-filtered'] === 'false');
    assert.ok(dimmed.length > 0, 'non-matching positions must be dimmed');
    assert.ok(dimmed.length < cells(app).length, 'matching positions must stay visible');
    assert.equal(cells(app).length, 108, 'dimming never removes a position from the map');
    assert.match(id(app, 'wallmap-filter-note').textContent, new RegExp(`of 108 positions do not match`));
    // Nothing but the presentation changed.
    assert.equal(allText(id(app, 'summary-body')), summaryBefore);
    assert.equal(detailText(app), detailBefore);
    assert.equal(focus(app), 'SENSOR:G+205');
    assert.equal(blockOf(cellFor(app, 'G+205'), 'F').attributes['data-selected'], 'true');

    click(byFilter(id(app, 'filters'), 'all'));
    await waitFor(() => id(app, 'wallmap-filter-note').textContent.includes('no filter is active'), 'the unfiltered note');
    assert.equal(cells(app).every((cell) => cell.attributes['data-filtered'] === 'true'), true);
    assert.equal(allText(id(app, 'summary-body')), summaryBefore);
  });
});

test('Wall-order Previous/Next walks the approved sequence over all 108 Sensors, Front before Rear, I7 and I16 included', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    assert.equal(selectionState(app), 'NONE');
    click(id(app, 'wall-next'));
    await waitFor(() => focus(app) === 'SENSOR:G+201', 'the first position');
    assert.equal(blockOf(cellFor(app, 'G+201'), 'F').attributes['data-selected'], 'true', 'the Front measurement comes first');
    click(id(app, 'wall-next'));
    await waitFor(() => blockOf(cellFor(app, 'G+201'), 'R').attributes['data-selected'] === 'true', 'the Rear measurement of the same Sensor');
    click(id(app, 'wall-next'));
    await waitFor(() => focus(app) === 'SENSOR:G+202', 'the next Product Sensor');
    assert.equal(blockOf(cellFor(app, 'G+202'), 'F').attributes['data-selected'], 'true');
    click(id(app, 'wall-prev'));
    await waitFor(() => blockOf(cellFor(app, 'G+201'), 'R').attributes['data-selected'] === 'true', 'back to the previous measurement');

    // I7 sits in its normal ordinal position: I6:R -> I7:F -> I7:R -> I8:F.
    click(rowForMeasurement(app, 'I6:R'));
    await waitFor(() => focus(app) === 'SENSOR:I6' && blockOf(cellFor(app, 'I6'), 'R').attributes['data-selected'] === 'true', 'I6 Rear selected');
    click(id(app, 'wall-next'));
    await waitFor(() => focus(app) === 'SENSOR:I7', 'I7 Front next');
    assert.equal(blockOf(cellFor(app, 'I7'), 'F').attributes['data-selected'], 'true');
    click(id(app, 'wall-next'));
    await waitFor(() => blockOf(cellFor(app, 'I7'), 'R').attributes['data-selected'] === 'true', 'I7 Rear next');
    click(id(app, 'wall-next'));
    await waitFor(() => focus(app) === 'SENSOR:I8', 'then I8 Front');
    assert.equal(blockOf(cellFor(app, 'I8'), 'F').attributes['data-selected'], 'true');
    assert.equal(cellFor(app, 'I7').attributes['data-selected'], 'false');
    // And I16 likewise: I15:R -> I16:F.
    click(rowForMeasurement(app, 'I15:R'));
    await waitFor(() => focus(app) === 'SENSOR:I15', 'I15 Rear selected');
    click(id(app, 'wall-next'));
    await waitFor(() => focus(app) === 'SENSOR:I16', 'I16 Front next');
    assert.equal(blockOf(cellFor(app, 'I16'), 'F').attributes['data-selected'], 'true');

    // Both ends are deterministic and reported, and the selection stays put.
    click(rowForMeasurement(app, 'J18:R'));
    await waitFor(() => focus(app) === 'SENSOR:J18' && blockOf(cellFor(app, 'J18'), 'R').attributes['data-selected'] === 'true', 'the last position');
    click(id(app, 'wall-next'));
    await waitFor(() => id(app, 'alert-line').textContent.includes('End of the Wall order reached'), 'the end notice');
    assert.equal(blockOf(cellFor(app, 'J18'), 'R').attributes['data-selected'], 'true');
    click(id(app, 'wall-prev'));
    await waitFor(() => blockOf(cellFor(app, 'J18'), 'F').attributes['data-selected'] === 'true', 'the Front measurement of the last Sensor');
  });
});

test('the Wall Map follows the node-scoped scenario, keeps the boundary and reports no failed request', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    await applyScenario(app, 'COMMUNICATION_LOSS_NODE', { node: 'TC-NODE-05' });
    await waitFor(() => cells(app).some((cell) => blocksOf(cell).some((b) => b.attributes['data-quality'] === 'BAD')), 'the affected node to render BAD');
    const affected = cells(app).filter((cell) => blocksOf(cell).some((b) => b.attributes['data-quality'] === 'BAD'));
    assert.ok(affected.length >= 18, 'the affected node must mark its Sensors on the Wall Map');
    // Every request the application made was a successful local GET, and the page is ready.
    assert.equal(app.requests.every((r) => r.status === 200 && r.method === 'GET'), true, JSON.stringify(app.requests.filter((r) => r.status !== 200)));
    assert.equal(app.requests.every((r) => r.path.startsWith('/api/')), true);
    assert.equal(id(app, 'app-root').attributes['data-app-state'], 'ready');
    assert.equal(id(app, 'boot-error').hidden, true, 'no initialization failure may be visible');
    assert.match(allText(id(app, 'boundary-chips')), /NO HARDWARE ACCESS/);
    // The map frame keeps the boundary beside the map, and the legend states the synthetic provenance.
    assert.match(allText(id(app, 'wallmap-legend')), /synthetic Preview observation/);
    assert.match(allText(id(app, 'wallmap-legend')), /FIELD VERIFICATION PENDING/);
    // Measurement Quality DISABLED is measurement availability, never the Sensor operational state.
    const legendText = allText(id(app, 'wallmap-legend'));
    assert.match(legendText, /Measurement Quality GOOD, UNCERTAIN, BAD, STALE, DISABLED/);
    assert.match(legendText, /DISABLED = measurement unavailable, channel not commissioned - not the Sensor operational Enabled\/Disabled state/);
    assert.doesNotMatch(legendText, /no measurement exists|NON_SENSOR_GAP/);
  });
});

test('the Wall Map presentation is bounded: fixed cells in a horizontally bounded frame, never a page-width element', () => {
  // Owner Manual Review correction: the frame is bounded HORIZONTALLY only. The vertical axis is
  // static (no height cap, no vertical scrolling) so the U-shape stays one continuous spatial
  // reference; the document is the single vertical scrolling surface.
  assert.match(css, /\.wallmap-frame\s*\{[^}]*overflow-x\s*:\s*auto/);
  assert.match(css, /\.wallmap-frame\s*\{[^}]*overflow-y\s*:\s*hidden/);
  assert.equal(/\.wallmap-frame\s*\{[^}]*max-height\s*:/.test(css), false,
    'the Wall Map frame must not cap its height: the document scrolls vertically, not the frame');
  assert.equal(/\.wallmap-frame\s*\{[^}]*overflow\s*:\s*(auto|scroll)/.test(css), false,
    'the Wall Map frame must not use the overflow shorthand: it would scroll both axes');
  assert.match(css, /\.wallmap-frame\s*\{[^}]*max-width\s*:\s*100%/);
  assert.match(css, /\.wallmap-frame\s*\{[^}]*min-width\s*:\s*0/);
  assert.match(css, /\.wallmap\s*\{[^}]*width\s*:\s*max-content/);
  assert.match(css, /--wj-cell-w\s*:\s*\d+px/);
  assert.match(css, /--wj-cell-h\s*:\s*\d+px/);
  assert.equal(/100vw/.test(css), false, 'no viewport-relative width may be used');
  // Nothing about the map uses an inline style: the content security policy allows no inline style.
  assert.equal(/style="/.test(html), false, 'the served page must contain no inline style attribute');
});
