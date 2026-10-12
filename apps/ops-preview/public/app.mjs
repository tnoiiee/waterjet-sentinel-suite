// Stage 0.4C-K2 - the Kiosk Preview Operations route with the Sensor Commissioning Monitor and the
// read-only TC Node Configuration section (the route originated in Stage 0.4C-K1).
//
// Every value is written with textContent. Nothing here connects, polls, reads or writes a device,
// opens a socket itself, or accepts an address. The Preview workflow state lives in this browser
// session only: it is never sent anywhere, never persisted, and it approves nothing.
//
// Filters, density and selection are presentation only. They choose which rows are SHOWN and how
// they look; they never touch the Mapping revision, the Runtime revision or the evidence.

import {
  DENSITY, DEFAULT_DENSITY, FIELD_VERIFICATION_TEXT, FILTERS, FRONT_REAR_DELTA_LABEL, MEASUREMENT_VIEW,
  OBSERVATION_KIND, PAIRED_THERMAL_EFFECT, PAIRED_THERMAL_GUIDANCE, PAIRED_THERMAL_MARKER,
  PAIRED_THERMAL_STATE, PreviewCommissioningSession, ROW_CLASS_PRESENTATION, SIDE_LABELS, WALL_LABELS,
  buildRowViews, buildThermalPairs, buildWallMap, canRecordPass, filterCounts, filterRows, getFilter,
  legend, loopTestSummary, pairForRow, summaryCounts, visibleMeasurements, wallMeasurementKey,
  wallOrderStep,
} from '/pkg/index.mjs';

const $ = (id) => document.getElementById(id);

// Boot state. `data-app-state` on the body is the single machine-readable signal that the Preview
// initialized: 'booting' while the first data request is in flight, 'ready' when the monitor is
// live, 'failed' when initialization did not complete. It exists because an inert page used to look
// the same as an initialized one.
const APP_STATE = Object.freeze({
  BOOTING: 'booting',
  READY: 'ready',
  FAILED: 'failed',
});

/** Safe, non-sensitive failure categories. No stack trace, path or internal detail reaches the UI. */
const BOOT_FAILURE = Object.freeze({
  DATA_REQUEST: 'the local commissioning data could not be requested',
  MODULE_LOAD: 'the application module did not load',
  RUNTIME: 'an unexpected error stopped the application',
});

function setAppState(value) {
  document.body.setAttribute('data-app-state', value);
}

/** Shows the visible initialization-failure surface with a safe category, never a raw error. */
function showBootFailure(category, detail = '') {
  setAppState(APP_STATE.FAILED);
  const box = $('boot-error');
  const text = $('boot-error-detail');
  if (text) {
    text.textContent = detail
      ? `${category}. ${detail} The Kiosk shell keeps its own reconnect state while the local host is unavailable.`
      : `${category}. The Kiosk shell keeps its own reconnect state while the local host is unavailable.`;
  }
  if (box) box.hidden = false;

  // The local console keeps the actionable text; the Operator screen does not show it.
  if (typeof console !== 'undefined') console.error('ops-preview boot failure:', category, detail);
}

function el(tag, attrs = {}, children = []) {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (v === null || v === undefined || v === false) continue;
    if (k === 'class') node.className = v;
    else if (k === 'disabled') node.disabled = Boolean(v);
    else if (k.startsWith('data-')) node.setAttribute(k, String(v));
    else if (k === 'text') node.textContent = String(v);
    else node.setAttribute(k, v === true ? '' : String(v));
  }
  for (const c of [].concat(children)) {
    if (c === null || c === undefined || c === false) continue;
    node.append(c instanceof Node ? c : document.createTextNode(String(c)));
  }
  return node;
}

function chip(text, cls = '') { return el('span', { class: `chip ${cls}` }, text); }

function clear(id) { $(id).replaceChildren(); }

function definitionList(pairs) {
  return el('dl', {}, pairs.flatMap(([k, v]) => [el('dt', {}, k), el('dd', {}, String(v))]));
}

const state = {
  boundary: null,
  schedule: null,
  counts: null,
  scenarios: [],
  sensorMap: null,
  opsSummary: null,
  // The read-only TC Node configuration projection from the server. It is never edited in the browser.
  tcNodeConfiguration: null,
  // The read-only Stage 0.4C-K3 evidence projection. Evidence and contract only; never edited here.
  tcAcquisitionEvidence: null,
  observations: new Map(),
  // The read-only paired thermal model, rebuilt from the authoritative observations on every load. It
  // is derived state: it is never written back into an observation, a row, a count or a verdict.
  pairs: null,
  session: new PreviewCommissioningSession({ sessionIndex: 0 }),
  scenarioKey: 'NORMAL_GOOD',
  nodeId: 'TC-NODE-01',
  tick: 0,
  filter: 'all',
  density: DEFAULT_DENSITY,
  selectedKey: null,
  alert: '',
  // Wall Map: the topology view model built once from the served matrix plus the schedule rows, and
  // the Front/Rear presentation control. The selection is NOT duplicated here - the Wall Map and the
  // Overview share the single `selectedKey`, so they can never disagree about what is selected.
  wallMap: null,
  wallMapError: '',
  measurementView: MEASUREMENT_VIEW.BOTH,
};

/**
 * Wall Map presentation constants. The codes are abbreviations of the Product commissioning states,
 * used only because a Sensor cell is small: the Sensor detail and the Overview always show the full
 * words, and the Wall Map legend states every code. They are never shown as a field approval: the
 * Stage boundary (SIMULATION / PREVIEW, FIELD VERIFICATION PENDING) sits above and beside the map.
 */
const commissioningCodes = (entries) => Object.freeze(Object.fromEntries(entries));
const COMMISSIONING_CODE = commissioningCodes([
  ['NOT_TESTED', 'NT'],
  ['IN_PROGRESS', 'IP'],
  ['PASS', 'PASS'],
  ['FAIL', 'FAIL'],
  ['PARTIAL', 'PART'],
  ['BLOCKED', 'BLOCK'],
]);

/** The compact quality marker: the ONE package marker, with the brackets the Overview uses removed. */
const compactMarker = (marker) => String(marker ?? '').replace(/[[\]]/g, '');

/** Short label for the commissioning state, with the full word in the tooltip and the legend. */
const commissioningCode = (state) => COMMISSIONING_CODE[state] ?? String(state ?? '-');

const sideOf = (view) => SIDE_LABELS[view?.drawingSide] ?? (view?.drawingSide ?? '-');

const unavailableText = () => (state.counts ? '' : 'preview data unavailable');

function setAlert(message) {
  state.alert = message;
  $('alert-line').textContent = message;
}

function statusOf(view) { return `cm-${String(view.commissioningState).toLowerCase()}`; }

// ---------------------------------------------------------------------------
// Boundary, Operations, Sensor Map, Mapping and Diagnostics
// ---------------------------------------------------------------------------

function renderBoundary() {
  const boundary = state.boundary;
  clear('boundary-chips');
  if (!boundary) {
    $('boundary-chips').append(chip('BOUNDARY UNAVAILABLE', 'err'));
    return;
  }
  const chips = [
    chip(`${boundary.mode}`, 'info'),
    chip(`Stage ${boundary.stageId}`),
    chip('NO HARDWARE ACCESS', 'warn'),
    chip('NO DEVICE WRITE', 'warn'),
    chip('FIELD VERIFICATION PENDING', 'warn'),
    chip(`Evidence ${boundary.evidenceBaseline}`),
    chip(`${boundary.drawingStatus}`),
  ];
  $('boundary-chips').append(...chips);
}

function renderOperations() {
  const body = $('operations-body');
  const boundary = state.boundary;
  body.replaceChildren();
  if (!boundary || !state.counts) {
    body.append(el('p', { class: 'note' }, unavailableText()));
    return;
  }
  body.append(el('div', { class: 'summary' }, [
    el('div', { class: 'cell info' }, [el('span', { class: 'k' }, 'Stage'), el('span', { class: 'v' }, boundary.stageId)]),
    el('div', { class: 'cell info' }, [el('span', { class: 'k' }, 'Mode'), el('span', { class: 'v' }, boundary.mode)]),
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'Physical Channels'), el('span', { class: 'v' }, String(state.counts.physicalChannels))]),
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'Product Sensors'), el('span', { class: 'v' }, String(state.counts.productSensors))]),
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'Product-compatible measurements'), el('span', { class: 'v' }, String(state.counts.productCompatibleMeasurements))]),
    el('div', { class: 'cell err' }, [el('span', { class: 'k' }, 'Hardware connection'), el('span', { class: 'v' }, 'none')]),
    el('div', { class: 'cell err' }, [el('span', { class: 'k' }, 'Device write authority'), el('span', { class: 'v' }, 'none')]),
  ]));
  body.append(el('p', { class: 'note' },
    'This Preview route is read-only. It contains no Pump, Valve, motion or output control of any kind, no Activation control, '
    + 'and no Production command path. The Mapping Configuration route and the Runtime Inspector remain separate local '
    + 'surfaces; this Preview does not replace them, and it is not reachable from any other origin.'));
}

function renderNodeSummary(explicitSummary) {
  // The summary follows the scenario currently under review, so a node that is down in the selected
  // scenario can never be shown as online by a stale boot-time fetch.
  const summary = explicitSummary ?? state.nodeSummary ?? state.opsSummary?.nodeSummary ?? null;
  const body = $('node-summary-body');
  body.replaceChildren();
  if (!summary) {
    body.append(el('p', { class: 'note' }, 'Node communication summary unavailable.'));
    return;
  }
  const table = el('table', {}, [
    el('thead', {}, [el('tr', {}, ['Node', 'Cabinet', 'Physical Channels', 'Channels in communication loss', 'Node state']
      .map((h) => el('th', { scope: 'col' }, h)))]),
    el('tbody', {}, summary.map((n) => el('tr', { class: n.state === 'ONLINE' ? 'pc' : 'tc' }, [
      el('td', {}, n.nodeId),
      el('td', {}, n.cabinet),
      el('td', { class: 'num' }, String(n.physicalChannels)),
      el('td', { class: 'num' }, String(n.channelsInCommunicationLoss)),
      el('td', { class: n.state === 'ONLINE' ? 'q-good' : 'q-bad' }, n.state),
    ]))),
  ]);
  body.append(table);
}

