// WJSS Operator presentation - the shared canvas plot kit.
//
// Every chart on the screen is drawn here, for three reasons. A gap in a series has to look the same
// everywhere or an operator learns two languages; the label placement has to survive a 1920x1080
// fixed viewport where a resized DOM axis label would reflow the page; and the Runtime publishes a
// small, fixed set of series, so a single kit is what keeps a chart from inventing a series to fill its
// own blank area. Nothing here fetches, nothing here formats a value the host did not publish.
//
// The one rule that is not negotiable: a missing sample stays missing. A null value ends the drawn
// segment (drawn = false) and the next real sample opens a new one. No smoothing, no carry-forward,
// no line across a DATA GAP - a connecting line would claim the plant was steady while nothing was
// being measured.

export const GAP_NOTE = Object.freeze('never interpolated: a null sample is a DATA GAP and ends the drawn segment');

// Colour comes from the stylesheet, not from a literal, so one density theme changes every chart. These
// are the same tokens global.css defines; a chart must not introduce a colour the palette has not.
export const SERIES_TOKENS = Object.freeze({
  primary: '--series-primary',
  secondary: '--series-secondary',
  setpoint: '--series-setpoint',
  band: '--series-band',
  alarm: '--series-alarm',
  gap: '--series-gap',
});

// HiDPI canvas sizing: measures the laid-out CSS box when present, resizes the backing store by
// devicePixelRatio (capped at 3x), applies a matching transform, and returns logical CSS pixel
// dimensions. Falls back safely to the canvas attribute dimensions in headless / Fake-DOM runs.
export function prepareCanvas(canvas, fallbackW, fallbackH) {
  if (!canvas || typeof canvas.getContext !== 'function') return null;
  const ctx = canvas.getContext('2d');
  if (!ctx) return null;

  let cssW = 0;
  let cssH = 0;
  if (typeof canvas.getBoundingClientRect === 'function') {
    const rect = canvas.getBoundingClientRect();
    if (rect && Number.isFinite(rect.width) && Number.isFinite(rect.height)) {
      cssW = Math.round(rect.width);
      cssH = Math.round(rect.height);
    }
  }
  if (cssW < 40 && typeof canvas.clientWidth === 'number' && canvas.clientWidth >= 40) {
    cssW = Math.round(canvas.clientWidth);
  }
  if (cssH < 40 && typeof canvas.clientHeight === 'number' && canvas.clientHeight >= 40) {
    cssH = Math.round(canvas.clientHeight);
  }
  if (cssW < 40) cssW = Math.max(40, Number(fallbackW) || Number(canvas.width) || 320);
  if (cssH < 40) cssH = Math.max(40, Number(fallbackH) || Number(canvas.height) || 140);

  const rawDpr = typeof window !== 'undefined' && typeof window.devicePixelRatio === 'number'
    && Number.isFinite(window.devicePixelRatio) && window.devicePixelRatio > 0
    ? window.devicePixelRatio : 1;
  const dpr = Math.min(3, Math.max(1, rawDpr));

  const targetW = Math.round(cssW * dpr);
  const targetH = Math.round(cssH * dpr);
  if (canvas.width !== targetW) canvas.width = targetW;
  if (canvas.height !== targetH) canvas.height = targetH;

  if (typeof ctx.setTransform === 'function') {
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  }
  return { ctx, width: cssW, height: cssH, dpr };
}

export function contextOf(canvas) {
  if (!canvas || typeof canvas.getContext !== 'function') return null;
  const ctx = canvas.getContext('2d');
  if (!ctx) return null;
  if (canvas.width < 40 || canvas.height < 40) return null;
  return ctx;
}

export function token(ctx, name, fallback) {
  if (typeof document === 'undefined' || !document.documentElement) return fallback;
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value === '' ? fallback : value;
}

// Convert Unix epoch seconds (TrendPoint.T in .NET), epoch ms, numeric string, or ISO-8601 string
// to finite Unix epoch seconds.
export function epochSecondsOf(value) {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value > 1e11 ? value / 1000 : value;
  }
  if (typeof value === 'string' && value.length > 0) {
    if (/^\d{9,13}$/.test(value)) {
      const n = Number(value);
      return n > 1e11 ? n / 1000 : n;
    }
    const parsed = Date.parse(value);
    if (Number.isFinite(parsed)) return parsed / 1000;
  }
  return null;
}

