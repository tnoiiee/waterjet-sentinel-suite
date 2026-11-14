// WJSS Operator presentation - the authoritative 108-Sensor U-Shape boiler wall map.
//
// The geometry is the accepted topology ruling, stated once here and asserted by the source
// contract test: four walls traversed LEFT, REAR, RIGHT, FRONT; six logical rows per wall;
// logical rows 1 to 3 are the UPPER tier and logical rows 4 to 6 are the LOWER tier; 24 Sensors
// on LEFT, 30 on REAR, 24 on RIGHT and 30 on FRONT, 108 in total; I7 and I16 are normal Product
// Sensors occupying their normal ordinal positions, never a gap, never an anchor and never a
// Water Jet identity.
//
// Nothing is drawn that the host did not publish. A matrix slot with no Sensor identity is
// rendered as a refused slot with its slot id, and a Sensor set that does not add up to the
// protected totals refuses the whole map instead of presenting a partial boiler.

import {
  EXPECTED_PER_WALL,
  EXPECTED_TOTAL,
  LOWER_ROWS,
  SAFE_RETURN_CENTRE_MM,
  UPPER_ROWS,
  WALL_COLUMNS,
  WALL_LABELS,
  WALL_ORDER,
  WALL_ROWS,
  WATER_JETS,
  WATER_JETS_BY_ID,
  WSB_CABINETS,
  tierOfRow,
} from './topology.mjs';

export {
  EXPECTED_PER_WALL,
  EXPECTED_TOTAL,
  LOWER_ROWS,
  UPPER_ROWS,
  WALL_COLUMNS,
  WALL_LABELS,
  WALL_ORDER,
  WALL_ROWS,
  tierOfRow,
};

export function isSensor(value) {
  return String(value).toUpperCase() === 'SENSOR';
}

export function visualOf(sensor) {
  if (!sensor) return { key: 'unknown', marker: '?', label: 'NO DATA' };
  const quality = String(sensor.quality || '');
  const classification = String(sensor.classification || '');
  if (quality === 'STALE' || quality === 'DISABLED') {
    return { key: 'stale', marker: '~', label: quality === 'STALE' ? 'STALE' : 'DISABLED' };
  }
  if (classification === 'DIRTY') return { key: 'dirty', marker: '!', label: 'DIRTY' };
  if (classification === 'CLEANER') return { key: 'clean', marker: '=', label: 'CLEANER' };
  if (quality === 'BAD') return { key: 'watch', marker: 'x', label: 'BAD DATA' };
  return { key: 'watch', marker: '-', label: classification === 'NOT_CLASSIFIED' ? 'NOT CLASSIFIED' : 'OBSERVE' };
}

export function alarmOf(sensor) {
  const state = String((sensor && sensor.alarmState) || 'NONE');
  if (state === 'NONE' || state === 'CLEARED_UNACK') return 'none';
  return String(sensor.alarmSeverity || '').toUpperCase() === 'HIGH' ? 'critical' : 'alarm';
}

export function queueOf(sensor) {
  const state = String((sensor && sensor.queueState) || 'NONE');
  if (state === 'ACTIVE') return 'next';
  if (state === 'QUEUED') return 'queued';
  return 'none';
}

export function buildModel(snapshot) {
  const slots = snapshot && Array.isArray(snapshot.wallMap) ? snapshot.wallMap : null;
  if (!slots) return { ok: false, error: 'the Snapshot carried no wall map' };

  const sensors = Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  const byId = new Map();
  for (const sensor of sensors) {
    if (sensor && typeof sensor.sensorId === 'string' && !byId.has(sensor.sensorId)) byId.set(sensor.sensorId, sensor);
  }

  const walls = new Map();
  for (const wall of WALL_ORDER) walls.set(wall, { slots: [], sensors: 0 });

  let nonSensor = 0;
  const seen = new Set();
  for (const slot of slots) {
    if (!slot || typeof slot.slotId !== 'string') continue;
    const wall = String(slot.wall || '').toUpperCase();
    const bucket = walls.get(wall);
    if (!bucket) {
      return { ok: false, error: 'the matrix names a wall outside LEFT, REAR, RIGHT and FRONT: ' + String(slot.wall) };
    }
    if (!isSensor(slot.positionKind)) nonSensor += 1;
    bucket.slots.push(slot);
    if (typeof slot.sensorId === 'string' && slot.sensorId.length > 0) {
      if (seen.has(slot.sensorId)) {
        return { ok: false, error: 'a Sensor identity is published on two matrix slots: ' + slot.sensorId };
      }
      seen.add(slot.sensorId);
      bucket.sensors += 1;
    }
  }

  if (seen.size !== EXPECTED_TOTAL) {
    return { ok: false, error: 'the matrix carries ' + String(seen.size) + ' Sensor positions, the protected total is ' + EXPECTED_TOTAL };
  }
  for (const wall of WALL_ORDER) {
    const expected = EXPECTED_PER_WALL[wall];
    if (walls.get(wall).sensors !== expected) {
      return { ok: false, error: wall + ' carries ' + String(walls.get(wall).sensors) + ' Sensors, the approved count is ' + expected };
    }
    walls.get(wall).slots.sort((a, b) => a.logicalRow - b.logicalRow || a.logicalColumn - b.logicalColumn);
  }

  const ordered = [];
  for (const wall of WALL_ORDER) {
    for (const slot of walls.get(wall).slots) {
      if (typeof slot.sensorId === 'string' && slot.sensorId.length > 0) ordered.push(slot.sensorId);
    }
  }

  return { ok: true, walls, sensorCount: byId.size, ordered, nonSensor };
}

