// WJSS Operator presentation - the authoritative 108-Sensor boiler wall map.
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

// The Single Topology Source Rule: this panel does not own a copy of the matrix. Every constant below
// comes from './topology.mjs', which is the one place the wall / tier / count table is written and the
// one place the contract test compares it against the .NET catalogs. Re-exporting keeps the map's own
// public shape intact for its callers while removing the second definition.
import { EXPECTED_PER_WALL, EXPECTED_TOTAL, LOWER_ROWS, UPPER_ROWS, WALL_COLUMNS, WALL_LABELS, WALL_ORDER, WALL_ROWS, tierOfRow } from './topology.mjs';

export { EXPECTED_PER_WALL, EXPECTED_TOTAL, LOWER_ROWS, UPPER_ROWS, WALL_COLUMNS, WALL_LABELS, WALL_ORDER, WALL_ROWS, tierOfRow };

export function isSensor(value) {
  return String(value).toUpperCase() === 'SENSOR';
}

/**
 * The visual state key of one Sensor. Colour never carries a state alone: the same switch also
 * chooses the marker shape and the text a cell prints, and a stale reading is reported as stale
 * on top of whatever classification it carries.
 */
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

/**
 * Groups the published matrix by wall and tier. Refusals are returned, never swallowed: the
 * caller shows them on the panel that failed rather than rendering a wall it cannot prove.
 */
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

  // The approved navigation order: wall order, then logical row, then logical column.
  const ordered = [];
  for (const wall of WALL_ORDER) {
    for (const slot of walls.get(wall).slots) {
      if (typeof slot.sensorId === 'string' && slot.sensorId.length > 0) ordered.push(slot.sensorId);
    }
  }

  return { ok: true, walls, sensorCount: byId.size, ordered, nonSensor };
}

function header(wall, count, devices, subtotal) {
  const head = document.createElement('div');
  head.className = 'wall-head';
  const name = document.createElement('span');
  name.className = 'wall-name';
  name.textContent = WALL_LABELS[wall] || wall;
  const total = document.createElement('span');
  total.className = 'wall-count';
  total.textContent = count + ' / ' + EXPECTED_PER_WALL[wall];
  head.appendChild(name);
  head.appendChild(total);
  if (devices) {
    const dev = document.createElement('span');
    dev.className = 'wall-devices';
    dev.textContent = devices;
    head.appendChild(dev);
  }
  if (subtotal) {
    const tally = document.createElement('span');
    tally.className = 'wall-subtotal';
    tally.textContent = subtotal;
    head.appendChild(tally);
  }
  return head;
}

function cell(slot, sensor, selectedId, onSelect) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'cell-btn';
  const id = document.createElement('span');
  id.className = 'cell-id';
  const value = document.createElement('span');
  value.className = 'cell-value';
  const mark = document.createElement('span');
  mark.className = 'cell-mark';
  mark.setAttribute('aria-hidden', 'true');

  if (!sensor) {
    button.dataset.gap = 'true';
    button.dataset.state = 'unknown';
    button.disabled = true;
    id.textContent = typeof slot.sensorId === 'string' && slot.sensorId.length > 0 ? slot.sensorId : slot.slotId;
    value.textContent = 'NO SENSOR PUBLISHED';
    button.title = slot.slotId + ': the Runtime published this matrix position without a Sensor identity, '
      + 'so the presentation shows the slot and refuses to invent one.';
    button.appendChild(id);
    button.appendChild(value);
    button.appendChild(mark);
    return button;
  }

  const visual = visualOf(sensor);
  button.dataset.state = visual.key;
  button.dataset.alarm = alarmOf(sensor);
  button.dataset.queue = queueOf(sensor);
  button.dataset.target = sensor.isActiveJobTarget === true ? 'true' : 'false';
  button.dataset.selected = sensor.sensorId === selectedId ? 'true' : 'false';
  button.setAttribute('aria-current', sensor.sensorId === selectedId ? 'true' : 'false');
  id.textContent = sensor.sensorId + ' ' + visual.marker + ' ' + visual.label;
  value.textContent = (typeof sensor.dirtyScore === 'number' ? sensor.dirtyScore.toFixed(1) : 'no score')
    + ' / ' + String(sensor.quality || 'NOT_PROVIDED');
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
  button.dataset.sensor = sensor.sensorId;
  button.appendChild(id);
  button.appendChild(value);
  button.appendChild(mark);
  return button;
}

