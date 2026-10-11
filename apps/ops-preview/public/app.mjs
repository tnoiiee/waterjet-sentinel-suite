// Stage 0.4C-K1 - the Kiosk Preview Operations route with the Sensor Commissioning Monitor.
//
// Every value is written with textContent. Nothing here connects, polls, reads or writes a device,
// opens a socket itself, or accepts an address. The Preview workflow state lives in this browser
// session only: it is never sent anywhere, never persisted, and it approves nothing.
//
// Filters, density and selection are presentation only. They choose which rows are SHOWN and how
// they look; they never touch the Mapping revision, the Runtime revision or the evidence.

import {
  DENSITY, DEFAULT_DENSITY, FIELD_VERIFICATION_TEXT, FILTERS, OBSERVATION_KIND,
  PreviewCommissioningSession, ROW_CLASS_PRESENTATION, buildRowViews, canRecordPass, filterCounts,
  filterRows, legend, summaryCounts,
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
  observations: new Map(),
  session: new PreviewCommissioningSession({ sessionIndex: 0 }),
  scenarioKey: 'NORMAL_GOOD',
  nodeId: 'TC-NODE-01',
  tick: 0,
  filter: 'all',
  density: DEFAULT_DENSITY,
  selectedKey: null,
  alert: '',
};

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
    `I7 and I16 are authoritative NON_SENSOR_GAP positions: they are never Sensors and never Water Jets. `
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
  $('scenario-summary').textContent = scenario ? `${scenario.key}: ${scenario.summary}` : '';
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

function renderDetail(views) {
  const identity = $('detail-identity');
  const observation = $('detail-observation');
  const workflow = $('detail-workflow');
  identity.replaceChildren();
  observation.replaceChildren();
  workflow.replaceChildren();
  clear('workflow-actions');

  const view = state.selectedKey ? views.find((v) => v.key === state.selectedKey) : null;
  if (!view) {
    identity.append(el('p', { class: 'note' }, 'Select a measurement in the Overview to review its identity, its synthetic observation and its Preview workflow.'));
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
  if (!state.schedule) return;
  const next = state.session.moveCursor(state.schedule, state.selectedKey, direction);
  state.selectedKey = next;
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
    renderSensorMap();
    renderMapping();
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