function cabinetInfoForJet(wjId) {
  for (const cabinet of WSB_CABINETS) {
    for (const binding of cabinet.jets) {
      if (binding.wj === wjId) {
        return cabinet.wsb + ' ' + binding.horizontalAxis + '/' + binding.verticalAxis;
      }
    }
  }
  return 'WSB?';
}

function queuePositionsMap(snapshot) {
  const map = new Map();
  const entries = snapshot && snapshot.queue && Array.isArray(snapshot.queue.entries)
    ? snapshot.queue.entries : [];
  for (const entry of entries) {
    if (entry && typeof entry.sensorId === 'string' && typeof entry.position === 'number') {
      map.set(entry.sensorId, entry.position);
    }
  }
  return map;
}

function nozzleStrip(wall, snapshot) {
  const active = snapshot ? snapshot.activeJob : null;
  const installed = WATER_JETS.filter((jet) => jet.installedWall === wall);
  const card = document.createElement('div');
  card.className = 'wsb-mini-card';

  const head = document.createElement('div');
  head.className = 'wsb-mini-head';
  const title = document.createElement('span');
  title.textContent = wall + ' INSTALLED NOZZLES';
  const count = document.createElement('span');
  count.className = 'wsb-mini-count';
  count.textContent = String(installed.length) + ' UNITS';
  head.appendChild(title);
  head.appendChild(count);
  card.appendChild(head);

  for (const jet of installed) {
    const isFiring = Boolean(active && active.jetId === jet.wj);
    const row = document.createElement('div');
    row.className = 'nozzle-row';
    const info = document.createElement('div');
    const idLine = document.createElement('div');
    idLine.className = 'noz-id';
    idLine.textContent = jet.wj + ' | ' + jet.iv + ' (' + cabinetInfoForJet(jet.wj) + ')';
    const metaLine = document.createElement('div');
    metaLine.className = 'noz-meta';
    metaLine.textContent = jet.installedWall + ' ' + jet.installedTier + ' -> ' + jet.targetWall + ' ' + jet.targetTier;
    info.appendChild(idLine);
    info.appendChild(metaLine);

    const pill = document.createElement('span');
    pill.className = isFiring ? 'pill-state pill-firing' : 'pill-state pill-standby';
    pill.textContent = isFiring ? 'ACTIVE' : 'STANDBY';
    row.appendChild(info);
    row.appendChild(pill);
    card.appendChild(row);
  }
  return card;
}

function sideFooterCard(label, statusText) {
  const card = document.createElement('div');
  card.className = 'wsb-footer-card num';
  const left = document.createElement('span');
  left.textContent = label;
  const right = document.createElement('span');
  right.className = 'wsb-footer-ok';
  right.textContent = statusText;
  card.appendChild(left);
  card.appendChild(right);
  return card;
}

function wallSummaryOf(snapshot, wall) {
  const summaries = snapshot && Array.isArray(snapshot.walls) ? snapshot.walls : [];
  for (const entry of summaries) {
    if (entry && String(entry.wall || '').toUpperCase() === wall) return entry;
  }
  return null;
}

function deviceNote(wall, snapshot) {
  const jets = Array.isArray(snapshot.waterJets) ? snapshot.waterJets : [];
  const targeted = jets.filter((jet) => String(jet.targetWall || '').toUpperCase() === wall);
  if (targeted.length === 0) return '';
  return 'Cleaned by ' + targeted.map((jet) => String(jet.waterJetId || '?')).join(' / ');
}

