// WJSS Stage 0.2.1A — self-hosted Google Sans asset, provenance record, and font wiring (source
// checks). Font loading, document.fonts.ready, and rendered geometry are asserted in Owner-local
// Microsoft Edge (e2e/layout.spec.ts FONT-A); jsdom has no font engine.
import { createHash } from 'node:crypto';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, '..');
const fontsDir = join(root, 'src/assets/fonts');
const read = (p) => readFileSync(join(root, p), 'utf8');
const sha256 = (buf) => createHash('sha256').update(buf).digest('hex');
const gitBlob = (buf) => createHash('sha1').update(Buffer.concat([Buffer.from(`blob ${buf.length}\0`), buf])).digest('hex');
const source = readFileSync(join(fontsDir, 'FONT_SOURCE.md'), 'utf8');

function walk(dir) {
  return readdirSync(dir).flatMap((n) => {
    const p = join(dir, n);
    return statSync(p).isDirectory() ? walk(p) : [p];
  });
}

describe('Google Sans asset set (OFL-1.1, official google/fonts source)', () => {
  it('contains exactly the WOFF2, the verbatim licence, the trademark notice, and the source record', () => {
    expect(readdirSync(fontsDir).sort()).toEqual(['.gitattributes', 'FONT_SOURCE.md', 'GoogleSans-Latin-Variable.woff2', 'OFL.txt', 'TRADEMARKS.md']);
    // Byte-for-byte on every platform (Git for Windows autocrlf would otherwise rewrite them).
    const attrs = readFileSync(join(fontsDir, '.gitattributes'), 'utf8');
    for (const line of ['OFL.txt -text', 'TRADEMARKS.md -text', '*.woff2 binary']) expect(attrs).toContain(line);
  });

  it('WOFF2 is a real WOFF2 file whose size and SHA-256 match FONT_SOURCE.md', () => {
    const woff2 = readFileSync(join(fontsDir, 'GoogleSans-Latin-Variable.woff2'));
    expect(woff2.subarray(0, 4).toString('latin1')).toBe('wOF2');
    expect(woff2.length).toBe(47672);
    expect(source).toContain(sha256(woff2));
    expect(source).toContain('47,672');
  });

  it('OFL.txt and TRADEMARKS.md are the verbatim package files (Git blob identity recorded)', () => {
    const ofl = readFileSync(join(fontsDir, 'OFL.txt'));
    const tm = readFileSync(join(fontsDir, 'TRADEMARKS.md'));
    expect(gitBlob(ofl)).toBe('035131accc0d7bc0ec5880f906800ebe1b1f854f');
    expect(gitBlob(tm)).toBe('621fbbd85d6e346f46b0ef4f0658d3cfba7e3f80');
    expect(source).toContain(sha256(ofl));
    expect(source).toContain(sha256(tm));
    const text = ofl.toString('utf8');
    expect(text).toContain('SIL Open Font License, Version 1.1');
    // No Reserved Font Name: the copyright line declares none (the term appears only in the
    // licence definition section).
    const copyright = text.split(/\r?\n/)[0];
    expect(copyright).toMatch(/^Copyright 2025 The Google Sans Project Authors/);
    expect(copyright).not.toMatch(/Reserved Font Name/i);
    expect(text.match(/Reserved Font Name/g)).toHaveLength(1);
    expect(tm.toString('utf8')).toMatch(/trademarks of Google LLC/);
  });

  it('FONT_SOURCE.md records provenance, the original file, processing, and the licence conclusion', () => {
    for (const s of [
      'https://github.com/google/fonts/tree/7085eb89a950e85db5b166b7a58d414544b4140c/ofl/googlesans',
      '7085eb89a950e85db5b166b7a58d414544b4140c',
      'GoogleSans[GRAD,opsz,wght].ttf',
      '4,974,940',
      'd0a87d835a944b8b40d0e82a5651bb59ab97b936a2aeed5946eb57e7b2a3a90a',
      'a5e76cc713206f1cfe9a7fc31cc1b6f035034d81',
      'fonttools==4.60.1',
      'OFL-1.1',
      'Reserved Font Name',
      'Modified Version',
      'tnum',
      '2026-10-06',
    ])
      expect(source, s).toContain(s);
  });
});

describe('font wiring: local WOFF2 only, one family for every visible character', () => {
  const css = read('src/global.css');
  const html = read('index.html');

  it('@font-face loads the local variable WOFF2 (400..700); no CDN or external CSS', () => {
    const face = css.match(/@font-face\s*\{([^}]*)\}/)[1];
    expect(face).toMatch(/font-family:\s*'Google Sans'/);
    expect(face).toMatch(/src:\s*url\('\.\/assets\/fonts\/GoogleSans-Latin-Variable\.woff2'\) format\('woff2'\)/);
    expect(face).toMatch(/font-weight:\s*400 700/);
    expect(html).toMatch(/<link rel="preload" href="\/src\/assets\/fonts\/GoogleSans-Latin-Variable\.woff2" as="font" type="font\/woff2" crossorigin \/>/);
    const all = [html, ...walk(join(root, 'src')).filter((p) => /\.(css|tsx?|html)$/.test(p)).map((p) => readFileSync(p, 'utf8'))].join('\n');
    expect(all).not.toMatch(/fonts\.googleapis|fonts\.gstatic|@import\s+url\(\s*['"]?https?:/);
  });

  it('UI and Sensor font tokens resolve to Google Sans with the approved fallback; no Bahnschrift', () => {
    expect(css).toMatch(/--font-family-ui:\s*'Google Sans';/);
    expect(css).toMatch(/--font-ui:\s*var\(--font-family-ui\), 'Segoe UI', system-ui, sans-serif;/);
    expect(css).toMatch(/--font-sensor:\s*var\(--font-ui\);/);
    const src = walk(join(root, 'src')).filter((p) => /\.(css|tsx?)$/.test(p)).map((p) => readFileSync(p, 'utf8')).join('\n');
    expect(src).not.toMatch(/Bahnschrift|font-stretch/);
    // Controls and the uPlot stylesheet are forced to the UI family; canvas axes too.
    expect(css).toMatch(/button,\s*input,\s*select,\s*textarea\s*\{\s*font-family:\s*inherit;/);
    expect(css).toMatch(/body \.uplot,\s*body \.uplot \*\s*\{\s*font-family:\s*var\(--font-ui\);/);
    expect(read('src/components/PressureTrend.tsx')).toMatch(/AXIS_FONT = '12px "Google Sans", "Segoe UI", system-ui, sans-serif'/);
  });

  it('first render waits (bounded) for the UI font', () => {
    const main = read('src/main.tsx');
    expect(main).toMatch(/waitForUiFont\(\)\.then\(/);
    expect(read('src/fontReady.ts')).toMatch(/timeoutMs = 2500/);
  });
});
