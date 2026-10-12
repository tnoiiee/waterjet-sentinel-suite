// Stage 0.4C-K1 (re-verified at 0.4C-K2) Front/Rear thermal-pair pass - presentation and real-page contracts for the
// READ-ONLY paired value (FrontTemperature - RearTemperature).
//
// These tests run the REAL public/app.mjs through the REAL Preview server against a stub DOM. They
// prove the application logic and the rendered text/structure. They do NOT lay anything out and they
// do not run a browser: no pixel position, no computed overflow and no visual result is claimed here.
import { test } from 'node:test';
import assert from 'node:assert/strict';

import { FIELD_VERIFICATION_TEXT, FRONT_REAR_DELTA_LABEL, PAIRED_THERMAL_STATE } from '../../../packages/commissioning/src/index.mjs';
import { allText, bootApp, byAction, byFilter, cellTexts, click, html, rowsOf, waitFor, walk } from './helpers/fakeDom.mjs';

/** Boots the real application, runs the body, and always closes the ephemeral server. */
async function withApp(body) {
  const app = await bootApp({});
  try {
    return await body(app);
  } finally {
    app.close();
  }
}

const id = (app, name) => app.byId[name];
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

const cells = (app) => nodesWith(id(app, 'wallmap'), 'data-slot-id');
const sensorCells = (app) => cells(app).filter((cell) => cell.attributes['data-sensor-id'] !== undefined);
const nonSensorCells = (app) => cells(app).filter((cell) => cell.attributes['data-position-kind'] !== 'SENSOR');
const cellFor = (app, sensorId) => sensorCells(app).find((cell) => cell.attributes['data-sensor-id'] === sensorId);
const blockOf = (cell, side) => nodesWith(cell, 'data-side').find((block) => block.attributes['data-side'] === side);
const deltaRowOf = (cell) => nodesByClass(cell, 'wj-dfr')[0] ?? null;
const deltaValueOf = (cell) => allText(nodesByClass(deltaRowOf(cell) ?? { childNodes: [] }, 'wj-dfr-val')[0] ?? { childNodes: [] });
const deltaUnitOf = (cell) => nodesByClass(deltaRowOf(cell) ?? { childNodes: [] }, 'wj-dfr-unit')[0] ?? null;
const deltaSymbolOf = (cell) => allText(nodesByClass(deltaRowOf(cell) ?? { childNodes: [] }, 'wj-dfr-sym')[0] ?? { childNodes: [] });
const tableRows = (app) => rowsOf(app.byId, 'monitor-table-body');
const detailPair = (app) => id(app, 'detail-pair');
const detailPairText = (app) => allText(detailPair(app));
const workflowAction = (app, name) => byAction(id(app, 'workflow-actions'), name);

/** The complete rendered Preview text: map, legend, notes, detail, summary and Overview rows. */
function renderedText(app) {
  const parts = ['wallmap', 'wallmap-legend', 'wallmap-selection-note', 'wallmap-filter-note',
    'detail-pair', 'detail-identity', 'detail-observation', 'detail-workflow', 'scenario-summary',
    'loop-progress-body', 'monitor-table-body'].map((name) => allText(id(app, name)));
  walk(id(app, 'wallmap'), (node) => {
    if (node.attributes?.title) parts.push(node.attributes.title);
    if (node.attributes?.['aria-label']) parts.push(node.attributes['aria-label']);
  });
  return parts.join(' ');
}

async function applyScenario(app, key, { tick = 0 } = {}) {
  click(byFilter(id(app, 'filters'), 'all'));
  id(app, 'scenario-select').value = key;
  id(app, 'tick-input').value = String(tick);
  click(id(app, 'scenario-apply'));
  await waitFor(() => id(app, 'scenario-summary').textContent.startsWith(key), `scenario ${key} to be applied`);
  await waitFor(() => cells(app).length === 108, 'the Wall Map to be complete after applying the scenario');
}

test('E1 every wall position renders exactly one read-only paired row, and only a valid pair carries a number', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    assert.equal(nodesByClass(id(app, 'wallmap'), 'wj-dfr').length, 108,
      'every logical position must carry exactly one paired row');
    for (const cell of cells(app)) {
      assert.notEqual(deltaRowOf(cell), null, `${cell.attributes['data-logical-label']} must carry the paired row`);
      assert.match(deltaRowOf(cell).attributes['data-delta-state'],
        new RegExp(`^(${Object.values(PAIRED_THERMAL_STATE).join('|')})$`),
        'the paired state of a position must be one of the five documented states');
      assert.match(deltaRowOf(cell).attributes['data-delta-available'], /^(true|false)$/);
    }
    // NORMAL_GOOD: all 108 complete pairs are available (I7 and I16 included); no non-Sensor cell exists.
    assert.equal(sensorCells(app).filter((c) => c.attributes['data-delta-available'] === 'true').length, 108);
    assert.equal(nonSensorCells(app).length, 0);
    for (const sensorId of ['I7', 'I16']) {
      const cell = sensorCells(app).find((c) => c.attributes['data-sensor-id'] === sensorId);
      assert.ok(cell, `${sensorId} is a Sensor cell`);
      assert.equal(cell.attributes['data-delta-available'], 'true');
      assert.notEqual(deltaValueOf(cell), '--');
      assert.equal(allText(deltaUnitOf(cell)), 'degC');
    }
  });
});

