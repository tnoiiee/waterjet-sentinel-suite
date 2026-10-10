// Stage 0.4B-2 — review surface rendered by the real public/app.mjs against a stub DOM and the real server.
// The configuration is the Excel-shaped authoritative default (synthetic rows, no plant data), written to a temporary
// copy OUTSIDE the repository. Presentation controls are exercised; layout and real-browser behaviour are NOT.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { loadConfiguration } from '../server.mjs';
import { buildWorkbook, authoritativeShapedRows } from '../../../packages/mapping-config/test/helpers/syntheticWorkbook.mjs';
import { allText, bootApp, fire, html, walk } from './helpers/fakeDom.mjs';

let app;
let tmp;
const by = () => app.byId;
const rows = (id) => by()[id].childNodes;
const cells = (row) => row.childNodes.map(allText);
const isData = (r) => r.className.split(' ').includes('data-row');
const dataRows = () => rows('tags-body').filter(isData);
const headers = () => rows('tags-body').filter((r) => r.className === 'group-row').map(allText);
const press = (id) => fire(by()[id], 'click');
const filterButton = (key) => by()['tag-filters'].childNodes.find((b) => b.attributes['data-filter'] === key);
const snapshot = () => ({
  revisions: allText(by()['revisions-body']),
  rack: rows('rack-body').map(allText).join('|'),
  undo: by()['btn-undo'].disabled,
  redo: by()['btn-redo'].disabled,
  reset: by()['btn-reset'].disabled,
  chips: allText(by().chips),
  validation: allText(by()['validation-body']),
  reserved: rows('reserved-body').map(allText).join('|'),
  impact: allText(by()['impact-body']),
});

before(async () => {
  tmp = mkdtempSync(join(tmpdir(), 'wjss-review-'));
  const xlsx = join(tmp, 'authoritative-shaped.xlsx');
  writeFileSync(xlsx, buildWorkbook({ rows: authoritativeShapedRows() }));
  app = await bootApp({ loader: () => loadConfiguration({ MAPPING_EXCEL_DEFAULT_PATH: xlsx }), waitId: 'reserved-body' });
  for (let i = 0; i < 100 && rows('tags-body').length === 0; i += 1) await new Promise((r) => setTimeout(r, 10));
});

after(() => {
  app.close();
  rmSync(tmp, { recursive: true, force: true });
});

test('U9 Comfortable density is the default, in the markup and after boot', () => {
  assert.match(html, /<body id="app-root" data-density="comfortable">/);
  assert.match(html, /id="density-comfortable" aria-pressed="true"/);
  assert.match(html, /id="density-compact" aria-pressed="false"/);
  assert.equal(by()['app-root'].attributes['data-density'], undefined, 'boot does not change the markup default');
});

test('U10 Compact density changes presentation only: no Mapping text, revision, history or control state changes', () => {
  const before = snapshot();
  press('density-compact');
  assert.equal(by()['app-root'].attributes['data-density'], 'compact');
  assert.equal(by()['density-compact'].attributes['aria-pressed'], 'true');
  assert.equal(by()['density-comfortable'].attributes['aria-pressed'], 'false');
  assert.deepEqual(snapshot(), before);
  press('density-comfortable');
  assert.equal(by()['app-root'].attributes['data-density'], 'comfortable');
  assert.deepEqual(snapshot(), before);
});

test('U11 filters do not alter the Draft or the default: revisions, history and the rack are identical after every filter', () => {
  const before = snapshot();
  const filterKeys = by()['tag-filters'].childNodes.map((b) => b.attributes['data-filter']);
  assert.deepEqual(filterKeys, ['all', 'runtime', 'readonly', 'pending', 'analog', 'digital', 'pump', 'iv', 'unresolved']);
  for (const key of filterKeys) {
    fire(filterButton(key), 'click');
    assert.equal(filterButton(key).attributes['aria-pressed'], 'true', key);
    assert.deepEqual(snapshot(), before, key);
  }
  press('view-flat');
  assert.deepEqual(snapshot(), before);
  press('view-grouped');
  fire(filterButton('all'), 'click');
  assert.deepEqual(snapshot(), before);
  assert.equal(by()['btn-undo'].disabled, true, 'no filter creates Draft history');
});