function renderProgress() {
  const body = $('progress-body');
  body.replaceChildren();
  const progress = state.opsSummary?.commissioningProgress;
  if (!progress) {
    body.append(el('p', { class: 'note' }, 'Commissioning progress unavailable.'));
    return;
  }
  body.append(el('div', { class: 'summary' }, [
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'Rows total'), el('span', { class: 'v' }, String(progress.total))]),
    el('div', { class: 'cell ok' }, [el('span', { class: 'k' }, 'Eligible for PASS'), el('span', { class: 'v' }, String(progress.eligibleForPass))]),
    el('div', { class: 'cell err' }, [el('span', { class: 'k' }, 'Blocked from PASS'), el('span', { class: 'v' }, String(progress.blockedFromPass))]),
    el('div', { class: 'cell warn' }, [el('span', { class: 'k' }, 'BLOCKED'), el('span', { class: 'v' }, String(progress.byState.BLOCKED))]),
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'NOT_TESTED'), el('span', { class: 'v' }, String(progress.byState.NOT_TESTED))]),
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'IN_PROGRESS'), el('span', { class: 'v' }, String(progress.byState.IN_PROGRESS))]),
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'PASS'), el('span', { class: 'v' }, String(progress.byState.PASS))]),
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'FAIL'), el('span', { class: 'v' }, String(progress.byState.FAIL))]),
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'PARTIAL'), el('span', { class: 'v' }, String(progress.byState.PARTIAL))]),
  ]));
}

/** Loop-Test progress over the population that can actually be loop tested (the 212 measurements). */
function renderLoopProgress() {
  const body = $('loop-progress-body');
  body.replaceChildren();
  if (!state.schedule) {
    body.append(el('p', { class: 'note' }, 'Loop-Test progress unavailable.'));
    return;
  }
  const progress = loopTestSummary(state.schedule, state.session);
  const cells = [
    ['Product Sensors', progress.productSensors, ''],
    ['Product-compatible measurements', progress.productCompatibleMeasurements, ''],
    ['NOT_TESTED', progress.byState.NOT_TESTED, ''],
    ['IN_PROGRESS', progress.byState.IN_PROGRESS, 'info'],
    ['PASS', progress.byState.PASS, 'ok'],
    ['FAIL', progress.byState.FAIL, 'err'],
    ['PARTIAL', progress.byState.PARTIAL, 'warn'],
    ['BLOCKED', progress.byState.BLOCKED, 'warn'],
    ['Eligible for PASS', progress.eligibleForPass, ''],
    ['Not loop-testable', progress.notLoopTestable, ''],
  ];
  body.append(...cells.map(([k, v, cls]) => el('div', { class: `cell ${cls}` }, [
    el('span', { class: 'k' }, k), el('span', { class: 'v' }, String(v)),
  ])));
  body.append(el('p', { class: 'note' }, progress.note));
}

// ---------------------------------------------------------------------------
// Commissioning Wall Map (read-only U-shaped plan view of the logical matrix)
// ---------------------------------------------------------------------------

/** The active filter rule, taken from the ONE package filter set the Overview uses. */
function viewMatchesFilter(view) {
  if (!view) return false;
  return getFilter(state.filter).match(view);
}

/** True when any measurement block the cell renders matches the active filter. */
function positionMatchesFilter(position, viewByKey) {
  return visibleMeasurements(position, state.measurementView)
    .some((block) => viewMatchesFilter(viewByKey.get(block.key)));
}

/**
 * The read-only paired row of a cell: the label, the signed value, the unit only while the value is
 * available, and a text marker. It is DERIVED - it has no Channel, no Sensor identity of its own, no
 * commissioning state and no Preview workflow, and it can never be selected as a measurement.
 */
function deltaRow(pair) {
  const state_ = pair?.state ?? PAIRED_THERMAL_STATE.DIFFERENTIAL_UNAVAILABLE;
  const attrs = {
    class: 'wj-dfr',
    'data-delta-state': String(state_),
    'data-delta-available': String(Boolean(pair?.deltaAvailable)),
  };
  const children = [el('span', { class: 'wj-dfr-label' }, FRONT_REAR_DELTA_LABEL)];
  if (pair?.deltaAvailable) {
    children.push(el('span', { class: 'wj-dfr-val' }, pair.deltaText));
    if (pair.unit) children.push(el('span', { class: 'wj-dfr-unit' }, String(pair.unit)));
  } else {
    // No current paired value means NO number and NO unit: the token says so and the reason travels
    // with it (in the Sensor detail and in the title), never as an invented value.
    children.push(el('span', { class: 'wj-dfr-val' }, '--'));
  }
  children.push(el('span', { class: 'wj-dfr-sym' }, pair?.marker ?? PAIRED_THERMAL_MARKER.DIFFERENTIAL_UNAVAILABLE));
  const derived = `${FRONT_REAR_DELTA_LABEL} ${pair?.deltaText ?? ''} ${pair?.unit ?? ''}`.trim();
  const sides = `Front ${pair?.frontValuePresented ?? '-'} ${pair?.frontUnit ?? ''} minus Rear ${pair?.rearValuePresented ?? '-'} ${pair?.rearUnit ?? ''}`.trim();
  attrs.title = pair?.deltaAvailable
    ? `${derived} - ${sides}; paired state ${pair.state}. ${pair.guidance} ${pair.effect}`
    : `${FRONT_REAR_DELTA_LABEL} unavailable (${pair?.reason ?? 'PAIR_IDENTITY_INCOMPLETE'}): no paired value is calculated and no value is substituted. `
      + `${PAIRED_THERMAL_GUIDANCE.DIFFERENTIAL_UNAVAILABLE} ${pair?.effect ?? PAIRED_THERMAL_EFFECT}`;
  return el('div', attrs, children);
}

/** The paired descriptor of a wall position, resolved from the served views (never re-derived). */
function pairForPosition(position, viewByKey) {
  const view = viewByKey.get(position.frontKey) ?? viewByKey.get(position.rearKey) ?? null;
  return pairForRow(state.pairs, view?.row ?? null);
}

/**
 * One measurement block of a cell: side, current value when valid, unit when a current value exists,
 * the quality marker and the commissioning state. A Sensor cell block is a button (it selects that
 * exact measurement); a NON_SENSOR_GAP block is inert - a gap is never selectable as a Sensor.
 */
function measurementBlock(block, view, position, selected) {
  const interactive = position.positionKind === 'SENSOR' && Boolean(view);
  const attrs = {
    class: `wj-m wj-${block.side.toLowerCase()}${interactive ? '' : ' wj-inert'}`,
    'data-side': block.side,
    'data-side-label': block.sideLabel,
    'data-measurement-key': block.key,
    'data-channel-key': block.channelKey,
    'data-position-kind': position.positionKind,
  };
  const children = [el('span', { class: 'wj-side' }, block.side)];
  if (!view) {
    // Defensive: a served observation is expected for every position. Saying so is honest; showing
    // a zero, a blank or the other side's value would not be.
    attrs['data-quality'] = 'NOT_SERVED';
    attrs['data-invalid'] = 'true';
    children.push(el('span', { class: 'wj-val' }, '--'), el('span', { class: 'wj-q' }, '?'), el('span', { class: 'wj-state' }, commissioningCode('NOT_TESTED')));
    attrs.title = `${position.label} ${block.sideLabel}: no observation was served for this measurement.`;
    return el('div', attrs, children);
  }
  const valid = view.currentValueValid === true;
  const value = valid ? String(view.engineeringValue) : '--';
  attrs['data-quality'] = String(view.quality);
  attrs['data-invalid'] = String(!valid);
  attrs['data-commissioning'] = String(view.commissioningState);
  attrs['data-blocked'] = String(Boolean(view.blockingReason));
  attrs['data-filtered'] = String(viewMatchesFilter(view));
  attrs['data-selected'] = String(selected);
  if (selected) attrs['aria-current'] = 'true';
  children.push(el('span', { class: 'wj-val' }, value));
  // A unit exists only while a current value exists: an unavailable value never carries one.
  if (valid && view.engineeringUnit) children.push(el('span', { class: 'wj-unit' }, String(view.engineeringUnit)));
  children.push(el('span', { class: 'wj-q' }, compactMarker(view.qualityMarker)));
  children.push(el('span', { class: 'wj-state' }, commissioningCode(view.commissioningState)));
  attrs.title = `${position.label} ${block.sideLabel} measurement (${view.productMeasurementIdentity ?? '-'})`
    + ` - Quality ${view.quality} ${view.qualityMarker}; `
    + (valid ? `${value} ${view.engineeringUnit ?? ''}`.trim() : 'no current value is presented, so no number and no unit are shown')
    + `; Commissioning ${view.commissioningState}`
    + (view.blockingReason ? `; blocked from PASS: ${view.blockingReason}` : '')
    + `; synthetic Preview observation, ${FIELD_VERIFICATION_TEXT} - not a hardware reading and not a field approval.`;
  if (!interactive) {
    // Drawing rows of a NON_SENSOR_GAP position: present as evidence, never selectable as a Sensor.
    attrs['data-gap-block'] = 'true';
    return el('div', attrs, children);
  }
  const button = el('button', { ...attrs, type: 'button' }, children);
  // The SELECTION identity is the physical Channel key: the same identity the Overview, Sensor detail
  // and the Preview workflow use, so the four surfaces can never disagree about what is selected.
  button.addEventListener('click', () => selectMeasurement(block.channelKey));
  return button;
}