test('E2 the paired value is not a Channel: no Overview row, no measurement identity and no selection target', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    assert.equal(tableRows(app).length, 240, 'the Overview stays at the 240 physical Channels');
    for (const row of tableRows(app)) {
      assert.equal(cellTexts(row).some((text) => text.startsWith(FRONT_REAR_DELTA_LABEL)), false,
        'no Overview row may represent a paired derived value as a Channel or a measurement');
    }
    // The whole monitor is rebuilt on every render, so every node is re-queried after a click.
    const cell = () => cellFor(app, 'G+205');
    click(blockOf(cell(), 'F'));
    await waitFor(() => blockOf(cell(), 'F').attributes['data-selected'] === 'true', 'the Front measurement selected');
    const before = allText(detailPair(app));
    const title = deltaRowOf(cell()).attributes.title;
    click(deltaRowOf(cell()));
    await waitFor(() => true, 'the click to settle');
    assert.equal(allText(detailPair(app)), before, 'the paired row is not a selection target and changes nothing');
    assert.equal(blockOf(cell(), 'F').attributes['data-selected'], 'true', 'the selection must not move');
    assert.match(title, /read-only/i);
    assert.match(title, /no (?:Alarm|alarm)/);
  });
});

test('E3 the Wall Map cell and the Sensor detail show the same paired value for the same Sensor', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    for (const sensorId of ['G+201', 'G+205', 'G+218', 'H9', 'I8', 'J18']) {
      const cell = () => cellFor(app, sensorId);
      click(blockOf(cell(), 'F'));
      await waitFor(() => blockOf(cell(), 'F').attributes['data-selected'] === 'true', `${sensorId} Front selected`);
      const text = detailPairText(app);
      assert.match(text, new RegExp(`Sensor pair\\s+${sensorId.replace('+', '\\+')}`), `${sensorId}: the detail must name the pair`);
      assert.ok(text.includes(deltaValueOf(cell())), `${sensorId}: the detail and the cell must show the same value`);
      assert.equal(detailPair(app).attributes['data-pair-state'], deltaRowOf(cell()).attributes['data-delta-state']);
      assert.match(text, /Front current value/);
      assert.match(text, /Rear current value/);
      assert.match(text, /dFR = Front - Rear/);
      assert.match(text, /Paired diagnostic state/);
      assert.match(text, /Guidance/);
      assert.match(text, /Synthetic scenario/);
      assert.match(text, /SYNTHETIC_PREVIEW/);
      assert.ok(text.includes(FIELD_VERIFICATION_TEXT), 'the detail must carry the field-verification state');
      assert.match(text, /READ-ONLY DIAGNOSTIC \/ TREND ONLY/);
      assert.match(text, /field approval/);
    }
    // The unit is shown only while a paired value exists, and it is the unit of both sides.
    const cell = cellFor(app, 'G+205');
    assert.equal(allText(deltaUnitOf(cell)), 'degC');
  });
});

test('E4 the sign is always visible, and each paired scenario renders its own declared state and marker', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    for (const cell of sensorCells(app)) {
      assert.match(deltaValueOf(cell), /^\+\d/, 'NORMAL_GOOD: every Front-above-Rear value carries an explicit + sign');
      assert.equal(deltaSymbolOf(cell), '[+]');
    }
    const expectations = [
      ['REDUCED_FRONT_REAR_DIFFERENTIAL', 'REDUCED_POSITIVE', PAIRED_THERMAL_STATE.REDUCED_POSITIVE_CANDIDATE, '[v]', /^\+/, /possible slag insulation/i],
      ['NEAR_ZERO_FRONT_REAR_DIFFERENTIAL', 'NEAR_ZERO', PAIRED_THERMAL_STATE.NEAR_ZERO_CANDIDATE, '[~]', /^\+/, /abnormal paired/i],
      ['REVERSED_FRONT_REAR_DIFFERENTIAL', 'REVERSED', PAIRED_THERMAL_STATE.REVERSED_CONTRADICTION, '[!]', /^-/, /never classified as slag by itself/i],
    ];
    for (const [key, mode, state, marker, sign, wording] of expectations) {
      await applyScenario(app, key);
      for (const cell of sensorCells(app)) {
        assert.equal(cell.attributes['data-delta-state'], state, `${key}: ${cell.attributes['data-sensor-id']}`);
        assert.equal(deltaSymbolOf(cell), marker);
        assert.match(deltaValueOf(cell), sign, `${key}: the sign must always be visible`);
      }
      click(blockOf(cellFor(app, 'G+205'), 'F'));
      await waitFor(() => detailPair(app).attributes['data-pair-state'] === state, `${key}: the detail to follow`);
      assert.match(detailPairText(app), wording);
      assert.match(id(app, 'scenario-summary').textContent, new RegExp(key));
      assert.equal(id(app, 'scenario-summary').attributes['data-paired-mode'], mode,
        `${key}: the served scenario must declare its paired mode`);
    }
  });
});

