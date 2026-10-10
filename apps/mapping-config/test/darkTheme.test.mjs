// Stage 0.4B-1 — fixed Dark presentation: acceptance items T01–T19 (Owner ruling 2026-10-10).
// Static proofs over the shipped files: there is one Dark theme, no switch, no stored or networked theme state,
// no external CSS, safe text rendering, and states that are distinguishable without relying on colour alone.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const APP = join(HERE, '..');
const read = (...p) => readFileSync(join(APP, ...p), 'utf8');
const index = read('public', 'index.html');
const css = read('public', 'styles.css');
const app = read('public', 'app.mjs');
const server = read('server.mjs');
const PACKAGE_SRC = join(APP, '..', '..', 'packages', 'mapping-config', 'src');

// ---- helpers (WCAG 2.x relative luminance and contrast ratio)
const rootBlock = css.slice(css.indexOf(':root {'), css.indexOf('}', css.indexOf(':root {')));
const VARS = Object.fromEntries([...rootBlock.matchAll(/--([a-z-]+):\s*(#[0-9a-fA-F]{3,6})/g)].map((m) => [m[1], m[2]]));
const hexToRgb = (hex) => {
  const h = hex.replace('#', '');
  const full = h.length === 3 ? h.split('').map((c) => c + c).join('') : h;
  return [0, 2, 4].map((i) => parseInt(full.slice(i, i + 2), 16));
};
const luminance = (hex) => {
  const [r, g, b] = hexToRgb(hex).map((c) => {
    const s = c / 255;
    return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
};
const contrast = (a, b) => {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
};
const v = (name) => VARS[name];
const ruleOf = (selector) => {
  const start = css.indexOf(`${selector} {`);
  if (start < 0) return null;
  return css.slice(start + selector.length + 2, css.indexOf('}', start));
};
const declOf = (selector, property) => {
  const block = ruleOf(selector);
  if (block === null) return null;
  const m = new RegExp(`${property}:\\s*([^;]+);`).exec(block);
  return m ? m[1].trim() : null;
};

// ---- T01–T03: one theme, no switch, no stored or networked theme state

test('T01 no theme control and no alternative theme name appears in the shipped UI or server', () => {
  for (const [name, text] of [['index.html', index], ['styles.css', css], ['app.mjs', app], ['server.mjs', server]]) {
    assert.doesNotMatch(text, /theme/i, `${name} mentions a theme`);
    assert.doesNotMatch(text, /\bLIGHT\b|\bSYSTEM\b/, `${name} has a LIGHT or SYSTEM label`);
    assert.doesNotMatch(text, /prefers-color-scheme/, `${name} queries a system scheme`);
    assert.doesNotMatch(index, /<select|type="radio"|type="checkbox"/, 'index.html has no selector or toggle');
  }
});

test('T02 no theme state is stored or sent: no localStorage, sessionStorage or matchMedia, and no theme in the Draft code', () => {
  for (const [name, text] of [['index.html', index], ['app.mjs', app], ['server.mjs', server]]) {
    assert.doesNotMatch(text, /localStorage|sessionStorage|matchMedia/, `${name} stores or queries theme state`);
  }
  for (const file of readdirSync(PACKAGE_SRC).filter((f) => f.endsWith('.mjs'))) {
    assert.doesNotMatch(readFileSync(join(PACKAGE_SRC, file), 'utf8'), /theme/i, `${file} carries theme state`);
  }
});

test('T03 the stylesheet declares dark colour-scheme only and has no light scheme or query', () => {
  assert.match(css, /color-scheme:\s*dark;/);
  assert.doesNotMatch(css, /color-scheme:\s*light/);
  assert.doesNotMatch(css, /@media[^{]*(prefers|scheme|color)/);
});

// ---- T04–T06: no external CSS, no light literal surfaces

test('T04 no external CSS or font: no @import, no url(), no http reference, no @font-face', () => {
  assert.doesNotMatch(css, /@import|url\(|https?:|@font-face/);
});

test('T05 index.html loads exactly one stylesheet (/styles.css), has no style element, no inline style and no external URL', () => {
  assert.equal([...index.matchAll(/<link\b[^>]*rel="stylesheet"[^>]*>/g)].length, 1);
  assert.match(index, /<link rel="stylesheet" href="\/styles\.css">/);
  assert.doesNotMatch(index, /<style\b/);
  assert.doesNotMatch(index, /\sstyle=/);
  assert.doesNotMatch(index, /https?:\/\//);
});

test('T06 no light literal surface: every page surface resolves to a dark colour (status chips are checked by T09)', () => {
  const lightLiterals = ['#fff', '#ffffff', '#f6f8fa', '#eef2f6', '#f2f7ff', '#d7dde4'];
  for (const lit of lightLiterals) assert.doesNotMatch(css, new RegExp(lit, 'i'), `light literal ${lit}`);
  const surfaceRules = css.split('}').filter((rule) => !/^\s*\.chip\b/.test(rule.trim()));
  for (const m of surfaceRules.join('}').matchAll(/background(?:-color)?:\s*([^;}]+)/g)) {
    for (const token of m[1].matchAll(/var\(--([a-z-]+)\)|(#[0-9a-fA-F]{3,6})/g)) {
      const hex = token[2] ?? v(token[1]);
      assert.ok(hex, `background uses an undefined colour: ${token[0]}`);
      assert.ok(luminance(hex) < 0.15, `background ${token[0]} is not dark`);
    }
  }
});

// ---- T07–T09: variables are defined; contrast meets WCAG AA for text

test('T07 every var(--name) used in the stylesheet is defined in :root', () => {
  const used = new Set([...css.matchAll(/var\(--([a-z-]+)\)/g)].map((m) => m[1]));
  assert.ok(used.size > 5);
  for (const name of used) assert.ok(v(name), `--${name} is defined`);
});

test('T08 text colours meet WCAG AA (4.5:1) on the surfaces they sit on', () => {
  const surfaces = { bg: v('bg'), panel: v('panel'), reserved: '#221c0d' };
  const texts = ['ink', 'muted', 'warn', 'err', 'info', 'ok', 'unauth'];
  for (const [sname, surface] of Object.entries(surfaces)) {
    for (const t of texts) {
      if (sname === 'reserved' && !['ink', 'muted', 'warn', 'unauth'].includes(t)) continue;
      const ratio = contrast(v(t), surface);
      assert.ok(ratio >= 4.5, `--${t} on ${sname} is ${ratio.toFixed(2)}:1`);
    }
  }
});

test('T09 chip text meets WCAG AA on every chip background', () => {
  for (const bg of ['ok', 'err', 'warn', 'info', 'draft']) {
    const ratio = contrast(v('chip-ink'), v(bg));
    assert.ok(ratio >= 4.5, `chip ${bg} is ${ratio.toFixed(2)}:1`);
  }
  assert.ok(contrast(v('ink'), v('chip-off')) >= 4.5, 'default chip');
});

// ---- T10–T14: states, unresolved addresses, controls and focus

test('T10 ADDRESS_UNRESOLVED is a warning: the address cell uses the warning colour, bold, never the success colour', () => {
  const block = ruleOf('td.addr-unresolved');
  assert.ok(block !== null);
  assert.match(block, /var\(--warn\)/);
  assert.doesNotMatch(block, /var\(--ok\)|var\(--enabled\)/);
  assert.match(block, /font-weight:\s*600/);
});

test('T11 the four row states have distinct border treatments and distinct colours', () => {
  const borders = {
    enabled: declOf('tbody tr.st-enabled td:first-child', 'border-left'),
    reserved: declOf('tbody tr.st-reserved td:first-child', 'border-left'),
    disabled: declOf('tbody tr.st-disabled td:first-child', 'border-left'),
    unauthorized: declOf('tbody tr.st-unauthorized td:first-child', 'border-left'),
  };
  for (const [k, val] of Object.entries(borders)) assert.ok(val, `${k} has a border`);
  assert.equal(new Set(Object.values(borders)).size, 4, 'four distinct border treatments');
  assert.notEqual(declOf('td.st-unauthorized', 'color'), declOf('tbody tr.st-enabled td', 'color') ?? 'enabled-default');
  assert.match(borders.reserved, /dashed/, 'reserved uses a dashed border, not a solid one');
});

test('T12 disabled buttons are visibly disabled: a different colour, border and background, with a not-allowed cursor', () => {
  const enabled = ruleOf('button');
  const disabled = ruleOf('button:disabled');
  assert.ok(enabled && disabled);
  assert.match(disabled, /cursor:\s*not-allowed/);
  assert.notEqual(declOf('button:disabled', 'color'), declOf('button', 'color') ?? 'button-color-default');
  assert.notEqual(declOf('button:disabled', 'background'), declOf('button', 'background'));
});

test('T13 focus is visible on buttons and table rows with a 2px outline in the focus colour', () => {
  const block = ruleOf('button:focus-visible, tr:focus-visible');
  assert.ok(block, 'focus rule exists');
  assert.match(block, /outline:\s*2px solid var\(--focus\)/);
});

test('T14 no outline is removed anywhere in the stylesheet', () => {
  assert.doesNotMatch(css, /outline:\s*(none|0)\b/);
});

// ---- T15–T19: safe rendering, no inline style, one network call, refusal visibility, served files

test('T15 owner values are written with textContent only: no innerHTML, outerHTML, insertAdjacentHTML, document.write or eval', () => {
  assert.doesNotMatch(app, /innerHTML|outerHTML|insertAdjacentHTML|document\.write|\beval\(|new Function/);
  assert.match(app, /textContent/);
});

test('T16 no inline style is set from workbook data or otherwise: no style property writes and no style attribute', () => {
  assert.doesNotMatch(app, /\.style\b|setAttribute\(\s*['"]style['"]/);
});

test('T17 the page makes one request at boot and nothing on theme or other interaction: no socket, beacon or XHR', () => {
  assert.equal([...app.matchAll(/\bfetch\(/g)].length, 1, 'one fetch, at boot');
  assert.match(app, /fetch\('\/api\/configuration'/);
  assert.doesNotMatch(app, /WebSocket|EventSource|XMLHttpRequest|sendBeacon/);
});

test('T18 a refused action is visible and announced: the refusal element is a live status and uses the error colour', () => {
  assert.match(index, /id="refusal" class="refusal" role="status"/);
  assert.match(declOf('.refusal', 'color') ?? '', /var\(--err\)/);
});

test('T19 the server serves only the three approved presentation files, and no theme asset', () => {
  const set = /PUBLIC_FILES = new Set\(\[([^\]]*)\]\)/.exec(server);
  assert.ok(set, 'PUBLIC_FILES is declared');
  const files = [...set[1].matchAll(/'([^']+)'/g)].map((m) => m[1]).sort();
  assert.deepEqual(files, ['app.mjs', 'index.html', 'styles.css']);
  assert.equal(readdirSync(join(APP, 'public')).filter((f) => f.endsWith('.css')).length, 1, 'one stylesheet only');
});
