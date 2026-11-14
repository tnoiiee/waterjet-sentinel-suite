// WJSS Operator presentation - Tabs 3, 4 and 6, plus the camera viewport container.
//
// The rule that governs this whole file: a panel shows a published field, or it shows that the field is
// not published. That is not a limitation of this screen, it is the honest description of what the
// read-only Runtime answers - /api/v1/snapshot, /api/v1/deltas, /api/v1/runtime,
// /api/v1/dispatch-workspace and /api/health. A "not published" line is information an operator can
// act on; a plausible number that no contract carries is not, and it is the one failure mode an HMI of
// this shape has no defence against later.
//
// Nothing here fetches, nothing here composes a request, and nothing here draws a control that could.

import { text, number, timeOf } from './feed.mjs';
import { AXIS_ROLES, AXIS_WORDING, BRAKE_OUTPUT_ORDER, CLEANING_PATH_STAGES, COCKPIT_STAGES, DEFAULT_ASSIGNMENTS, EXPECTED_PER_WALL, EXPECTED_TOTAL, NATIVE_PER_MILLIMETRE, PUMP_STOP_ORDER_RULE, SAFE_RETURN_CENTRE_MM, SAFE_RETURN_CENTRE_NATIVE, SCALING_NOTE, STAGE_COORDINATE_NOTE, WALL_ORDER, WORKSPACE_MAX_MM, WORKSPACE_MIN_MM, WSB_CABINETS, WSB_SOURCE_NOTE, agreeWithSnapshot, assignmentCountByJet, coordinatePair, grouped, summaryPills, tierOfRow } from './topology.mjs';
import { axisOf, captionFor, contextOf, drawGrid, drawSeries, formatTick, formatted, frameOf, mapperFor, normalise, placeLabels, rangeOf, timeRangeOf } from './chartKit.mjs';

const NOT_PUBLISHED = 'NOT_PUBLISHED_BY_RUNTIME';

function clear(node) {
  while (node.firstChild) node.removeChild(node.firstChild);
}

function block(parent, className, text_) {
  const node = document.createElement('div');
  node.className = className;
  if (text_ !== undefined) node.textContent = text_;
  parent.appendChild(node);
  return node;
}

function paragraph(parent, className, value) {
  const node = document.createElement('p');
  node.className = className;
  node.textContent = value;
  parent.appendChild(node);
  return node;
}

function heading(parent, value) {
  const node = document.createElement('h3');
  node.className = 'group-title';
  node.textContent = value;
  parent.appendChild(node);
  return node;
}

function rows(host_, entries) {
  const list = document.createElement('dl');
  list.className = 'facts';
  for (const entry of entries) {
    const term = document.createElement('dt');
    term.textContent = entry[0];
    const value = document.createElement('dd');
    value.textContent = entry[1];
    if (entry[2]) value.dataset.tone = entry[2];
    list.appendChild(term);
    list.appendChild(value);
  }
  host_.appendChild(list);
  return list;
}

function table(host_, headers, body) {
  const element = document.createElement('table');
  element.className = 'grid-table';
  const head = document.createElement('thead');
  const headRow = document.createElement('tr');
  for (const label of headers) {
    const cell = document.createElement('th');
    cell.scope = 'col';
    cell.textContent = label;
    headRow.appendChild(cell);
  }
  head.appendChild(headRow);
  const bodyNode = document.createElement('tbody');
  for (const row of body) {
    const tr = document.createElement('tr');
    if (row.tone) tr.dataset.tone = row.tone;
    for (const cell of row.cells) {
      const td = document.createElement('td');
      td.textContent = cell;
      tr.appendChild(td);
    }
    bodyNode.appendChild(tr);
  }
  element.appendChild(head);
  element.appendChild(bodyNode);
  host_.appendChild(element);
  return element;
}

function byId(list, key, value) {
  for (const item of list) {
    if (item[key] === value) return item;
  }
  return null;
}

// ---------------------------------------------------------------- Tab 3: WJ & Pump