function cell(slot, sensor, selectedId, queuePos, onSelect) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'sds-cell';

  const top = document.createElement('div');
  top.className = 'sds-top';
  const id = document.createElement('span');
  id.className = 'sds-id';
  const val = document.createElement('div');
  val.className = 'sds-val num';

  if (!sensor) {
    button.dataset.gap = 'true';
    button.dataset.state = 'unknown';
    button.disabled = true;
    id.textContent = typeof slot.sensorId === 'string' && slot.sensorId.length > 0 ? slot.sensorId : slot.slotId;
    val.textContent = 'NO SDS';
    button.title = slot.slotId + ': the Runtime published this matrix position without a Sensor identity, '
      + 'so the presentation shows the slot and refuses to invent one.';
    top.appendChild(id);
    button.appendChild(top);
    button.appendChild(val);
    return button;
  }

  const visual = visualOf(sensor);
  const qState = queueOf(sensor);
  const isTarget = sensor.isActiveJobTarget === true;
  button.dataset.state = visual.key;
  button.dataset.alarm = alarmOf(sensor);
  button.dataset.queue = qState;
  button.dataset.target = isTarget ? 'true' : 'false';
  button.dataset.selected = sensor.sensorId === selectedId ? 'true' : 'false';
  button.setAttribute('aria-current', sensor.sensorId === selectedId ? 'true' : 'false');
  button.dataset.sensor = sensor.sensorId;

  id.textContent = sensor.sensorId;
  top.appendChild(id);

  if (isTarget) {
    const badge = document.createElement('span');
    badge.className = 'sds-badge next-tag';
    badge.textContent = 'ACT';
    top.appendChild(badge);
  } else if (queuePos !== undefined) {
    const badge = document.createElement('span');
    badge.className = 'sds-badge';
    badge.textContent = 'Q' + String(queuePos);
    top.appendChild(badge);
  } else if (visual.key === 'watch' || visual.key === 'stale' || visual.key === 'unknown') {
    const badge = document.createElement('span');
    badge.className = 'sds-badge unc-warn';
    badge.textContent = visual.marker;
    top.appendChild(badge);
  }

  val.textContent = typeof sensor.dirtyScore === 'number' ? sensor.dirtyScore.toFixed(1) : '--';

  button.title = sensor.sensorId
    + ' - ' + (WALL_LABELS[String(slot.wall).toUpperCase()] || slot.wall)
    + ', logical row ' + slot.logicalRow + ' (' + tierOfRow(slot.logicalRow) + '), column ' + slot.logicalColumn
    + '; classification ' + String(sensor.classification || 'NOT_PROVIDED')
    + ' on basis ' + String(sensor.classificationBasis || 'NOT_PROVIDED')
    + '; quality ' + String(sensor.quality || 'NOT_PROVIDED')
    + (sensor.qualityReason ? ' (' + sensor.qualityReason + ')' : '')
    + '; cleaning device ' + String(sensor.assignedWaterJetId || 'NOT_PROVIDED')
    + ' with isolation valve ' + String(sensor.assignedIsolationValveId || 'NOT_PROVIDED');
  button.addEventListener('click', () => onSelect(sensor.sensorId));

  button.appendChild(top);
  button.appendChild(val);
  return button;
}

function halfGrid(wall, slots, sensorById, rows, selectedId, queuePosMap, onSelect) {
  const cols = WALL_COLUMNS[wall];
  const scoped = slots.filter((slot) => rows.indexOf(slot.logicalRow) >= 0);
  const grid = document.createElement('div');
  grid.className = 'wall-half-grid cols-' + String(cols);
  for (const slot of scoped) {
    const sensor = sensorById.get(slot.sensorId) || null;
    grid.appendChild(cell(slot, sensor, selectedId, sensor ? queuePosMap.get(sensor.sensorId) : undefined, onSelect));
  }
  for (let i = scoped.length; i < rows.length * cols; i += 1) {
    const pad = document.createElement('div');
    pad.className = 'sds-cell';
    pad.dataset.gap = 'true';
    pad.dataset.state = 'unknown';
    grid.appendChild(pad);
  }
  return grid;
}

