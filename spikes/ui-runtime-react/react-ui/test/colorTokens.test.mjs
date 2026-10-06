// WJSS Stage 0.2.1A — central colour tokens (readability refinement). Source checks only:
// approved ranges, separation of process / alarm / uncertain colours, and computed WCAG
// contrast of white Sensor text on every process shade. The Owner's visual review in
// Microsoft Edge remains required; no standards certification is claimed.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

// Colour tokens: values live only in global.css; this checks the approved ranges, the separation
// of process and alarm colours, and WCAG contrast of white Sensor text on every process shade.
// (A computed colour check; it does not replace the Owner's visual review.)
describe('central colour tokens', () => {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const css = fs.readFileSync(path.resolve(here, '../src/global.css'), 'utf8');
  const tok = (name) => {
    const m = css.match(new RegExp(`${name}:\\s*(#[0-9a-fA-F]{6});`));
    if (!m) throw new Error(`token ${name} missing`);
    return m[1].toLowerCase();
  };
  const rgb = (hex) => [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
  const lum = (hex) => {
    const [r, g, b] = rgb(hex).map((c) => {
      const x = c / 255;
      return x <= 0.03928 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4;
    });
    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
  };
  const contrast = (a, b) => {
    const [l1, l2] = [lum(a), lum(b)].sort((x, y) => y - x);
    return (l1 + 0.05) / (l2 + 0.05);
  };
  const mix = (a, b, t) => {
    const [x, y] = [rgb(a), rgb(b)];
    return '#' + x.map((c, i) => Math.round(c + (y[i] - c) * t).toString(16).padStart(2, '0')).join('');
  };

  it('process colours sit in the approved desaturated ranges', () => {
    expect(tok('--cleaner-base')).toBe('#256b4a');
    expect(tok('--cleaner-strong')).toBe('#2f8059');
    expect(tok('--dirty-base')).toBe('#b33a2f');
    expect(tok('--dirty-strong')).toBe('#8d2f27');
  });

  it('white Sensor text has at least 4.5:1 contrast on every Dirty / Cleaner / neutral shade', () => {
    const white = tok('--cell-text');
    const shades = [];
    for (const [b, s] of [
      ['--cleaner-base', '--cleaner-strong'],
      ['--dirty-base', '--dirty-strong'],
    ])
      for (let t = 0; t <= 1.0001; t += 0.25) shades.push(mix(tok(b), tok(s), t));
    shades.push(tok('--not-classified'), tok('--quality-neutral'));
    for (const c of shades) expect(contrast(white, c), c).toBeGreaterThanOrEqual(4.5);
  });

  it('alarm colour is separated from Dirty red and from the uncertain amber (hue and luminance)', () => {
    const alarm = tok('--alarm');
    expect(alarm).not.toBe(tok('--dirty-base'));
    // Alarm yellow is far brighter than any Dirty shade (distinct even without hue perception).
    expect(contrast(alarm, tok('--dirty-base'))).toBeGreaterThanOrEqual(3);
    expect(contrast(alarm, tok('--dirty-strong'))).toBeGreaterThanOrEqual(3);
    expect(tok('--uncertain')).not.toBe(alarm);
    expect(tok('--selection')).not.toBe(alarm);
  });

  it('critical modal palette is distinct from Dirty red, Alarm amber, selection cyan and Active Job white', () => {
    const edge = tok('--critical-edge');
    const surface = tok('--critical-surface');
    for (const other of ['--dirty-base', '--dirty-strong', '--alarm', '--selection', '--job-outer', '--uncertain']) expect(edge).not.toBe(tok(other));
    // Hue separation from Dirty red and Alarm amber (degrees on the colour wheel).
    const hue = (hex) => {
      const [r, g, b] = rgb(hex).map((c) => c / 255);
      const mx = Math.max(r, g, b);
      const mn = Math.min(r, g, b);
      const d = mx - mn;
      if (d === 0) return 0;
      const h = mx === r ? ((g - b) / d) % 6 : mx === g ? (b - r) / d + 2 : (r - g) / d + 4;
      return (h * 60 + 360) % 360;
    };
    const dist = (a, b) => Math.min(Math.abs(a - b), 360 - Math.abs(a - b));
    expect(dist(hue(edge), hue(tok('--dirty-base')))).toBeGreaterThanOrEqual(25);
    expect(dist(hue(edge), hue(tok('--alarm')))).toBeGreaterThanOrEqual(60);
    expect(dist(hue(edge), hue(tok('--selection')))).toBeGreaterThanOrEqual(60);
    // Readable text on the critical surface (WCAG AA).
    expect(contrast(tok('--critical-ink'), surface)).toBeGreaterThanOrEqual(7);
    expect(contrast(tok('--text-strong'), surface)).toBeGreaterThanOrEqual(7);
    // Not neon: saturation-limited edge (max channel minus min channel below 75 % of range).
    const [r, g, b] = rgb(edge);
    expect(Math.max(r, g, b) - Math.min(r, g, b)).toBeLessThan(0.75 * 255);
  });

  it('TS palette only references tokens (no colour values outside global.css)', () => {
    const ts = fs.readFileSync(path.resolve(here, '../src/visual/toCellVisual.ts'), 'utf8');
    const palette = ts.slice(ts.indexOf('export const PALETTE'), ts.indexOf('});', ts.indexOf('export const PALETTE')));
    expect(palette).not.toMatch(/#[0-9a-fA-F]{3,6}\b|hsl\(|rgb\(/);
    expect((palette.match(/var\(--/g) ?? []).length).toBeGreaterThanOrEqual(10);
  });

  it('cell, page, and trend styles take colours from tokens (no hard-coded process / alarm colours)', () => {
    const ops = fs.readFileSync(path.resolve(here, '../src/components/Operations.module.css'), 'utf8');
    const cell = fs.readFileSync(path.resolve(here, '../src/components/SensorCell.module.css'), 'utf8');
    for (const src of [ops, cell]) {
      expect(src).not.toMatch(/#[0-9a-fA-F]{6}\b/);
      expect(src).not.toMatch(/hsl\(/);
      // No glow / heavy shadow effects.
      expect(src).not.toMatch(/text-shadow/);
      expect(src).not.toMatch(/box-shadow:\s*0 \d+px \d{2,}px/);
    }
  });
});

