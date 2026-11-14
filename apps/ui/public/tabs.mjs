// WJSS Operator presentation - Tabs 1-6 renderers and the IP Camera mount container.
//
// The rule that governs this whole file: a panel shows a published field, or it shows that the field is
// not published. A "not published" line is information an operator can act on; a plausible number that
// no contract carries is not. Nothing here fetches, nothing here composes a request, and nothing here
// draws a control that could.

import { text, number, timeOf } from './feed.mjs';
import {
  AXIS_ROLES,
  AXIS_WORDING,
  BRAKE_OUTPUT_ORDER,
  CLEANING_PATH_STAGES,
  COCKPIT_STAGES,
  DEFAULT_ASSIGNMENTS,
  EXPECTED_PER_WALL,
  NATIVE_PER_MILLIMETRE,
  PUMP_STOP_ORDER_RULE,
  SAFE_RETURN_CENTRE_MM,
  SAFE_RETURN_CENTRE_NATIVE,
  SCALING_NOTE,
  STAGE_COORDINATE_NOTE,
  WATER_JETS,
  WATER_JETS_BY_ID,
  WORKSPACE_MAX_MM,
  WORKSPACE_MIN_MM,
  WSB_CABINETS,
  WSB_SOURCE_NOTE,
  agreeWithSnapshot,
  assignmentCountByJet,
  coordinatePair,
  grouped,
  summaryPills,
  tierOfRow,
} from './topology.mjs';
import { partitionSensors } from './wallMap.mjs';
import {
  axisOf,
  captionFor,
  drawGrid,
  drawSeries,
  drawTimeAxis,
  formatTick,
  formatted,
  frameOf,
  mapperFor,
  normalise,
  prepareCanvas,
  rangeOf,
  timeRangeOf,
} from './chartKit.mjs';

const NOT_PUBLISHED = 'NOT_PUBLISHED_BY_RUNTIME';

function clear(node) {
  while (node.firstChild) node.removeChild(node.firstChild);
}

function block(parent, className, textValue) {
  const node = document.createElement('div');
  node.className = className;
  if (textValue !== undefined) node.textContent = textValue;
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

function rows(hostNode, entries) {
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
  hostNode.appendChild(list);
  return list;
}

function kpiTile(parent, label, value, sub, tone) {
  const tile = document.createElement('div');
  tile.className = 'kpi-tile';
  if (tone) tile.dataset.tone = tone;
  const lbl = document.createElement('span');
  lbl.className = 'kpi-tile-lbl';
  lbl.textContent = label;
  const val = document.createElement('span');
  val.className = 'kpi-tile-val num';
  val.textContent = value;
  if (tone) val.dataset.tone = tone;
  const subEl = document.createElement('span');
  subEl.className = 'kpi-tile-sub num';
  subEl.textContent = sub;
  tile.appendChild(lbl);
  tile.appendChild(val);
  tile.appendChild(subEl);
  parent.appendChild(tile);
  return tile;
}

function table(hostNode, headers, body) {
  const element = document.createElement('table');
  element.className = 'grid-table num';
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
  hostNode.appendChild(element);
  return element;
}

function byId(list, key, value) {
  for (const item of list) {
    if (item && item[key] === value) return item;
  }
  return null;
}

// ---------------------------------------------------------------- Tab 3: WJ & Pump

function axisWording(letter) {
  return AXIS_WORDING[AXIS_ROLES[letter]] || ('axis ' + text(letter, '?'));
}

function publishedIv(published, canonicalIv) {
  const ivLabel = text(canonicalIv, 'IV?');
  if (!published) return ivLabel + ' (published identity unavailable)';
  return ivLabel + ' (published: ' + text(published.dedicatedIsolationValveId, 'none') + ')';
}

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
    ? 'TOPOLOGY AGREED: ' + agreement.reason + ' | ' + WSB_SOURCE_NOTE
    : 'TOPOLOGY DISAGREES WITH THE CANONICAL TABLE - drawn from published payload: ' + agreement.reason;
  note.dataset.tone = agreement.ok ? 'ok' : 'bad';

  const grid = block(root, 'wsb-grid');
  for (const cabinet of WSB_CABINETS) {
    const card = block(grid, 'wsb-card');
    card.dataset.wsb = cabinet.wsb;
    paragraph(card, 'wsb-name', cabinet.wsb + ' - ' + cabinet.controller);
    for (const binding of cabinet.jets) {
      const published = byId(waterJets, 'waterJetId', binding.wj);
      const canonicalJet = WATER_JETS_BY_ID[binding.wj] || null;
      const canonicalIv = canonicalJet ? canonicalJet.iv : 'IV?';
      const row = block(card, 'wsb-jet');
      row.dataset.tone = published ? 'ok' : 'bad';
      const horizontal = axisWording(binding.horizontalAxis);
      const vertical = axisWording(binding.verticalAxis);
      const title = document.createElement('p');
      title.className = 'wsb-jet-title';
      title.textContent = binding.wj + ' + ' + publishedIv(published, canonicalIv)
        + ' | Brake ' + String(BRAKE_OUTPUT_ORDER[binding.horizontalAxis]) + '+' + String(BRAKE_OUTPUT_ORDER[binding.verticalAxis]);
      row.appendChild(title);
      const axes = document.createElement('p');
      axes.className = 'wsb-jet-axes';
      axes.textContent = horizontal + ' ' + binding.horizontalAxis
        + ' | ' + vertical + ' ' + binding.verticalAxis + ' (A/C=Horiz, B/D=Vert)';
      row.appendChild(axes);
      if (published) {
        const covered = document.createElement('p');
        covered.className = 'wsb-jet-cover';
        covered.textContent = 'Installed ' + text(published.installedWall) + '/' + text(published.installedRegion)
          + ' -> Cleans ' + text(published.targetWall) + '/' + text(published.targetRegion)
          + ' | Default ' + String(DEFAULT_ASSIGNMENTS[binding.wj])
          + ', Published ' + String(assignments.assigned[binding.wj] === undefined ? 0 : assignments.assigned[binding.wj]);
        row.appendChild(covered);
        const placement = document.createElement('p');
        placement.className = 'wsb-jet-placement';
        placement.textContent = 'Placement ' + text(published.placementKind, NOT_PUBLISHED)
          + ' - ' + (Array.isArray(published.placementAnchors) && published.placementAnchors.length
            ? published.placementAnchors.join(', ')
            : 'no placement anchor held or inferred');
        row.appendChild(placement);
      } else {
        paragraph(row, 'wsb-jet-missing', 'this Water Jet is not present in the published payload');
      }
      if (active && active.jetId === binding.wj) {
        const live = document.createElement('p');
        live.className = 'wsb-jet-live';
        live.textContent = 'ACTIVE JOB: ' + text(active.phaseLabel, text(active.phase, NOT_PUBLISHED))
          + ' | ' + text(active.valveId, NOT_PUBLISHED) + ' ' + text(active.valveOpenCommand, NOT_PUBLISHED);
        row.appendChild(live);
      }
    }
  }
}

