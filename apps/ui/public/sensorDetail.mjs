// WJSS Operator presentation - Selected Sensor detail and bounded Pressure Trend.
//
// Everything printed here is read straight from the Snapshot's published Sensor record and bounded
// trend window. No temperature is invented from a channel name, no gap in the pressure series is
// smoothed over, and no trend history is kept across a host reset.

import { text, number, timeOf } from './feed.mjs';
import { WALL_LABELS, tierOfRow } from './topology.mjs';

function fact(list, label, value, tone) {
  const dt = document.createElement('dt');
  dt.textContent = label;
  const dd = document.createElement('dd');
  dd.textContent = value;
  if (tone) dd.dataset.tone = tone;
  list.appendChild(dt);
  list.appendChild(dd);
}

function kvRow(parent, label, value, tone) {
  const row = document.createElement('div');
  row.className = 'kv-row';
  const left = document.createElement('span');
  left.textContent = label;
  const right = document.createElement('strong');
  right.textContent = value;
  if (tone) right.dataset.tone = tone;
  row.appendChild(left);
  row.appendChild(right);
  parent.appendChild(row);
}

function findSensor(snapshot, selectedId) {
  const sensors = snapshot && Array.isArray(snapshot.sensors) ? snapshot.sensors : [];
  if (!selectedId) return null;
  for (const sensor of sensors) {
    if (sensor && sensor.sensorId === selectedId) return sensor;
  }
  return null;
}

export function renderSensorDetail(host, snapshot, selectedId) {
  host.textContent = '';
  const sensor = findSensor(snapshot, selectedId);
  if (!sensor) {
    const note = document.createElement('p');
    note.className = 'note';
    note.textContent = 'Select a Sensor on the Boiler Wall Map or press an arrow key to inspect its published record.';
    host.appendChild(note);
    return;
  }

  const list = document.createElement('dl');
  list.className = 'facts';
  const wall = String(sensor.wall || '').toUpperCase();
  fact(list, 'sensor', text(sensor.sensorId) + ' (device ' + text(sensor.deviceId) + ')');
  fact(list, 'location', (WALL_LABELS[wall] || text(sensor.wall)) + ' - row '
    + text(sensor.logicalRow) + ' (' + tierOfRow(sensor.logicalRow) + '), col '
    + text(sensor.logicalColumn) + ', scan order ' + text(sensor.scanOrder));
  fact(list, 'cleaning path', text(sensor.assignedWaterJetId) + ' / ' + text(sensor.assignedIsolationValveId));
  fact(list, 'thermocouple channels', text(sensor.tcFrontChannel) + ' (front) / '
    + text(sensor.tcRearChannel) + ' (rear)');
  fact(list, 'dirty score', number(sensor.dirtyScore, 1) + ' (last validated '
    + number(sensor.lastValidatedScore, 1) + ')',
    String(sensor.classification) === 'DIRTY' ? 'bad'
      : String(sensor.classification) === 'CLEANER' ? 'ok' : null);
  fact(list, 'classification', text(sensor.classification) + ' on basis ' + text(sensor.classificationBasis));
  fact(list, 'quality', text(sensor.quality) + (sensor.qualityReason ? ' - ' + sensor.qualityReason : ''),
    String(sensor.quality) === 'GOOD' ? 'ok' : 'warn');
  fact(list, 'source timestamp', timeOf(sensor.sourceTimestamp));
  fact(list, 'queue state', text(sensor.queueState) + (sensor.isActiveJobTarget === true ? ' (ACTIVE JOB TARGET)' : ''));
  fact(list, 'alarm', text(sensor.alarmState) + (sensor.alarmSeverity ? ' / ' + sensor.alarmSeverity : ''),
    String(sensor.alarmState).indexOf('ACTIVE') === 0 ? 'bad' : null);
  fact(list, 'active job target', sensor.isActiveJobTarget === true ? 'YES' : 'no');
  host.appendChild(list);
}