// Symmetric on purpose: four cabinets, two Water Jets each, four axes each. The cabinet and axis letters
// come from the canonical table in topology.mjs (which the gate checks against the .NET catalogs); the
// identities, walls, tiers and pairings come from the Snapshot, so a profile that disagrees with the
// table shows up as a refusal rather than as a tidy grid.
export function renderWsbGrid(root, snapshot) {
  clear(root);
  if (!snapshot) {
    paragraph(root, 'empty-state', 'no Snapshot yet - the WJ and Pump view is waiting for the first accepted frame');
    return;
  }
  const waterJets = Array.isArray(snapshot.waterJets) ? snapshot.waterJets : [];
  const sensors = Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  const agreement = agreeWithSnapshot(waterJets);
  const assignments = assignmentCountByJet(sensors);
  const active = snapshot.activeJob || null;

  const note = block(root, 'topo-note');
  note.textContent = agreement.ok
    ? 'TOPOLOGY AGREED: ' + agreement.reason
    : 'TOPOLOGY DISAGREES WITH THE CANONICAL TABLE - the grid below is drawn from the published payload: ' + agreement.reason;
  note.dataset.tone = agreement.ok ? 'ok' : 'bad';
  paragraph(root, 'topo-source', WSB_SOURCE_NOTE);
  paragraph(root, 'group-title', 'Motion cabinets - 4 WSB x 2 WJ');
  const grid = block(root, 'wsb-grid');
  for (const cabinet of WSB_CABINETS) {
    const card = block(grid, 'wsb-card');
    card.dataset.wsb = cabinet.wsb;
    paragraph(card, 'wsb-name', cabinet.wsb + '  -  ' + cabinet.controller);
    for (const binding of cabinet.jets) {
      const published = byId(waterJets, 'waterJetId', binding.wj);
      const row = block(card, 'wsb-jet');
      row.dataset.tone = published ? 'ok' : 'bad';
      const horizontal = axisWording(binding.horizontalAxis);
      const vertical = axisWording(binding.verticalAxis);
      const title = document.createElement('p');
      title.className = 'wsb-jet-title';
      title.textContent = binding.wj + '  /  ' + publishedIv(published, binding.iv)
        + '  /  brake ' + String(BRAKE_OUTPUT_ORDER[binding.horizontalAxis]) + '+' + String(BRAKE_OUTPUT_ORDER[binding.verticalAxis]);
      row.appendChild(title);
      const axes = document.createElement('p');
      axes.className = 'wsb-jet-axes';
      axes.textContent = horizontal + ' ' + binding.horizontalAxis
        + '  |  ' + vertical + ' ' + binding.verticalAxis
        + '  (A/C = Horizontal, B/D = Vertical)';
      row.appendChild(axes);
      if (published) {
        const covered = document.createElement('p');
        covered.className = 'wsb-jet-cover';
        covered.textContent = 'installed ' + text(published.installedWall) + '/' + text(published.installedRegion)
          + '  ->  cleans ' + text(published.targetWall) + '/' + text(published.targetRegion)
          + '  |  default assignment ' + String(DEFAULT_ASSIGNMENTS[binding.wj])
          + ', published ' + String(assignments.assigned[binding.wj] === undefined ? 0 : assignments.assigned[binding.wj]);
        row.appendChild(covered);
        const placement = document.createElement('p');
        placement.className = 'wsb-jet-placement';
        placement.dataset.tone = 'mute';
        placement.textContent = 'placement ' + text(published.placementKind, NOT_PUBLISHED)
          + ' - ' + (Array.isArray(published.placementAnchors) && published.placementAnchors.length
            ? published.placementAnchors.join(', ')
            : 'no placement anchor is held, and none is inferred');
        row.appendChild(placement);
      } else {
        paragraph(row, 'wsb-jet-missing', 'this Water Jet is not present in the published payload');
      }
      if (active && active.jetId === binding.wj) {
        const live = document.createElement('p');
        live.className = 'wsb-jet-live';
        live.dataset.tone = 'info';
        live.textContent = 'ACTIVE JOB ON THIS JET: ' + text(active.phaseLabel, text(active.phase, NOT_PUBLISHED))
          + '  |  IV ' + text(active.valveId, NOT_PUBLISHED) + ' ' + text(active.valveOpenCommand, NOT_PUBLISHED);
        row.appendChild(live);
      }
    }
  }

}

function axisWording(letter) {
  return AXIS_WORDING[AXIS_ROLES[letter]] || ('axis ' + String(letter));
}

function publishedIv(published, canonical) {
  if (!published) return canonical + ' (published identity unavailable)';
  return canonical + ' (published: ' + text(published.dedicatedIsolationValveId, 'none') + ')';
}

export function renderPumpCard(root, snapshot) {
  clear(root);
  const pump = snapshot ? snapshot.pump : null;
  const runtime = snapshot ? snapshot.runtime : null;
  if (!pump) {
    paragraph(root, 'empty-state', 'no Pump state published in this Snapshot');
    return;
  }
  const active = snapshot.activeJob || null;
  rows(root, [
    ['state', text(pump.state, NOT_PUBLISHED), toneForRunState(pump.state)],
    ['pressure', number(pump.pressure, 2, 'bar'), pump.pressure === null ? 'mute' : 'ok'],
    ['setpoint', number(pump.setpoint, 2, 'bar')],
    ['ready band', number(pump.readyBandLow, 2) + ' - ' + number(pump.readyBandHigh, 2) + ' bar'],
    ['in ready band', pump.ready ? 'YES' : 'NO', pump.ready ? 'ok' : 'warn'],
    ['stop requested at', timeOf(pump.stopRequestedAt)],
    ['job pump source', active ? text(active.pumpPressureSourceId, NOT_PUBLISHED) : 'no active Job', 'mute'],
    ['job pump quality', active ? text(active.pumpPressureQuality, NOT_PUBLISHED) : 'no active Job', 'mute'],
    ['valid input', active ? (active.pumpPressureInputValid ? 'YES' : 'NO') : 'no active Job'],
    ['valve outlet', active ? number(active.valveOutletPressureBar, 2, 'bar') : 'no active Job'],
    ['host uptime', runtime ? number(runtime.uptimeSeconds, 0, 's') : NOT_PUBLISHED, 'mute'],
  ]);
  const rule = paragraph(root, 'interlock-rule', PUMP_STOP_ORDER_RULE);
  rule.dataset.tone = 'warn';
  paragraph(root, 'interlock-note',
    'the order above is the commissioned interlock rule this build was prepared against; the Runtime '
    + 'publishes pump state and the last stop request, and does not publish a per-output confirmation, '
    + 'so no line of this card claims the outputs were observed in that order');
}

function toneForRunState(state) {
  if (state === 'RUNNING') return 'ok';
  if (state === 'TRIPPED') return 'bad';
  if (state === 'STOPPED') return 'mute';
  return 'warn';
}

// The control affordances on this screen are read from the availability the host itself publishes:
// sequence.controls carries one {enabled, reason} pair per control. A disabled control therefore shows
// the host's own reason, and the panel states what PR #47 would have to add before any of them could
// move - a command route that does not exist in this build.
export function renderMaintenanceLock(root, snapshot) {
  clear(root);
  const sequence = snapshot ? snapshot.sequence : null;
  const controls = sequence ? sequence.controls : null;
  const mode = sequence ? text(sequence.mode, NOT_PUBLISHED) : NOT_PUBLISHED;
  paragraph(root, 'manual-mode', 'auto sequence mode: ' + mode
    + '  |  in RUNNING and PAUSE_REQUESTED the manual surface is locked by the Runtime itself');
  if (!controls) {
    paragraph(root, 'empty-state', 'no control availability published in this Snapshot');
    return;
  }
  const entries = [
    ['start', 'Start'],
    ['pauseAfterCurrentJob', 'Pause after current Job'],
    ['resume', 'Resume'],
    ['abortActiveJob', 'Abort active Job'],
    ['resetCritical', 'Reset Critical'],
    ['pumpStart', 'Pump start'],
  ];
  const grid = block(root, 'control-grid');
  for (const [key, label] of entries) {
    const control = controls[key];
    const cell = block(grid, 'control-cell');
    cell.dataset.tone = control && control.enabled ? 'ready' : 'locked';
    const name = document.createElement('p');
    name.className = 'control-name';
    name.textContent = label;
    cell.appendChild(name);
    const state = document.createElement('p');
    state.className = 'control-state';
    state.textContent = (control && control.enabled ? 'AVAILABLE IN THE CONTRACT' : 'LOCKED')
      + ' - ' + text(control ? control.reason : null, 'no availability published');
    cell.appendChild(state);
    const gate = document.createElement('p');
    gate.className = 'control-gate';
    gate.textContent = 'read-only build: no POST route exists, so this stays display-only until PR #47 lands a command surface';
    cell.appendChild(gate);
  }
}