function toneForRunState(state) {
  if (state === 'RUNNING') return 'ok';
  if (state === 'TRIPPED') return 'bad';
  if (state === 'STOPPED') return 'mute';
  return 'warn';
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
  const strip = block(root, 'kpi-strip-5');
  kpiTile(strip, 'HP PUMP STATE', text(pump.state, NOT_PUBLISHED),
    pump.ready ? 'READY IN BAND' : 'NOT IN READY BAND', toneForRunState(pump.state));
  kpiTile(strip, 'OUTLET PRESSURE', number(pump.pressure, 2, 'bar'),
    'Setpoint ' + number(pump.setpoint, 2, 'bar'), pump.pressure === null ? 'mute' : 'info');
  kpiTile(strip, 'READY BAND', number(pump.readyBandLow, 1) + ' - ' + number(pump.readyBandHigh, 1) + ' bar',
    'Stop req: ' + timeOf(pump.stopRequestedAt), pump.ready ? 'ok' : 'warn');
  kpiTile(strip, 'ACTIVE VALVE OUTLET', active ? number(active.valveOutletPressureBar, 2, 'bar') : 'NO ACTIVE JOB',
    active ? text(active.pumpPressureSourceId, NOT_PUBLISHED) + ' | ' + text(active.pumpPressureQuality, NOT_PUBLISHED) : 'No active valve',
    active && active.pumpPressureInputValid ? 'ok' : 'mute');
  kpiTile(strip, 'HOST UPTIME', runtime ? number(runtime.uptimeSeconds, 0, 's') : NOT_PUBLISHED,
    'Stage ' + text(runtime ? runtime.stageMarker : null, NOT_PUBLISHED), 'mute');

  const rule = paragraph(root, 'interlock-rule', PUMP_STOP_ORDER_RULE);
  rule.dataset.tone = 'warn';
  paragraph(root, 'interlock-note',
    'The order above is the commissioned interlock rule this build was prepared against; the Runtime '
    + 'publishes pump state and the last stop request, and does not publish a per-output confirmation, '
    + 'so no line of this card claims the outputs were observed in that order.');
}

export function renderMaintenanceLock(root, snapshot) {
  clear(root);
  const sequence = snapshot ? snapshot.sequence : null;
  const controls = sequence ? sequence.controls : null;
  const mode = sequence ? text(sequence.mode, NOT_PUBLISHED) : NOT_PUBLISHED;
  paragraph(root, 'manual-mode', 'Auto sequence mode: ' + mode
    + ' | Read-only display of contract availability (no write command surface exists in this build).');
  if (!controls) {
    paragraph(root, 'empty-state', 'no control availability published in this Snapshot');
    return;
  }
  const entries = [
    ['start', 'Start'],
    ['pauseAfterCurrentJob', 'Pause After Job'],
    ['resume', 'Resume'],
    ['abortActiveJob', 'Abort Active Job'],
    ['resetCritical', 'Reset Critical'],
    ['pumpStart', 'Pump Start'],
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
    state.textContent = (control && control.enabled ? 'AVAILABLE' : 'LOCKED')
      + ' - ' + text(control ? control.reason : null, 'not published');
    cell.appendChild(state);
    const gate = document.createElement('p');
    gate.className = 'control-gate';
    gate.textContent = 'Display-only: read-only build has no command route.';
    cell.appendChild(gate);
  }
}

// ---------------------------------------------------------------- Tab 1: Active Cockpit Actions & Delay Bands

export function renderActiveCockpit(root, snapshot, view, onFocusActive, onToggleOverlay) {
  const active = snapshot ? snapshot.activeJob : null;
  const hasActive = Boolean(active && active.targetSensorId);
  const row = block(root, 'cockpit-meta-row');
  const actions = block(row, 'cockpit-actions');

  const focus = document.createElement('button');
  focus.type = 'button';
  focus.className = 'btn-sm primary cockpit-btn';
  focus.textContent = hasActive
    ? 'Focus Active (' + text(active.targetSensorId) + ')'
    : 'Focus Active (Standby)';
  focus.disabled = !hasActive;
  focus.dataset.action = 'focus-active';
  actions.appendChild(focus);

  const overlay = document.createElement('button');
  overlay.type = 'button';
  overlay.className = 'btn-sm cockpit-btn';
  overlay.dataset.action = 'toggle-overlay';
  if (!hasActive) {
    overlay.disabled = true;
    overlay.setAttribute('aria-pressed', 'false');
    overlay.textContent = 'Mode: STANDBY';
  } else {
    overlay.setAttribute('aria-pressed', view.overlay === 'cleaning' ? 'true' : 'false');
    overlay.textContent = view.overlay === 'cleaning' ? 'View: CLEANING NOW' : 'View: INSPECTING';
  }
  actions.appendChild(overlay);

  const coal = paragraph(row, 'cockpit-missing', 'COAL FLOW: ' + NOT_PUBLISHED);
  coal.title = 'The read-only Runtime publishes no boiler load channel, so no coal flow figure appears anywhere on this screen.';

  actions.addEventListener('click', (event) => {
    const target = event.target;
    const button = target && target.closest ? target.closest('.cockpit-btn') : null;
    if (!button || button.disabled || !button.dataset.action) return;
    if (button.dataset.action === 'focus-active') onFocusActive();
    if (button.dataset.action === 'toggle-overlay') onToggleOverlay();
  });
}

function selectedSensorOf(snapshot, selectedId) {
  const sensors = snapshot && Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  if (selectedId) {
    for (const sensor of sensors) {
      if (sensor && sensor.sensorId === selectedId) return sensor;
    }
  }
  const active = snapshot ? snapshot.activeJob : null;
  const wanted = active ? active.targetSensorId : null;
  for (const sensor of sensors) {
    if (wanted && sensor && sensor.sensorId === wanted) return sensor;
  }
  return sensors.length ? sensors[0] : null;
}

function sameWallPeers(snapshot, selected) {
  const sensors = snapshot && Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  if (!selected || !selected.wall) return [];
  const wall = String(selected.wall).toUpperCase();
  return sensors
    .filter((entry) => entry && String(entry.wall || '').toUpperCase() === wall
      && entry.sensorId !== selected.sensorId
      && typeof entry.dirtyScore === 'number' && Number.isFinite(entry.dirtyScore))
    .sort((a, b) => b.dirtyScore - a.dirtyScore)
    .slice(0, 3);
}

