// WJSS Stage 0.2.1A — static guards: U-shaped layout retained; React renders the runtime wall map
// and never imports the mapping module or generates Sensor IDs. (Plain .mjs: uses node:fs.)
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const here = path.dirname(fileURLToPath(import.meta.url));

describe('wall map source guards', () => {
  it('U-shaped grid areas are retained in the stylesheet (Rear top, Left/Right sides, Front bottom)', () => {
    const css = fs.readFileSync(path.resolve(here, '../src/components/Operations.module.css'), 'utf8');
    expect(css).toMatch(/'rear rear rear'\s*'left center right'\s*'front front front'/);
    expect(css).not.toMatch(/repeat\(14,/);
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
