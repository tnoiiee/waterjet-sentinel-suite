// WJSS Stage 0.2.1A — static guards: U-shaped layout retained; React renders the runtime wall map
// and never imports the mapping module or generates Sensor IDs. (Plain .mjs: uses node:fs.)
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const here = path.dirname(fileURLToPath(import.meta.url));

describe('wall map source guards', () => {
  it('U-shaped placement is retained in the stylesheet (Rear top, Left/Right sides, Front bottom, compact center)', () => {
    const css = fs.readFileSync(path.resolve(here, '../src/components/Operations.module.css'), 'utf8');
    const rule = (name) => {
      const m = css.match(new RegExp(`(?:\\}|\\*\\/)\\s*\\.${name}\\s*\\{([^}]*)\\}`));
      return m ? m[1] : '';
    };
    expect(rule('wall_REAR')).toMatch(/top:\s*0/);
    expect(rule('wall_FRONT')).toMatch(/bottom:\s*0/);
    expect(rule('wall_LEFT')).toMatch(/left:\s*0/);
    expect(rule('wall_RIGHT')).toMatch(/right:\s*0/);
    expect(css).toMatch(/\.wall_LEFT,\s*\.wall_RIGHT\s*\{[^}]*margin-block:\s*auto/);
    expect(rule('overviewCenter')).toMatch(/width:\s*var\(--u-center-w\)/);
    expect(rule('overviewCenter')).toMatch(/height:\s*var\(--u-center-h\)/);
    // No transform-based scaling, no flat legacy grid.
    expect(css).not.toMatch(/transform:\s*scale/);
    expect(css).not.toMatch(/repeat\(14,/);
    expect(css).not.toMatch(/repeat\(18,/);
  });

  it('React source does not import the mapping module or generate Sensor IDs', () => {
    const files = [];
    const walk = (d) => {
      for (const e of fs.readdirSync(d, { withFileTypes: true })) {
        const p = path.join(d, e.name);
        if (e.isDirectory()) walk(p);
        else if (/\.(ts|tsx)$/.test(e.name)) files.push(p);
      }
    };
    walk(path.resolve(here, '../src'));
    expect(files.length).toBeGreaterThan(5);
    for (const f of files) {
      const t = fs.readFileSync(f, 'utf8');
      expect(t, f).not.toMatch(/sensorMap/);
      expect(t, f).not.toMatch(/`(G\+|G|H|I|J)\$\{/);
      expect(t, f).not.toMatch(/SYN-(LEFT|REAR|RIGHT|FRONT)/);
    }
  });
});
