// Stage 0.4B-2 — UI Readability Refinement tests (Tests 14–27).
//
// 14. Comfortable density is the default.
// 15. Compact density changes presentation only.
// 16. Filters do not alter Draft or default revisions.
// 17. Pump and IV grouping is correct.
// 18. Reserved rows are grouped separately.
// 19. Placeholder summary shows 17 Input + 1 Output = 18 Total.
// 20. Compact address state does not hide Error status.
// 21. Expanded address reasons remain read-only.
// 22. Validation warnings are grouped deterministically.
// 23. All element IDs are unique.
// 24. Workbook-generated values are written as text, not unsafe HTML.
// 25. Server remains loopback-only.
// 26. GET/HEAD-only HTTP boundary remains enforced.
// 27. No Hardware or Write control is introduced.

import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { register } from 'node:module';

import { createUiServer, loadConfiguration, UI_BIND_HOST } from '../server.mjs';
import { buildWorkbook, authoritativeShapedRows } from '../../../packages/mapping-config/test/helpers/syntheticWorkbook.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const PUBLIC = join(HERE, '..', 'public');
const html = readFileSync(join(PUBLIC, 'index.html'), 'utf8');
const appSrc = readFileSync(join(PUBLIC, 'app.mjs'), 'utf8');
const css = readFileSync(join(PUBLIC, 'styles.css'), 'utf8');

const realFetch = globalThis.fetch;
let server;
let base;
let tmp;
const byId = {};

class FakeNode {
  constructor(tag) {
    this.tagName = tag;
    this.attributes = {};
    this.childNodes = [];
    this.listeners = {};
    this.textContent = '';
    this.disabled = false;
    this.className = '';
    this.title = '';
    const self = this;
    this.classList = {
      add(...classes) {
        const current = self.className ? self.className.split(/\s+/) : [];
        for (const c of classes) {
          if (!current.includes(c)) current.push(c);
        }
        self.className = current.join(' ');
      },
      remove(...classes) {
        const current = self.className ? self.className.split(/\s+/) : [];
        self.className = current.filter((c) => !classes.includes(c)).join(' ');
      },
      toggle(cls, force) {
        const current = self.className ? self.className.split(/\s+/) : [];
        const has = current.includes(cls);
        const shouldHave = force !== undefined ? Boolean(force) : !has;
        if (shouldHave && !has) current.push(cls);
        else if (!shouldHave && has) {
          self.className = current.filter((c) => c !== cls).join(' ');
          return shouldHave;
        }
        self.className = current.join(' ');
        return shouldHave;
      },
      contains(cls) {
        return (self.className ? self.className.split(/\s+/) : []).includes(cls);
      },
    };
  }
  setAttribute(k, v) { this.attributes[k] = String(v); }
  getAttribute(k) { return this.attributes[k] ?? null; }
  append(...nodes) { this.childNodes.push(...nodes); }
  replaceChildren(...nodes) { this.childNodes = nodes; this.textContent = ''; }
  addEventListener(t, f) { (this.listeners[t] ??= []).push(f); }
  focus() {}
}

class FakeText extends FakeNode {
  constructor(t) { super('#text'); this.textContent = t; this.data = t; }
}

const allText = (node) => (node instanceof FakeText ? node.textContent
  : (node ? node.childNodes.map(allText).join(' ') : ''));
const rowsOf = (id) => (byId[id] ? byId[id].childNodes : []);
const cellText = (row) => row.childNodes.map((td) => allText(td));
const fire = (node, type, evt = {}) => {
  for (const f of node?.listeners[type] ?? []) f({ preventDefault() {}, ...evt });
};