function tierBlock(wall, slots, sensorById, tier, selectedId, onSelect) {
  const rows = tier === 'UPPER' ? UPPER_ROWS : LOWER_ROWS;
  const scoped = slots.filter((slot) => rows.indexOf(slot.logicalRow) >= 0);
  const block = document.createElement('div');
  block.className = 'tier';
  const label = document.createElement('p');
  label.className = 'tier-label';
  label.textContent = tier + ' - logical rows ' + rows[0] + ' to ' + rows[rows.length - 1];
  block.appendChild(label);

  const grid = document.createElement('div');
  grid.className = 'slotgrid';
  for (const slot of scoped) grid.appendChild(cell(slot, sensorById.get(slot.sensorId) || null, selectedId, onSelect));
  for (let i = scoped.length; i < rows.length * WALL_COLUMNS[wall]; i += 1) {
    const pad = document.createElement('div');
    pad.className = 'cell-btn';
    pad.dataset.gap = 'true';
    pad.dataset.state = 'unknown';
    pad.textContent = '';
    grid.appendChild(pad);
  }
  block.appendChild(grid);
  return block;
}

/** Wall-level device note: which Water Jets list this wall as their target. Read-only reference. */
function deviceNote(wall, snapshot) {
  const jets = Array.isArray(snapshot.waterJets) ? snapshot.waterJets : [];
  const targeted = jets.filter((jet) => String(jet.targetWall || '').toUpperCase() === wall);
  if (targeted.length === 0) return '';
  return 'cleaned by ' + targeted.map((jet) => String(jet.waterJetId || '?')).join(' ');
}

/**
 * The published per-wall subtotal, taken from OperationalSnapshot.walls (WallSummary) rather than
 * counted from the cells on this panel. A mismatch between the two is a host-side contradiction, so the
 * line prints both numbers and lets the operator see the disagreement instead of the map hiding it.
 */
function wallSubtotal(snapshot, wall) {
  const summaries = snapshot && Array.isArray(snapshot.walls) ? snapshot.walls : [];
  const summary = summaries.find((entry) => String(entry && entry.wall || '').toUpperCase() === wall);
  if (!summary) return wall + ': no WallSummary published for this wall';
  const cells = countedFor(snapshot, wall);
  const counts = 'cleaner ' + String(summary.cleaner) + ', dirty ' + String(summary.dirty)
    + ', uncertain ' + String(summary.uncertain) + ', not classified ' + String(summary.notClassified)
    + ', max score ' + (typeof summary.maxScore === 'number' ? summary.maxScore.toFixed(1) : 'no score');
  return counts + (cells === summary.total ? '' : ' | CELLS ON THIS WALL: ' + String(cells) + ' vs TOTAL ' + String(summary.total));
}

function countedFor(snapshot, wall) {
  const sensors = Array.isArray(snapshot && snapshot.sensors) ? snapshot.sensors : [];
  let count = 0;
  for (const sensor of sensors) {
    if (String((sensor && sensor.wall) || '').toUpperCase() === wall) count += 1;
  }
  return count;
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

  for (const wall of WALL_ORDER) {
    const entry = model.walls.get(wall);
    const section = document.createElement('section');
    section.className = 'wall';
    section.dataset.wall = wall;
    section.setAttribute('aria-label', (WALL_LABELS[wall] || wall) + ' tier map');
    section.appendChild(header(wall, entry.sensors, deviceNote(wall, snapshot), wallSubtotal(snapshot, wall)));
    section.appendChild(tierBlock(wall, entry.slots, sensorById, 'UPPER', selection, onSelect));
    section.appendChild(tierBlock(wall, entry.slots, sensorById, 'LOWER', selection, onSelect));
    host.appendChild(section);
  }
}

export function renderLegend(host) {
  host.textContent = '';
  const items = [
    ['dirty', 'DIRTY'], ['watch', 'OBSERVE or BAD'], ['clean', 'CLEANER'],
    ['unknown', 'NOT CLASSIFIED'], ['stale', 'STALE or DISABLED'],
  ];
  for (const [state, label] of items) {
    const item = document.createElement('span');
    item.className = 'legend-item';
    const swatch = document.createElement('span');
    swatch.className = 'legend-swatch';
    swatch.dataset.state = state;
    const text = document.createElement('span');
    text.textContent = label;
    item.appendChild(swatch);
    item.appendChild(text);
    host.appendChild(item);
  }
  const extra = document.createElement('span');
  extra.className = 'legend-item';
  extra.textContent = 'NEXT = the active cleaning target, Q = queued, boxed = critical alarm, hatched = no Sensor published';
  host.appendChild(extra);
}

/**
 * Keyboard navigation over the approved order. The step is taken inside the 108-name list, so the
 * selection cannot land on a slot the host did not publish, and it never fabricates a Sensor.
 */
export function stepSelection(ordered, currentId, delta) {
  if (!Array.isArray(ordered) || ordered.length === 0) return null;
  const index = ordered.indexOf(currentId);
  if (index < 0) return ordered[0];
  const next = (index + delta + ordered.length) % ordered.length;
  return ordered[next];
}
