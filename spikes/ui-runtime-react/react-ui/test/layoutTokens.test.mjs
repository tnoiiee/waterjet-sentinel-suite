// WJSS Stage 0.2.1A — static guards for the fullscreen Design Addendum: central UI scale tokens,
// no duplicated sizing, readable minimum Sensor typography, one size container, Cannon size.
// These are source checks only. Final viewport fit requires actual browser evidence
// (e2e/layout.spec.ts, Owner-local Microsoft Edge).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const here = path.dirname(fileURLToPath(import.meta.url));
const read = (p) => fs.readFileSync(path.resolve(here, '../src', p), 'utf8');
const globalCss = read('global.css');
const opsCss = read('components/Operations.module.css');
const cellCss = read('components/SensorCell.module.css');

const SCALE_TOKENS = [
  '--sensor-cell-width',
  '--sensor-cell-height',
  '--sensor-id-font-size',
  '--sensor-value-font-size',
  '--sensor-gap',
  '--wall-h',
  '--u-center-w',
  '--u-center-h',
  '--map-col-w',
  '--bottom-row-h',
  '--top-status-h',
  '--alarm-strip-h',
  '--detail-h',
  '--job-h',
  '--queue-row-h',
];

const tokenValue = (name) => {
  const m = globalCss.match(new RegExp(`${name}:\\s*([^;]+);`));
  return m ? m[1].replace(/\s+/g, ' ').trim() : null;
};
const px = (v) => Number(String(v).replace('px', ''));