before(async () => {
  register(new URL('./helpers/pkgResolveHook.mjs', import.meta.url));
  for (const m of html.matchAll(/\bid="([^"]+)"/g)) byId[m[1]] = new FakeNode(m[1]);
  globalThis.Node = FakeNode;
  const fakeBody = new FakeNode('body');
  fakeBody.className = 'density-comfortable';
  globalThis.document = {
    body: fakeBody,
    getElementById: (id) => (byId[id] ??= new FakeNode(id)),
    createElement: (tag) => new FakeNode(tag),
    createTextNode: (t) => new FakeText(t),
    querySelector: () => null,
    activeElement: null,
  };

  tmp = mkdtempSync(join(tmpdir(), 'wjss-readability-'));
  const xlsx = join(tmp, 'authoritative-shaped.xlsx');
  writeFileSync(xlsx, buildWorkbook({ rows: authoritativeShapedRows() }));

  server = createUiServer({ loader: () => loadConfiguration({ MAPPING_EXCEL_DEFAULT_PATH: xlsx }) });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  base = origin.origin;
  globalThis.fetch = (path, opts) => realFetch(`${base}${path}`, opts);

  await import('../public/app.mjs');
  for (let i = 0; i < 200 && byId['rack-body'].childNodes.length === 0; i += 1) {
    await new Promise((r) => setTimeout(r, 10));
  }
});

after(() => {
  server.close();
  globalThis.fetch = realFetch;
  rmSync(tmp, { recursive: true, force: true });
});

test('14. Comfortable density is the default', () => {
  assert.match(html, /<body[^>]*class="[^"]*density-comfortable[^"]*"/);
  assert.equal(byId['btn-density-comfortable'].attributes['aria-pressed'], 'true');
  assert.equal(byId['btn-density-compact'].attributes['aria-pressed'], 'false');
});

test('15. Compact density changes presentation only', () => {
  const revBefore = allText(byId['revisions-body']);

  // Click compact density button
  fire(byId['btn-density-compact'], 'click');
  assert.equal(globalThis.document.body.className, 'density-compact');
  assert.equal(byId['btn-density-compact'].attributes['aria-pressed'], 'true');
  assert.equal(byId['btn-density-comfortable'].attributes['aria-pressed'], 'false');

  const revAfter = allText(byId['revisions-body']);
  assert.equal(revBefore, revAfter, 'density change did not alter revisions');

  // Switch back to comfortable
  fire(byId['btn-density-comfortable'], 'click');
  assert.equal(globalThis.document.body.className, 'density-comfortable');
});

test('16. Filters do not alter Draft or default revisions', () => {
  const revBefore = allText(byId['revisions-body']);

  // Click pump filter
  fire(byId['filter-pump'], 'click');
  assert.equal(byId['filter-pump'].attributes['aria-pressed'], 'true');
  const pumpRows = rowsOf('tags-body').map(cellText);
  assert.ok(pumpRows.length > 0 && pumpRows.every((r) => r[0].startsWith('PUMP_')));

  const revAfterPump = allText(byId['revisions-body']);
  assert.equal(revBefore, revAfterPump, 'pump filter did not alter revisions');

  // Click valves filter
  fire(byId['filter-valves'], 'click');
  const valveRows = rowsOf('tags-body').map(cellText);
  assert.ok(valveRows.length > 0 && valveRows.every((r) => /^IV[1-8]_/.test(r[0])));

  const revAfterValves = allText(byId['revisions-body']);
  assert.equal(revBefore, revAfterValves, 'valves filter did not alter revisions');

  // Reset to all
  fire(byId['filter-all'], 'click');
  assert.equal(rowsOf('tags-body').length, 26);
});

test('17. Pump and IV grouping is correct', () => {
  // Toggle grouping on
  fire(byId['btn-group-equipment'], 'click');
  const allRows = rowsOf('tags-body');

  const headerRows = allRows.filter((r) => r.className === 'group-hdr').map(cellText);
  assert.ok(headerRows.some((r) => r[0].includes('Pump')));
  assert.ok(headerRows.some((r) => r[0].includes('IV1')));
  assert.ok(headerRows.some((r) => r[0].includes('IV8')));

  // Toggle grouping back off for subsequent flat tests
  fire(byId['btn-group-equipment'], 'click');
  assert.equal(rowsOf('tags-body').length, 26);
});