/** One wall position cell: identity, evidence class marker and the rendered measurement blocks. */
function wallCell(position, viewByKey) {
  const blocks = visibleMeasurements(position, state.measurementView);
  const isGap = position.positionKind !== 'SENSOR';
  const selectedPosition = state.selectedKey ? state.wallMap.positionByChannelKey.get(state.selectedKey) ?? null : null;
  const selected = selectedPosition === position;
  const visibleViews = blocks.map((block) => viewByKey.get(block.key)).filter(Boolean);
  const classMarkers = [...new Set(visibleViews.map((view) => view.rowClassMarker).filter(Boolean))];
  const attrs = {
    class: `wj-cell${isGap ? ' wj-gap' : ''}${selected ? ' wj-selected' : ''}`,
    'data-wall': position.wall,
    'data-slot-id': position.slotId,
    'data-position-kind': position.positionKind,
    'data-logical-label': position.label,
    'data-wall-row': position.wallRow,
    'data-wall-column': position.wallColumn,
    'data-selected': String(selected),
    'data-filtered': String(positionMatchesFilter(position, viewByKey)),
  };
  // A NON_SENSOR_GAP cell never carries a Sensor identity; that is the whole point of the position.
  if (!isGap) attrs['data-sensor-id'] = position.sensorId;
  const head = el('div', { class: 'wj-head' }, [
    el('span', { class: 'wj-id' }, position.label),
    el('span', { class: 'wj-class' }, isGap ? 'NON_SENSOR_GAP' : (classMarkers.join(' ') || '[PC]')),
  ]);
  // The paired state travels on the cell as well, so the row's styling and the tests can read it
  // without re-deriving anything.
  const pair = pairForPosition(position, viewByKey);
  attrs['data-delta-state'] = String(pair.state);
  attrs['data-delta-available'] = String(Boolean(pair.deltaAvailable));
  const children = [head];
  for (const block of blocks) {
    children.push(measurementBlock(block, viewByKey.get(block.key) ?? null, position, selected && state.selectedKey === block.channelKey));
  }
  children.push(deltaRow(pair));
  const cell = el('div', attrs, children);
  if (isGap) {
    cell.title = `${position.label} - NON_SENSOR_GAP position: never a Sensor and never a Water Jet.`
      + ' Its two drawing rows are a Product-topology conflict, so no PASS can ever be recorded for them.';
  }
  return cell;
}

function wallBand(entry, viewByKey) {
  const cells = entry.rows.flat().map((position) => wallCell(position, viewByKey));
  return el('section', {
    class: `wj-band wj-band-${entry.wall.toLowerCase()}`,
    'data-wall': entry.wall,
    'data-wall-columns': entry.wallColumns,
    'data-wall-rows': entry.rows.length,
    'data-sensor-count': entry.sensorCount,
    'data-gap-count': entry.gapCount,
    'aria-label': `${WALL_LABELS[entry.wall]}: ${entry.sensorCount} Product Sensors and ${entry.gapCount} NON_SENSOR_GAP position(s)`,
  }, [
    el('div', { class: 'wj-band-head' }, [
      el('span', { class: 'wj-band-title' }, WALL_LABELS[entry.wall]),
      el('span', { class: 'wj-band-counts' }, `${entry.sensorCount} Sensors${entry.gapCount ? ` | ${entry.gapCount} NON_SENSOR_GAP` : ''}`),
    ]),
    el('div', { class: 'wj-cells' }, cells),
  ]);
}

/** The hollow centre of the U: the protected counts and the Stage boundary, never a control. */
function wallHollow(map) {
  return el('div', { class: 'wallmap-hollow' }, [
    el('div', { class: 'wj-hollow-line' }, `${map.counts.logicalPositions} logical positions`),
    el('div', { class: 'wj-hollow-line' }, `${map.counts.productSensors} Product Sensors`),
    el('div', { class: 'wj-hollow-line' }, `${map.counts.nonSensorGaps} NON_SENSOR_GAP (${map.gapPositions.map((p) => p.label).join(', ')})`),
    el('div', { class: 'wj-hollow-line' }, `${map.counts.productCompatibleMeasurements} measurements`),
    el('div', { class: 'wj-hollow-line sim' }, 'SIMULATION / PREVIEW'),
    el('div', { class: 'wj-hollow-line' }, FIELD_VERIFICATION_TEXT),
  ]);
}

function renderWallLegend() {
  const list = $('wallmap-legend');
  list.replaceChildren();
  const entries = [
    ['[PC]', 'Product-compatible measurement (a Product Sensor identity)'],
    ['[IC]', 'Drawing internal conflict - may record FAIL or PARTIAL, never PASS'],
    ['NON_SENSOR_GAP', 'I7 and I16: never a Sensor and never a Water Jet; no verdict exists for them'],
    ['OK ? X ~ OFF', 'Quality GOOD, UNCERTAIN, BAD, STALE, DISABLED (the same markers as the Overview)'],
    ['NT IP PASS FAIL PART BLOCK', 'Commissioning NOT_TESTED, IN_PROGRESS, PASS, FAIL, PARTIAL, BLOCKED (read-only state of the Preview workflow)'],
    ['--', 'no current value is presented: never a number, never a unit (the cause is named in Sensor detail)'],
    ['degC', 'engineering unit, shown only while a current value exists'],
    ['Front / Rear', 'two separate measurements: nothing here merges them and no value is copied between them'],
    ['dFR', 'FrontTemperature - RearTemperature for this Sensor pair: a signed read-only derived value, unit shown only while it is available'],
    ['dFR +4.3', 'Front above Rear: the expected paired heat-flow direction (the sign is always visible)'],
    ['[v]', 'reduced positive gradient: a possible reduced local heat-transfer / possible slag-insulation candidate - synthetic demonstration only, never a confirmed result'],
    ['[~]', 'near-zero paired gradient: an abnormal paired response requiring investigation - no Product deadband is approved'],
    ['[!]', 'Front below Rear: a paired-measurement contradiction - check mapping, Sensor health, installation and the process transient; never classified as slag by itself'],
    ['dFR --', 'no paired value is calculated: never a number, never a unit, and the cause is named in Sensor detail'],
    ['no effect', 'the paired value is read-only diagnostic / trend: no alarm, queue, cleaning or control effect and no field approval'],
    ['synthetic', `every value is a synthetic Preview observation - ${FIELD_VERIFICATION_TEXT}`],
  ];
  list.append(...entries.map(([marker, text]) => el('div', { class: 'wallmap-legend-item' }, [
    el('span', { class: 'marker' }, marker), el('span', {}, text),
  ])));
}

function renderWallMap(views) {
  const frame = $('wallmap-frame');
  const body = $('wallmap');
  body.replaceChildren();
  frame.setAttribute('data-measurement-view', state.measurementView);
  const map = state.wallMap;
  const selectionNote = $('wallmap-selection-note');
  const filterNote = $('wallmap-filter-note');
  if (!map) {
    frame.setAttribute('data-wallmap-selection', 'UNAVAILABLE');
    selectionNote.textContent = '';
    filterNote.textContent = '';
    $('wallmap-legend').replaceChildren();
    body.append(el('p', { class: 'note' }, state.wallMapError || 'Commissioning Wall Map unavailable: the approved wall topology was not served.'));
    return;
  }
  // Two views of the same served rows, deliberately: the cells are addressed by measurement identity
  // (Sensor + Front/Rear) and the SELECTION by physical Channel key, which is the one identity the
  // Overview, Sensor detail and the Preview workflow already use. Nothing is duplicated by this.
  const viewByKey = new Map();
  const viewByChannelKey = new Map();
  for (const view of views) {
    viewByChannelKey.set(view.key, view);
    if (view.productMeasurementIdentity) viewByKey.set(view.productMeasurementIdentity, view);
  }
  const selectedPosition = state.selectedKey ? map.positionByChannelKey.get(state.selectedKey) ?? null : null;
  const notApplicable = Boolean(state.selectedKey) && !selectedPosition;
  frame.setAttribute('data-wallmap-selection', notApplicable ? 'NOT_APPLICABLE' : (selectedPosition ? 'POSITION' : 'NONE'));
  frame.setAttribute('data-wallmap-focus', selectedPosition ? `${selectedPosition.positionKind}:${selectedPosition.label}` : 'NONE');

  const entry = (wall) => map.walls.find((w) => w.wall === wall);
  // Approved U-shaped arrangement (Stage 0.2.1A presentation): Rear on top, Left and Right at the
  // sides, Front at the bottom, compact centre. The DOM order follows that convention.
  body.append(
    wallBand(entry('REAR'), viewByKey),
    wallBand(entry('LEFT'), viewByKey),
    wallHollow(map),
    wallBand(entry('RIGHT'), viewByKey),
    wallBand(entry('FRONT'), viewByKey),
  );

  const selectedView = state.selectedKey ? viewByChannelKey.get(state.selectedKey) ?? null : null;
  if (notApplicable) {
    selectionNote.textContent = 'The selected Overview row is an unassigned physical Channel (or carries no Wall Map position): '
      + 'no Wall Map position applies, and no Sensor is selected.';
  } else if (!selectedPosition) {
    selectionNote.textContent = 'No measurement selected yet. Select a Wall Map position (Front or Rear) or an Overview row.';
  } else {
    selectionNote.textContent = `Selected: ${selectedPosition.label} ${selectedView ? sideOf(selectedView) : ''}`.trim()
      + (selectedView
        ? ` (${selectedView.productMeasurementIdentity ?? '-'}) - Quality ${selectedView.quality} ${selectedView.qualityMarker}, `
          + `${selectedView.currentValueValid ? `${selectedView.engineeringValue} ${selectedView.engineeringUnit ?? ''}`.trim() : 'no current value presented'}, `
          + `Commissioning ${selectedView.commissioningState}`
        : '')
      + (selectedPosition.selectableAsSensor
        ? '. Selection only: it changes no evidence, no observation and no Preview verdict.'
        : '. NON_SENSOR_GAP position: never a Sensor, and no PASS can ever be recorded for it.');
  }

  const dimmed = map.positions.filter((position) => !positionMatchesFilter(position, viewByKey)).length;
  if (state.filter === 'all') {
    filterNote.textContent = `Wall Map follows the Overview filter: no filter is active, so all ${map.positions.length} positions are shown.`;
  } else {
    const label = FILTERS.find((f) => f.key === state.filter)?.label ?? state.filter;
    filterNote.textContent = `Filter '${label}' is presentation only: ${dimmed} of ${map.positions.length} positions do not match and are dimmed, `
      + 'the map shape is kept, and the evidence, the observations, the counts and the current selection are unchanged.'
      + (dimmed === map.positions.length
        ? ' No Wall Map position matches this filter: the 24 unassigned physical Channels have no position in the logical matrix and are listed in the Overview.'
        : '');
  }
  renderWallLegend();
}