function drawScoreBarPanel(ctx, box, selected, peers, dirtyThresh) {
  ctx.strokeStyle = '#28303b';
  ctx.lineWidth = 1;
  ctx.strokeRect(box.x0, box.y0, box.w, box.h);

  const threshX = box.x0 + 6 + Math.min(1, Math.max(0, dirtyThresh / 100)) * Math.max(20, box.w - 12);
  ctx.save();
  ctx.fillStyle = '#94a3b8';
  ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
  ctx.textBaseline = 'top';
  ctx.fillText('SDS ' + (selected ? text(selected.sensorId) : '-') + ' vs Wall Peers (0-100)', box.x0 + 6, box.y0 + 5);

  const bars = [
    { label: selected ? text(selected.sensorId) + ' Cur' : 'Cur', value: selected ? selected.dirtyScore : null, color: '#e05246' },
    { label: selected ? text(selected.sensorId) + ' Val' : 'Val', value: selected ? selected.lastValidatedScore : null, color: '#38bdf8' },
  ];
  for (const peer of peers) {
    bars.push({
      label: text(peer.sensorId) + ' Peer',
      value: peer.dirtyScore,
      color: peer.dirtyScore >= dirtyThresh ? 'rgba(224, 82, 70, 0.55)' : 'rgba(46, 160, 109, 0.65)',
    });
  }

  const topOffset = 22;
  const bottomPad = 16;
  const availH = Math.max(28, box.h - topOffset - bottomPad);
  const rowPitch = Math.max(14, Math.floor(availH / Math.max(2, bars.length)));
  const barH = Math.max(10, Math.min(16, rowPitch - 4));
  const barW = Math.max(20, box.w - 12);

  for (let i = 0; i < bars.length; i += 1) {
    const item = bars[i];
    const yPos = box.y0 + topOffset + i * rowPitch;
    if (yPos + barH > box.y0 + box.h - bottomPad + 2) break;
    ctx.fillStyle = '#15191f';
    ctx.fillRect(box.x0 + 6, yPos, barW, barH);
    if (typeof item.value === 'number' && Number.isFinite(item.value)) {
      const fillW = Math.min(1, Math.max(0, item.value / 100)) * barW;
      ctx.fillStyle = item.color;
      ctx.fillRect(box.x0 + 6, yPos, fillW, barH);
    }
    ctx.fillStyle = '#f1f5f9';
    ctx.textBaseline = 'middle';
    ctx.fillText(item.label + ': ' + (typeof item.value === 'number' ? item.value.toFixed(1) : '--'),
      box.x0 + 10, yPos + barH / 2);
  }

  ctx.strokeStyle = '#fbbf24';
  ctx.lineWidth = 1;
  ctx.setLineDash([3, 3]);
  ctx.beginPath();
  ctx.moveTo(threshX, box.y0 + 18);
  ctx.lineTo(threshX, box.y0 + box.h - 14);
  ctx.stroke();
  ctx.setLineDash([]);

  ctx.fillStyle = '#fbbf24';
  ctx.textBaseline = 'bottom';
  ctx.fillText('Dirty Threshold: ' + String(dirtyThresh), box.x0 + 6, box.y0 + box.h - 3);
  ctx.restore();
}

