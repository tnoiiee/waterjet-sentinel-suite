// WJSS Operator presentation - the selected Sensor and its bounded trend.
//
// The facts are the published Sensor projection and nothing else: identity, placement, assigned
// cleaning devices, TC channel identities, score and quality with its reason, queue and alarm
// state. A value the host did not publish reads NOT_PROVIDED; the presentation never computes a
// substitute, never carries a value forward from a previous revision and never presents a stale
// reading as current.
//
// The trend is the Snapshot-delivered bounded window. The canvas is used instead of an inline
// vector element so this file contains no namespace literal of any kind, and a null series value
// is drawn as a break in the line: a missing sample never becomes an interpolated segment.

import { text, number, timeOf } from './feed.mjs';

const SERIES_COLORS = ['#4bb4ff', '#2fbf71', '#f2b53c', '#ff5d5d'];

export function renderSensorDetail(host, snapshot, sensorId) {
  host.textContent = '';
  const sensors = snapshot && Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  const sensor = sensors.find((entry) => entry && entry.sensorId === sensorId);

  if (!sensor) {
    const row = document.createElement('dt');
    row.textContent = 'selection';
    const value = document.createElement('dd');
    value.dataset.tone = 'mute';
    value.textContent = sensorId ? 'no Sensor published under ' + String(sensorId) : 'no Sensor selected';
    host.appendChild(row);
    host.appendChild(value);
    const note = document.createElement('dt');
    note.textContent = 'how to select';
    const noteValue = document.createElement('dd');
    noteValue.dataset.tone = 'mute';
    noteValue.textContent = 'click a wall-map cell, or use the arrow keys to walk the approved order';
    host.appendChild(note);
    host.appendChild(noteValue);
    return null;
  }

  const slot = (Array.isArray(snapshot.wallMap) ? snapshot.wallMap : [])
    .find((entry) => entry && entry.sensorId === sensor.sensorId);

  const rows = [
    ['sensor', text(sensor.sensorId), null],
    ['device', text(sensor.deviceId), null],
    ['wall', text(sensor.wall), null],
    ['logical row', text(sensor.logicalRow) + ' - ' + (slot ? tierOfText(slot.logicalRow) : 'tier not published'), null],
    ['logical column', text(sensor.logicalColumn), null],
    ['wall column / row', text(sensor.wallColumn) + ' / ' + text(sensor.wallRow), null],
    ['scan order', text(sensor.scanOrder), null],
    ['position kind', text(sensor.positionKind), null],
    ['cleaning device', text(sensor.assignedWaterJetId), null],
    ['isolation valve', text(sensor.assignedIsolationValveId), null],
    ['TC front channel', text(sensor.tcFrontChannel), null],
    ['TC rear channel', text(sensor.tcRearChannel), null],
    ['classification', text(sensor.classification), 'pill ' + text(sensor.classification, 'NONE')],
    ['basis', text(sensor.classificationBasis), null],
    ['dirty score', number(sensor.dirtyScore, 1), null],
    ['last validated score', number(sensor.lastValidatedScore, 1), null],
    ['last validated at', timeOf(sensor.lastValidatedAt), null],
    ['quality', text(sensor.quality), 'pill ' + text(sensor.quality, 'NONE')],
    ['quality reason', text(sensor.qualityReason), null],
    ['source timestamp', timeOf(sensor.sourceTimestamp), null],
    ['queue state', text(sensor.queueState), null],
    ['active job target', sensor.isActiveJobTarget === true ? 'YES - this Sensor is being cleaned' : 'no', null],
    ['alarm state', text(sensor.alarmState), null],
    ['alarm severity', text(sensor.alarmSeverity), null],
  ];

  for (const [label, value, className] of rows) {
    const term = document.createElement('dt');
    term.textContent = label;
    const entry = document.createElement('dd');
    if (className) {
      const pill = document.createElement('span');
      pill.className = className;
      pill.textContent = value;
      entry.appendChild(pill);
    } else {
      entry.textContent = value;
    }
    if (label === 'quality' && (String(sensor.quality) === 'STALE' || String(sensor.quality) === 'BAD')) {
      entry.dataset.tone = 'bad';
    }
    host.appendChild(term);
    host.appendChild(entry);
  }
  return sensor;
}

function tierOfText(row) {
  if (row === 1 || row === 2 || row === 3) return 'UPPER tier';
  if (row === 4 || row === 5 || row === 6) return 'LOWER tier';
  return 'unplaced row';
}

/**
 * Draws the bounded trend window. The window is what the Snapshot carried: the presentation does
 * not grow it, does not stitch an older window onto it and does not draw a point the host omitted.
 */