function selectMeasurement(key) {
  state.selectedKey = key;
  renderMonitor();
}

function renderSensorMap() {
  const body = $('sensor-map-body');
  body.replaceChildren();
  const map = state.sensorMap;
  if (!map) {
    body.append(el('p', { class: 'note' }, 'Sensor Map unavailable.'));
    return;
  }
  body.append(el('p', { class: 'note' }, map.note));
  body.append(el('div', { class: 'summary' }, [
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'Logical positions'), el('span', { class: 'v' }, String(map.slotCount))]),
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'Product Sensors'), el('span', { class: 'v' }, String(map.sensorSlotCount))]),
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'Thermocouple channels'), el('span', { class: 'v' }, String(map.logicalMatrix.thermocoupleChannels))]),
    el('div', { class: 'cell' }, [el('span', { class: 'k' }, 'NON_SENSOR_GAP positions'), el('span', { class: 'v' }, String(map.nonSensorGapSlots.length))]),
  ]));
  body.append(el('div', { class: 'table-wrap' }, el('table', {}, [
    el('thead', {}, [el('tr', {}, ['Wall', 'Positions', 'Sensor positions', 'NON_SENSOR_GAP positions', 'Thermocouple channels']
      .map((h) => el('th', { scope: 'col' }, h)))]),
    el('tbody', {}, map.walls.map((wall) => {
      const sensors = wall.slots.filter((s) => s.positionKind === 'SENSOR');
      const gaps = wall.slots.filter((s) => s.positionKind === 'NON_SENSOR_GAP');
      const channels = sensors.reduce((n, s) => n + (s.tcChannels?.length ?? 0), 0);
      return el('tr', {}, [wall.wall, String(wall.slots.length), String(sensors.length), String(gaps.length), String(channels)].map((v, i) => el('td', { class: i === 0 ? '' : 'num' }, String(v))));
    })),
  ])));
  body.append(el('p', { class: 'note' },
    `The Commissioning Wall Map below renders these same 108 positions, read-only. `
    + `I7 and I16 are authoritative NON_SENSOR_GAP positions: they are never Sensors and never Water Jets. `
    + `The logical matrix is ${map.logicalMatrix.columns} x ${map.logicalMatrix.rows} positions.`));
}

function renderMapping() {
  const body = $('mapping-body');
  body.replaceChildren();
  const counts = state.counts;
  if (!counts) {
    body.append(el('p', { class: 'note' }, unavailableText()));
    return;
  }
  body.append(definitionList([
    ['Mapping revision', 'BASELINE_REV1'],
    ['Evidence source', 'docs/evidence/tc-drawing-schedule-rev1.csv'],
    ['Schedule rows', counts.physicalChannels],
    ['Mapped physical Channels', counts.mappedChannels],
    ['Unassigned physical Channels', counts.unassignedChannels],
    ['Product-compatible measurements', counts.productCompatibleMeasurements],
    ['Product Sensors', counts.productSensors],
    ['DRAWING_VERIFIED rows', counts.drawingVerifiedRows],
    ['Drawing internal-conflict rows', counts.drawingInternalConflictRows],
    ['Product-topology conflict rows', counts.productTopologyConflictRows],
    ['Unresolved rows', counts.unresolvedRows],
    ['Protected baseline match', counts.matchesProtectedBaseline ? 'yes (240-row Baseline REV1)' : 'no - investigate'],
  ]));
  body.append(el('p', { class: 'note' },
    'Evidence-derived Mapping is presented as evidence. It is NOT field-loop-test verified, and this Preview does not change it. '
    + 'The Mapping Configuration surface runs as its own separate local route; this Preview never navigates to another origin.'));
}

// ---------------------------------------------------------------------------
// TC Node Configuration (Stage 0.4C-K2) - read-only projection of the six-Node configuration
// ---------------------------------------------------------------------------

/** Honest status words. Each status is written out as text; the codes are listed in the legend. */
const NODE_STATUS_TEXT = Object.freeze({
  CONFIGURED: 'CONFIGURED',
  VALIDATION_FAILED: 'VALIDATION FAILED',
  PORT_UNRESOLVED: 'UNRESOLVED',
  PROTOCOL_UNRESOLVED: 'UNRESOLVED',
  REACHABILITY_NOT_TESTED: 'NOT TESTED',
  ACQUISITION_NOT_IMPLEMENTED: 'NOT IMPLEMENTED',
  HARDWARE_VERIFICATION_PENDING: 'PENDING',
  FIELD_VERIFICATION_PENDING: 'PENDING',
  HARDWARE_ACCESS_NOT_AUTHORIZED: 'NOT AUTHORIZED',
});

const statusText = (code) => NODE_STATUS_TEXT[code] ?? (code === null || code === undefined ? 'NOT PROVIDED' : String(code));

const NODE_CONFIGURATION_ROWS = Object.freeze([
  ['Cabinet ID', (n) => n.cabinetId ?? 'NOT PROVIDED'],
  ['Test IP', (n) => n.ipAddress ?? 'NOT PROVIDED'],
  ['Environment', (n) => n.addressEnvironment],
  ['Configuration mode', (n) => n.configurationMode],
  ['Head station (expected)', (n) => `${n.expectedCouplerModel} x ${n.expectedHeadStationCount}`],
  ['TC input modules (expected)', (n) => `${n.expectedThermocoupleModuleCount} x ${n.expectedThermocoupleModuleModel}`],
  ['Channels per module (expected)', (n) => String(n.expectedChannelsPerThermocoupleModule)],
  ['Physical TC Channels (expected)', (n) => String(n.expectedPhysicalChannelCount)],
  ['End module (expected)', (n) => `${n.expectedEndModuleModel} x ${n.expectedEndModuleCount}`],
  ['Port', (n) => statusText(n.portStatus)],
  ['Protocol', (n) => statusText(n.protocolStatus)],
  ['Configuration', (n) => statusText(n.configurationStatus)],
  ['Reachability', (n) => statusText(n.reachabilityStatus)],
  ['Acquisition', (n) => statusText(n.acquisitionStatus)],
  ['Hardware verification', (n) => statusText(n.hardwareVerificationStatus)],
  ['Field verification', (n) => statusText(n.fieldVerificationStatus)],
  ['Hardware access', (n) => statusText(n.hardwareAccessStatus)],
]);

const NODE_CONFIGURATION_LEGEND = Object.freeze([
  ['CONFIGURED', 'the identity, Cabinet and Test IP validate as one atomic configuration; it is not reachability'],
  ['NOT TESTED (REACHABILITY_NOT_TESTED)', 'no network probe exists in this Stage, so no reachability is claimed'],
  ['NOT IMPLEMENTED (ACQUISITION_NOT_IMPLEMENTED)', 'no physical process image is read and no Channel value is acquired'],
  ['PENDING (HARDWARE / FIELD VERIFICATION PENDING)', 'neither the hardware nor the field installation has been verified'],
  ['NOT AUTHORIZED (HARDWARE_ACCESS_NOT_AUTHORIZED)', 'this Stage contains no Hardware access path'],
  ['UNRESOLVED (PORT_UNRESOLVED / PROTOCOL_UNRESOLVED)', 'no network port and no protocol are approved, so none is assumed'],
]);

function renderNodeConfiguration() {
  const view = state.tcNodeConfiguration;
  const body = $('node-config-body');
  const totals = $('node-config-totals');
  const legend = $('node-config-legend');
  const validation = $('node-config-validation');
  const authority = $('node-config-authority');
  body.replaceChildren();
  totals.replaceChildren();
  legend.replaceChildren();
  if (!view) {
    validation.textContent = 'Node configuration unavailable: the configuration projection could not be read.';
    body.append(el('p', { class: 'note' }, 'Node configuration unavailable.'));
    return;
  }

  validation.textContent = view.validation.valid
    ? `Configuration validation: PASSED (0 issues) · Configuration status: ${statusText(view.configurationStatus)}`
    : `Configuration validation: FAILED (${view.validation.issueCount} issue(s): ${view.validation.issueCodes.join(', ')}) · `
      + `Configuration status: ${statusText(view.configurationStatus)}`;

  const t = view.totals;
  totals.append(...[
    ['Nodes', t.nodes, ''],
    ['Cabinets', t.cabinets, ''],
    ['TC Modules', t.thermocoupleModules, ''],
    ['Physical TC Channels', t.physicalTcChannels, ''],
    ['Configured Test IPs', t.configuredTestIps, 'info'],
    ['Reachability tested', t.reachabilityTested, ''],
    ['Acquisition verified', t.acquisitionVerified, ''],
    ['Field verified', t.fieldVerified, ''],
  ].map(([k, v, cls]) => el('div', { class: cls ? `cell ${cls}` : 'cell' }, [
    el('span', { class: 'k' }, k), el('span', { class: 'v' }, String(v)),
  ])));

  // Transposed presentation: one column per Node, so all six Nodes are visible together at 1920x1080.
  const nodes = view.nodes;
  body.append(el('table', { id: 'node-config-table' }, [
    el('thead', {}, [el('tr', {}, [
      el('th', { scope: 'col' }, 'Attribute'),
      ...nodes.map((n) => el('th', { scope: 'col' }, n.nodeId ?? 'NODE ID NOT PROVIDED')),
    ])]),
    el('tbody', {}, NODE_CONFIGURATION_ROWS.map(([label, pick]) => el('tr', {}, [
      el('th', { scope: 'row' }, label),
      ...nodes.map((n) => el('td', {}, pick(n))),
    ]))),
  ]));

  legend.append(...NODE_CONFIGURATION_LEGEND.map(([code, meaning]) => el('li', {}, [
    el('span', { class: 'marker' }, code), ` - ${meaning}`,
  ])));

  authority.textContent = 'NO HARDWARE ACCESS / NO DEVICE WRITE. This section sends no network request, probes no address, '
    + 'opens no socket, contacts no device, reads no process image and offers no connection, test, activation or polling control. '
    + 'Configured identity is not verification: reachability, acquisition, hardware verification and field verification are all unverified.';
}