test('18. Reserved rows are grouped separately', () => {
  assert.equal(byId['reserved-title'].textContent, 'Reserved channels awaiting Owner identity (18)');
  assert.equal(rowsOf('reserved-body').length, 18);
  const tagRows = rowsOf('tags-body').map(cellText).map((c) => c[0]);
  for (const reservedTag of ['AI-020', 'AI-035', 'DI-037', 'DO-031']) {
    assert.ok(!tagRows.includes(reservedTag), `${reservedTag} must not be in tags table`);
  }
});

test('19. Placeholder summary shows 17 Input + 1 Output = 18 Total', () => {
  const sourceText = allText(byId['source-body']);
  assert.match(sourceText, /17 Input \+ 1 Output = 18 Total/);

  const reservedNote = byId['reserved-note'].textContent;
  assert.match(reservedNote, /17 Input \+ 1 Output = 18 Total/);
});

test('20. Compact address state does not hide Error status', () => {
  const tagRows = rowsOf('tags-body');
  const pumpOutletRow = tagRows.find((r) => cellText(r)[0] === 'PUMP_OUTLET_PRESSURE');
  assert.ok(pumpOutletRow);

  const addressTd = pumpOutletRow.childNodes[7];
  assert.match(allText(addressTd), /^ADDRESS UNRESOLVED · \d+ reasons/);
  assert.equal(addressTd.className, 'addr-unresolved');
});

test('21. Expanded address reasons remain read-only', () => {
  fire(byId['btn-expand-reasons'], 'click');
  const tagRows = rowsOf('tags-body');
  const pumpOutletRow = tagRows.find((r) => cellText(r)[0] === 'PUMP_OUTLET_PRESSURE');
  const addressTd = pumpOutletRow.childNodes[7];

  const fullText = allText(addressTd);
  assert.match(fullText, /HEAD_STATION_PROFILE_NOT_VERIFIED/);
  assert.match(fullText, /NO_VERIFIED_PROCESS_IMAGE_RULE/);

  assert.doesNotMatch(fullText, /<input|<button type="submit"|<select/);
});

test('22. Validation warnings are grouped deterministically', () => {
  const valBody = byId['validation-body'];
  const text = allText(valBody);
  assert.match(text, /Module profiles incomplete/);
  assert.match(text, /Engineering ranges pending/);
  assert.match(text, /Unresolved addresses/);
  assert.match(text, /Validation warnings/);
});

test('23. All element IDs are unique', () => {
  const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map((m) => m[1]);
  const seen = new Set();
  for (const id of ids) {
    assert.ok(!seen.has(id), `duplicate ID found: ${id}`);
    seen.add(id);
  }
});

test('24. Workbook-generated values are written as text, not unsafe HTML', () => {
  assert.doesNotMatch(appSrc, /innerHTML|outerHTML|insertAdjacentHTML|document\.write|\beval\(/);
  assert.match(appSrc, /textContent/);
});

test('25. Server remains loopback-only', () => {
  assert.equal(UI_BIND_HOST, '127.0.0.1');
});

test('26. GET/HEAD-only HTTP boundary remains enforced', async () => {
  for (const method of ['POST', 'PUT', 'DELETE']) {
    const res = await fetch('/api/configuration', { method, body: '{}' });
    assert.equal(res.status, 405, `Method ${method} was not rejected`);
  }
});

test('27. No Hardware or Write control is introduced', () => {
  const forbiddenTerms = /\b(connect|poll|write|force|activate|command|test_hardware|production|read device)\b/i;
  const buttonLabels = [...html.matchAll(/<button\b[^>]*>([\s\S]*?)<\/button>/g)].map((m) => m[1]);
  for (const label of buttonLabels) {
    assert.doesNotMatch(label, forbiddenTerms, `Forbidden button label: ${label}`);
  }
  const allIds = [...html.matchAll(/\bid="([^"]+)"/g)].map((m) => m[1]);
  for (const id of allIds) {
    assert.doesNotMatch(id, forbiddenTerms, `Forbidden id: ${id}`);
  }
});