// ---------------------------------------------------------------- Tab 1: active cockpit
//
// The cockpit is the Job the host says is running, in the words the host supplies, plus the only two
// pressure figures published for it. The buttons are the published availability of the six sequence
// controls read back as text: this build has no command route, so a pressed button would be a lie, and
// the two affordances that do exist are display choices of this screen (which Sensor is in focus, and
// whether the wall overlay is showing).
export function renderActiveCockpit(root, snapshot, view, onFocusActive, onToggleOverlay) {
  clear(root);
  const active = snapshot ? snapshot.activeJob : null;
  if (!active) {
    paragraph(root, 'empty-state', 'no active Job published: the cockpit shows nothing because the host reports nothing');
    return;
  }
  const stage = active.phaseLabel || active.phase;
  const line = paragraph(root, 'cockpit-line',
    'CLEANING NOW: ' + text(active.targetSensorId, '?') + '  |  jet ' + text(active.jetId, '?')
    + '  |  IV ' + text(active.valveId, '?') + ' ' + text(active.valveOpenCommand, NOT_PUBLISHED)
    + '  |  stage ' + text(stage, NOT_PUBLISHED)
    + '  |  phase progress ' + (typeof active.phaseProgress === 'number'
      ? active.phaseProgress.toFixed(2) + ' of 1 (a presentation-only fraction, never a per cent)'
      : NOT_PUBLISHED));
  line.dataset.tone = 'info';
  const rows2 = block(root, 'cockpit-facts');
  rows(rows2, [
    ['lifecycle', text(active.lifecycle, NOT_PUBLISHED)],
    ['cleaning phase', text(active.cleaningPhase, NOT_PUBLISHED)],
    ['pump outlet', number(active.pumpOutletPressureBar, 2, 'bar') + ' (' + text(active.pumpPressureQuality, NOT_PUBLISHED) + ')'],
    ['valve outlet', number(active.valveOutletPressureBar, 2, 'bar') + ' (' + text(active.valvePressureQuality, NOT_PUBLISHED) + ')'],
    ['setpoint', number(active.pumpReadySetpointBar, 2, 'bar')],
    ['diagnosis', text(active.valveDiagnosis, NOT_PUBLISHED), 'mute'],
    ['safe return', text(active.safeReturn && active.safeReturn.step, 'none: no Mandatory Safe Return is running for this job'), 'mute'],
  ]);
  const actions = block(root, 'cockpit-actions');
  const focus = document.createElement('button');
  focus.type = 'button';
  focus.className = 'cockpit-btn';
  focus.textContent = 'Focus Active - select ' + text(active.targetSensorId, 'the active target');
  focus.dataset.action = 'focus-active';
  actions.appendChild(focus);
  const overlay = document.createElement('button');
  overlay.type = 'button';
  overlay.className = 'cockpit-btn';
  overlay.dataset.action = 'toggle-overlay';
  overlay.setAttribute('aria-pressed', view.overlay === 'cleaning' ? 'true' : 'false');
  overlay.textContent = 'INSPECTING (' + text(active.targetSensorId, '-') + ') / CLEANING NOW';
  actions.appendChild(overlay);
  const coal = paragraph(root, 'cockpit-missing', 'COAL FLOW: ' + NOT_PUBLISHED + ' - the read-only Runtime publishes no boiler load channel, so no flow figure appears anywhere on this screen');
  coal.dataset.tone = 'warn';
  // Bound to the row this paint created, never to the host: the host survives every repaint, and a
  // listener added to it once a second is a second, third and hundredth handler for one click.
  actions.addEventListener('click', (event) => {
    const target = event.target;
    const button = target && target.closest ? target.closest('.cockpit-btn') : null;
    if (!button || !button.dataset.action) return;
    if (button.dataset.action === 'focus-active') onFocusActive();
    if (button.dataset.action === 'toggle-overlay') onToggleOverlay();
  });
}