// ---------------------------------------------------------------------------
// TC Acquisition Evidence (Stage 0.4C-K3) - read-only projection of the evidence gate
// ---------------------------------------------------------------------------

/** Honest words for the K3 evidence statuses. The code stays visible next to the text. */
const EVIDENCE_STATUS_TEXT = Object.freeze({
  CONFIGURED_NOT_TESTED: 'CONFIGURED, NOT TESTED',
  NOT_CONFIGURED: 'NOT CONFIGURED',
  PROCESS_IMAGE_CANDIDATE_UNVERIFIED: 'CANDIDATE, UNVERIFIED',
  PROCESS_IMAGE_DERIVATION_BLOCKED: 'DERIVATION BLOCKED',
  BYTE_ORDER_UNRESOLVED: 'UNRESOLVED',
  WORD_ORDER_UNRESOLVED: 'UNRESOLVED',
  STATUS_LOCATION_UNRESOLVED: 'UNRESOLVED',
  ENCODING_UNRESOLVED: 'UNRESOLVED',
  WIRE_BREAK_DECODING_BLOCKED: 'DECODING BLOCKED',
  FIRMWARE_NOT_VERIFIED: 'NOT VERIFIED',
  ADDRESS_UNRESOLVED: 'UNRESOLVED',
  FRESHNESS_THRESHOLDS_UNRESOLVED: 'OWNER INPUT PENDING',
});

const evidenceStatus = (code) => {
  if (code === null || code === undefined) return 'NOT PROVIDED';
  const text = EVIDENCE_STATUS_TEXT[code] ?? NODE_STATUS_TEXT[code];
  return text ? `${text} (${code})` : String(code);
};

const withCandidate = (field) => field?.candidate
  ? `${evidenceStatus(field.status)} · candidate ${field.candidate} [${field.candidateClass}, ${field.candidateScope}]`
  : evidenceStatus(field?.status);

const TC_EVIDENCE_ROWS = Object.freeze([
  ['Cabinet ID', (n) => n.cabinetId ?? 'NOT PROVIDED'],
  ['Test IP', (n) => n.ipAddress ?? 'NOT PROVIDED'],
  ['Port', (n) => evidenceStatus(n.portStatus)],
  ['Protocol', (n) => evidenceStatus(n.protocolStatus)],
  ['Reachability', (n) => evidenceStatus(n.reachabilityStatus)],
  ['Acquisition', (n) => evidenceStatus(n.acquisitionStatus)],
  ['Communication state', (n) => evidenceStatus(n.communicationState)],
  ['Process image', (n) => evidenceStatus(n.processImageStatus)],
  ['Byte order', (n) => evidenceStatus(n.byteOrderStatus)],
  ['Word order', (n) => evidenceStatus(n.wordOrderStatus)],
  ['Wire-break decoding', (n) => evidenceStatus(n.wireBreakDecodingStatus)],
  ['Firmware', (n) => evidenceStatus(n.firmwareStatus)],
  ['Hardware verification', (n) => evidenceStatus(n.hardwareVerificationStatus)],
  ['Field verification', (n) => evidenceStatus(n.fieldVerificationStatus)],
  ['Hardware access', (n) => evidenceStatus(n.hardwareAccessStatus)],
]);

function renderTcAcquisitionEvidence() {
  const view = state.tcAcquisitionEvidence;
  const validation = $('tc-evidence-validation');
  const decisions = $('tc-evidence-decisions');
  const body = $('tc-evidence-body');
  const sources = $('tc-evidence-sources');
  const image = $('tc-evidence-process-image');
  const requests = $('tc-evidence-requests');
  const authority = $('tc-evidence-authority');
  for (const node of [decisions, body, sources, image, requests]) node.replaceChildren();
  if (!view) {
    validation.textContent = 'TC acquisition evidence unavailable: the evidence projection could not be read.';
    body.append(el('p', { class: 'note' }, 'TC acquisition evidence unavailable.'));
    return;
  }

  validation.textContent = (view.validation.valid
    ? `Evidence validation: 0 issues · evidence set ${view.evidenceSetId} · evidence Stage ${view.evidenceStage} · mode ${view.evidenceMode}`
    : `Evidence validation: ${view.validation.issueCount} issue(s): ${view.validation.issueCodes.join(', ')} · evidence set ${view.evidenceSetId}`)
    + ` · Preview identity Stage ${view.stageId}`;

  const p = view.decisions.protocol;
  const port = view.decisions.port;
  const note = p.evidenceNote;
  decisions.append(definitionList([
    ['Protocol decision', p.status],
    ['Protocol evidence status', p.evidenceStatus],
    ['Protocol candidate status', `${p.candidateStatus}${p.candidateRetained ? '' : ' - no candidate is retained; no protocol is inferred from the device family'}`],
    ['Protocol source completeness', p.sourceCompleteness],
    ['Non-authoritative evidence note', note
      ? `${note.reference} Original document readable in this review: ${note.originalDocumentReadableInCurrentReview ? 'yes' : 'no'}; committed profile section truncated: ${note.committedProfileSectionTruncated ? 'yes' : 'no'}; `
        + `as-installed applicability verified: ${note.asInstalledApplicabilityVerified ? 'yes' : 'no'}; firmware verified: ${note.firmwareVerified ? 'yes' : 'no'}; configuration verified: ${note.configurationVerified ? 'yes' : 'no'}; `
        + `port resolved: ${note.portResolved ? 'yes' : 'no'}; candidate retained: ${note.protocolCandidateRetained ? 'yes' : 'no'}; adapter authorized: ${note.adapterAuthorized ? 'yes' : 'no'}; acquisition authorized: ${note.acquisitionAuthorized ? 'yes' : 'no'}.`
      : 'none'],
    ['Approved for a Test adapter', p.approvedForTestAdapter ? 'yes' : `no - Owner approval ${p.ownerApproval}`],
    ['Protocol activated', p.activated ? 'yes' : 'no'],
    ['Port decision', port.status],
    ['Port candidate', port.candidate ? `${port.candidate.value} (${port.status})` : `none - ${port.reason ?? 'no evidence'}; no default is assumed`],
    ['Communication state model', `${view.communicationStateModel.states.length} states, contract only; current Node state ${evidenceStatus(view.communicationStateModel.currentNodeState)}`],
    ['Freshness thresholds', `aging ${view.freshness.thresholds.agingAfterMs} · stale ${view.freshness.thresholds.staleAfterMs} · Last-Valid retention ${view.freshness.thresholds.lastValidValueRetentionMs}`],
  ]));

  const nodes = view.nodes;
  body.append(el('table', { id: 'tc-evidence-table' }, [
    el('thead', {}, [el('tr', {}, [
      el('th', { scope: 'col' }, 'Attribute'),
      ...nodes.map((n) => el('th', { scope: 'col' }, n.nodeId ?? 'NODE ID NOT PROVIDED')),
    ])]),
    el('tbody', {}, TC_EVIDENCE_ROWS.map(([label, pick]) => el('tr', {}, [
      el('th', { scope: 'row' }, label),
      ...nodes.map((n) => el('td', {}, pick(n))),
    ]))),
  ]));

  const byClass = Object.entries(view.claimSummary.byClass).filter(([, n]) => n > 0).map(([k, n]) => `${k} ${n}`).join(' · ');
  sources.append(el('p', { class: 'note' }, `${view.totals.claims} classified claims: ${byClass}.`));
  sources.append(el('table', { id: 'tc-evidence-sources-table' }, [
    el('thead', {}, [el('tr', {}, ['Source', 'Kind', 'Revision', 'Availability in session', 'Connected storage', 'Content', 'Evidence status', 'Readable in this review', 'Hash'].map((h) => el('th', { scope: 'col' }, h)))]),
    el('tbody', {}, view.sources.map((s) => el('tr', {}, [
      el('td', {}, s.title), el('td', {}, s.kind), el('td', {}, s.revision ?? 'NOT PROVIDED'), el('td', {}, s.availability),
      el('td', {}, s.storageAccess ?? 'n/a'), el('td', {}, s.contentRetrieval ?? 'n/a'), el('td', {}, s.evidenceStatus ?? 'n/a'),
      el('td', {}, s.readableInCurrentReview === true ? 'yes' : s.readableInCurrentReview === 'TRUNCATED' ? 'TRUNCATED' : 'no'),
      el('td', {}, s.hashRecorded ? `recorded (${s.hashBasis})` : 'none'),
    ]))),
  ]));
  sources.append(definitionList([
    ['Status location', withCandidate(view.statuses.statusLocation)],
    ['Byte order', withCandidate(view.statuses.byteOrder)],
    ['Word order', withCandidate(view.statuses.wordOrder)],
    ['Encoding', withCandidate(view.statuses.encoding)],
    ['Wire-break decoding', `${evidenceStatus(view.statuses.wireBreakDecoding.status)} - evidence classification ${view.statuses.wireBreakDecoding.evidenceClassification}; source completeness ${view.statuses.wireBreakDecoding.sourceCompleteness}; `
      + `Hardware observation ${view.statuses.wireBreakDecoding.hardwareObservationStatus}; physical decoder implemented: ${view.statuses.wireBreakDecoding.physicalDecoderImplemented ? 'yes' : 'no'}; `
      + `${view.statuses.wireBreakDecoding.provided.length} of ${view.statuses.wireBreakDecoding.requiredEvidence.length} required evidence items provided`],
    ['Wire-break evidence record', view.statuses.wireBreakDecoding.evidenceRecord
      ? `Manufacturer document transcription (Stage 0B): ${view.statuses.wireBreakDecoding.evidenceRecord.manufacturerDocumentTranscription.join('; ')}. Reviewer paraphrase: ${view.statuses.wireBreakDecoding.evidenceRecord.reviewerParaphrase.join('; ')}. `
        + `As-installed configuration: ${view.statuses.wireBreakDecoding.evidenceRecord.asInstalledConfiguration}. Field observation: ${view.statuses.wireBreakDecoding.evidenceRecord.fieldObservation}. Ambiguity: ${view.statuses.wireBreakDecoding.evidenceRecord.ambiguity}.`
      : 'none'],
  ]));

  const c = view.processImageCandidate;
  // Owner ruling (PR #21): while derivation is blocked, no module, word, start, end, offset or range is
  // rendered. The only arithmetic shown is RECORD_TO_RECORD_CONSISTENCY_ONLY, classified literally.
  image.append(el('p', { class: 'note' }, `Process image: DERIVATION BLOCKED (${c.state}); address ${evidenceStatus(c.addressStatus)}; authoritative no; activation possible no. Blockers: ${c.blockers.join('; ')}.`));
  const rr = view.recordToRecordConsistency;
  if (rr) {
    image.append(el('p', { class: 'note' }, `${rr.classification}: ${rr.compared} schedule rows compared with the repository profile numbers, ${rr.mismatches} mismatches. Meaning: ${rr.meaning.join('; ')}.`));
  }
  image.append(el('p', { class: 'note' }, `Verification blockers: ${c.verificationBlockers.join('; ')}.`));

  requests.append(...view.missingEvidenceRequests.map((r) => el('li', {}, [
    el('span', { class: 'marker' }, r.requestId), ` - ${r.item} (blocks: ${r.blocks.join('; ')}) - ${r.status}`,
  ])));

  authority.textContent = `${view.authority.statements.join(' / ')}. This section sends no network request, probes no address, opens no socket, `
    + 'contacts no device, reads no process image and offers no connection, test, activation or polling control. A candidate is not an address; '
    + 'model-level evidence does not resolve an instance; the synthetic wire-break scenario is not physical evidence.';
}