export function renderDelayBands(canvas, caption, snapshot, workspace, selectedId, scoreHistory) {
  if (!canvas || !caption) return;
  const sized = prepareCanvas(canvas, 560, 165);
  if (!sized) {
    caption.textContent = 'delay bands: canvas not available in this browser';
    return;
  }
  const { ctx, width, height } = sized;
  const configuration = workspace ? workspace.configuration : null;
  const ranges = configuration && Array.isArray(configuration.boilerRanges) ? configuration.boilerRanges : [];
  const selected = selectedSensorOf(snapshot, selectedId);
  const peers = sameWallPeers(snapshot, selected);
  const dirtyThresh = snapshot && snapshot.config && typeof snapshot.config.dirtyThreshold === 'number'
    ? snapshot.config.dirtyThreshold : 65;

  ctx.clearRect(0, 0, width, height);
  ctx.fillStyle = '#0d1014';
  ctx.fillRect(0, 0, width, height);

  const leftW = Math.max(120, Math.floor(width * 0.56) - 18);
  const leftBox = { x0: 10, y0: 10, w: leftW, h: Math.max(60, height - 20) };
  const rightBox = {
    x0: leftBox.x0 + leftBox.w + 12,
    y0: 10,
    w: Math.max(100, width - (leftBox.x0 + leftBox.w + 22)),
    h: leftBox.h,
  };

  if (ranges.length > 0) {
    const low = Math.min.apply(null, ranges.map((range) => range.autoDelayMinSeconds));
    const high = Math.max.apply(null, ranges.map((range) => range.autoDelayMaxSeconds));
    const active = configuration ? text(configuration.activeBoilerCondition, NOT_PUBLISHED) : NOT_PUBLISHED;

    ctx.strokeStyle = '#28303b';
    ctx.lineWidth = 1;
    ctx.strokeRect(leftBox.x0, leftBox.y0, leftBox.w, leftBox.h);

    for (let index = 0; index < ranges.length; index += 1) {
      const range = ranges[index];
      const lane = leftBox.y0 + (index + 0.16) * (leftBox.h / ranges.length);
      const laneHeight = (leftBox.h / ranges.length) * 0.68;
      const from = leftBox.x0 + ((range.autoDelayMinSeconds - low) / Math.max(1, high - low)) * leftBox.w;
      const to = leftBox.x0 + ((range.autoDelayMaxSeconds - low) / Math.max(1, high - low)) * leftBox.w;
      const isActive = text(range.condition, '') === active;

      ctx.save();
      ctx.fillStyle = isActive ? 'rgba(56, 189, 248, 0.28)' : 'rgba(148, 163, 184, 0.14)';
      ctx.fillRect(from, lane, Math.max(4, to - from), laneHeight);
      ctx.strokeStyle = isActive ? '#38bdf8' : '#475569';
      ctx.lineWidth = isActive ? 1.5 : 1;
      ctx.strokeRect(from, lane, Math.max(4, to - from), laneHeight);
      ctx.fillStyle = isActive ? '#f1f5f9' : '#94a3b8';
      ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
      ctx.textBaseline = 'middle';
      ctx.fillText(text(range.condition, '?') + ' ' + String(range.autoDelayMinSeconds) + '-' + String(range.autoDelayMaxSeconds) + 's',
        leftBox.x0 + 6, lane + laneHeight / 2);
      ctx.restore();
    }

    const preview = workspace ? workspace.preview : null;
    const applied = preview && typeof preview.appliedDelaySeconds === 'number' ? preview.appliedDelaySeconds : null;
    if (applied !== null) {
      const x = leftBox.x0 + ((applied - low) / Math.max(1, high - low)) * leftBox.w;
      ctx.save();
      ctx.strokeStyle = '#34d399';
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(x, leftBox.y0);
      ctx.lineTo(x, leftBox.y0 + leftBox.h);
      ctx.stroke();
      ctx.fillStyle = '#34d399';
      ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
      ctx.fillText('Applied ' + String(applied) + 's', Math.min(x + 4, leftBox.x0 + leftBox.w - 72), leftBox.y0 + 12);
      ctx.restore();
    }

    drawScoreBarPanel(ctx, rightBox, selected, peers, dirtyThresh);

    caption.textContent = 'Delay ' + String(low) + '-' + String(high) + 's | Condition: ' + active
      + ' | Applied: ' + (applied === null ? NOT_PUBLISHED : String(applied) + 's')
      + ' | ' + (selected ? text(selected.sensorId) : '-') + ' dirty '
      + (selected && typeof selected.dirtyScore === 'number' ? selected.dirtyScore.toFixed(1) : '--')
      + ', validated ' + (selected && typeof selected.lastValidatedScore === 'number' ? selected.lastValidatedScore.toFixed(1) : '--');
    return;
  }

  // Fallback when DispatchWorkspace carries no boilerRanges yet: draw real observed rolling
  // Dirty Score history for the selected SDS (never invented) + same-wall peer comparison bars.
  ctx.strokeStyle = '#28303b';
  ctx.lineWidth = 1;
  ctx.strokeRect(leftBox.x0, leftBox.y0, leftBox.w, leftBox.h);

  const hist = Array.isArray(scoreHistory) ? scoreHistory : [];
  const plot = {
    x0: leftBox.x0 + 30,
    y0: leftBox.y0 + 22,
    w: Math.max(40, leftBox.w - 38),
    h: Math.max(30, leftBox.h - 38),
  };

  ctx.save();
  ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
  ctx.textBaseline = 'top';
  ctx.fillStyle = '#f1f5f9';
  ctx.fillText('SDS ' + (selected ? text(selected.sensorId) : '-') + ' Observed Score History', leftBox.x0 + 6, leftBox.y0 + 5);
  ctx.fillStyle = '#64748b';
  ctx.textAlign = 'right';
  ctx.fillText('Delay bands: awaiting workspace', leftBox.x0 + leftBox.w - 6, leftBox.y0 + 5);
  ctx.textAlign = 'left';

  // Horizontal 0 / 50 / 100 gridlines
  for (const val of [0, 50, 100]) {
    const gy = plot.y0 + (1 - val / 100) * plot.h;
    ctx.strokeStyle = 'rgba(148, 163, 184, 0.16)';
    ctx.beginPath();
    ctx.moveTo(plot.x0, gy);
    ctx.lineTo(plot.x0 + plot.w, gy);
    ctx.stroke();
    ctx.fillStyle = '#94a3b8';
    ctx.textAlign = 'right';
    ctx.textBaseline = 'middle';
    ctx.fillText(String(val), plot.x0 - 4, gy);
  }
  ctx.textAlign = 'left';

  // Threshold dashed amber line
  const threshY = plot.y0 + (1 - Math.min(100, Math.max(0, dirtyThresh)) / 100) * plot.h;
  ctx.strokeStyle = '#fbbf24';
  ctx.setLineDash([4, 3]);
  ctx.beginPath();
  ctx.moveTo(plot.x0, threshY);
  ctx.lineTo(plot.x0 + plot.w, threshY);
  ctx.stroke();
  ctx.setLineDash([]);

  // Observed rolling frame curve (null breaks the stroke; never interpolated)
  if (hist.length > 0) {
    ctx.strokeStyle = selected && String(selected.classification) === 'DIRTY' ? '#e05246' : '#38bdf8';
    ctx.lineWidth = 1.8;
    ctx.beginPath();
    let drawn = false;
    let lastPt = null;
    for (let i = 0; i < hist.length; i += 1) {
      const sample = hist[i];
      const v = sample && typeof sample.score === 'number' && Number.isFinite(sample.score) ? sample.score : null;
      if (v === null) {
        drawn = false;
        continue;
      }
      const x = plot.x0 + (hist.length === 1 ? plot.w : (i / (hist.length - 1)) * plot.w);
      const y = plot.y0 + (1 - Math.min(100, Math.max(0, v)) / 100) * plot.h;
      if (!drawn) {
        ctx.moveTo(x, y);
        drawn = true;
      } else {
        ctx.lineTo(x, y);
      }
      lastPt = { x, y };
    }
    ctx.stroke();
    if (lastPt) {
      ctx.fillStyle = '#38bdf8';
      ctx.beginPath();
      ctx.arc(lastPt.x, lastPt.y, 3, 0, Math.PI * 2);
      ctx.fill();
    }
  } else {
    ctx.fillStyle = '#94a3b8';
    ctx.textBaseline = 'middle';
    ctx.fillText('Collecting published Snapshot frames...', plot.x0 + 6, plot.y0 + plot.h / 2);
  }

  ctx.fillStyle = '#94a3b8';
  ctx.textBaseline = 'bottom';
  ctx.fillText(String(hist.length) + ' observed frame(s) | Threshold ' + String(dirtyThresh), plot.x0, leftBox.y0 + leftBox.h - 3);
  ctx.restore();

  drawScoreBarPanel(ctx, rightBox, selected, peers, dirtyThresh);

  caption.textContent = 'Delay bands: awaiting DispatchWorkspace | '
    + (selected ? text(selected.sensorId) : '-') + ' dirty '
    + (selected && typeof selected.dirtyScore === 'number' ? selected.dirtyScore.toFixed(1) : '--')
    + ', validated ' + (selected && typeof selected.lastValidatedScore === 'number' ? selected.lastValidatedScore.toFixed(1) : '--')
    + ' (' + String(hist.length) + ' observed frame(s)) | Coal Flow: ' + NOT_PUBLISHED;
}

// ---------------------------------------------------------------- Tab 4: Sensor & Path

const SDS_FILTER_OPTIONS = Object.freeze(['ALL', 'LEFT', 'REAR', 'RIGHT', 'FRONT', 'DIRTY', 'UNCERTAIN']);

export function renderSensorFilters(root, activeFilter, onFilter) {
  clear(root);
  const group = block(root, 'toggle-group');
  for (const key of SDS_FILTER_OPTIONS) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'filter-btn';
    btn.dataset.sdsFilter = key;
    btn.setAttribute('aria-pressed', activeFilter === key ? 'true' : 'false');
    btn.textContent = key;
    group.appendChild(btn);
  }
  group.addEventListener('click', (event) => {
    const target = event.target;
    const btn = target && target.closest ? target.closest('.filter-btn') : null;
    if (btn && btn.dataset.sdsFilter) onFilter(btn.dataset.sdsFilter);
  });
}