// Boiler-condition delay bands paired with the selected Sensor's score pair. The bands are the four
// ranges the workspace configuration publishes for the condition in force, and the score is the
// published dirty / last-validated pair of one Sensor: two scales, both named, neither converted into
// the other.
export function renderDelayBands(canvas, caption, snapshot, workspace) {
  if (!canvas || !caption) return;
  const ctx = contextOf(canvas);
  if (!ctx) {
    caption.textContent = 'delay bands: canvas not available in this browser';
    return;
  }
  const configuration = workspace ? workspace.configuration : null;
  const ranges = configuration && Array.isArray(configuration.boilerRanges) ? configuration.boilerRanges : [];
  const frame = frameOf(canvas, { dualAxis: true, captions: true });
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  if (!frame) {
    caption.textContent = 'delay bands: canvas has no frame to draw into';
    return;
  }
  if (!ranges.length) {
    caption.textContent = 'delay bands: ' + NOT_PUBLISHED + ' - this workspace carries no Boiler Condition ranges';
    return;
  }
  const box = { x0: frame.x0, y0: frame.y0, x1: frame.x1, y1: frame.y1, w: frame.x1 - frame.x0, h: frame.y1 - frame.y0 };
  const low = Math.min.apply(null, ranges.map((range) => range.autoDelayMinSeconds));
  const high = Math.max.apply(null, ranges.map((range) => range.autoDelayMaxSeconds));
  const active = configuration ? text(configuration.activeBoilerCondition, NOT_PUBLISHED) : NOT_PUBLISHED;
  for (let index = 0; index < ranges.length; index += 1) {
    const range = ranges[index];
    const lane = box.y0 + (index + 0.18) * (box.h / ranges.length);
    const laneHeight = (box.h / ranges.length) * 0.64;
    const from = box.x0 + ((range.autoDelayMinSeconds - low) / Math.max(1, high - low)) * box.w;
    const to = box.x0 + ((range.autoDelayMaxSeconds - low) / Math.max(1, high - low)) * box.w;
    ctx.save();
    ctx.fillStyle = text(range.condition, '') === active ? 'rgba(227, 179, 65, 0.32)' : 'rgba(154, 165, 177, 0.14)';
    ctx.fillRect(from, lane, Math.max(2, to - from), laneHeight);
    ctx.strokeStyle = text(range.condition, '') === active ? 'rgba(227, 179, 65, 0.95)' : 'rgba(154, 165, 177, 0.5)';
    ctx.lineWidth = 1;
    ctx.strokeRect(from, lane, Math.max(2, to - from), laneHeight);
    ctx.fillStyle = 'rgba(230, 237, 243, 0.9)';
    ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
    ctx.textBaseline = 'middle';
    ctx.fillText(text(range.condition, '?') + '  ' + String(range.autoDelayMinSeconds) + '-' + String(range.autoDelayMaxSeconds) + ' s',
      box.x0 + 4, lane + laneHeight / 2);
    ctx.restore();
  }
  const preview = workspace ? workspace.preview : null;
  const applied = preview && typeof preview.appliedDelaySeconds === 'number' ? preview.appliedDelaySeconds : null;
  if (applied !== null) {
    const x = box.x0 + ((applied - low) / Math.max(1, high - low)) * box.w;
    ctx.save();
    ctx.strokeStyle = 'rgba(86, 211, 100, 0.95)';
    ctx.setLineDash([]);
    ctx.lineWidth = 1.6;
    ctx.beginPath();
    ctx.moveTo(x, box.y0);
    ctx.lineTo(x, box.y1);
    ctx.stroke();
    ctx.restore();
  }
  const selected = selectedSensorOf(snapshot);
  caption.textContent = 'delay bands ' + String(low) + '-' + String(high) + ' s  |  condition in force: ' + active
    + '  |  applied: ' + (applied === null ? NOT_PUBLISHED : String(applied) + ' s')
    + '  |  selected Sensor dirty score ' + (selected && typeof selected.dirtyScore === 'number' ? selected.dirtyScore.toFixed(1) : 'no score')
    + ', last validated ' + (selected && typeof selected.lastValidatedScore === 'number' ? selected.lastValidatedScore.toFixed(1) : 'no score')
    + (selected && selected.classification ? ' (' + String(selected.classification) + ')' : '');
}

function selectedSensorOf(snapshot) {
  const sensors = snapshot && Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  const active = snapshot ? snapshot.activeJob : null;
  const wanted = active ? active.targetSensorId : null;
  for (const sensor of sensors) {
    if (wanted && sensor && sensor.sensorId === wanted) return sensor;
  }
  return sensors.length ? sensors[0] : null;
}

// ---------------------------------------------------------------- Tab 4: Sensor & Path

export function renderSensorMaster(root, snapshot, selectedId, activeTargetId, onSelect) {
  clear(root);
  const sensors = snapshot && Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  if (!sensors.length) {
    paragraph(root, 'empty-state', 'no Sensor set published in this Snapshot');
    return;
  }
  const list = block(root, 'sds-master-list');
  for (const sensor of sensors) {
    const row = document.createElement('button');
    row.type = 'button';
    row.className = 'sds-row';
    row.dataset.sensorId = String(sensor.sensorId);
    if (sensor.sensorId === selectedId) row.setAttribute('aria-current', 'true');
    const id = document.createElement('span');
    id.className = 'sds-row-id';
    id.textContent = text(sensor.sensorId, '?');
    const place = document.createElement('span');
    place.className = 'sds-row-place';
    place.textContent = text(sensor.wall, '?') + ' ' + String(sensor.logicalRow) + '/' + String(sensor.logicalColumn)
      + '  ' + tierOfRow(sensor.logicalRow);
    const jet = document.createElement('span');
    jet.className = 'sds-row-jet';
    jet.textContent = text(sensor.assignedWaterJetId, 'no jet') + ' / ' + text(sensor.assignedIsolationValveId, 'no IV');
    row.appendChild(id);
    row.appendChild(place);
    row.appendChild(jet);
    row.title = text(sensor.classification, NOT_PUBLISHED) + ' / ' + text(sensor.queueState, '?')
      + ' / ' + text(sensor.quality, '?');
    row.dataset.tone = sensor.classification === 'DIRTY' ? 'bad' : sensor.classification === 'CLEANER' ? 'ok' : 'warn';
    if (sensor.sensorId === activeTargetId) row.setAttribute('aria-current', 'true');
    list.appendChild(row);
  }
  list.addEventListener('click', (event) => {
    const target = event.target;
    const row = target && target.closest ? target.closest('.sds-row') : null;
    if (row && row.dataset.sensorId) onSelect(row.dataset.sensorId);
  });
}