function renderDiagnostics() {
  const body = $('diagnostics-body');
  body.replaceChildren();
  const boundary = state.boundary;
  if (!boundary) {
    body.append(el('p', { class: 'note' }, 'Diagnostics unavailable.'));
    return;
  }
  body.append(el('ul', { class: 'legend' }, boundary.notes.map((n) => el('li', {}, n))));
  body.append(definitionList([
    ['Stage', boundary.stageId],
    ['Mode', boundary.mode],
    ['Live transport implemented', 'no'],
    ['Hardware connection', 'none'],
    ['Device write authority', 'none'],
    ['Field verified', 'no - FIELD VERIFICATION PENDING'],
    ['Approved local origin', boundary.approvedOrigin],
    ['Bind host', boundary.bindHost],
    ['Evidence baseline', boundary.evidenceBaseline],
    ['Drawing status', boundary.drawingStatus],
    ['Wire-break decoding', 'BLOCKED in the baseline - the scenario is simulated'],
  ]));
  body.append(el('p', { class: 'note' },
    'There are no device diagnostics in this Preview: no node configuration, no module fault read, no link statistics and no '
    + 'transport counters exist, because no device is contacted. The states shown are synthetic presentation states.'));
}

// ---------------------------------------------------------------------------
// Commissioning Monitor
// ---------------------------------------------------------------------------

function currentViews() {
  if (!state.schedule || state.observations.size === 0) return [];
  return buildRowViews(state.schedule, state.observations, state.session);
}

function renderScenarios() {
  const select = $('scenario-select');
  select.replaceChildren();
  for (const scenario of state.scenarios) {
    select.append(el('option', { value: scenario.key }, `${scenario.label}${scenario.simulatedDiagnostic ? ' (simulated)' : ''}`));
  }
  const nodeSelect = $('node-select');
  nodeSelect.replaceChildren();
  for (const node of state.opsSummary?.nodeSummary ?? []) {
    nodeSelect.append(el('option', { value: node.nodeId }, node.nodeId));
  }
  const scenario = state.scenarios.find((s) => s.key === state.scenarioKey);
  const paired = scenario?.pairedDiagnostic
    ? ` Paired diagnostic demonstration: ${scenario.pairedMode} - ${scenario.pairedNote}`
    : '';
  $('scenario-summary').textContent = scenario ? `${scenario.key}: ${scenario.summary}${paired}` : '';
  $('scenario-summary').setAttribute('data-paired-mode', String(scenario?.pairedMode ?? ''));
}

function renderSummary(views) {
  const body = $('summary-body');
  body.replaceChildren();
  if (!state.schedule) return;
  const s = summaryCounts(state.schedule, views);
  const cells = [
    ['Physical Channels', s.totalPhysicalChannels, ''],
    ['Mapped', s.mappedPhysicalChannels, ''],
    ['Unassigned', s.unassignedPhysicalChannels, 'warn'],
    ['Product-compatible measurements', s.productCompatibleMeasurements, ''],
    ['Product Sensors', s.productSensors, ''],
    ['Conflict rows', s.conflictRows, 'err'],
    ['GOOD', s.good, 'ok'],
    ['UNCERTAIN', s.uncertain, 'warn'],
    ['BAD', s.bad, 'err'],
    ['STALE', s.stale, 'warn'],
    ['Communication loss', s.communicationLoss, 'err'],
    ['Simulated wire break', s.simulatedWireBreak, 'warn'],
    ['NOT_TESTED', s.notTested, ''],
    ['IN_PROGRESS', s.inProgress, 'info'],
    ['PASS', s.passed, 'ok'],
    ['FAIL', s.failed, 'err'],
    ['PARTIAL', s.partial, 'warn'],
    ['BLOCKED', s.blocked, 'warn'],
    ['Current value valid', s.currentValueValid, ''],
    ['Current value unavailable', s.currentValueUnavailable, 'err'],
  ];
  body.append(...cells.map(([k, v, cls]) => el('div', { class: `cell ${cls}` }, [
    el('span', { class: 'k' }, k), el('span', { class: 'v' }, String(v)),
  ])));
}

function renderFilters(views) {
  const container = $('filters');
  container.replaceChildren();
  const counts = filterCounts(views);
  for (const filter of FILTERS) {
    const button = el('button', {
      type: 'button',
      'data-filter': filter.key,
      'aria-pressed': String(state.filter === filter.key),
      class: 'toggle',
    }, `${filter.label} (${counts[filter.key]})`);
    button.addEventListener('click', () => {
      state.filter = filter.key;
      renderMonitor();
    });
    container.append(button);
  }
}

function renderTable(views) {
  const rows = filterRows(views, state.filter);
  const body = $('monitor-table-body');
  body.replaceChildren();
  for (const view of rows) {
    const cells = [
      ['Sensor ID', view.sensorId ?? '-'],
      ['Side', view.drawingSide ?? '-'],
      ['Measurement', view.productMeasurementIdentity ?? '-'],
      ['Node', view.node],
      ['Module', view.module],
      ['Channel', String(view.physicalChannel)],
      ['Terminal', view.terminal ?? '-'],
      ['Drawing state', view.drawingVerificationState],
      ['Product compatibility', view.productCompatibility],
      ['Raw', view.rawValue],
      ['Engineering value', view.engineeringValue],
      ['Unit', view.engineeringUnit ?? '-'],
      ['Quality', `${view.qualityMarker} ${view.quality}`],
      // A simulated diagnostic is marked in the row itself, so the simulation label travels with the
      // value and cannot be lost by collapsing the detail panel.
      ['Diagnostic', view.simulatedDiagnostic ? `${view.diagnosticText} [SIMULATED - NOT HARDWARE DETECTED]` : view.diagnosticText],
      ['Current value valid', view.currentValueValid ? 'yes' : 'no'],
      ['Last valid (historical)', `${view.lastValidValue} ${view.lastValidLabel}`],
      ['Last update', view.observationTimestamp],
      // An age beyond the Preview threshold is marked in the row, so STALE never looks like a fresh
      // sample even before the detail panel is opened.
      ['Value age', view.valueAgeExceedsPreviewThreshold ? `${view.valueAge} - stale (Preview threshold)` : view.valueAge],
      ['Commissioning', view.commissioningState],
      ['Field verification', view.fieldVerificationText],
    ];
    const row = el('tr', {
      class: [view.rowClassClassName, statusOf(view), state.selectedKey === view.key ? 'selected' : ''].filter(Boolean).join(' '),
      'data-channel-key': view.key,
      tabindex: '0',
    }, cells.map(([, value]) => el('td', {}, String(value))));
    row.addEventListener('click', () => {
      state.selectedKey = view.key;
      renderMonitor();
    });
    row.addEventListener('keydown', (evt) => {
      if (evt.key === 'Enter') {
        state.selectedKey = view.key;
        renderMonitor();
      }
    });
    body.append(row);
  }
  $('monitor-count').textContent =
    `Showing ${rows.length} of ${views.length} physical Channels - filter '${state.filter}' changes the view only.`;
}