export function renderSensorMaster(root, snapshot, selectedId, activeTargetId, onSelect, activeFilter) {
  clear(root);
  const sensors = snapshot && Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  if (!sensors.length) {
    paragraph(root, 'empty-state', 'no Sensor set published in this Snapshot');
    return;
  }
  const filter = activeFilter || 'ALL';
  const filtered = sensors.filter((sensor) => {
    if (!sensor) return false;
    if (filter === 'ALL') return true;
    if (filter === 'DIRTY') return String(sensor.classification) === 'DIRTY' && String(sensor.quality) === 'GOOD';
    if (filter === 'UNCERTAIN') {
      return String(sensor.quality) !== 'GOOD'
        || (String(sensor.classification) !== 'DIRTY' && String(sensor.classification) !== 'CLEANER');
    }
    return String(sensor.wall || '').toUpperCase() === filter;
  });

  if (!filtered.length) {
    paragraph(root, 'empty-state', 'No Sensor matches filter ' + filter + ' (' + String(sensors.length) + ' total published).');
    return;
  }

  const list = block(root, 'sds-master-list');
  for (const sensor of filtered) {
    const row = document.createElement('button');
    row.type = 'button';
    row.className = 'sds-row';
    row.dataset.sensorId = text(sensor.sensorId, '');
    if (sensor.sensorId === selectedId || sensor.sensorId === activeTargetId) {
      row.setAttribute('aria-current', 'true');
    }
    const id = document.createElement('span');
    id.className = 'sds-row-id';
    id.textContent = text(sensor.sensorId, '?');
    const place = document.createElement('span');
    place.className = 'sds-row-place';
    place.textContent = text(sensor.wall, '?') + ' R' + text(sensor.logicalRow, '?') + 'C' + text(sensor.logicalColumn, '?')
      + ' ' + tierOfRow(sensor.logicalRow) + ' | ' + (typeof sensor.dirtyScore === 'number' ? sensor.dirtyScore.toFixed(1) : '--');
    const jet = document.createElement('span');
    jet.className = 'sds-row-jet';
    jet.textContent = text(sensor.assignedWaterJetId, '-') + ' / ' + text(sensor.assignedIsolationValveId, '-');
    const stateBadge = document.createElement('span');
    stateBadge.className = 'sds-row-state';
    const isUnc = String(sensor.quality || '') !== 'GOOD';
    stateBadge.textContent = isUnc ? text(sensor.quality, '?') : text(sensor.classification, '?');

    row.appendChild(id);
    row.appendChild(place);
    row.appendChild(jet);
    row.appendChild(stateBadge);
    row.title = text(sensor.classification, NOT_PUBLISHED) + ' / ' + text(sensor.queueState, '?')
      + ' / ' + text(sensor.quality, '?');
    row.dataset.tone = isUnc ? 'warn' : (sensor.classification === 'DIRTY' ? 'bad' : sensor.classification === 'CLEANER' ? 'ok' : 'warn');
    list.appendChild(row);
  }
  list.addEventListener('click', (event) => {
    const target = event.target;
    const row = target && target.closest ? target.closest('.sds-row') : null;
    if (row && row.dataset.sensorId) onSelect(row.dataset.sensorId);
  });
}

export function renderWorkspace(canvas, caption, snapshot, selectedSensor, activeTarget) {
  if (!canvas || !caption) return;
  const sized = prepareCanvas(canvas, 320, 240);
  if (!sized) {
    caption.textContent = 'workspace: canvas not available in this browser';
    return;
  }
  const { ctx, width, height } = sized;
  ctx.clearRect(0, 0, width, height);
  ctx.fillStyle = '#0d1014';
  ctx.fillRect(0, 0, width, height);

  // Centered 1:1 square inside safe margins so zero labels ever clip at any edge
  const padL = 34;
  const padR = 18;
  const padT = 16;
  const padB = 26;
  const side = Math.max(72, Math.min(width - padL - padR, height - padT - padB));
  const x0 = Math.round(padL + Math.max(0, (width - padL - padR - side) / 2));
  const y0 = Math.round(padT + Math.max(0, (height - padT - padB - side) / 2));
  const box = { x0, y0, x1: x0 + side, y1: y0 + side, w: side, h: side };

  ctx.save();
  ctx.strokeStyle = '#384250';
  ctx.lineWidth = 1;
  ctx.strokeRect(box.x0, box.y0, box.w, box.h);
  ctx.restore();
  drawGrid(ctx, box, 0.25, 'soft');

  const extent = WORKSPACE_MAX_MM - WORKSPACE_MIN_MM;
  const centreFx = (SAFE_RETURN_CENTRE_MM.horizontal - WORKSPACE_MIN_MM) / extent;
  const centreFy = (SAFE_RETURN_CENTRE_MM.vertical - WORKSPACE_MIN_MM) / extent;
  const cx = box.x0 + centreFx * box.w;
  const cy = box.y1 - centreFy * box.h;

  // Crosshair & circle at Safe Return Centre (125, 125)
  ctx.save();
  ctx.strokeStyle = '#22d3ee';
  ctx.lineWidth = 1.5;
  ctx.beginPath();
  ctx.moveTo(cx - 10, cy);
  ctx.lineTo(cx + 10, cy);
  ctx.moveTo(cx, cy - 10);
  ctx.lineTo(cx, cy + 10);
  ctx.stroke();
  ctx.beginPath();
  ctx.arc(cx, cy, 6, 0, Math.PI * 2);
  ctx.stroke();

  // Clean axis tick numbers outside the square
  ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
  ctx.fillStyle = '#94a3b8';
  for (let mm = WORKSPACE_MIN_MM; mm <= WORKSPACE_MAX_MM; mm += 70) {
    const frac = (mm - WORKSPACE_MIN_MM) / extent;
    const tx = box.x0 + frac * box.w;
    const ty = box.y1 - frac * box.h;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';
    ctx.fillText(String(mm), tx, box.y1 + 5);
    ctx.textAlign = 'right';
    ctx.textBaseline = 'middle';
    ctx.fillText(String(mm), box.x0 - 5, ty);
  }

  // Callout inside upper-right quadrant, clamped cleanly inside box bounds
  const labelText = 'Centre (' + String(SAFE_RETURN_CENTRE_MM.horizontal) + ', ' + String(SAFE_RETURN_CENTRE_MM.vertical) + ')';
  const calloutX = Math.min(cx + 10, box.x1 - 92);
  const calloutY = Math.max(box.y0 + 12, cy - 16);
  ctx.strokeStyle = 'rgba(34, 211, 238, 0.55)';
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(cx + 4, cy - 4);
  ctx.lineTo(calloutX, calloutY + 3);
  ctx.stroke();
  ctx.fillStyle = '#22d3ee';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';
  ctx.fillText(labelText, calloutX + 2, calloutY);
  ctx.restore();

  caption.textContent = '0-' + String(WORKSPACE_MAX_MM) + ' mm (1:1) | ' + SCALING_NOTE
    + ' | Centre (' + String(SAFE_RETURN_CENTRE_MM.horizontal) + ', ' + String(SAFE_RETURN_CENTRE_MM.vertical)
    + ') = ' + grouped(SAFE_RETURN_CENTRE_NATIVE.horizontal) + ' native';
  return {
    selected: selectedSensor ? 'selected ' + String(selectedSensor) + ' - no published position' : 'nothing selected',
    activeTarget: activeTarget ? 'active target ' + String(activeTarget) + ' - no published position' : 'no active target',
  };
}

