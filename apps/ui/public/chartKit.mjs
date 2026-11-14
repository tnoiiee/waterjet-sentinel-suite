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

export function contextOf(canvas) {
  if (!canvas || typeof canvas.getContext !== 'function') return null;
  const ctx = canvas.getContext('2d');
  if (!ctx) return null;
  // A canvas that is only 1 pixel tall or wide means the layout has not settled; drawing into it
  // would produce a line no one can read, so the caller is told to skip this frame.
  if (canvas.width < 40 || canvas.height < 40) return null;
  return ctx;
}

export function token(ctx, name, fallback) {
  if (typeof document === 'undefined' || !document.documentElement) return fallback;
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value === '' ? fallback : value;
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
    if (!point || !Number.isFinite(point.t)) continue;
    if (first === null || point.t < first) first = point.t;
    if (last === null || point.t > last) last = point.t;
  }
  if (first === null) return null;
  if (last === first) last = first + (Number.isFinite(fallbackSpan) ? fallbackSpan : 60);
  return { first, last };
}

// The plot frame. Left gutter carries the engineering unit, right gutter carries the second axis when
// a chart is dual-axis, and the top band carries the series captions. A chart that cannot fit the
// gutters does not draw: a clipped axis on a wall map is worse than an empty panel and a caption.
export function frameOf(canvas, options) {
  const left = options.dualAxis ? 62 : 46;
  const right = options.dualAxis ? 58 : 12;
  const top = options.captions === false ? 10 : 26;
  const bottom = 22;
  const width = canvas.width;
  const height = canvas.height;
  if (width < left + right + 60 || height < top + bottom + 40) return null;
  return { left, right, top, bottom, x0: left, y0: height - bottom, x1: width - right, y1: top, w: width - left - right, h: height - top - bottom };
}

export function mapperFor(frame, range, timeRange) {
  const spanX = timeRange.last - timeRange.first;
  return (point, value) => {
    if (!Number.isFinite(point.t)) return null;
    const x = frame.x0 + ((point.t - timeRange.first) / spanX) * frame.w;
    const fraction = (value - range.min) / (range.max - range.min);
    const y = frame.y1 + (1 - fraction) * frame.h;
    return { x, y };
  };
}

export function axisOf(ctx, frame, range, unit, side) {
  const ticks = ticksFor(range.min, range.max, 4);
  ctx.save();
  ctx.font = '10px ui-monospace, Menlo, Consolas, monospace';
  ctx.textBaseline = 'middle';
  for (const tick of ticks) {
    const fraction = (tick - range.min) / (range.max - range.min);
    const y = frame.y1 - fraction * frame.h;
    ctx.strokeStyle = 'rgba(120, 140, 160, 0.22)';
    ctx.beginPath();
    ctx.moveTo(frame.x0, y);
    ctx.lineTo(frame.x1, y);
    ctx.stroke();
    const text = formatTick(tick) + (side === 'left' ? '' : '');
    ctx.fillStyle = 'rgba(154, 165, 177, 0.95)';
    if (side === 'left') {
      ctx.textAlign = 'right';
      ctx.fillText(text, frame.x0 - 5, y);
    } else {
      ctx.textAlign = 'left';
      ctx.fillText(text, frame.x1 + 5, y);
    }
  }
  if (unit) {
    // The unit belongs to the axis it describes, printed once above the ticks: a reader who is
    // comparing two charts must never have to infer whether this column is bar or per cent.
    ctx.textAlign = side === 'left' ? 'left' : 'right';
    ctx.fillStyle = 'rgba(154, 165, 177, 0.78)';
    ctx.fillText(unit, side === 'left' ? 2 : frame.x1 + 5, frame.y1 + 12);
  }
  ctx.restore();
  return { ticks };
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
  return value.toFixed(2);
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
  ctx.fillStyle = 'rgba(121, 192, 255, 0.10)';
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
  ctx.strokeStyle = 'rgba(227, 179, 65, 0.9)';
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
  ctx.fillStyle = tone === 'mute' ? 'rgba(154, 165, 177, 0.9)' : 'rgba(230, 237, 243, 0.92)';
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
// The 2D workspace panel puts a text label on every published position. Two positions can be a
// millimetre apart, so labels are placed on a rank basis: the first label claims its exact point, the
// next label that would collide is pushed along a free direction until it clears. The pushed label
// keeps a leader line back to its true point, so the correction is visible instead of hidden.
export function placeLabels(entries, box, minGapX, minGapY) {
  const placed = [];
  for (const entry of entries) {
    const anchorX = box.x0 + entry.fx * box.w;
    const anchorY = box.y1 - entry.fy * box.h;
    let x = anchorX;
    let y = anchorY;
    let shifts = 0;
    while (shifts < 24 && collides(placed, x, y, entry.width, entry.height, minGapX, minGapY)) {
      y -= entry.height + minGapY;
      shifts += 1;
      if (y < box.y0) {
        y = box.y1 - 6;
        x += entry.width + minGapX;
      }
      if (x + entry.width > box.x1) x = box.x0;
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
  for (let index = 0; index <= 1; index += stepFraction) {
    const x = box.x0 + index * box.w;
    const y = box.y1 - index * box.h;
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