// Human-readable UTC HH:MM:SS for chart X-axis labels (never raw epoch integers like 1794665562).
export function formatAxisTime(value) {
  const sec = epochSecondsOf(value);
  if (sec === null) return '--:--:--';
  const date = new Date(sec * 1000);
  if (Number.isNaN(date.getTime())) return '--:--:--';
  return date.toISOString().slice(11, 19);
}

// A series is { name, unit, values: [ { t, value } | null ], live } - value null is a gap.
// The path below walks the samples in time order and starts a new sub-path after any gap, which is
// the whole of the gap rule expressed in canvas calls.
export function drawSeries(ctx, points, mapper) {
  let drawn = false;
  ctx.beginPath();
  for (const point of points) {
    const value = point === null || point.value === null || !Number.isFinite(point.value) ? null : point.value;
    if (value === null) {
      drawn = false;
      continue;
    }
    const placed = mapper(point, value);
    if (placed === null) {
      drawn = false;
      continue;
    }
    if (!drawn) {
      ctx.moveTo(placed.x, placed.y);
      drawn = true;
      continue;
    }
    ctx.lineTo(placed.x, placed.y);
  }
  ctx.stroke();
  return { segments: drawn };
}

export function rangeOf(seriesList, extra) {
  let min = Number.POSITIVE_INFINITY;
  let max = Number.NEGATIVE_INFINITY;
  for (const series of seriesList) {
    for (const point of series.values) {
      if (!point || !Number.isFinite(point.value)) continue;
      if (point.value < min) min = point.value;
      if (point.value > max) max = point.value;
    }
  }
  if (Array.isArray(extra)) {
    for (const value of extra) {
      if (!Number.isFinite(value)) continue;
      if (value < min) min = value;
      if (value > max) max = value;
    }
  }
  if (!Number.isFinite(min) || !Number.isFinite(max)) return null;
  if (min === max) {
    min -= 1;
    max += 1;
  }
  const pad = (max - min) * 0.08;
  return { min: min - pad, max: max + pad };
}

export function timeRangeOf(points, fallbackSpan) {
  let first = null;
  let last = null;
  for (const point of points) {
    if (!point) continue;
    const t = epochSecondsOf(point.t);
    if (t === null) continue;
    if (first === null || t < first) first = t;
    if (last === null || t > last) last = t;
  }
  if (first === null) return null;
  if (last === first) last = first + (Number.isFinite(fallbackSpan) ? fallbackSpan : 60);
  return { first, last };
}

// The plot frame. Left gutter carries the engineering unit, right gutter carries the second axis when
// a chart is dual-axis, and the top band carries the series captions. Accepts optional logical CSS size
// from prepareCanvas so HiDPI backing stores draw in CSS pixel coordinates.
export function frameOf(canvas, options, size) {
  const opts = options || {};
  const left = opts.dualAxis ? 62 : 50;
  const right = opts.dualAxis ? 58 : 16;
  const top = opts.captions === false ? 12 : 24;
  const bottom = 22;
  const width = size && Number.isFinite(size.width) ? size.width : canvas.width;
  const height = size && Number.isFinite(size.height) ? size.height : canvas.height;
  if (width < left + right + 60 || height < top + bottom + 36) return null;
  return {
    left, right, top, bottom,
    x0: left, y0: height - bottom,
    x1: width - right, y1: top,
    w: width - left - right, h: height - top - bottom,
  };
}

export function mapperFor(frame, range, timeRange) {
  const spanX = Math.max(1e-6, timeRange.last - timeRange.first);
  const spanY = Math.max(1e-6, range.max - range.min);
  return (point, value) => {
    const t = epochSecondsOf(point.t);
    if (t === null) return null;
    const x = frame.x0 + ((t - timeRange.first) / spanX) * frame.w;
    const fraction = (value - range.min) / spanY;
    const y = frame.y1 + (1 - fraction) * frame.h;
    return { x, y };
  };
}