export function renderPathTable(root, workspace) {
  clear(root);
  paragraph(root, 'scale-note', SCALING_NOTE + ' | 1 mm is never rendered as a pulse count');
  const timeline = workspace ? workspace.timeline : null;
  const steps = timeline && Array.isArray(timeline.steps) ? timeline.steps : [];
  if (!steps.length) {
    paragraph(root, 'empty-state', 'no Cleaning Job Workspace timeline published - canonical stage order shown');
  }
  paragraph(root, 'coord-note', STAGE_COORDINATE_NOTE);
  const body = [];
  for (const stage of COCKPIT_STAGES) {
    const step = byId(steps, 'stage', stage.stage);
    body.push({
      cells: [
        stage.key,
        stage.stage,
        step ? text(step.ordinal, '-') : '-',
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
  table(root, ['Position', 'Stage', '#', 'Label', 'State', 'mm', 'Native'], body);
  const keys = CLEANING_PATH_STAGES.map((stage) => stage.stage).join(', ');
  paragraph(root, 'stage-keys', 'Stage identities indexed: ' + keys);
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
    ['revision (snapshot)', snapshot ? text(snapshot.revision, 'none') : 'none', 'mute'],
    ['revision (health)', health && health.currentRevision !== null && health.currentRevision !== undefined ? String(health.currentRevision) : NOT_PUBLISHED],
    ['generated at', snapshot ? timeOf(snapshot.generatedAt) : NOT_PUBLISHED],
    ['server time', status ? timeOf(status.serverTimeUtc) : NOT_PUBLISHED],
    ['tick', status ? number(status.tickIntervalMilliseconds, 0, 'ms') : NOT_PUBLISHED],
    ['evolution', status ? (status.evolutionRunning ? 'RUNNING' : 'STOPPED') : NOT_PUBLISHED, status && status.evolutionRunning ? 'ok' : 'warn'],
    ['accepted ticks', status ? text(status.acceptedTicks, NOT_PUBLISHED) : NOT_PUBLISHED],
    ['rejected transitions', status ? text(status.rejectedTransitions, NOT_PUBLISHED) : NOT_PUBLISHED, status && status.rejectedTransitions > 0 ? 'warn' : 'mute'],
    ['uptime', runtime ? number(runtime.uptimeSeconds, 0, 's') : NOT_PUBLISHED],
    ['invariant violations', runtime ? text(runtime.invariantViolations, NOT_PUBLISHED) : NOT_PUBLISHED, runtime && runtime.invariantViolations > 0 ? 'bad' : 'ok'],
    ['second Jobs accepted', runtime ? text(runtime.acceptedSecondJobs, NOT_PUBLISHED) : NOT_PUBLISHED],
    ['second Jobs refused', runtime ? text(runtime.refusedSecondJobs, NOT_PUBLISHED) : NOT_PUBLISHED],
    ['SSE clients', runtime ? text(runtime.sseClients, NOT_PUBLISHED) : NOT_PUBLISHED, 'mute'],
    ['config revision', config ? text(config.revision, NOT_PUBLISHED) : NOT_PUBLISHED],
    ['config label', config ? text(config.label, NOT_PUBLISHED) : NOT_PUBLISHED],
    ['dirty threshold', config ? number(config.dirtyThreshold, 2) : NOT_PUBLISHED],
    ['stale threshold', config ? number(config.staleThresholdMs, 0, 'ms') : NOT_PUBLISHED],
    ['feed', connection ? text(connection.label, 'unknown') : 'unknown', connection && connection.online ? 'ok' : 'bad'],
  ]);

  if (historian) {
    heading(root, 'Historian');
    rows(root, [
      ['wired', historian.wired ? 'YES' : 'NO', historian.wired ? 'ok' : 'mute'],
      ['depth / capacity', text(historian.depth, '0') + ' / ' + text(historian.capacity, '0')],
      ['near overflow', historian.nearOverflow ? 'YES' : 'NO', historian.nearOverflow ? 'bad' : 'ok'],
      ['rejected', text(historian.rejected, '0'), historian.rejected > 0 ? 'warn' : 'ok'],
      ['gap markers', text(historian.gapMarkers, '0'), historian.gapMarkers > 0 ? 'warn' : 'ok'],
      ['last batch latency', historian.lastBatchLatencyMs === null || historian.lastBatchLatencyMs === undefined ? 'no batch yet' : number(historian.lastBatchLatencyMs, 0, 'ms')],
    ]);
  }

  if (status) {
    heading(root, 'Runtime Counters');
    const counts = status.sensorCounts;
    const disabledCount = Array.isArray(status.disabledSensorIds) ? status.disabledSensorIds.length : 0;
    rows(root, [
      ['logical slots', counts && counts.logicalSlots ? text(counts.logicalSlots.label, NOT_PUBLISHED) + ' = ' + text(counts.logicalSlots.count, '0') : NOT_PUBLISHED],
      ['Sensor locations', counts && counts.sensorLocations ? text(counts.sensorLocations.label, NOT_PUBLISHED) + ' = ' + text(counts.sensorLocations.count, '0') : NOT_PUBLISHED],
      ['TC channels', counts && counts.thermocoupleChannels ? text(counts.thermocoupleChannels.label, NOT_PUBLISHED) + ' = ' + text(counts.thermocoupleChannels.count, '0') : NOT_PUBLISHED],
      ['physical TC', counts && counts.physicalThermocoupleChannels ? text(counts.physicalThermocoupleChannels.label, NOT_PUBLISHED) + ' = ' + text(counts.physicalThermocoupleChannels.count, '0') : NOT_PUBLISHED],
      ['unassigned TC', counts && counts.unassignedThermocoupleChannels ? text(counts.unassignedThermocoupleChannels.label, NOT_PUBLISHED) + ' = ' + text(counts.unassignedThermocoupleChannels.count, '0') : NOT_PUBLISHED],
      ['Water Jets', counts && counts.waterJets ? text(counts.waterJets.count, '0') : NOT_PUBLISHED],
      ['Isolation Valves', counts && counts.isolationValves ? text(counts.isolationValves.count, '0') : NOT_PUBLISHED],
      ['queue', text(status.queueCount, '0') + ' queued of ' + text(status.queueCapacity, '0') + ' (' + text(status.queuePlaceholder, 'no placeholder') + ')'],
      ['active Job', status.activeJobPresent ? text(status.activeJobId, 'present') : 'none'],
      ['disabled Sensors', disabledCount ? String(disabledCount) : 'none'],
      ['alarm counts', 'active ' + text(status.activeAlarmCount, '0') + ', cleared-unack ' + text(status.clearedAlarmCount, '0')],
      ['delta history', text(status.deltaHistoryDepth, '0') + ' / ' + text(status.deltaHistoryCapacity, '0') + ', newest rev ' + (status.newestDeltaRevision === null || status.newestDeltaRevision === undefined ? 'none' : String(status.newestDeltaRevision))],
      ['state history', text(status.stateHistoryDepth, '0') + ' / ' + text(status.stateHistoryCapacity, '0')],
      ['last fault', text(status.lastFaultCode, 'none') + ' - ' + text(status.lastFaultDetail, 'no detail'), status.lastFaultCode ? 'bad' : 'ok'],
      ['startup fault', text(status.startupFaultCode, 'none'), status.startupFaultCode ? 'bad' : 'ok'],
      ['acquisition label', counts && counts.equipmentTopology ? text(counts.equipmentTopology.acquisition, NOT_PUBLISHED) : NOT_PUBLISHED, 'mute'],
    ]);
  }

  heading(root, 'Device Links');
  if (!devices.length) {
    paragraph(root, 'empty-state', 'no device link health published in this Snapshot');
  } else {
    table(root, ['Device', 'State', 'Timeouts', 'Last OK', 'Latency', 'OK', 'Fail'], devices.map((device) => ({
      cells: [
        text(device.deviceId, '?'),
        text(device.state, NOT_PUBLISHED),
        text(device.consecutiveTimeouts, '0'),
        timeOf(device.lastSuccessAt),
        device.lastLatencyMs === null || device.lastLatencyMs === undefined ? '-' : number(device.lastLatencyMs, 0, 'ms'),
        text(device.pollsOk, '0'),
        text(device.pollsFailed, '0'),
      ],
      tone: device.state === 'ONLINE' ? 'ok' : device.state === 'TIMEOUT' ? 'bad' : 'warn',
    })));
  }
}

export function renderWallBreakdown(root, snapshot) {
  clear(root);
  const sensors = snapshot && Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  const tally = sensors.length > 0
    ? partitionSensors(sensors, snapshot ? snapshot.walls : null)
    : summaryPills(snapshot ? snapshot.walls : null);

  if (tally.state === 'no data') {
    paragraph(root, 'empty-state', tally.note);
    return;
  }

  // Visual 3-segment mutually exclusive cleanliness distribution bars per wall (sum === 24 / 30 / 108)
  const barList = block(root, 'dist-bar-list num');
  for (const entry of tally.perWall) {
    const row = block(barList, 'dist-row');
    const head = block(row, 'dist-row-head');
    const left = document.createElement('strong');
    left.textContent = text(entry.wall, '?') + ' (' + String(entry.total) + ' SDS)';
    const right = document.createElement('span');
    right.textContent = 'Clean ' + String(entry.cleaner) + ' | Dirty ' + String(entry.dirty)
      + ' | Unc ' + String(entry.uncertain);
    head.appendChild(left);
    head.appendChild(right);

    const bar = block(row, 'wall-bar');
    for (const [cls, cnt] of [
      ['dirty', entry.dirty],
      ['unc', entry.uncertain],
      ['clean', entry.cleaner],
    ]) {
      if (cnt > 0) {
        const seg = block(bar, 'wall-bar-seg ' + cls);
        seg.style.flexGrow = String(cnt);
      }
    }
  }

  const body = tally.perWall.map((entry) => ({
    cells: [
      text(entry.wall, '?'),
      String(entry.total),
      String(entry.cleaner),
      String(entry.dirty),
      String(entry.uncertain),
      String(entry.publishedUncertain !== undefined ? entry.publishedUncertain : (entry.uncertain || 0)),
      entry.maxScore === null || entry.maxScore === undefined ? '-' : number(entry.maxScore, 1),
    ],
    tone: entry.total === EXPECTED_PER_WALL[entry.wall] ? 'ok' : 'warn',
  }));
  body.push({
    cells: [
      'TOTAL',
      String(tally.total),
      String(tally.pills[0].value),
      String(tally.pills[1].value),
      String(tally.pills[2].value),
      '-',
      '-',
    ],
    tone: tally.state === 'mismatch' ? 'bad' : 'ok',
  });
  table(root, ['Wall', 'Tot', 'Clean', 'Dirty', 'Unc', 'RawUnc', 'Max'], body);
  paragraph(root, 'breakdown-note', tally.note);
}

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
  const sized = prepareCanvas(canvas, 680, 200);
  if (!sized) {
    caption.textContent = 'trend explorer: canvas not available in this browser';
    return;
  }
  const { ctx, width, height } = sized;
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
  const frame = frameOf(canvas, { dualAxis: false, captions: false }, sized);

  ctx.clearRect(0, 0, width, height);
  ctx.fillStyle = '#0d1014';
  ctx.fillRect(0, 0, width, height);

  if (!frame || !range || !timeRange) {
    ctx.fillStyle = '#94a3b8';
    ctx.font = '11px "Segoe UI", Roboto, sans-serif';
    ctx.fillText('No finite samples on enabled channels yet (' + String(points.length) + ' points)', 16, 28);
    caption.textContent = 'trend explorer: ' + String(points.length) + ' samples, nothing on an enabled channel to plot';
    note.textContent = 'a channel with every sample missing is a DATA GAP, not a flat line';
    return;
  }
  const mapper = mapperFor(frame, range, timeRange);
  axisOf(ctx, frame, range, 'bar', 'left');
  drawTimeAxis(ctx, frame, timeRange);

  ctx.save();
  ctx.lineWidth = 1.8;
  for (const entry of shown) {
    ctx.strokeStyle = entry.name === 'pressure' ? '#38bdf8'
      : entry.name === 'setpoint' ? '#fbbf24'
        : '#34d399';
    drawSeries(ctx, entry.values, mapper);
  }
  ctx.restore();

  const span = { min: 0, max: 100 };
  const normalized = series.map((entry) => normalise(entry.values, range));
  caption.textContent = captionFor(shown.length ? shown : series, points, trend.capacity)
    + ' | vertical: ' + formatTick(range.min) + ' to ' + formatTick(range.max) + ' bar'
    + ' | 0-100% span ' + formatTick(span.min) + ' to ' + formatTick(span.max);
  note.textContent = 'Bounded window (' + text(trend.capacity, '0')
    + ' samples) for published channels only; Coal Flow, SDS Temp, and IV History are ' + NOT_PUBLISHED + '.';
  return { series, normalized, enabled };
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
    if (typeof raw === 'number') return formatted(raw, 'bar');
  }
  return 'DATA GAP';
}