export function renderSelectedCard(host, snapshot, selectedId) {
  host.textContent = '';
  const sensor = findSensor(snapshot, selectedId);
  if (!sensor) {
    const note = document.createElement('p');
    note.className = 'note';
    note.textContent = 'Click any SDS cell on the Boiler Wall Map to inspect.';
    host.appendChild(note);
    return;
  }
  const wall = String(sensor.wall || '').toUpperCase();
  kvRow(host, 'Wall / Tier', wall + ' ' + tierOfRow(sensor.logicalRow) + ' (R' + text(sensor.logicalRow) + ' C' + text(sensor.logicalColumn) + ')');
  kvRow(host, 'Dirty Score', number(sensor.dirtyScore, 1) + ' (val ' + number(sensor.lastValidatedScore, 1) + ')',
    String(sensor.classification) === 'DIRTY' ? 'bad' : String(sensor.classification) === 'CLEANER' ? 'ok' : 'warn');
  kvRow(host, 'State / Qual', text(sensor.classification) + ' | ' + text(sensor.quality));
  kvRow(host, 'Assigned WJ', text(sensor.assignedWaterJetId) + ' / ' + text(sensor.assignedIsolationValveId));
  kvRow(host, 'Queue / Alarm', text(sensor.queueState) + ' | ' + text(sensor.alarmState));
  kvRow(host, 'TC Pair', text(sensor.tcFrontChannel) + ' / ' + text(sensor.tcRearChannel));
}