function cleanlinessBar(summary) {
  const wrap = document.createElement('div');
  wrap.className = 'wall-bar-wrap num';
  const bar = document.createElement('div');
  bar.className = 'wall-bar';

  const dirty = summary ? Number(summary.dirty) || 0 : 0;
  const unc = summary ? (Number(summary.uncertain) || 0) + (Number(summary.notClassified) || 0) : 0;
  const clean = summary ? Number(summary.cleaner) || 0 : 0;

  for (const [cls, count] of [['dirty', dirty], ['unc', unc], ['clean', clean]]) {
    if (count > 0) {
      const seg = document.createElement('div');
      seg.className = 'wall-bar-seg ' + cls;
      seg.style.flexGrow = String(count);
      bar.appendChild(seg);
    }
  }
  wrap.appendChild(bar);

  const labels = document.createElement('div');
  labels.className = 'wall-bar-labels';
  const left = document.createElement('span');
  left.textContent = summary
    ? 'D:' + String(dirty) + ' | U:' + String(unc) + ' | C:' + String(clean)
    : 'Wall summary unavailable';
  const right = document.createElement('span');
  right.textContent = summary && typeof summary.maxScore === 'number'
    ? 'Max ' + summary.maxScore.toFixed(1)
    : 'Max --';
  labels.appendChild(left);
  labels.appendChild(right);
  wrap.appendChild(labels);
  return wrap;
}

function wallBlock(wall, entry, sensorById, snapshot, selectedId, queuePosMap, onSelect) {
  const section = document.createElement('section');
  section.className = 'wall-block';
  section.dataset.wall = wall;
  section.setAttribute('aria-label', (WALL_LABELS[wall] || wall) + ' tier map');

  const head = document.createElement('div');
  head.className = 'wall-header';
  const name = document.createElement('strong');
  name.textContent = wall + ' (' + String(WALL_COLUMNS[wall]) + 'x' + String(WALL_ROWS) + ' = ' + String(entry.sensors) + ')';
  const meta = document.createElement('span');
  meta.textContent = deviceNote(wall, snapshot);
  head.appendChild(name);
  head.appendChild(meta);
  section.appendChild(head);

  const gridWrap = document.createElement('div');
  gridWrap.className = 'wall-grid-wrap';
  gridWrap.appendChild(halfGrid(wall, entry.slots, sensorById, UPPER_ROWS, selectedId, queuePosMap, onSelect));

  const divider = document.createElement('div');
  divider.className = 'tier-divider';
  const divLabel = document.createElement('span');
  divLabel.textContent = 'UPPER (R1-3) / LOWER (R4-6)';
  divider.appendChild(divLabel);
  gridWrap.appendChild(divider);

  gridWrap.appendChild(halfGrid(wall, entry.slots, sensorById, LOWER_ROWS, selectedId, queuePosMap, onSelect));
  section.appendChild(gridWrap);
  section.appendChild(cleanlinessBar(wallSummaryOf(snapshot, wall)));
  return section;
}

function centerHub(snapshot) {
  const hub = document.createElement('div');
  hub.className = 'u-center-hub';

  const traj = document.createElement('div');
  traj.className = 'hub-trajectory';
  const top = document.createElement('div');
  top.className = 'hub-traj-top';
  const route = document.createElement('span');
  route.className = 'hub-traj-route num';
  const badge = document.createElement('span');

  const sub = document.createElement('div');
  sub.className = 'hub-traj-sub num';
  const subLeft = document.createElement('span');
  const subRight = document.createElement('span');

  const active = snapshot ? snapshot.activeJob : null;
  if (active && active.jetId) {
    const jet = WATER_JETS_BY_ID[active.jetId];
    route.textContent = String(active.jetId)
      + (jet ? ' [' + jet.installedWall + ' ' + jet.installedTier + ']' : '')
      + ' -> ' + String(active.targetSensorId || '?')
      + (jet ? ' [' + jet.targetWall + ' ' + jet.targetTier + ']' : '');
    badge.className = 'pill-state pill-firing num';
    badge.textContent = String(active.phaseLabel || active.phase || 'ACTIVE');
    subLeft.textContent = 'Valve: ' + String(active.valveId || '-')
      + ' (' + String(active.valveOpenCommand || 'N/A')
      + (typeof active.valveOutletPressureBar === 'number' ? ' | ' + active.valveOutletPressureBar.toFixed(1) + ' bar' : '') + ')';
    subRight.textContent = 'Cabinet: ' + cabinetInfoForJet(active.jetId)
      + ' | Park (' + String(SAFE_RETURN_CENTRE_MM.horizontal) + ', ' + String(SAFE_RETURN_CENTRE_MM.vertical) + ')';
  } else {
    route.textContent = 'STANDBY - NO ACTIVE CLEANING JOB';
    badge.className = 'pill-state pill-standby';
    badge.textContent = 'IDLE';
    subLeft.textContent = 'Safe Return Centre: (' + String(SAFE_RETURN_CENTRE_MM.horizontal) + ', ' + String(SAFE_RETURN_CENTRE_MM.vertical) + ') mm';
    subRight.textContent = '108 SDS | 4 WSB x 2 WJ | Read-Only';
  }

  top.appendChild(route);
  top.appendChild(badge);
  sub.appendChild(subLeft);
  sub.appendChild(subRight);
  traj.appendChild(top);
  traj.appendChild(sub);
  hub.appendChild(traj);

  const legend = document.createElement('div');
  legend.className = 'legend-grid';
  legend.id = 'legend';
  renderLegend(legend);
  hub.appendChild(legend);

  return hub;
}

