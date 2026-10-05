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

  it('Sensor typography meets the approved minimums (ID >= 12 px, value >= 14 px)', () => {
    expect(px(tokenValue('--sensor-id-font-size'))).toBeGreaterThanOrEqual(12);
    expect(px(tokenValue('--sensor-id-font-size'))).toBeLessThanOrEqual(13);
    expect(px(tokenValue('--sensor-value-font-size'))).toBeGreaterThanOrEqual(14);
    expect(px(tokenValue('--sensor-value-font-size'))).toBeLessThanOrEqual(16);
  });

  it('Sensor cell scale is bounded: height 42..50 px, width 44..54 px (capped at large viewports)', () => {
    expect(tokenValue('--sensor-cell-height')).toMatch(/^clamp\( ?42px,.*, ?50px ?\)$/);
    expect(tokenValue('--sensor-cell-width')).toMatch(/^clamp\(44px,.*, ?54px\)$/);
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
});