test('F1 a Front verdict never changes the Rear observation, the Rear verdict or the paired value', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    const cell = () => cellFor(app, 'G+205');
    const stateOf = (side) => blockOf(cell(), side).attributes['data-commissioning'];
    const delta = () => deltaValueOf(cell());

    const before = delta();
    click(blockOf(cell(), 'F'));
    await waitFor(() => blockOf(cell(), 'F').attributes['data-selected'] === 'true', 'the Front measurement selected');
    click(workflowAction(app, 'begin-BASELINE'));
    await waitFor(() => stateOf('F') === 'IN_PROGRESS', 'the Front observation to start');
    click(workflowAction(app, 'verdict-PASS'));
    await waitFor(() => stateOf('F') === 'PASS', 'the Front PASS');
    assert.equal(stateOf('R'), 'NOT_TESTED', 'the Rear measurement keeps its own state');
    assert.equal(delta(), before, 'the paired value must not move with a Preview verdict');

    // Clearing the Front result changes neither the Rear state nor the paired value.
    click(id(app, 'btn-clear-result'));
    await waitFor(() => stateOf('F') === 'NOT_TESTED', 'the Front result cleared');
    assert.equal(stateOf('R'), 'NOT_TESTED');
    assert.equal(delta(), before);
  });
});

test('F2 the paired value gives the workflow no extra target and never blocks an existing one', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    // The action set is a function of the selected measurement only.
    click(blockOf(cellFor(app, 'G+205'), 'F'));
    await waitFor(() => blockOf(cellFor(app, 'G+205'), 'F').attributes['data-selected'] === 'true', 'selected');
    const actions = nodesWith(id(app, 'workflow-actions'), 'data-action').map((node) => node.attributes['data-action']);
    assert.deepEqual([...actions].sort(), ['begin-BASELINE', 'begin-OPEN_CIRCUIT', 'begin-RECOVERY', 'begin-STIMULUS',
      'verdict-FAIL', 'verdict-PARTIAL', 'verdict-PASS'].sort());
    assert.equal(actions.some((a) => a.toLowerCase().includes('delta') || a.toLowerCase().includes('paire')), false,
      'no workflow action may target the paired value');
    // An evidence-blocked measurement still presents its paired value: the block is about PASS, not about the pair.
    const blockedId = sensorCells(app).filter((c) => blockOf(c, 'F')?.attributes['data-blocked'] === 'true'
      || blockOf(c, 'R')?.attributes['data-blocked'] === 'true').map((c) => c.attributes['data-sensor-id']);
    assert.ok(blockedId.length > 0, 'the fixture must contain an evidence-blocked measurement');
    const blocked = () => cellFor(app, blockedId[0]);
    const side = blockOf(blocked(), 'F')?.attributes['data-blocked'] === 'true' ? 'F' : 'R';
    assert.equal(blocked().attributes['data-delta-available'], 'true');
    click(blockOf(blocked(), side));
    await waitFor(() => blockOf(blocked(), side).attributes['data-selected'] === 'true', 'the blocked measurement selected');
    assert.match(detailPairText(app), /dFR = Front - Rear\s+\+\d/);
    assert.equal(workflowAction(app, 'verdict-PASS').disabled, true, 'the PASS verdict stays blocked by the evidence');
  });
});

test('G1 the served module graph carries the paired model and the wall map model', async () => {
  await withApp(async (app) => {
    for (const name of ['thermalpair.mjs', 'wallmap.mjs']) {
      const response = await fetch(`/pkg/${name}`);
      assert.equal(response.status, 200, `/pkg/${name} must be served`);
      const source = await response.text();
      assert.ok(source.length > 500, `/pkg/${name} must be a real module`);
    }
    const paired = await (await fetch('/pkg/thermalpair.mjs')).text();
    assert.match(paired, /buildThermalPairs/);
    assert.match(paired, /PAIRED_THERMAL_EFFECT/);
    // The served page must be the real page: the module that renders the paired row is executed by it.
    assert.match(html, /id="detail-pair"/);
    assert.match(html, /id="wallmap"/);
  });
});

