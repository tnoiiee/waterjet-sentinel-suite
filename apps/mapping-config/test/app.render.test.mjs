// Stage 0.4B-1 — runs the real public/app.mjs against a stub DOM and the real server.
// Verifies rendering, the Draft rack controls, refusals and Undo/Redo/Reset. It does not
// prove layout or real-browser behaviour; that is reported as NOT VERIFIED in a browser.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { register } from 'node:module';
import { createUiServer } from '../server.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const html = readFileSync(join(HERE, '..', 'public', 'index.html'), 'utf8');
const realFetch = globalThis.fetch;
let server;
const byId = {};

class FakeNode {
  constructor(tag) {
    this.tagName = tag; this.attributes = {}; this.childNodes = []; this.listeners = {};
    this.textContent = ''; this.disabled = false; this.className = ''; this.title = '';
    this.classList = { add() {}, remove() {} };
  }
  setAttribute(k, v) { this.attributes[k] = String(v); }
  append(...nodes) { this.childNodes.push(...nodes); }
  replaceChildren(...nodes) { this.childNodes = nodes; this.textContent = ''; }
  addEventListener(t, f) { (this.listeners[t] ??= []).push(f); }
  focus() {}
}
class FakeText extends FakeNode {
  constructor(t) { super('#text'); this.textContent = t; this.data = t; }
}
const allText = (node) => (node instanceof FakeText ? node.textContent
  : node.childNodes.map(allText).join(' '));
const rowsOf = (id) => byId[id].childNodes;
const cellText = (row) => row.childNodes.map((td) => allText(td));
const fire = (node, type, evt = {}) => { for (const f of node.listeners[type] ?? []) f({ preventDefault() {}, ...evt }); };

before(async () => {
  register(new URL('./helpers/pkgResolveHook.mjs', import.meta.url));
  for (const m of html.matchAll(/\bid="([^"]+)"/g)) byId[m[1]] = new FakeNode(m[1]);
  globalThis.Node = FakeNode;
  globalThis.document = {
    getElementById: (id) => (byId[id] ??= new FakeNode(id)),
    createElement: (tag) => new FakeNode(tag),
    createTextNode: (t) => new FakeText(t),
    querySelector: () => null,
    activeElement: null,
  };
  server = createUiServer();
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  const base = origin.origin;
  globalThis.fetch = (path, opts) => realFetch(`${base}${path}`, opts);
  await import('../public/app.mjs');
  for (let i = 0; i < 200 && byId['rack-body'].childNodes.length === 0; i += 1) {
    await new Promise((r) => setTimeout(r, 10));
  }
});

after(() => {
  server.close();
  globalThis.fetch = realFetch;
});

test('the UI renders every section from the real configuration', () => {
  assert.equal(rowsOf('rack-body').length, 10, 'rack rows');
  assert.equal(rowsOf('tags-body').length, 26, 'tag rows: pump inlet, pump outlet, IV1-8, 16 limits (no Main Valve row)');
  assert.match(allText(byId.chips), /SIMULATION ONLY/);
  assert.match(allText(byId.chips), /NO HARDWARE ACCESS/);
  assert.match(allText(byId.chips), /ACTIVATION NOT AUTHORIZED/);
  assert.match(allText(byId.chips), /Validation: VALID/);
  assert.match(allText(byId['validation-body']), /Activation ready: no/);
  assert.match(allText(byId['revisions-body']), /RackTopologyRevision/);
  assert.match(allText(byId['impact-body']), /ACTIVATION NOT AUTHORIZED/);
});

test('RackSlot and ProcessModulePosition are both shown; addresses are shown as unresolved text only', () => {
  const first = cellText(rowsOf('rack-body')[2]);
  assert.equal(first[0], '3');
  assert.equal(first[1], 'DI-MODULE-01');
  assert.equal(first[4], '1');
  const tagRow = cellText(rowsOf('tags-body')[0]);
  assert.match(tagRow[7], /^ADDRESS UNRESOLVED/);
  assert.doesNotMatch(tagRow.join(' '), /\b\d+\.\d\b|offset \d/i);
});

test('fixed modules are not movable; the Coupler row carries no move handler', () => {
  const coupler = rowsOf('rack-body')[0];
  assert.equal(coupler.attributes.draggable, 'false');
  assert.equal(coupler.listeners.keydown, undefined);
});

test('Alt+Arrow moves a module in the Draft; Undo and Reset respond to the change', () => {
  assert.equal(byId['btn-undo'].disabled, true);
  assert.equal(byId['btn-reset'].disabled, true);
  const ai3 = rowsOf('rack-body')[7];
  assert.equal(ai3.attributes['data-id'], 'AI-MODULE-03');
  fire(ai3, 'keydown', { altKey: true, key: 'ArrowUp' });
  const order = rowsOf('rack-body').map((r) => r.attributes['data-id']);
  assert.equal(order[6], 'AI-MODULE-03', 'one Alt+ArrowUp step moves the row up by one place');
  assert.equal(byId['btn-undo'].disabled, false);
  assert.equal(byId['btn-reset'].disabled, false);
  assert.equal(byId['btn-redo'].disabled, true);
});

test('a refused move shows the refusal code and leaves the Draft unchanged', () => {
  const before = rowsOf('rack-body').map((r) => r.attributes['data-id']).join(',');
  const supply = rowsOf('rack-body')[1];
  assert.equal(supply.attributes['data-id'], 'SUPPLY-01');
  fire(supply, 'keydown', { altKey: true, key: 'ArrowUp' });
  assert.match(byId.refusal.textContent, /COUPLER_NOT_FIRST/);
  assert.equal(rowsOf('rack-body').map((r) => r.attributes['data-id']).join(','), before);
});

test('Undo restores the default order; Redo re-applies; Reset returns to the default', () => {
  const moved = rowsOf('rack-body').map((r) => r.attributes['data-id']).join(',');
  byId['btn-undo'].listeners.click[0]();
  assert.equal(rowsOf('rack-body')[5].attributes['data-id'], 'AI-MODULE-01');
  byId['btn-redo'].listeners.click[0]();
  assert.equal(rowsOf('rack-body').map((r) => r.attributes['data-id']).join(','), moved);
  byId['btn-reset'].listeners.click[0]();
  assert.equal(rowsOf('rack-body')[5].attributes['data-id'], 'AI-MODULE-01');
  assert.equal(byId['btn-reset'].disabled, true);
});

test('drag and drop onto a row moves the module through the same Draft path', () => {
  const dragged = rowsOf('rack-body')[8]; // AO-MODULE-01 (reorderable)
  const id = dragged.attributes['data-id'];
  const data = {};
  const transfer = { setData: (_k, v) => { data.id = v; }, getData: () => data.id, effectAllowed: '', dropEffect: '' };
  fire(dragged, 'dragstart', { dataTransfer: transfer });
  fire(rowsOf('rack-body')[5], 'drop', { dataTransfer: transfer });
  assert.equal(rowsOf('rack-body')[5].attributes['data-id'], id);
});