describe('central UI scale tokens', () => {
  it('every scale token is defined exactly once, in global.css only', () => {
    for (const t of SCALE_TOKENS) {
      const defs = globalCss.match(new RegExp(`${t}:`, 'g')) ?? [];
      expect(defs.length, t).toBe(1);
      expect(opsCss, `${t} redefined in Operations.module.css`).not.toMatch(new RegExp(`${t}:`));
      expect(cellCss, `${t} redefined in SensorCell.module.css`).not.toMatch(new RegExp(`${t}:`));
    }
  });

  it('Sensor typography meets the readability scale (ID 13 px, value 16 px, marker 8..10 px)', () => {
    expect(px(tokenValue('--sensor-id-font-size'))).toBe(13);
    expect(px(tokenValue('--sensor-value-font-size'))).toBe(16);
    expect(px(tokenValue('--sensor-marker-size'))).toBeGreaterThanOrEqual(8);
    expect(px(tokenValue('--sensor-marker-size'))).toBeLessThanOrEqual(10);
    // Reserved marker zone to the right of the ID row: 12..14 px.
    expect(px(tokenValue('--sensor-marker-reserve'))).toBeGreaterThanOrEqual(12);
    expect(px(tokenValue('--sensor-marker-reserve'))).toBeLessThanOrEqual(14);
    expect(px(tokenValue('--sensor-gap').replace('var(--space-inline)', '4px'))).toBe(4);
  });

  it('Sensor cell scale is bounded: height 46..50 px, width 52..56 px (capped at large viewports)', () => {
    expect(tokenValue('--sensor-cell-height')).toMatch(/^clamp\( ?46px,.*, ?50px ?\)$/);
    expect(tokenValue('--sensor-cell-width')).toMatch(/^clamp\(52px,.*, ?56px\)$/);
    expect(px(tokenValue('--u-center-w'))).toBeGreaterThanOrEqual(180);
    expect(px(tokenValue('--u-center-w'))).toBeLessThanOrEqual(210);
    expect(px(tokenValue('--u-center-h'))).toBeGreaterThanOrEqual(130);
    expect(px(tokenValue('--u-center-h'))).toBeLessThanOrEqual(170);
  });

  it('Sensor cells and Cannon slots consume the tokens (same outer size), no hard-coded cell size', () => {
    const cell = cellCss.match(/\.cell\s*\{([^}]*)\}/)[1];
    expect(cell).toMatch(/width:\s*var\(--sensor-cell-width\)/);
    expect(cell).toMatch(/height:\s*var\(--sensor-cell-height\)/);
    expect(cellCss).toMatch(/\.index\s*\{[^}]*font-size:\s*var\(--sensor-id-font-size\)/);
    expect(cellCss).toMatch(/\.score\s*\{[^}]*font-size:\s*var\(--sensor-value-font-size\)/);
    const cannon = opsCss.match(/\.cannon\s*\{([^}]*)\}/)[1];
    expect(cannon).toMatch(/width:\s*var\(--sensor-cell-width\)/);
    expect(cannon).toMatch(/height:\s*var\(--sensor-cell-height\)/);
    expect(opsCss).toMatch(/grid-template-columns:\s*repeat\(var\(--wall-cols, 5\), var\(--sensor-cell-width\)\)/);
    expect(globalCss).not.toMatch(/--cell:/);
  });

  it('no 8 px or 9 px text, and no transform scaling, in Operations or Sensor cell styles', () => {
    for (const css of [opsCss, cellCss]) {
      expect(css).not.toMatch(/font-size:\s*[89](\.\d+)?px/);
      expect(css).not.toMatch(/font:\s*\d+\s+[89](\.\d+)?px/);
      expect(css).not.toMatch(/transform:\s*scale/);
      expect(css).not.toMatch(/zoom:/);
    }
  });

  it('strict viewport-height model: app root is 100dvh with overflow hidden; one size container', () => {
    const app = opsCss.match(/\.app\s*\{([^}]*)\}/)[1];
    expect(app).toMatch(/height:\s*100dvh/);
    expect(app).toMatch(/overflow:\s*hidden/);
    expect((opsCss.match(/container-type:\s*size/g) ?? []).length).toBe(1);
    const main = opsCss.match(/\.main\s*\{([^}]*)\}/)[1];
    expect(main).toMatch(/min-height:\s*0/);
    expect(main).toMatch(/grid-template-rows:\s*minmax\(0, 1fr\) var\(--bottom-row-h\)/);
  });

  it('Diagnostics is a fixed drawer bounded inside the viewport (no layout space)', () => {
    const diag = opsCss.match(/\.diag\s*\{([^}]*)\}/)[1];
    expect(diag).toMatch(/position:\s*fixed/);
    expect(diag).toMatch(/width:\s*320px/);
    expect(diag).toMatch(/max-height:\s*min\(60vh/);
    expect(opsCss).toMatch(/\.diagBody\s*\{[^}]*overflow:\s*auto/);
  });

  it('typography scale: every text token is >= 11 px and matches the approved sizes', () => {
    const fonts = [...globalCss.matchAll(/(--font-[a-z-]+):\s*([\d.]+)px;/g)];
    expect(fonts.length).toBeGreaterThanOrEqual(20);
    for (const [, name, v] of fonts) expect(Number(v), name).toBeGreaterThanOrEqual(11);
    const expected = {
      '--font-app-title': 15,
      '--font-alarm': 12,
      '--font-card-title': 15,
      '--font-wall-title': 14,
      '--font-wall-meta': 12,
      '--font-label': 12,
      '--font-value': 13,
      '--font-job': 13,
      '--font-queue-head': 12,
      '--font-queue-row': 13,
      '--font-badge': 11,
      '--font-trend-title': 14,
      '--font-axis': 12,
      '--font-legend': 12,
      '--font-camera-title': 14,
      '--font-camera-state': 12,
      '--font-diag': 12,
    };
    for (const [k, v] of Object.entries(expected)) expect(px(tokenValue(k)), k).toBe(v);
    expect(px(tokenValue('--font-chip'))).toBeGreaterThanOrEqual(12.5);
    expect(px(tokenValue('--font-chip'))).toBeLessThanOrEqual(13);
  });

  it('spacing scale 2/4/6/8/12/16/24 and three surface levels are defined centrally', () => {
    const spaces = ['--space-micro', '--space-inline', '--space-dense', '--space-small', '--space-card', '--space-section', '--space-major'].map((t) => px(tokenValue(t)));
    expect(spaces).toEqual([2, 4, 6, 8, 12, 16, 24]);
    for (const t of ['--surface-page', '--surface-card', '--surface-nested', '--border-subtle', '--border-strong', '--text-strong', '--text', '--muted']) expect(tokenValue(t), t).toMatch(/^#[0-9a-f]{6}$/);
    // Status bar height 40..42 px.
    expect(px(tokenValue('--top-status-h'))).toBeGreaterThanOrEqual(40);
    expect(px(tokenValue('--top-status-h'))).toBeLessThanOrEqual(42);
  });

  it('Sensor cell zones: ID, reserved marker zone, value, and bottom rail are separate grid areas', () => {
    const cell = cellCss.match(/\.cell\s*\{([^}]*)\}/)[1];
    expect(cell).toMatch(/display:\s*grid/);
    expect(cell).toMatch(/grid-template-columns:\s*minmax\(0, 1fr\) var\(--sensor-marker-size\)/);
    expect(cell).toMatch(/grid-template-rows:\s*14px minmax\(0, 1fr\) var\(--sensor-rail-h\)/);
    expect(cell).toMatch(/font-variant-numeric:\s*tabular-nums/);
    const idx = cellCss.match(/\.index\s*\{([^}]*)\}/)[1];
    expect(idx).toMatch(/grid-column:\s*1;/);
    expect(idx).toMatch(/grid-row:\s*1;/);
    expect(idx).toMatch(/overflow:\s*hidden/);
    expect(idx).not.toMatch(/position:\s*absolute/);
    const zone = cellCss.match(/\.markerZone\s*\{([^}]*)\}/)[1];
    expect(zone).toMatch(/grid-column:\s*2;/);
    expect(zone).toMatch(/grid-row:\s*1;/);
    expect(cellCss).toMatch(/\.rail\s*\{[^}]*grid-row:\s*3;/);
    // Markers never use absolute positioning over the ID.
    for (const sel of ['markerZone', 'rail', 'badge', 'alarmIcon']) {
      const body = cellCss.match(new RegExp(`\\.${sel}\\s*\\{([^}]*)\\}`))[1];
      expect(body, sel).not.toMatch(/position:\s*absolute/);
    }
  });

  it('GlobalQueue columns follow the approved widths (Pos 40, Sensor 90..110, Score 90, Since clean 110, Status 90)', () => {
    const w = (c) => px(opsCss.match(new RegExp(`\\.${c}\\s*\\{[^}]*width:\\s*(\\d+)px`))[1]);
    expect(w('colPos')).toBe(40);
    expect(w('colSensor')).toBeGreaterThanOrEqual(90);
    expect(w('colSensor')).toBeLessThanOrEqual(110);
    expect(w('colScore')).toBe(90);
    expect(w('colAge')).toBe(110);
    expect(w('colStatus')).toBe(90);
    expect(opsCss).toMatch(/\.table\s*\{[^}]*font-variant-numeric:\s*tabular-nums/);
  });
});