test('G2 applying every scenario leaves the page initialized, with the failure banner hidden', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    const keys = id(app, 'scenario-select').childNodes.map((option) => option.attributes.value);
    assert.equal(keys.length, 13, 'the selector offers the ten protected scenarios plus the three paired demonstrations');
    for (const key of keys) {
      await applyScenario(app, key);
      assert.equal(app.byId['app-root'].attributes['data-app-state'], 'ready', `${key}: the page must stay ready`);
      assert.equal(id(app, 'boot-error').hidden, true, `${key}: no initialization failure may be raised`);
      assert.equal(cells(app).length, 108, `${key}: the Wall Map must stay complete`);
      assert.equal(tableRows(app).length, 240, `${key}: the Overview must stay at 240 rows`);
    }
  });
});

test('G3 the paired wording stays candidate-bounded in every scenario', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    const forbidden = [
      /slag (?:is )?confirmed/i, /confirmed slag/i, /automatic slag detection (?:is )?complete/i,
      /clean wall (?:is )?confirmed/i, /confirmed clean wall/i, /dispatch (?:the )?water ?jet/i,
      /cleaning (?:is )?required/i, /sensor (?:has )?failed/i, /pair (?:has )?failed/i,
      /alarm (?:is )?active/i, /alarm raised/i,
    ];
    for (const key of ['NORMAL_GOOD', 'REDUCED_FRONT_REAR_DIFFERENTIAL', 'NEAR_ZERO_FRONT_REAR_DIFFERENTIAL',
      'REVERSED_FRONT_REAR_DIFFERENTIAL', 'STALE', 'SIMULATED_WIRE_BREAK', 'RECOVERY']) {
      await applyScenario(app, key);
      click(blockOf(cellFor(app, 'G+205'), 'F'));
      await waitFor(() => blockOf(cellFor(app, 'G+205'), 'F').attributes['data-selected'] === 'true', `${key}: selected`);
      const text = renderedText(app);
      for (const pattern of forbidden) {
        assert.equal(pattern.test(text), false, `${key}: the rendered Preview must never claim '${pattern}'`);
      }
      assert.match(text, /(?:read-only|READ-ONLY)/i, `${key}: the paired value must stay visibly read-only`);
    }
  });
});

test('G4 an invalid side removes the paired value, and a filter never changes a value or a count', async () => {
  await withApp(async (app) => {
    await waitFor(() => cells(app).length === 108, 'the Wall Map to render');
    const good = sensorCells(app).map((cell) => deltaValueOf(cell));
    const evidence = allText(id(app, 'summary-body'));
    click(byFilter(id(app, 'filters'), 'quality-bad'));
    await waitFor(() => sensorCells(app).some((cell) => cell.attributes['data-filtered'] === 'false'),
      'the filter to dim a non-matching position');
    assert.deepEqual(sensorCells(app).map((cell) => deltaValueOf(cell)), good,
      'a filter is presentation only: it may not change a paired value');
    assert.equal(cells(app).length, 108, 'the map shape is kept');
    assert.equal(sensorCells(app).length, 108);
    assert.equal(allText(id(app, 'summary-body')), evidence, 'the evidence counts are unchanged by a filter');
    click(byFilter(id(app, 'filters'), 'all'));
    await waitFor(() => sensorCells(app).every((cell) => cell.attributes['data-filtered'] === 'true'), 'the filter cleared');
    assert.equal(tableRows(app).length, 240, 'the Overview is complete again');

    // A scenario that invalidates one side of the pair must show an unavailable paired value with a
    // named reason - never a fabricated number and never a Last Valid Value.
    for (const key of ['STALE', 'SIMULATED_WIRE_BREAK']) {
      await applyScenario(app, key);
      const unavailable = sensorCells(app).filter((cell) => cell.attributes['data-delta-available'] === 'false');
      assert.ok(unavailable.length > 0, `${key}: at least one pair must lose its delta`);
      for (const cell of unavailable) {
        assert.equal(deltaValueOf(cell), '--');
        assert.equal(deltaUnitOf(cell), null);
        assert.match(deltaRowOf(cell).attributes.title, /(?:CURRENT_VALUE_UNAVAILABLE|QUALITY_NOT_VALID|STALE|COMMUNICATION_LOSS|WIRE_BREAK)/);
        assert.match(deltaRowOf(cell).attributes.title, /never|no value is substituted/i);
      }
      const stillAvailable = sensorCells(app).filter((cell) => cell.attributes['data-delta-available'] === 'true');
      assert.ok(stillAvailable.length > 0, `${key}: a valid pair must keep its delta`);
    }
  });
});