export function drawTrend(canvas, caption, note, snapshot, pump) {
  if (!canvas || typeof canvas.getContext !== 'function') return;
  const ctx = canvas.getContext('2d');
  if (!ctx) return;

  const trend = snapshot ? snapshot.trend : null;
  const points = trend && Array.isArray(trend.points) ? trend.points : [];
  const capacity = trend && typeof trend.capacity === 'number' ? trend.capacity : 0;
  const names = trend && Array.isArray(trend.seriesNames) ? trend.seriesNames : [];
  const active = snapshot ? snapshot.activeJob : null;

  const width = canvas.width;
  const height = canvas.height;
  ctx.clearRect(0, 0, width, height);

  const pad = { left: 46, right: 16, top: 10, bottom: 20 };
  const plotW = width - pad.left - pad.right;
  const plotH = height - pad.top - pad.bottom;

  // Dark canvas backdrop
  ctx.fillStyle = '#0d1014';
  ctx.fillRect(0, 0, width, height);

  if (points.length === 0) {
    ctx.fillStyle = '#94a3b8';
    ctx.font = '12px "Segoe UI", Roboto, sans-serif';
    ctx.fillText('No trend samples published in this frame.', pad.left + 8, pad.top + 28);
    if (caption) caption.textContent = '0 of ' + String(capacity) + ' samples - waiting for published history.';
    if (note) note.textContent = 'DATA GAP: no published pressure sample yet.';
    return;
  }

  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;
  const consider = (v) => {
    if (typeof v === 'number' && Number.isFinite(v)) {
      if (v < min) min = v;
      if (v > max) max = v;
    }
  };
  for (const point of points) {
    consider(point.setpoint);
    if (Array.isArray(point.series)) {
      for (const v of point.series) consider(v);
    }
  }
  if (pump) {
    consider(pump.readyBandLow);
    consider(pump.readyBandHigh);
    consider(pump.setpoint);
    consider(pump.pressure);
  }
  if (!Number.isFinite(min) || !Number.isFinite(max)) {
    min = 0;
    max = 28;
  }
  if (min > 0) min = Math.max(0, min - 2);
  if (max === min) {
    min -= 1;
    max += 1;
  } else {
    const margin = (max - min) * 0.12;
    min = Math.max(0, min - margin);
    max += margin;
  }

  const xOf = (index) => pad.left + (points.length === 1 ? plotW / 2 : (index / (points.length - 1)) * plotW);
  const yOf = (value) => pad.top + plotH - ((value - min) / (max - min)) * plotH;

  // Shaded green Ready Band
  if (pump && typeof pump.readyBandLow === 'number' && typeof pump.readyBandHigh === 'number') {
    const topY = yOf(pump.readyBandHigh);
    const botY = yOf(pump.readyBandLow);
    ctx.fillStyle = 'rgba(46, 160, 109, 0.12)';
    ctx.fillRect(pad.left, topY, plotW, Math.max(2, botY - topY));
  }

  // Horizontal grid lines
  ctx.strokeStyle = '#28303b';
  ctx.lineWidth = 1;
  for (let i = 0; i <= 2; i += 1) {
    const gy = pad.top + (i / 2) * plotH;
    ctx.beginPath();
    ctx.moveTo(pad.left, gy);
    ctx.lineTo(width - pad.right, gy);
    ctx.stroke();
  }

  // Highlight explicit DATA GAP columns where primary series is null
  let gapCount = 0;
  for (let i = 0; i < points.length; i += 1) {
    const v = Array.isArray(points[i].series) ? points[i].series[0] : null;
    if (typeof v !== 'number' || !Number.isFinite(v)) {
      gapCount += 1;
      const gx = xOf(i);
      ctx.fillStyle = 'rgba(148, 163, 184, 0.10)';
      ctx.fillRect(gx - 2, pad.top, 4, plotH);
    }
  }

  // Dashed amber setpoint series
  ctx.strokeStyle = '#fbbf24';
  ctx.setLineDash([4, 3]);
  ctx.lineWidth = 1.25;
  ctx.beginPath();
  let drawn = false;
  for (let i = 0; i < points.length; i += 1) {
    const v = points[i].setpoint;
    if (typeof v === 'number' && Number.isFinite(v)) {
      const x = xOf(i);
      const y = yOf(v);
      if (!drawn) ctx.moveTo(x, y); else ctx.lineTo(x, y);
      drawn = true;
    } else {
      drawn = false;
    }
  }
  ctx.stroke();
  ctx.setLineDash([]);

  // Solid cyan/emerald primary series (nulls break the line, never interpolated)
  const colours = ['#38bdf8', '#34d399', '#a78bfa'];
  const count = names.length > 0 ? names.length : 1;
  for (let s = 0; s < count; s += 1) {
    ctx.strokeStyle = colours[s % colours.length];
    ctx.lineWidth = s === 0 ? 2 : 1.3;
    ctx.beginPath();
    drawn = false;
    for (let i = 0; i < points.length; i += 1) {
      const series = points[i].series;
      const v = Array.isArray(series) ? series[s] : null;
      if (typeof v === 'number' && Number.isFinite(v)) {
        const x = xOf(i);
        const y = yOf(v);
        if (!drawn) ctx.moveTo(x, y); else ctx.lineTo(x, y);
        drawn = true;
      } else {
        drawn = false;
      }
    }
    ctx.stroke();
  }

  // Latest active point marker
  for (let i = points.length - 1; i >= 0; i -= 1) {
    const series = points[i].series;
    const v = Array.isArray(series) ? series[0] : null;
    if (typeof v === 'number' && Number.isFinite(v)) {
      ctx.fillStyle = '#38bdf8';
      ctx.beginPath();
      ctx.arc(xOf(i), yOf(v), 3, 0, Math.PI * 2);
      ctx.fill();
      break;
    }
  }

  // Y-axis ticks & X-axis time markers
  ctx.fillStyle = '#94a3b8';
  ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
  ctx.textAlign = 'right';
  ctx.fillText(max.toFixed(1) + 'b', pad.left - 5, pad.top + 8);
  ctx.fillText(((min + max) / 2).toFixed(1) + 'b', pad.left - 5, pad.top + plotH / 2 + 3);
  ctx.fillText(min.toFixed(1) + 'b', pad.left - 5, pad.top + plotH);

  ctx.textAlign = 'left';
  ctx.fillText(timeOf(points[0].t), pad.left, height - 5);
  ctx.textAlign = 'right';
  ctx.fillText('NOW ' + timeOf(points[points.length - 1].t), width - pad.right, height - 5);
  ctx.textAlign = 'left';

  if (caption) {
    caption.textContent = String(points.length) + ' of ' + String(capacity) + ' samples'
      + ' | series: ' + (names.length > 0 ? names.join(', ') : 'unnamed')
      + (gapCount > 0 ? ' | ' + String(gapCount) + ' DATA GAP sample(s)' : '')
      + ' | null values break the line and are never interpolated';
  }
  if (note) {
    note.textContent = active && typeof active.valveOutletPressureBar === 'number'
      ? 'Active ' + text(active.valveId) + ' live: ' + active.valveOutletPressureBar.toFixed(1)
        + ' bar (no IV history channel published)'
      : 'No IV history channel published by read-only Runtime';
  }
}