test('U12 the Tag mapping table groups Pump, IV1-IV8 and the reserved rows, in that order', () => {
  fire(filterButton('all'), 'click');
  const h = headers();
  assert.equal(h[0], 'Pump · 2 rows');
  for (let n = 1; n <= 8; n += 1) assert.equal(h[n], `IV${n} · 3 rows`);
  assert.equal(h[h.length - 1], 'Reserved / Owner pending · 18 rows');
  // Rows sit under their own header.
  const order = rows('tags-body').map((r) => (r.className === 'group-row' ? `#${allText(r)}` : cells(r)[0]));
  const at = (label) => order.findIndex((x) => x === `#${label}`);
  const slice = (label, next) => order.slice(at(label) + 1, at(next));
  assert.deepEqual(slice('Pump · 2 rows', 'IV1 · 3 rows').sort(), ['PUMP_INLET_PRESSURE', 'PUMP_OUTLET_PRESSURE']);
  assert.deepEqual(slice('IV3 · 3 rows', 'IV4 · 3 rows').sort(), ['IV3_LOWER_LIMIT', 'IV3_OUTLET_PRESSURE', 'IV3_UPPER_LIMIT']);
});

test('U13 reserved rows are grouped separately and shown only by the pending filter, with no binding or address', () => {
  fire(filterButton('pending'), 'click');
  const shown = dataRows();
  assert.equal(shown.length, 18);
  assert.ok(shown.every((r) => r.attributes['data-kind'] === 'reserved'));
  assert.deepEqual(headers(), ['Reserved / Owner pending · 18 rows']);
  for (const r of shown) {
    const c = cells(r);
    assert.match(c[1], /^RESERVED · OWNER INPUT PENDING/);
    assert.equal(c[4], 'no (reserved)');
    assert.equal(c[7], 'ADDRESS UNRESOLVED · no binding');
  }
  for (const key of ['runtime', 'pump', 'iv']) {
    fire(filterButton(key), 'click');
    assert.ok(dataRows().every((r) => r.attributes['data-kind'] === 'binding'), key);
  }
  fire(filterButton('all'), 'click');
});

test('U14 the placeholder summary reads 17 Input + 1 Output = 18 Total and explains the output row', () => {
  const text = by()['placeholder-summary'].textContent;
  assert.match(text, /17 Input \+ 1 Output = 18 Total/);
  assert.match(text, /[Pp]laceholder input rows 17 \+ placeholder output rows 1 = 18 total Owner-input-pending rows/);
  const source = allText(by()['source-body']);
  assert.match(source, /Placeholder input rows 17 \+ placeholder output rows 1 = 18 total Owner-input-pending rows/);
  assert.match(source, /18 reserved placeholder rows \(1 of them an output, DO-031\)/);
  assert.doesNotMatch(text, /\b17 (placeholder|reserved|total)/i, 'no ambiguous total of 17');
});

test('U15 the address cell is compact by default, and expanding it shows the reasons as read-only text', () => {
  const row = dataRows().find((r) => cells(r)[0] === 'PUMP_OUTLET_PRESSURE');
  const td = row.childNodes[7];
  assert.equal(td.className, 'addr-unresolved');
  const details = td.childNodes[0];
  assert.equal(details.tagName, 'details');
  const compact = allText(td);
  assert.match(compact, /^ADDRESS UNRESOLVED · \d+ reasons$/);
  assert.doesNotMatch(compact, /HEAD_STATION_PROFILE_NOT_VERIFIED|MODULE_/, 'reasons are not repeated in the row by default');
  details.open = true;
  fire(details, 'toggle');
  const list = details.childNodes[1];
  assert.equal(list.tagName, 'ul');
  const n = Number(/(\d+) reasons/.exec(compact)[1]);
  assert.equal(list.childNodes.length, n, 'the expansion lists exactly the counted reasons');
  assert.ok(allText(list).includes('HEAD_STATION_PROFILE_NOT_VERIFIED'));
  const tags = [];
  walk(details, (x) => tags.push(x.tagName));
  assert.deepEqual([...new Set(tags)].sort(), ['#text', 'details', 'li', 'summary', 'ul'], 'expanded content is text only: no control, input or link');
  details.open = false;
  fire(details, 'toggle');
  assert.equal(details.childNodes.length, 1, 'collapsing removes the list');
});