export function axisOf(ctx, frame, range, unit, side) {
  const ticks = ticksFor(range.min, range.max, 3);
  const spanY = Math.max(1e-6, range.max - range.min);
  ctx.save();
  ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
  ctx.textBaseline = 'middle';
  for (const tick of ticks) {
    const fraction = (tick - range.min) / spanY;
    const y = frame.y1 + (1 - fraction) * frame.h;
    ctx.strokeStyle = 'rgba(120, 140, 160, 0.22)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(frame.x0, y);
    ctx.lineTo(frame.x1, y);
    ctx.stroke();
    const label = formatTick(tick);
    ctx.fillStyle = 'rgba(148, 163, 184, 0.95)';
    if (side === 'left') {
      ctx.textAlign = 'right';
      ctx.fillText(label, frame.x0 - 6, y);
    } else {
      ctx.textAlign = 'left';
      ctx.fillText(label, frame.x1 + 6, y);
    }
  }
  if (unit) {
    ctx.textAlign = side === 'left' ? 'left' : 'right';
    ctx.fillStyle = 'rgba(148, 163, 184, 0.78)';
    ctx.fillText(unit, side === 'left' ? 4 : frame.x1 + 6, frame.y1 + 4);
  }
  ctx.restore();
  return { ticks };
}

export function drawTimeAxis(ctx, frame, timeRange) {
  if (!timeRange) return;
  const spanSec = Math.max(0, Math.round(timeRange.last - timeRange.first));
  const mid = (timeRange.first + timeRange.last) / 2;
  const y = frame.y0 + 13;
  ctx.save();
  ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
  ctx.fillStyle = 'rgba(148, 163, 184, 0.95)';
  ctx.textBaseline = 'middle';

  ctx.textAlign = 'left';
  ctx.fillText(formatAxisTime(timeRange.first) + (spanSec > 0 ? ' (-' + String(spanSec) + 's)' : ''), frame.x0, y);
  if (frame.w > 260) {
    ctx.textAlign = 'center';
    ctx.fillText(formatAxisTime(mid), frame.x0 + frame.w / 2, y);
  }
  ctx.textAlign = 'right';
  ctx.fillText('NOW ' + formatAxisTime(timeRange.last), frame.x1, y);
  ctx.restore();
}

export function ticksFor(min, max, count) {
  const step = (max - min) / (count + 1);
  const ticks = [];
  for (let index = 0; index <= count + 1; index += 1) {
    ticks.push(min + step * index);
  }
  return ticks;
}

export function formatTick(value) {
  if (Math.abs(value) >= 1000) return value.toFixed(0);
  if (Math.abs(value) >= 100) return value.toFixed(1);
  return value.toFixed(1);
}

export function captionFor(seriesList, points, capacity) {
  const named = seriesList.map((series) => series.name + ' ' + latestLabel(series)).join('  |  ');
  return String(points.length) + ' of ' + String(capacity) + ' samples: ' + named;
}

export function latestLabel(series) {
  if (!series.values.length) return 'no sample';
  for (let index = series.values.length - 1; index >= 0; index -= 1) {
    const point = series.values[index];
    if (point && Number.isFinite(point.value)) return formatted(point.value, series.unit);
  }
  return 'DATA GAP';
}

export function formatted(value, unit) {
  if (!Number.isFinite(value)) return 'NOT_PUBLISHED_BY_RUNTIME';
  const digits = Math.abs(value) >= 100 ? 0 : Math.abs(value) >= 10 ? 1 : 2;
  return value.toFixed(digits) + (unit ? ' ' + unit : '');
}

// The ready band and the setpoint are drawn as they are published: a band as a filled span, the
// setpoint as a dashed line. A setpoint drawn as a solid line reads as a measurement.
export function drawBand(ctx, frame, low, high, mapper) {
  if (!Number.isFinite(low) || !Number.isFinite(high)) return false;
  const top = mapper({ t: 0 }, high);
  const bottom = mapper({ t: 0 }, low);
  if (top === null || bottom === null) return false;
  ctx.save();
  ctx.fillStyle = 'rgba(46, 160, 109, 0.12)';
  ctx.fillRect(frame.x0, Math.min(top.y, bottom.y), frame.w, Math.abs(bottom.y - top.y));
  ctx.restore();
  return true;
}

export function drawSetpoint(ctx, frame, mapper, value) {
  if (!Number.isFinite(value)) return false;
  const placed = mapper({ t: 0 }, value);
  if (placed === null) return false;
  ctx.save();
  ctx.setLineDash([4, 3]);
  ctx.strokeStyle = 'rgba(251, 191, 36, 0.9)';
  ctx.beginPath();
  ctx.moveTo(frame.x0, placed.y);
  ctx.lineTo(frame.x1, placed.y);
  ctx.stroke();
  ctx.restore();
  return true;
}