// The 2D workspace, 1:1 and square: 0..280 mm on both axes, the canonical Safe Return centre drawn
// where the contract fixes it, and a marker for the Sensor the operator is inspecting plus the active
// target. Positions of P1..P6 are not published, so nothing is plotted for them - the six stage rows
// live in the table beside this canvas, where the published stage and state are shown as they are.
export function renderWorkspace(canvas, caption, snapshot, selectedSensor, activeTarget) {
  if (!canvas || !caption) return;
  const ctx = contextOf(canvas);
  if (!ctx) {
    caption.textContent = 'workspace: canvas not available in this browser';
    return;
  }
  const box = { x0: 18, y0: 18, x1: canvas.width - 18, y1: canvas.height - 18 };
  box.w = box.x1 - box.x0;
  box.h = box.y1 - box.y0;
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  ctx.save();
  ctx.strokeStyle = 'rgba(120, 140, 160, 0.55)';
  ctx.lineWidth = 1;
  ctx.strokeRect(box.x0, box.y0, box.w, box.h);
  ctx.restore();
  drawGrid(ctx, box, 0.25, 'soft');

  const extent = WORKSPACE_MAX_MM - WORKSPACE_MIN_MM;
  const centreFx = (SAFE_RETURN_CENTRE_MM.horizontal - WORKSPACE_MIN_MM) / extent;
  const centreFy = (SAFE_RETURN_CENTRE_MM.vertical - WORKSPACE_MIN_MM) / extent;
  const cx = box.x0 + centreFx * box.w;
  const cy = box.y1 - centreFy * box.h;
  ctx.save();
  ctx.strokeStyle = 'rgba(121, 192, 255, 0.95)';
  ctx.lineWidth = 1.4;
  ctx.beginPath();
  ctx.moveTo(cx - 9, cy);
  ctx.lineTo(cx + 9, cy);
  ctx.moveTo(cx, cy - 9);
  ctx.lineTo(cx, cy + 9);
  ctx.stroke();
  ctx.beginPath();
  ctx.arc(cx, cy, 5.5, 0, Math.PI * 2);
  ctx.stroke();
  ctx.restore();

  // Scale marks first: 0, 70, 140, 210 and 280 mm on both axes, which is geometry the contract defines.
  const markers = [];
  for (let mm = WORKSPACE_MIN_MM; mm <= WORKSPACE_MAX_MM; mm += 70) {
    markers.push({ label: String(mm), fx: (mm - WORKSPACE_MIN_MM) / extent, fy: 0, width: 24, height: 11, slot: 'bottom' });
    markers.push({ label: String(mm), fx: 0, fy: (mm - WORKSPACE_MIN_MM) / extent, width: 24, height: 11, slot: 'left' });
  }
  // Then the one position this product fixes in contract, and nothing else: no payload of the read-only
  // Runtime carries a millimetre position for a Sensor or for a stage, so no dot is drawn for either.
  // Plotting a Sensor on this frame would mean choosing a coordinate for it, and a chosen coordinate
  // that no contract carries is exactly the kind of mark an operator would later trust.
  markers.push({ label: 'Centre ' + String(SAFE_RETURN_CENTRE_MM.horizontal) + ', ' + String(SAFE_RETURN_CENTRE_MM.vertical), fx: centreFx, fy: centreFy, width: 104, height: 12, tone: 'info' });
  const placed = placeLabels(markers, box, 6, 3);

  ctx.save();
  ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
  ctx.textBaseline = 'middle';
  for (const label of placed) {
    ctx.strokeStyle = 'rgba(154, 165, 177, 0.55)';
    ctx.beginPath();
    ctx.moveTo(label.anchorX, label.anchorY);
    ctx.lineTo(label.x + 2, label.y);
    ctx.stroke();
    ctx.fillStyle = label.tone === 'cyan' ? 'rgba(121, 192, 255, 0.95)' : label.tone === 'amber' ? 'rgba(227, 179, 65, 0.95)' : 'rgba(230, 237, 243, 0.92)';
    ctx.fillText(label.label, label.x, label.y);
  }
  ctx.restore();

  // The identity of the selection and of the active target is printed as text: both are published,
  // neither has a published position, and an identity is worth showing while a position is not.
  caption.textContent = 'workspace ' + String(WORKSPACE_MIN_MM) + '-' + String(WORKSPACE_MAX_MM)
    + ' mm on both axes, 1:1 square - ' + SCALING_NOTE
    + '  |  Centre fixed by contract at ' + String(SAFE_RETURN_CENTRE_MM.horizontal) + ' mm, '
    + String(SAFE_RETURN_CENTRE_MM.vertical) + ' mm = ' + grouped(SAFE_RETURN_CENTRE_NATIVE.horizontal) + ' native';
  return {
    selected: selectedSensor ? 'selected ' + String(selectedSensor) + ' - no published position' : 'nothing selected',
    activeTarget: activeTarget ? 'active target ' + String(activeTarget) + ' - no published position' : 'no active target',
  };
}

export function renderPathTable(root, workspace) {
  clear(root);
  paragraph(root, 'scale-note', SCALING_NOTE + '  |  1 mm is never rendered as a pulse count');
  const timeline = workspace ? workspace.timeline : null;
  const steps = timeline && Array.isArray(timeline.steps) ? timeline.steps : [];
  if (!steps.length) {
    paragraph(root, 'empty-state', 'no Cleaning Job Workspace timeline published - the stage rows below carry the canonical order only');
  }
  paragraph(root, 'coord-note', STAGE_COORDINATE_NOTE);
  const body = [];
  for (const stage of COCKPIT_STAGES) {
    const step = byId(steps, 'stage', stage.stage);
    body.push({
      cells: [
        stage.key,
        stage.stage,
        step ? String(step.ordinal) : '- not in this workspace -',
        step ? text(step.label, NOT_PUBLISHED) : NOT_PUBLISHED,
        step ? text(step.state, NOT_PUBLISHED) : NOT_PUBLISHED,
        stage.key === 'CENTRE' || stage.key === 'SAFE_RETURN'
          ? coordinatePair(SAFE_RETURN_CENTRE_MM.horizontal).text
          : 'mm ' + NOT_PUBLISHED,
        stage.key === 'CENTRE' || stage.key === 'SAFE_RETURN'
          ? grouped(SAFE_RETURN_CENTRE_NATIVE.horizontal) + ' / ' + grouped(SAFE_RETURN_CENTRE_NATIVE.vertical)
          : 'Native (x' + grouped(NATIVE_PER_MILLIMETRE) + ') ' + NOT_PUBLISHED,
      ],
      tone: step ? (step.state === 'COMPLETE' ? 'ok' : 'info') : 'mute',
    });
  }
  table(root, ['position', 'published stage', 'ordinal', 'label', 'state', 'mm', 'native'], body);
  const keys = CLEANING_PATH_STAGES.map((stage) => stage.stage).join(', ');
  paragraph(root, 'stage-keys', 'stage identities indexed by this panel: ' + keys);
}

