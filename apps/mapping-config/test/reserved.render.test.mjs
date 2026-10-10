// Stage 0.4B-1 — reserved inventory and analog presentation, rendered by the real public/app.mjs against a stub DOM.
// The configuration is the Excel-shaped authoritative default (synthetic rows, no plant data), written to a temporary
// copy OUTSIDE the repository and removed afterwards. No seed and no device access are involved.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { register } from 'node:module';
import { createUiServer, loadConfiguration } from '../server.mjs';
import { buildWorkbook, authoritativeShapedRows } from '../../../packages/mapping-config/test/helpers/syntheticWorkbook.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const html = readFileSync(join(HERE, '..', 'public', 'index.html'), 'utf8');
const realFetch = globalThis.fetch;
const byId = {};
let server;
let tmp;

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
const allText = (node) => (node instanceof FakeText ? node.textContent : node.childNodes.map(allText).join(' '));
const rowsOf = (id) => byId[id].childNodes;
const cellText = (row) => row.childNodes.map((td) => allText(td));
const tagRow = (tag) => rowsOf('tags-body').map(cellText).find((c) => c[0] === tag);
const reservedRow = (tag) => rowsOf('reserved-body').find((r) => r.attributes['data-tag'] === tag);

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
  tmp = mkdtempSync(join(tmpdir(), 'wjss-reserved-'));
  const xlsx = join(tmp, 'authoritative-shaped.xlsx');
  writeFileSync(xlsx, buildWorkbook({ rows: authoritativeShapedRows() }));
  server = createUiServer({ loader: () => loadConfiguration({ MAPPING_EXCEL_DEFAULT_PATH: xlsx }) });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const base = `http://127.0.0.1:${server.address().port}`;
  globalThis.fetch = (path, opts) => realFetch(`${base}${path}`, opts);
  await import('../public/app.mjs');
  for (let i = 0; i < 200 && byId['reserved-body'].childNodes.length === 0; i += 1) {
    await new Promise((r) => setTimeout(r, 10));
  }
});

after(() => {
  server.close();
  globalThis.fetch = realFetch;
  rmSync(tmp, { recursive: true, force: true });
});

test('the reserved heading shows the count 18 and all 18 rows are rendered', () => {
  assert.equal(byId['reserved-title'].textContent, 'Reserved channels awaiting Owner identity (18)');
  assert.equal(rowsOf('reserved-body').length, 18);
});

test('each reserved row has the 11 required columns, in the order of the reserved table header', () => {
  const section = html.slice(html.indexOf('id="reserved"'), html.indexOf('id="validation"'));
  const headers = [...section.matchAll(/<th scope="col">([^<]+)<\/th>/g)].map((m) => m[1]);
  assert.deepEqual(headers, ['Workbook I/O Tag', 'ModuleInstanceId', 'Channel', 'Direction', 'Physical Status', 'Signal Identity',
    'Binding Status', 'Owner Input Status', 'Auto-binding Eligibility', 'Address Status', 'Mapping Authorization']);
  for (const row of rowsOf('reserved-body')) assert.equal(row.childNodes.length, headers.length);
});

test('AI-020, AI-035, DI-037 and DO-031 are all present in the reserved section', () => {
  for (const tag of ['AI-020', 'AI-035', 'DI-037', 'DO-031']) assert.ok(reservedRow(tag), tag);
});

test('DO-031 remains OUTPUT and NOT_AUTHORIZED_IN_READ_ONLY_STAGE in the reserved section', () => {
  const cells = cellText(reservedRow('DO-031'));
  assert.equal(cells[3], 'OUTPUT · NOT AUTHORIZED');
  assert.equal(cells[10], 'NOT_AUTHORIZED_IN_READ_ONLY_STAGE');
});

test('every reserved row reads UNRESOLVED, UNBOUND, OWNER_INPUT_PENDING, PROHIBITED and ADDRESS_UNRESOLVED', () => {
  for (const row of rowsOf('reserved-body')) {
    const c = cellText(row);
    assert.equal(c[4], 'USED / RESERVED');
    assert.equal(c[5], 'UNRESOLVED');
    assert.equal(c[6], 'UNBOUND');
    assert.equal(c[7], 'OWNER_INPUT_PENDING');
    assert.equal(c[8], 'PROHIBITED');
    assert.equal(c[9], 'ADDRESS_UNRESOLVED');
  }
});

test('the reserved note says the rows are not bindings, not spare and have no address', () => {
  assert.match(byId['reserved-note'].textContent, /not enabled bindings/);
  assert.match(byId['reserved-note'].textContent, /not SPARE or FREE/);
  assert.match(byId['reserved-note'].textContent, /18 rows are USED \/ RESERVED: INPUT 17 · OUTPUT 1/);
});

test('the source summary reports 18 reserved rows, one of them the output DO-031', () => {
  const source = allText(byId['source-body']);
  assert.match(source, /18 reserved placeholder rows \(1 of them an output, DO-031\)/);
  assert.match(source, /0 other outputs NOT AUTHORIZED/, 'this synthetic-shaped workbook has DO-031 as its only output row');
});

test('analog pressure rows read Polarity NOT APPLICABLE and Contact NOT APPLICABLE', () => {
  for (const tag of ['PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE', 'IV1_OUTLET_PRESSURE', 'IV8_OUTLET_PRESSURE']) {
    const c = tagRow(tag);
    assert.ok(c, tag);
    assert.equal(c[5], 'NOT APPLICABLE', `${tag} polarity`);
    assert.equal(c[6], 'NOT APPLICABLE', `${tag} contact`);
  }
});

test('digital limits read ACTIVE_WHEN_CLOSED and NO', () => {
  for (const tag of ['IV1_LOWER_LIMIT', 'IV8_UPPER_LIMIT']) {
    const c = tagRow(tag);
    assert.equal(c[5], 'ACTIVE_WHEN_CLOSED', `${tag} polarity`);
    assert.equal(c[6], 'NO', `${tag} contact`);
  }
});

test('NOT SET and UNKNOWN never appear on an analog row, checked by name for every analog pressure row', () => {
  const analog = ['PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE', ...Array.from({ length: 8 }, (_, i) => `IV${i + 1}_OUTLET_PRESSURE`)];
  for (const tag of analog) {
    const cells = tagRow(tag);
    assert.ok(cells, tag);
    assert.doesNotMatch(cells[5] + ' ' + cells[6], /NOT SET|UNKNOWN/, tag);
  }
});

test('an unresolved address is shown as unresolved text, never as a success state', () => {
  const c = tagRow('PUMP_OUTLET_PRESSURE');
  assert.match(c[7], /^ADDRESS UNRESOLVED/);
  const td = rowsOf('tags-body').find((r) => cellText(r)[0] === 'PUMP_OUTLET_PRESSURE').childNodes[7];
  assert.equal(td.className, 'addr-unresolved');
});