/**
 * The read-only paired thermal card. It shows both authoritative side values, the derived signed
 * delta (never a bare number, never a fabricated one), the paired diagnostic state, its guidance, the
 * Synthetic provenance, the field-verification state and the explicit no-effect statement.
 */
function renderDetailPair(view) {
  const card = $('detail-pair');
  card.replaceChildren();
  if (!view) {
    card.setAttribute('data-pair-state', PAIRED_THERMAL_STATE.DIFFERENTIAL_UNAVAILABLE);
    card.append(el('p', { class: 'note' },
      'Select a measurement in the Overview or the Commissioning Wall Map to review its read-only paired thermal diagnostic.'));
    return;
  }
  const pair = pairForRow(state.pairs, view.row);
  card.setAttribute('data-pair-state', String(pair.state));
  card.append(el('h4', {}, 'Paired thermal diagnostic (read-only, derived)'));
  const sideValue = (presented, unit) => (presented === null ? 'UNAVAILABLE' : `${presented}${unit ? ` ${unit}` : ''}`);
  card.append(definitionList([
    ['Sensor pair', pair.sensorId ?? 'no Product Sensor pair'],
    ['Front measurement', `${pair.frontMeasurementIdentity ?? '-'} (${SIDE_LABELS.F})`],
    ['Front current value', sideValue(pair.frontValuePresented, pair.frontUnit)],
    ['Rear measurement', `${pair.rearMeasurementIdentity ?? '-'} (${SIDE_LABELS.R})`],
    ['Rear current value', sideValue(pair.rearValuePresented, pair.rearUnit)],
    [`${FRONT_REAR_DELTA_LABEL} = Front - Rear`, pair.deltaAvailable ? `${pair.deltaText} ${pair.unit}` : `-- (${pair.reason})`],
    ['Paired diagnostic state', `${pair.marker} ${pair.state}`],
    ['Guidance', pair.guidance],
    ['Synthetic scenario', `${pair.scenarioKey ?? '-'} (paired mode ${pair.pairedDifferentialMode})`],
    ['Observation provenance', pair.provenance ?? 'SYNTHETIC_PREVIEW'],
    ['Field verification', FIELD_VERIFICATION_TEXT],
  ]));
  card.append(el('p', { class: 'pair-effect' }, pair.effect));
}

function renderDetail(views) {
  const identity = $('detail-identity');
  const observation = $('detail-observation');
  const workflow = $('detail-workflow');
  identity.replaceChildren();
  observation.replaceChildren();
  renderDetailPair(null);
  workflow.replaceChildren();
  clear('workflow-actions');

  const view = state.selectedKey ? views.find((v) => v.key === state.selectedKey) : null;
  if (!view) {
    identity.append(el('p', { class: 'note' }, 'Select a measurement in the Overview to review its identity, its synthetic observation and its Preview workflow.'));
    renderDetailPair(null);
    return;
  }
  const row = view.row;
  identity.append(el('h4', {}, 'Identity (read-only evidence)'));
  identity.append(definitionList([
    ['Sensor ID', view.sensorId ?? '-'],
    ['Drawing side', view.drawingSide ?? '-'],
    ['Product measurement identity', view.productMeasurementIdentity ?? '-'],
    ['Physical wall / location', `${row.cabinet} ${row.moduleInstance} - ${view.terminal ?? 'no field terminal'}`],
    ['Node', view.node],
    ['Module', view.module],
    ['Channel', view.physicalChannel],
    ['Terminal', view.terminal ?? '-'],
    ['Drawing page / sheet', `${row.tablePage} / ${row.diagramPage}`],
    ['Mapping revision', view.mappingRevision],
    ['Drawing status', view.drawingStatus],
    ['Product compatibility', view.productCompatibility],
    ['Field verification state', view.fieldVerificationText],
  ]));

  observation.append(el('h4', {}, 'Synthetic observation (read-only)'));
  observation.append(definitionList([
    ['Raw value', view.rawValue],
    ['Engineering value', view.engineeringValue],
    ['Unit', view.engineeringUnit ?? '-' ],
    ['Quality', `${view.qualityMarker} ${view.quality}`],
    ['DiagnosticReason', view.diagnosticReason],
    ['IsCurrentValueValid', view.currentValueValid ? 'true' : 'false'],
    ['LastValidValue', view.lastValidValue],
    ['LastValidTimestamp', view.lastValidTimestamp ?? '-'],
    ['Observation timestamp', view.observationTimestamp],
    ['Value age', `${view.valueAge}${view.valueAgeExceedsPreviewThreshold ? ' (exceeds Preview threshold)' : ''}`],
    ['Node communication state', view.nodeCommunicationState],
    ['Scenario source', `${view.scenarioSource} (${state.scenarioKey})`],
    ['Scenario note', view.scenarioNote ?? '-'],
  ]));
  if (view.simulatedDiagnostic) {
    observation.append(el('p', { class: 'sim' }, view.simulatedDiagnosticLabel));
  }

  // The read-only paired Front/Rear diagnostic of the selected measurement's Sensor.
  renderDetailPair(view);

  const record = state.session.recordOf(row);
  workflow.append(el('h4', {}, 'Preview workflow (approves nothing)'));
  workflow.append(definitionList([
    ['CommissioningSessionId', record.commissioningSessionId],
    ['Tester', record.tester],
    ['Started time', record.startedAt ?? 'session start (in-memory)'],
    ['Baseline observation', record.baselineObservation ? `${record.baselineObservation.quality} @ ${record.baselineObservation.observationTimestamp}` : 'not started'],
    ['Stimulus observation', record.stimulusObservation ? `${record.stimulusObservation.quality} @ ${record.stimulusObservation.observationTimestamp}` : 'not started'],
    ['Open-circuit observation', record.openCircuitObservation ? `${record.openCircuitObservation.quality} @ ${record.openCircuitObservation.observationTimestamp}` : 'not started'],
    ['Recovery observation', record.recoveryObservation ? `${record.recoveryObservation.quality} @ ${record.recoveryObservation.observationTimestamp}` : 'not started'],
    ['Verdict', record.verdict ?? 'none recorded'],
    ['Remark', record.remark || 'none'],
    ['Approval state', record.approvalState],
    ['Evidence blocking', view.blockingReason ?? 'none'],
    ['Scenario blocking', view.observationBlockingReason ?? 'none'],
  ]));

  const actions = $('workflow-actions');
  for (const kind of Object.values(OBSERVATION_KIND)) {
    const button = el('button', { type: 'button', class: 'toggle', 'data-action': `begin-${kind}` }, `Begin ${kind} observation`);
    button.addEventListener('click', () => {
      try {
        state.session.beginObservation(row, kind, state.observations.get(view.key));
        setAlert(`Preview observation '${kind}' recorded for ${view.key}. This is not Production evidence.`);
      } catch (err) {
        setAlert(`Refused: ${err.message}`);
      }
      renderMonitor();
    });
    actions.append(el('span', { class: 'band' }, [button]));
  }
  for (const verdict of ['PASS', 'FAIL', 'PARTIAL']) {
    const allowed = verdict === 'PASS' ? canRecordPass(row) : Boolean(row.sensorId);
    const button = el('button', {
      type: 'button',
      class: 'toggle',
      'data-action': `verdict-${verdict}`,
      disabled: allowed ? undefined : true,
      title: allowed ? undefined : `Not recordable: ${view.blockingReason ?? 'no Sensor identity'}`,
    }, `Record ${verdict}`);
    if (allowed) {
      button.addEventListener('click', () => {
        try {
          state.session.recordVerdict(row, { verdict, remark: $('remark-input').value ?? '' });
          setAlert(`Preview verdict ${verdict} recorded for ${view.key}. It approves nothing and is not Production evidence.`);
        } catch (err) {
          setAlert(`Refused: ${err.message}`);
        }
        renderMonitor();
      });
    }
    actions.append(button);
  }
  actions.append(el('span', { class: 'muted' }, `${view.rowClassText}`));

  $('btn-record-remark').disabled = false;
  $('btn-clear-result').disabled = false;
}

function renderLegend() {
  const list = $('legend');
  list.replaceChildren();
  list.append(...legend().map((entry) => el('li', {}, [el('span', { class: 'marker' }, entry.marker), entry.text])));
}

function renderMonitor() {
  const views = currentViews();
  renderScenarios();
  renderNodeSummary();
  renderSummary(views);
  renderLoopProgress();
  renderWallMap(views);
  renderFilters(views);
  renderTable(views);
  renderDetail(views);
  renderLegend();
  const boundary = state.boundary;
  if (boundary) {
    $('viewbar').setAttribute('data-stage', boundary.stageId);
  }
  $('density-comfortable').setAttribute('aria-pressed', String(state.density === DENSITY.COMFORTABLE));
  $('density-compact').setAttribute('aria-pressed', String(state.density === DENSITY.COMPACT));
  $('view-both').setAttribute('aria-pressed', String(state.measurementView === MEASUREMENT_VIEW.BOTH));
  $('view-front').setAttribute('aria-pressed', String(state.measurementView === MEASUREMENT_VIEW.FRONT));
  $('view-rear').setAttribute('aria-pressed', String(state.measurementView === MEASUREMENT_VIEW.REAR));
  document.body.setAttribute('data-density', state.density);
}