export function drawTrend(canvas, caption, note, snapshot, pump) {
  const context = typeof canvas.getContext === 'function' ? canvas.getContext('2d') : null;
  const trend = snapshot && snapshot.trend ? snapshot.trend : null;
  const points = trend && Array.isArray(trend.points) ? trend.points : [];
  const names = trend && Array.isArray(trend.seriesNames) ? trend.seriesNames : [];
  const capacity = trend && typeof trend.capacity === 'number' ? trend.capacity : 0;

  const width = canvas.width;
  const height = canvas.height;

  if (!context) {
    if (caption) caption.textContent = 'trend: this browser offered no drawing surface';
    return;
  }

  context.fillStyle = '#0c141b';
  context.fillRect(0, 0, width, height);

  const pad = { left: 44, right: 10, top: 10, bottom: 18 };
  const plotW = Math.max(10, width - pad.left - pad.right);
  const plotH = Math.max(10, height - pad.top - pad.bottom);

  if (points.length === 0) {
    context.strokeStyle = '#27384a';
    context.strokeRect(pad.left, pad.top, plotW, plotH);
    context.fillStyle = '#6f8496';
    context.font = '11px monospace';
    context.fillText('the window is empty: the Runtime has published no trend point yet', pad.left + 8, pad.top + 22);
    if (caption) caption.textContent = 'trend: 0 of ' + String(capacity) + ' points, capacity ' + String(capacity);
    if (note) note.textContent = 'An empty window is a fact about the host, not a rendering failure.';
    return;
  }

  let low = Infinity;
  let high = -Infinity;
  let gaps = 0;
  for (const point of points) {
    if (!point) continue;
    if (typeof point.setpoint === 'number' && Number.isFinite(point.setpoint)) {
      low = Math.min(low, point.setpoint);
      high = Math.max(high, point.setpoint);
    }
    const series = Array.isArray(point.series) ? point.series : [];
    for (const value of series) {
      if (value === null || value === undefined) { gaps += 1; continue; }
      if (typeof value !== 'number' || !Number.isFinite(value)) continue;
      low = Math.min(low, value);
      high = Math.max(high, value);
    }
  }
  if (pump) {
    if (typeof pump.readyBandLow === 'number') low = Math.min(low, pump.readyBandLow);
    if (typeof pump.readyBandHigh === 'number') high = Math.max(high, pump.readyBandHigh);
  }
  if (!Number.isFinite(low) || !Number.isFinite(high)) { low = 0; high = 1; }
  if (high - low < 1e-6) { high = low + 1; }
  const margin = (high - low) * 0.08;
  low -= margin;
  high += margin;

  const xAt = (index) => pad.left + (points.length === 1 ? plotW / 2 : (index / (points.length - 1)) * plotW);
  const yAt = (value) => pad.top + plotH - ((value - low) / (high - low)) * plotH;

  // Frame, then the ready band, then the job and alarm columns: the background facts are drawn
  // first so a series line is never hidden behind a shaded region.
  context.strokeStyle = '#27384a';
  context.strokeRect(pad.left, pad.top, plotW, plotH);
  context.fillStyle = '#10221a';
  for (let i = 0; i < points.length; i += 1) {
    if (points[i] && points[i].jobActive === true) {
      context.fillRect(xAt(i) - 1.5, pad.top, 3, plotH);
    }
  }
  context.fillStyle = '#2b1315';
  for (let i = 0; i < points.length; i += 1) {
    if (points[i] && points[i].alarmActive === true) {
      context.fillRect(xAt(i) - 1.5, pad.top, 3, plotH);
    }
  }
  if (pump && typeof pump.readyBandLow === 'number' && typeof pump.readyBandHigh === 'number') {
    const yTop = yAt(pump.readyBandHigh);
    const yBottom = yAt(pump.readyBandLow);
    context.fillStyle = 'rgba(47,191,113,0.16)';
    context.fillRect(pad.left, Math.min(yTop, yBottom), plotW, Math.abs(yBottom - yTop));
  }

  context.font = '10px monospace';
  context.fillStyle = '#6f8496';
  context.fillText(number(high, 1), 3, pad.top + 8);
  context.fillText(number(low, 1), 3, pad.top + plotH);
  context.fillText(String(points.length) + ' of ' + String(capacity) + ' points', pad.left, height - 5);

  // Setpoint as a stepped line: it is a property of each published point, so it is drawn per point
  // rather than as one constant invented by the view.
  context.strokeStyle = '#9db0c0';
  context.setLineDash([4, 3]);
  context.beginPath();
  let started = false;
  for (let i = 0; i < points.length; i += 1) {
    const point = points[i];
    if (!point || typeof point.setpoint !== 'number' || !Number.isFinite(point.setpoint)) { started = false; continue; }
    const x = xAt(i);
    const y = yAt(point.setpoint);
    if (!started) { context.moveTo(x, y); started = true; } else { context.lineTo(x, y); }
  }
  context.stroke();
  context.setLineDash([]);

  for (let series = 0; series < Math.max(names.length, 1); series += 1) {
    context.strokeStyle = SERIES_COLORS[series % SERIES_COLORS.length];
    context.lineWidth = 1.2;
    context.beginPath();
    let drawn = false;
    for (let i = 0; i < points.length; i += 1) {
      const point = points[i];
      const value = point && Array.isArray(point.series) ? point.series[series] : null;
      if (value === null || value === undefined || typeof value !== 'number' || !Number.isFinite(value)) {
        drawn = false;
        continue;
      }
      const x = xAt(i);
      const y = yAt(value);
      if (!drawn) { context.moveTo(x, y); drawn = true; } else { context.lineTo(x, y); }
    }
    context.stroke();
  }
  context.lineWidth = 1;

  if (caption) {
    caption.textContent = 'trend: ' + names.map((name, index) => name + ' = ' + SERIES_COLORS[index % SERIES_COLORS.length]).join(', ')
      + ' - window ' + String(points.length) + ' of capacity ' + String(capacity);
  }
  if (note) {
    note.textContent = gaps > 0
      ? String(gaps) + ' null series value' + (gaps === 1 ? '' : 's') + ' in this window: drawn as '
        + (gaps === 1 ? 'a gap' : 'gaps') + ', never interpolated.'
      : 'No gap in this window: every published point carried a value.';
  }
}