// ---------------------------------------------------------------- Tab 6: System & Trends

export function renderSystemSummary(root, snapshot, status, health, connection) {
  clear(root);
  const runtime = snapshot ? snapshot.runtime : null;
  const historian = runtime ? runtime.historian : null;
  const config = snapshot ? snapshot.config : null;
  const communication = snapshot ? snapshot.communication : null;
  const devices = communication && Array.isArray(communication.devices) ? communication.devices : [];

  rows(root, [
    ['host', health ? text(health.host, NOT_PUBLISHED) : 'not read yet', 'mute'],
    ['liveness', health ? text(health.status, NOT_PUBLISHED) : NOT_PUBLISHED, health && health.status === 'ALIVE' ? 'ok' : 'warn'],
    ['profile', health ? text(health.deviceProfile, NOT_PUBLISHED) : (status ? text(status.profile, NOT_PUBLISHED) : NOT_PUBLISHED)],
    ['acquisition', health ? text(health.acquisitionMode, NOT_PUBLISHED) : NOT_PUBLISHED],
    ['readiness (health)', health ? (health.ready ? 'READY' : 'NOT READY') : NOT_PUBLISHED, health && health.ready ? 'ok' : 'warn'],
    ['readiness (status)', status ? text(status.readinessCode, NOT_PUBLISHED) : NOT_PUBLISHED, 'mute'],
    ['readiness detail', status ? text(status.readinessDetail, NOT_PUBLISHED) : NOT_PUBLISHED, 'mute'],
    ['scenario', status ? text(status.scenario, NOT_PUBLISHED) : NOT_PUBLISHED],
    ['stage', status ? text(status.stageMarker, NOT_PUBLISHED) : (health ? text(health.stageMarker, NOT_PUBLISHED) : NOT_PUBLISHED)],
    ['revision (snapshot)', snapshot ? String(snapshot.revision) : 'none', 'mute'],
    ['revision (health)', health && health.currentRevision !== null && health.currentRevision !== undefined ? String(health.currentRevision) : NOT_PUBLISHED],
    ['generated at', snapshot ? timeOf(snapshot.generatedAt) : NOT_PUBLISHED],
    ['server time', status ? timeOf(status.serverTimeUtc) : NOT_PUBLISHED],
    ['tick', status ? number(status.tickIntervalMilliseconds, 0, 'ms') : NOT_PUBLISHED],
    ['evolution', status ? (status.evolutionRunning ? 'RUNNING' : 'STOPPED') : NOT_PUBLISHED, status && status.evolutionRunning ? 'ok' : 'warn'],
    ['accepted ticks', status ? String(status.acceptedTicks) : NOT_PUBLISHED],
    ['rejected transitions', status ? String(status.rejectedTransitions) : NOT_PUBLISHED, status && status.rejectedTransitions > 0 ? 'warn' : 'mute'],
    ['uptime', runtime ? number(runtime.uptimeSeconds, 0, 's') : NOT_PUBLISHED],
    ['invariant violations', runtime ? String(runtime.invariantViolations) : NOT_PUBLISHED, runtime && runtime.invariantViolations > 0 ? 'bad' : 'ok'],
    ['second Jobs accepted', runtime ? String(runtime.acceptedSecondJobs) : NOT_PUBLISHED],
    ['second Jobs refused', runtime ? String(runtime.refusedSecondJobs) : NOT_PUBLISHED],
    ['SSE clients', runtime ? String(runtime.sseClients) : NOT_PUBLISHED, 'mute'],
    ['config revision', config ? String(config.revision) : NOT_PUBLISHED],
    ['config label', config ? text(config.label, NOT_PUBLISHED) : NOT_PUBLISHED],
    ['dirty threshold', config ? number(config.dirtyThreshold, 2) : NOT_PUBLISHED],
    ['stale threshold', config ? number(config.staleThresholdMs, 0, 'ms') : NOT_PUBLISHED],
    ['feed', connection ? text(connection.label, 'unknown') : 'unknown', connection && connection.online ? 'ok' : 'bad'],
  ]);

  if (historian) {
    heading(root, 'Historian');
    rows(root, [
      ['wired', historian.wired ? 'YES' : 'NO', historian.wired ? 'ok' : 'mute'],
      ['depth / capacity', String(historian.depth) + ' / ' + String(historian.capacity)],
      ['near overflow', historian.nearOverflow ? 'YES' : 'NO', historian.nearOverflow ? 'bad' : 'ok'],
      ['rejected', String(historian.rejected), historian.rejected > 0 ? 'warn' : 'ok'],
      ['gap markers', String(historian.gapMarkers), historian.gapMarkers > 0 ? 'warn' : 'ok'],
      ['last batch latency', historian.lastBatchLatencyMs === null || historian.lastBatchLatencyMs === undefined ? 'no batch yet' : number(historian.lastBatchLatencyMs, 0, 'ms')],
    ]);
  }

  if (status) {
    heading(root, 'Counters the host reports about itself');
    const counts = status.sensorCounts;
    rows(root, [
      ['logical slots', counts ? text(counts.logicalSlots.label, NOT_PUBLISHED) + ' = ' + String(counts.logicalSlots.count) : NOT_PUBLISHED],
      ['Sensor locations', counts ? text(counts.sensorLocations.label, NOT_PUBLISHED) + ' = ' + String(counts.sensorLocations.count) : NOT_PUBLISHED],
      ['TC channels', counts ? text(counts.thermocoupleChannels.label, NOT_PUBLISHED) + ' = ' + String(counts.thermocoupleChannels.count) : NOT_PUBLISHED],
      ['physical TC', counts ? text(counts.physicalThermocoupleChannels.label, NOT_PUBLISHED) + ' = ' + String(counts.physicalThermocoupleChannels.count) : NOT_PUBLISHED],
      ['unassigned TC', counts ? text(counts.unassignedThermocoupleChannels.label, NOT_PUBLISHED) + ' = ' + String(counts.unassignedThermocoupleChannels.count) : NOT_PUBLISHED],
      ['Water Jets', counts ? String(counts.waterJets.count) : NOT_PUBLISHED],
      ['Isolation Valves', counts ? String(counts.isolationValves.count) : NOT_PUBLISHED],
      ['queue', status.queueCount + ' queued of ' + status.queueCapacity + ' (' + text(status.queuePlaceholder, 'no placeholder') + ')'],
      ['active Job', status.activeJobPresent ? text(status.activeJobId, 'present') : 'none'],
      ['disabled Sensors', status.disabledSensorIds.length ? String(status.disabledSensorIds.length) : 'none'],
      ['alarm counts', 'active ' + String(status.activeAlarmCount) + ', cleared-unack ' + String(status.clearedAlarmCount)],
      ['delta history', String(status.deltaHistoryDepth) + ' / ' + String(status.deltaHistoryCapacity) + ', newest rev ' + (status.newestDeltaRevision === null || status.newestDeltaRevision === undefined ? 'none' : String(status.newestDeltaRevision))],
      ['state history', String(status.stateHistoryDepth) + ' / ' + String(status.stateHistoryCapacity)],
      ['last fault', text(status.lastFaultCode, 'none') + ' - ' + text(status.lastFaultDetail, 'no detail'), status.lastFaultCode ? 'bad' : 'ok'],
      ['startup fault', text(status.startupFaultCode, 'none'), status.startupFaultCode ? 'bad' : 'ok'],
      ['acquisition label', counts && counts.equipmentTopology ? text(counts.equipmentTopology.acquisition, NOT_PUBLISHED) : NOT_PUBLISHED, 'mute'],
    ]);
  }

  heading(root, 'Device links');
  if (!devices.length) {
    paragraph(root, 'empty-state', 'no device link health published in this Snapshot');
  } else {
    table(root, ['device', 'state', 'timeouts', 'last success', 'latency', 'polls ok', 'polls failed'], devices.map((device) => ({
      cells: [
        text(device.deviceId, '?'),
        text(device.state, NOT_PUBLISHED),
        String(device.consecutiveTimeouts),
        timeOf(device.lastSuccessAt),
        device.lastLatencyMs === null || device.lastLatencyMs === undefined ? 'not measured' : number(device.lastLatencyMs, 0, 'ms'),
        String(device.pollsOk),
        String(device.pollsFailed),
      ],
      tone: device.state === 'ONLINE' ? 'ok' : device.state === 'TIMEOUT' ? 'bad' : 'warn',
    })));
  }
}