// A single current value that has no history channel is shown as a marker, never as a line. This is
// the shape the active isolation-valve pressure takes on the Main Control trend: the host publishes one
// scalar, so the screen draws one scalar and says so.
export function drawScalarMarker(ctx, frame, y, label, tone) {
  if (!Number.isFinite(y)) return false;
  ctx.save();
  ctx.strokeStyle = 'rgba(255, 123, 114, 0.55)';
  ctx.beginPath();
  ctx.moveTo(frame.x1 - 46, y);
  ctx.lineTo(frame.x1 - 6, y);
  ctx.stroke();
  ctx.fillStyle = 'rgba(255, 123, 114, 0.95)';
  ctx.beginPath();
  ctx.arc(frame.x1 - 6, y, 3.5, 0, Math.PI * 2);
  ctx.fill();
  ctx.font = '9.5px ui-monospace, Menlo, Consolas, monospace';
  ctx.textAlign = 'right';
  ctx.textBaseline = 'bottom';
  ctx.fillStyle = tone === 'mute' ? 'rgba(148, 163, 184, 0.9)' : 'rgba(241, 245, 249, 0.92)';
  ctx.fillText(label, frame.x1 - 12, y - 3);
  ctx.restore();
  return true;
}

// Normalised 0-100% view of any channel, for the historical explorer: the operator picks channels to
// compare shapes, and the engineering range and the live value stay printed beside the toggle, so the
// normalisation never hides what the units are.
export function normalise(values, range) {
  if (!range || range.max === range.min) return values.map(() => null);
  return values.map((value) => {
    if (value === null || !Number.isFinite(value.value)) return null;
    return ((value.value - range.min) / (range.max - range.min)) * 100;
  });
}

// ---- point placement that cannot overlap labels ------------------------------------------------
export function placeLabels(entries, box, minGapX, minGapY) {
  const placed = [];
  for (const entry of entries) {
    const anchorX = box.x0 + entry.fx * box.w;
    const anchorY = box.y1 - entry.fy * box.h;
    let x = anchorX;
    let y = anchorY;
    if (x + entry.width > box.x1 - 2) x = Math.max(box.x0 + 2, box.x1 - entry.width - 2);
    if (y - entry.height / 2 < box.y0 + 2) y = box.y0 + entry.height / 2 + 2;
    if (y + entry.height / 2 > box.y1 - 2) y = box.y1 - entry.height / 2 - 2;
    let shifts = 0;
    while (shifts < 24 && collides(placed, x, y, entry.width, entry.height, minGapX, minGapY)) {
      y -= entry.height + minGapY;
      shifts += 1;
      if (y < box.y0 + 6) {
        y = box.y1 - 8;
        x += entry.width + minGapX;
      }
      if (x + entry.width > box.x1 - 2) x = box.x0 + 4;
    }
    placed.push({
      label: entry.label,
      tone: entry.tone,
      slot: entry.slot,
      x,
      y,
      anchorX,
      anchorY,
      moved: shifts > 0,
    });
  }
  return placed;
}

function collides(placed, x, y, width, height, minGapX, minGapY) {
  for (const other of placed) {
    if (Math.abs(x - other.x) < width + minGapX && Math.abs(y - other.y) < height + minGapY) return true;
  }
  return false;
}

export function drawGrid(ctx, box, stepFraction, tone) {
  ctx.save();
  ctx.strokeStyle = tone === 'soft' ? 'rgba(120, 140, 160, 0.16)' : 'rgba(120, 140, 160, 0.30)';
  ctx.lineWidth = 1;
  for (let index = 0; index <= 1.0001; index += stepFraction) {
    const clamped = Math.min(1, index);
    const x = box.x0 + clamped * box.w;
    const y = box.y1 - clamped * box.h;
    ctx.beginPath();
    ctx.moveTo(x, box.y0);
    ctx.lineTo(x, box.y1);
    ctx.stroke();
    ctx.beginPath();
    ctx.moveTo(box.x0, y);
    ctx.lineTo(box.x1, y);
    ctx.stroke();
  }
  ctx.restore();
}