test('U16 validation shows grouped counts first, in a fixed order, with unresolved addresses and Owner identities counted', () => {
  const groups = by()['validation-body'].childNodes.find((n) => n.className === 'vgroups').childNodes;
  const labels = groups.map((g) => allText(g.childNodes[0]).replace(/^\s*\d+\s*/, '').trim());
  assert.deepEqual(labels, ['Errors', 'Module profiles incomplete', 'Engineering ranges pending', 'Owner identities pending', 'Unresolved addresses', 'Other warnings']);
  const count = (label) => Number(allText(groups[labels.indexOf(label)].childNodes[0]).trim().split(/\s+/)[0]);
  assert.equal(count('Errors'), 0);
  assert.equal(count('Owner identities pending'), 18);
  const owner = groups[labels.indexOf('Owner identities pending')];
  owner.open = true;
  fire(owner, 'toggle');
  assert.equal(owner.childNodes[1].childNodes.length, 18);
  assert.match(allText(owner.childNodes[1]), /DO-031: identity UNRESOLVED · OWNER_INPUT_PENDING · output not authorized/);
  owner.open = false;
  fire(owner, 'toggle');
  assert.equal(count('Unresolved addresses'), 26);
  assert.ok(count('Module profiles incomplete') > 0);
  // Details are collapsed (except errors) and expand to the individual items.
  const addresses = groups[labels.indexOf('Unresolved addresses')];
  assert.equal(addresses.childNodes.length, 1);
  addresses.open = true;
  fire(addresses, 'toggle');
  assert.equal(addresses.childNodes[1].childNodes.length, 26);
  addresses.open = false;
  fire(addresses, 'toggle');
  // The earlier summary lines remain.
  assert.match(allText(by()['validation-body']), /Activation ready: no · Activation: ACTIVATION NOT AUTHORIZED/);
});

test('U17 the evidence review lists every rack module and keeps addresses unresolved with ProcessModulePosition separate', () => {
  const rack = rows('rack-body').length;
  assert.equal(rows('evidence-body').length, rack, 'one evidence row per rack module');
  const text = allText(by()['evidence-summary']);
  assert.match(text, /NOT AUTHORITATIVE · address-capable: no/);
  assert.match(text, /0 complete · \d+ incomplete of \d+/);
  const ai = rows('evidence-body').map(cells).find((c) => /750-471 · AI-MODULE-01/.test(c[0]));
  assert.equal(ai[3], 'NOT_PROVIDED · INCOMPLETE');
  assert.equal(ai[4], 'NONE PROVIDED');
  assert.equal(ai[5], 'NOT VERIFIED');
  assert.match(ai[8], /^ADDRESS UNRESOLVED · \d+ reasons$/);
  assert.equal(ai[7], 'NOT_VERIFIED', 'ProcessImageOrder is not derived');
  assert.notEqual(ai[2], '—', 'ProcessModulePosition is still shown');
  const coupler = rows('evidence-body').map(cells).find((c) => /750-362/.test(c[0]));
  assert.equal(coupler[2], '—');
  assert.equal(coupler[8], 'NOT APPLICABLE');
  const sources = allText(by()['evidence-sources']);
  assert.match(sources, /OWNER-WORKBOOK · OWNER_WORKBOOK_FACT · T8_IO_Card_Mapping\.xlsx · SHA-256 4e0337e25c8377c0/);
  assert.match(allText(by()['revisions-body']), /ProcessImageEvidenceRevision/);
});

test('U18 every control rendered is a Draft control or a presentation control; none names a device action', () => {
  // Static buttons come from the markup; the filter buttons are created by the app.
  const buttons = [...html.matchAll(/<button\b[^>]*>([\s\S]*?)<\/button>/g)].map((m) => m[1].trim());
  for (const id of Object.keys(by())) walk(by()[id], (n) => { if (n.tagName === 'button') buttons.push(allText(n)); });
  assert.ok(buttons.length >= 16);
  const forbidden = /\b(connect|poll|read device|write|force|activate|command|start|stop|acknowledge|reset hardware|override|test_hardware|production)\b/i;
  for (const label of buttons) assert.doesNotMatch(label, /^(Activate|Connect|Poll|Write|Start|Stop|Acknowledge)\b/i, label);
  for (const label of buttons.filter((l) => !/^(Undo|Redo|Reset)$/.test(l))) assert.doesNotMatch(label, forbidden, label);
  const tags = new Set();
  for (const id of Object.keys(by())) walk(by()[id], (n) => tags.add(n.tagName));
  for (const t of ['input', 'select', 'textarea', 'form']) assert.equal(tags.has(t), false, t);
});