export function renderWallMap(host, model, snapshot, selection, onSelect) {
  host.textContent = '';
  if (!model || !model.ok) {
    const refusal = document.createElement('p');
    refusal.className = 'note';
    refusal.dataset.tone = 'bad';
    refusal.textContent = 'WALL MAP REFUSED: ' + (model && model.error ? model.error : 'no model')
      + ' - the presentation shows nothing rather than a topology it cannot prove.';
    host.appendChild(refusal);
    return;
  }

  const sensors = Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  const sensorById = new Map();
  for (const sensor of sensors) {
    if (sensor && typeof sensor.sensorId === 'string') sensorById.set(sensor.sensorId, sensor);
  }
  const queuePosMap = queuePositionsMap(snapshot);

  // Column 1 (4fr): Left Installed Nozzles + LEFT Wall (4x6) + WSB Topology Footer
  const leftCol = document.createElement('div');
  leftCol.className = 'u-col u-col-side';
  leftCol.appendChild(nozzleStrip('LEFT', snapshot));
  leftCol.appendChild(wallBlock('LEFT', model.walls.get('LEFT'), sensorById, snapshot, selection, queuePosMap, onSelect));
  leftCol.appendChild(sideFooterCard('WSB1 / WSB4 Topology (A/C Horiz, B/D Vert)', 'CANONICAL'));

  // Column 2 (5.2fr): REAR Wall (5x6) + U-Center Command Hub + FRONT Wall (5x6)
  const centerCol = document.createElement('div');
  centerCol.className = 'u-col u-col-center';
  centerCol.appendChild(wallBlock('REAR', model.walls.get('REAR'), sensorById, snapshot, selection, queuePosMap, onSelect));
  centerCol.appendChild(centerHub(snapshot));
  centerCol.appendChild(wallBlock('FRONT', model.walls.get('FRONT'), sensorById, snapshot, selection, queuePosMap, onSelect));

  // Column 3 (4fr): Right Installed Nozzles + RIGHT Wall (4x6) + WSB Topology Footer
  const rightCol = document.createElement('div');
  rightCol.className = 'u-col u-col-side';
  rightCol.appendChild(nozzleStrip('RIGHT', snapshot));
  rightCol.appendChild(wallBlock('RIGHT', model.walls.get('RIGHT'), sensorById, snapshot, selection, queuePosMap, onSelect));
  rightCol.appendChild(sideFooterCard('WSB2 / WSB3 Topology (108 SDS Normal)', '108 / 108'));

  host.appendChild(leftCol);
  host.appendChild(centerCol);
  host.appendChild(rightCol);
}

export function renderLegend(host) {
  host.textContent = '';
  const items = [
    ['clean', 'CLEANER'],
    ['dirty', 'DIRTY'],
    ['watch', 'OBSERVE/BAD'],
    ['stale', 'STALE'],
    ['unknown', 'NOT CLASSIFIED'],
  ];
  for (const [state, label] of items) {
    const item = document.createElement('span');
    item.className = 'legend-item';
    const swatch = document.createElement('span');
    swatch.className = 'legend-swatch';
    swatch.dataset.state = state;
    const textNode = document.createElement('span');
    textNode.textContent = label;
    item.appendChild(swatch);
    item.appendChild(textNode);
    host.appendChild(item);
  }
  const extra = document.createElement('span');
  extra.className = 'legend-item';
  extra.textContent = 'ACT=Active | Q#=Queued';
  host.appendChild(extra);
}

export function stepSelection(ordered, currentId, delta) {
  if (!Array.isArray(ordered) || ordered.length === 0) return null;
  const index = ordered.indexOf(currentId);
  if (index < 0) return ordered[0];
  const next = (index + delta + ordered.length) % ordered.length;
  return ordered[next];
}