export function renderWallBreakdown(root, snapshot) {
  clear(root);
  const summary = summaryPills(snapshot ? snapshot.walls : null);
  if (summary.state === 'no data') {
    paragraph(root, 'empty-state', summary.note);
    return;
  }
  const body = summary.perWall.map((wall) => ({
    cells: [
      text(wall.wall, '?'),
      String(wall.total),
      String(wall.cleaner),
      String(wall.dirty),
      String(wall.uncertain),
      String(wall.notClassified),
      wall.maxScore === null || wall.maxScore === undefined ? 'no score' : number(wall.maxScore, 2),
    ],
    tone: wall.total === EXPECTED_PER_WALL[wall.wall] ? 'ok' : 'warn',
  }));
  body.push({
    cells: ['TOTAL', String(summary.total), String(summary.pills[0].value), String(summary.pills[1].value), summary.breakdown, '-', '-'],
    tone: summary.state === 'mismatch' ? 'bad' : 'ok',
  });
  table(root, ['wall', 'total', 'cleaner', 'dirty', 'uncertain + not classified', 'max score', ''], body);
  paragraph(root, 'breakdown-note', summary.note);
}

// The explorer draws the only history the read-only API carries: the bounded trend window of the
// Snapshot, whose published series are pressure, setpoint, ready-band-low and ready-band-high. Each
// channel toggle states its engineering range and its live value, because a normalised plot that hides
// its units is how a bar becomes a per cent without anyone deciding that.
export function renderTrendExplorer(canvas, caption, note, snapshot, view) {
  if (!canvas || !caption || !note) return;
  const trend = snapshot ? snapshot.trend : null;
  const points = trend && Array.isArray(trend.points) ? trend.points : [];
  const names = trend && Array.isArray(trend.seriesNames) ? trend.seriesNames : [];
  if (!names.length) {
    caption.textContent = 'trend explorer: no series names published in this Snapshot';
    note.textContent = NOT_PUBLISHED;
    return;
  }
  const ctx = contextOf(canvas);
  if (!ctx) {
    caption.textContent = 'trend explorer: canvas not available in this browser';
    return;
  }
  const enabled = names.map((name) => view.enabled.indexOf(name) >= 0);
  const series = names.map((name, index) => ({
    name,
    unit: 'bar',
    values: points.map((point) => {
      const raw = point && Array.isArray(point.series) ? point.series[index] : null;
      return point ? { t: point.t, value: typeof raw === 'number' ? raw : null } : null;
    }),
  }));
  const shown = series.filter((entry, index) => enabled[index]);
  const range = rangeOf(shown);
  const timeRange = timeRangeOf(points.filter(Boolean).map((point) => ({ t: point.t, value: 0 })), 60);
  const frame = frameOf(canvas, { dualAxis: false, captions: false });
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  if (!frame || !range || !timeRange) {
    caption.textContent = 'trend explorer: ' + String(points.length) + ' samples, nothing on an enabled channel to plot';
    note.textContent = 'a channel with every sample missing is a DATA GAP, not a flat line';
    return;
  }
  const mapper = mapperFor(frame, range, timeRange);
  ctx.save();
  ctx.lineWidth = 1.5;
  for (const entry of shown) {
    ctx.strokeStyle = entry.name === 'pressure' ? 'rgba(86, 211, 100, 0.95)'
      : entry.name === 'setpoint' ? 'rgba(227, 179, 65, 0.95)'
        : 'rgba(121, 192, 255, 0.85)';
    drawSeries(ctx, entry.values, mapper);
  }
  ctx.restore();
  axisOf(ctx, frame, range, 'bar', 'left');

  const span = { min: 0, max: 100 };
  const normalized = series.map((entry) => normalise(entry.values, range));
  caption.textContent = captionFor(shown.length ? shown : series, points, trend.capacity)
    + '  |  vertical: ' + formatTick(range.min) + ' to ' + formatTick(range.max) + ' bar'
    + '  |  0-100% span ' + formatTick(span.min) + ' to ' + formatTick(span.max);
  note.textContent = 'the Runtime publishes a bounded window of ' + String(trend.capacity)
    + ' samples for these four channels and nothing else: no coal flow, no per-Sensor temperature, no '
    + 'isolation-valve history. Those rows below say ' + NOT_PUBLISHED + ' and stay that way until a '
    + 'contract carries them - this screen does not draw a line to fill a blank.';
  return { series: series, normalized: normalized, enabled: enabled };
}