async function loadObservations(overrides = {}) {
  const scenarioKey = overrides.scenarioKey ?? state.scenarioKey;
  const tick = overrides.tick ?? state.tick;
  const nodeId = overrides.nodeId ?? state.nodeId;
  const query = new URLSearchParams({ scenario: scenarioKey, tick: String(tick) });
  if (nodeId) query.set('node', nodeId);
  const response = await fetch(`/api/commissioning/observations?${query.toString()}`);
  if (!response.ok) {
    const detail = await response.json().catch(() => ({ error: 'preview data unavailable' }));
    setAlert(`Scenario not applied: ${detail.error}`);
    return;
  }
  const payload = await response.json();
  state.scenarioKey = payload.scenarioKey;
  state.tick = payload.tick;
  state.nodeId = payload.nodeId ?? state.nodeId;
  state.observations = new Map(Object.entries(payload.observations));
  // Paired Front/Rear values are DERIVED here, from the observations that were just served: no paired
  // value is stored on the host, in the evidence or in any verdict.
  state.pairs = buildThermalPairs(state.schedule, state.observations);
  state.summary = payload.summary;
  state.nodeSummary = payload.nodeSummary;
  state.scenarioProgress = payload.commissioningProgress;
  setAlert('');
  renderMonitor();
}

// ---------------------------------------------------------------------------
// Controls
// ---------------------------------------------------------------------------

function wireControls() {
  $('scenario-apply').addEventListener('click', () => {
    // `||` not `??`: an empty control value (a not-yet-populated selector) must fall back to the
    // current state, never be sent to the host as an empty scenario or node identity.
    const scenario = $('scenario-select').value || state.scenarioKey;
    const node = $('node-select').value || state.nodeId;
    const tickRaw = Number.parseInt($('tick-input').value ?? '0', 10);
    state.scenarioKey = scenario;
    state.nodeId = node;
    state.tick = Number.isInteger(tickRaw) && tickRaw >= 0 ? tickRaw : 0;
    loadObservations();
  });
  $('scenario-reset').addEventListener('click', () => {
    state.session.clearAll();
    setAlert('Preview-only results cleared. Evidence-derived BLOCKED states are unchanged.');
    renderMonitor();
  });
  $('density-comfortable').addEventListener('click', () => { state.density = DENSITY.COMFORTABLE; renderMonitor(); });
  $('density-compact').addEventListener('click', () => { state.density = DENSITY.COMPACT; renderMonitor(); });
  $('view-both').addEventListener('click', () => setMeasurementView(MEASUREMENT_VIEW.BOTH));
  $('view-front').addEventListener('click', () => setMeasurementView(MEASUREMENT_VIEW.FRONT));
  $('view-rear').addEventListener('click', () => setMeasurementView(MEASUREMENT_VIEW.REAR));
  $('wall-prev').addEventListener('click', () => moveWallOrder(-1));
  $('wall-next').addEventListener('click', () => moveWallOrder(1));
  $('btn-record-remark').addEventListener('click', () => {
    const view = currentViews().find((v) => v.key === state.selectedKey);
    if (!view) {
      setAlert('Select a measurement before recording a remark.');
      return;
    }
    try {
      state.session.recordRemark(view.row, $('remark-input').value ?? '');
      setAlert(`Preview remark recorded for ${view.key}.`);
    } catch (err) {
      setAlert(`Refused: ${err.message}`);
    }
    renderMonitor();
  });
  $('btn-clear-result').addEventListener('click', () => {
    const view = currentViews().find((v) => v.key === state.selectedKey);
    if (!view) {
      setAlert('Select a measurement before clearing a Preview result.');
      return;
    }
    const result = state.session.clear(view.row);
    setAlert(result.cleared ? `Preview result cleared for ${view.key}.` : 'No Preview result existed for this measurement.');
    renderMonitor();
  });
  $('btn-prev').addEventListener('click', () => moveCursor(-1));
  $('btn-next').addEventListener('click', () => moveCursor(1));

  $('exit-button').addEventListener('click', () => { $('exit-guard').hidden = false; });
  $('exit-cancel').addEventListener('click', () => { $('exit-guard').hidden = true; });
  $('exit-confirm').addEventListener('click', () => {
    $('exit-guard').hidden = true;
    setAlert('Exit confirmed. The Kiosk shell asks for its own confirmation before the window closes.');
    if (typeof window !== 'undefined' && typeof window.close === 'function') window.close();
  });
}

function moveCursor(direction) {
  // Physical schedule order: the evidence order of the 240 rows. Unchanged by this pass.
  if (!state.schedule) return;
  const next = state.session.moveCursor(state.schedule, state.selectedKey, direction);
  state.selectedKey = next;
  renderMonitor();
}

/** Wall order: the approved logical position order, Sensor by Sensor, Front measurement then Rear. */
function moveWallOrder(direction) {
  if (!state.wallMap) {
    setAlert('Wall order navigation is unavailable: the approved wall topology was not served.');
    return;
  }
  const step = wallOrderStep(state.wallMap, state.selectedKey, direction);
  if (!step.moved) {
    setAlert(direction === 1
      ? 'End of the Wall order reached: the last Product Sensor, Rear measurement.'
      : 'Start of the Wall order reached: the first Product Sensor, Front measurement.');
    return;
  }
  state.selectedKey = step.key;
  const label = wallMeasurementKey(state.wallMap, step.key) ?? step.key;
  setAlert(`Wall order: ${label} selected (Front before Rear; I7 and I16 are skipped as NON_SENSOR_GAP positions).`);
  renderMonitor();
}

/** Presentation control only: which measurement blocks the Wall Map renders. */
function setMeasurementView(view) {
  state.measurementView = view;
  const label = view === MEASUREMENT_VIEW.BOTH ? 'both the Front and the Rear measurement'
    : `only the ${view === MEASUREMENT_VIEW.FRONT ? 'Front' : 'Rear'} measurement`;
  setAlert(`Wall Map now shows ${label}. Presentation only: the two measurements stay separate and no value is copied between them.`);
  renderMonitor();
}

// ---------------------------------------------------------------------------
// Uncaught-failure surface (so a broken interaction can never look like an idle screen)
// ---------------------------------------------------------------------------

if (typeof window !== 'undefined' && typeof window.addEventListener === 'function') {
  window.addEventListener('error', () => {
    if (document.body.getAttribute('data-app-state') === APP_STATE.READY) {
      showBootFailure(BOOT_FAILURE.RUNTIME);
    }
  });
  window.addEventListener('unhandledrejection', () => {
    if (document.body.getAttribute('data-app-state') === APP_STATE.READY) {
      showBootFailure(BOOT_FAILURE.RUNTIME);
    }
  });
}

// ---------------------------------------------------------------------------
// Boot
// ---------------------------------------------------------------------------

async function getJson(path) {
  const response = await fetch(path);
  if (!response.ok) throw new Error(`${path} returned ${response.status}`);
  return response.json();
}

async function loadInitialData() {
  const [boundary, schedule, scenarios, opsSummary, sensorMap] = await Promise.all([
    getJson('/api/boundary'),
    getJson('/api/commissioning/schedule'),
    getJson('/api/commissioning/scenarios'),
    getJson('/api/operations/summary'),
    getJson('/api/sensor-map'),
  ]);
  state.boundary = boundary;
  state.schedule = schedule;
  state.counts = schedule.counts;
  state.scenarios = scenarios.scenarios;
  state.opsSummary = opsSummary;
  state.sensorMap = sensorMap;
  state.nodeId = opsSummary.nodeSummary[0].nodeId;
  // The TC Node configuration is isolated: a failure here degrades only its own section.
  try {
    state.tcNodeConfiguration = await getJson('/api/tc-node-configuration');
  } catch {
    state.tcNodeConfiguration = null;
  }
  // The K3 evidence projection is isolated the same way: its failure degrades only its own section.
  try {
    state.tcAcquisitionEvidence = await getJson('/api/tc-acquisition-evidence');
  } catch {
    state.tcAcquisitionEvidence = null;
  }
  // One canonical topology path: the served logical matrix carries the positions and the Sensor
  // identities, the schedule rows carry the Front/Rear measurement identities. The builder refuses a
  // topology it cannot prove (a missing wall, a duplicated Sensor, a gap that is not I7 or I16, a
  // Sensor without exactly one Front and one Rear measurement) rather than drawing a wrong wall.
  try {
    state.wallMap = buildWallMap(sensorMap, schedule.rows);
    state.wallMapError = '';
  } catch (err) {
    state.wallMap = null;
    state.wallMapError = 'the approved wall topology could not be built from the served matrix and the evidence rows';
    if (typeof console !== 'undefined') console.error('ops-preview wall map:', err.message);
  }
}

async function boot() {
  setAppState(APP_STATE.BOOTING);
  $('exit-guard').hidden = true;
  // A previous failure surface (or a reload) must never leave the banner visible on a healthy boot.
  $('boot-error').hidden = true;
  try {
    wireControls();
  } catch {
    // A missing element or a wiring failure must never leave an empty page behind.
    showBootFailure(BOOT_FAILURE.MODULE_LOAD);
    throw new Error('control wiring failed');
  }

  setAlert('');

  // Stage 1 - request the read-only data. A failure here is a local-host/data problem.
  try {
    await loadInitialData();
  } catch {
    setAlert('Preview data unavailable. The Kiosk shell shows its reconnect state while the local host is down.');
    showBootFailure(BOOT_FAILURE.DATA_REQUEST);
    try { renderBoundary(); renderOperations(); } catch { /* the failure surface is already visible */ }
    return;
  }

  // Stage 2 - render. A failure here is an application defect, not a data problem, and saying so is
  // the difference between a diagnosable screen and a silently empty one.
  try {
    renderBoundary();
    renderOperations();
    renderNodeSummary(state.opsSummary.nodeSummary);
    renderProgress();
    renderLoopProgress();
    renderWallMap([]);
    renderSensorMap();
    renderMapping();
    renderNodeConfiguration();
    renderTcAcquisitionEvidence();
    renderDiagnostics();
    await loadObservations();
    setAppState(APP_STATE.READY);
  } catch {
    showBootFailure(BOOT_FAILURE.RUNTIME);
  }
}

try {
  await boot();
} catch {
  showBootFailure(BOOT_FAILURE.RUNTIME);
}