/* Tab 1(d): IP Camera mount container.
 * Zero external addresses, double-slash tokens, or image elements appear in the camera renderer.
 */
export function renderCameraPanel(root, snapshot, selectedJet, onSelectJet) {
  clear(root);
  const active = snapshot ? snapshot.activeJob : null;
  const activeJetId = active && active.jetId ? String(active.jetId) : null;
  const mode = selectedJet && selectedJet !== 'AUTO' ? String(selectedJet) : 'AUTO';
  const resolvedId = mode === 'AUTO' ? activeJetId : mode;
  const canonical = resolvedId ? WATER_JETS_BY_ID[resolvedId] : null;
  const publishedJet = resolvedId ? byId(snapshot && snapshot.waterJets ? snapshot.waterJets : [], 'waterJetId', resolvedId) : null;
  const displayJet = resolvedId || 'NO ACTIVE JET';
  const sensorId = active && (!resolvedId || resolvedId === activeJetId)
    ? text(active.targetSensorId, NOT_PUBLISHED)
    : 'STANDBY';

  root.dataset.syncedJet = resolvedId || 'none';

  const viewport = block(root, 'cam-viewport');
  const canvas = document.createElement('canvas');
  canvas.width = 340;
  canvas.height = 136;
  canvas.setAttribute('role', 'img');
  canvas.setAttribute('aria-label', 'Camera viewport mount container for ' + displayJet);
  viewport.appendChild(canvas);

  const sized = prepareCanvas(canvas, 340, 136);
  if (sized) {
    const ctx = sized.ctx;
    const w = sized.width;
    const h = sized.height;
    ctx.fillStyle = '#0b0e13';
    ctx.fillRect(0, 0, w, h);

    for (let x = 20; x <= w - 20; x += 18) {
      ctx.strokeStyle = '#18212e';
      ctx.lineWidth = 3;
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, h);
      ctx.stroke();
      ctx.strokeStyle = '#263244';
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, h);
      ctx.stroke();
    }

    const cx = Math.floor(w * 0.5);
    const cy = Math.floor(h * 0.46);
    ctx.strokeStyle = '#22d3ee';
    ctx.lineWidth = 1.2;
    ctx.beginPath();
    ctx.arc(cx, cy, 12, 0, Math.PI * 2);
    ctx.moveTo(cx - 20, cy);
    ctx.lineTo(cx - 6, cy);
    ctx.moveTo(cx + 6, cy);
    ctx.lineTo(cx + 20, cy);
    ctx.moveTo(cx, cy - 20);
    ctx.lineTo(cx, cy - 6);
    ctx.moveTo(cx, cy + 6);
    ctx.lineTo(cx, cy + 20);
    ctx.stroke();

    ctx.fillStyle = 'rgba(13, 16, 20, 0.86)';
    ctx.fillRect(8, h - 40, w - 16, 18);
    ctx.fillStyle = '#fbbf24';
    ctx.font = '9px ui-monospace, Menlo, Consolas, monospace';
    ctx.textAlign = 'center';
    ctx.fillText('NO CAMERA SOURCE - THE READ-ONLY RUNTIME HAS NO IMAGE ROUTE', w * 0.5, h - 28);
    ctx.textAlign = 'left';
  }

  const hud = block(viewport, 'cam-hud-overlay');
  const hudTop = block(hud, 'cam-hud-top');
  const hudId = document.createElement('span');
  hudId.className = 'cam-hud-id';
  hudId.textContent = mode === 'AUTO' ? 'AUTO | ' + displayJet : displayJet;
  const hudState = document.createElement('span');
  hudState.className = 'cam-hud-status';
  hudState.textContent = active && resolvedId === activeJetId ? 'SYNCED TO ACTIVE' : 'MOUNT READY';
  hudTop.appendChild(hudId);
  hudTop.appendChild(hudState);

  const hudBot = block(hud, 'cam-hud-bot');
  const hudAim = document.createElement('span');
  hudAim.textContent = 'TARGET: ' + sensorId;
  const hudPark = document.createElement('span');
  hudPark.textContent = 'PARK (' + String(SAFE_RETURN_CENTRE_MM.horizontal) + ', ' + String(SAFE_RETURN_CENTRE_MM.vertical) + ')';
  hudBot.appendChild(hudAim);
  hudBot.appendChild(hudPark);

  const side = block(root, 'cam-side-meta num');
  const btnStrip = block(side, 'cam-btn-strip');
  const choices = ['AUTO'].concat(WATER_JETS.map((entry) => entry.wj));
  for (const choice of choices) {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'cam-btn';
    btn.dataset.camChoice = choice;
    btn.setAttribute('aria-pressed', mode === choice ? 'true' : 'false');
    btn.textContent = choice;
    btnStrip.appendChild(btn);
  }
  btnStrip.addEventListener('click', (event) => {
    const target = event.target;
    const btn = target && target.closest ? target.closest('.cam-btn') : null;
    if (btn && btn.dataset.camChoice && typeof onSelectJet === 'function') {
      onSelectJet(btn.dataset.camChoice);
    }
  });

  const addKv = (k, v) => {
    const row = block(side, 'kv-row');
    const s = document.createElement('span');
    s.textContent = k;
    const st = document.createElement('strong');
    st.textContent = v;
    row.appendChild(s);
    row.appendChild(st);
  };

  const instWall = publishedJet
    ? text(publishedJet.installedWall) + ' ' + text(publishedJet.installedRegion)
    : (canonical ? canonical.installedWall + ' ' + canonical.installedTier : NOT_PUBLISHED);
  const targWall = publishedJet
    ? text(publishedJet.targetWall) + ' ' + text(publishedJet.targetRegion)
    : (canonical ? canonical.targetWall + ' ' + canonical.targetTier : NOT_PUBLISHED);

  addKv('Installed', instWall);
  addKv('Target', targWall);
  addKv('Safe Park', '(' + String(SAFE_RETURN_CENTRE_MM.horizontal) + ', ' + String(SAFE_RETURN_CENTRE_MM.vertical) + ') mm');

  const zero = paragraph(side, 'cam-zero-note',
    'zero - no image element, no source, no address, no request');
  zero.title = 'NO CAMERA SOURCE - THE READ-ONLY RUNTIME HAS NO IMAGE ROUTE';
}