export function renderChannelToggles(root, snapshot, view, onToggle) {
  clear(root);
  const group = document.createElement('div');
  group.className = 'channel-group';
  root.appendChild(group);
  const trend = snapshot ? snapshot.trend : null;
  const names = trend && Array.isArray(trend.seriesNames) ? trend.seriesNames : [];
  if (!names.length) {
    paragraph(root, 'empty-state', 'no trend channels published in this Snapshot');
    return;
  }
  for (let index = 0; index < names.length; index += 1) {
    const name = names[index];
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'channel';
    button.dataset.channel = name;
    button.setAttribute('aria-pressed', view.enabled.indexOf(name) >= 0 ? 'true' : 'false');
    const label = document.createElement('span');
    label.className = 'channel-name';
    label.textContent = name;
    const live = document.createElement('span');
    live.className = 'channel-live';
    live.textContent = liveValueLabel(trend, index);
    button.appendChild(label);
    button.appendChild(live);
    group.appendChild(button);
  }
  for (const absent of ['coal-flow t/h', 'SDS temperature degC', 'IV pressure history', 'per-Sensor TC degC']) {
    const row = document.createElement('p');
    row.className = 'channel-absent';
    row.textContent = absent + ': ' + NOT_PUBLISHED;
    group.appendChild(row);
  }
  // Same rule as the cockpit: the group is created by this paint, so its listener count is one.
  group.addEventListener('click', (event) => {
    const target = event.target;
    const button = target && target.closest ? target.closest('.channel') : null;
    if (button && button.dataset.channel) onToggle(button.dataset.channel);
  });
}

function liveValueLabel(trend, index) {
  const points = trend && Array.isArray(trend.points) ? trend.points : [];
  for (let cursor = points.length - 1; cursor >= 0; cursor -= 1) {
    const point = points[cursor];
    const raw = point && Array.isArray(point.series) ? point.series[index] : null;
    if (typeof raw === 'number') return 'live ' + formatted(raw, 'bar');
  }
  return 'live DATA GAP';
}

// ---------------------------------------------------------------- Tab 1(d): camera viewport container
//
// A mount point, not a stream. There is no image or video route on this host, so the panel reserves the
// box, names the Water Jet the view would belong to, and says plainly that nothing is wired. It makes
// no request of any kind: no img element, no source, no address, and no URL that a scanner would have
// to forgive. When a camera surface exists, it appears inside this container and nowhere else.
export function renderCameraPanel(root, snapshot) {
  clear(root);
  const active = snapshot ? snapshot.activeJob : null;
  const jetId = active ? text(active.jetId, 'no active Job') : 'no active Job';
  const sensorId = active ? text(active.targetSensorId, NOT_PUBLISHED) : NOT_PUBLISHED;
  const jet = active && active.jetId ? byId(snapshot.waterJets || [], 'waterJetId', active.jetId) : null;
  root.dataset.syncedJet = active && active.jetId ? String(active.jetId) : 'none';
  const frame = block(root, 'camera-frame');
  frame.setAttribute('role', 'img');
  frame.setAttribute('aria-label', 'Camera viewport placeholder for ' + jetId);
  paragraph(frame, 'camera-title', 'IP Camera - ' + jetId);
  paragraph(frame, 'camera-sentence', 'NO CAMERA SOURCE - THE READ-ONLY RUNTIME HAS NO IMAGE ROUTE');
  const facts = block(root, 'camera-facts');
  rows(facts, [
    ['synced to', jetId, active ? 'info' : 'mute'],
    ['cleaning', sensorId],
    ['installed wall', jet ? text(jet.installedWall, NOT_PUBLISHED) + '/' + text(jet.installedRegion, NOT_PUBLISHED) : NOT_PUBLISHED],
    ['target wall', jet ? text(jet.targetWall, NOT_PUBLISHED) + '/' + text(jet.targetRegion, NOT_PUBLISHED) : NOT_PUBLISHED],
    ['mount', 'container ready: a future stream surface renders inside this box and nowhere else'],
    ['requests', 'zero - no image element, no source, no address, no request'],
  ]);
  paragraph(root, 'camera-note',
    'the panel is deliberately empty rather than illustrative: a still frame or a mock would read as a '
    + 'live view on a wall, and a wall screen that is trusted once has to be right about what it is not showing');
}
