// Stage 0.4B-2 — a loader-injected invalid binding and hostile workbook-derived strings, rendered by the real app.
// The injection happens in the loader (the server's data), not in the app: the app must still show the error and
// must render every string as text. Layout is not exercised.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { loadConfiguration } from '../server.mjs';
import { buildWorkbook, authoritativeShapedRows } from '../../../packages/mapping-config/test/helpers/syntheticWorkbook.mjs';
import { allText, bootApp, fire, walk, FakeNode } from './helpers/fakeDom.mjs';

const HOSTILE = '<b>x</b>';
const HOSTILE_IMG = '<img src=x onerror=alert(1)>';
let app;
let tmp;
const by = () => app.byId;

before(async () => {
  tmp = mkdtempSync(join(tmpdir(), 'wjss-hostile-'));
  const xlsx = join(tmp, 'authoritative-shaped.xlsx');
  writeFileSync(xlsx, buildWorkbook({ rows: authoritativeShapedRows() }));
  const loader = async () => {
    const r = JSON.parse(JSON.stringify(await loadConfiguration({ MAPPING_EXCEL_DEFAULT_PATH: xlsx }))); // unfrozen copy
    const b = r.configuration.bindings.find((x) => x.tagName === 'IV1_UPPER_LIMIT');
    b.activePolarity = null; // an invalid binding that the loader supplied
    r.reservedInventory.rows[0].workbookTag = HOSTILE;
    r.importReport.issueSummary[HOSTILE_IMG] = 1; // a hostile issue code, rendered in the import issue list
    return r;
  };
  app = await bootApp({ loader, waitId: 'reserved-body' });
  for (let i = 0; i < 100 && by()['tags-body'].childNodes.length === 0; i += 1) await new Promise((r) => setTimeout(r, 10));
});

after(() => {
  app.close();
  rmSync(tmp, { recursive: true, force: true });
});

test('U19 an invalid binding is shown as ERROR × 1 on its row, counted in the Errors group, and that group is open', () => {
  const row = by()['tags-body'].childNodes.find((r) => r.className.includes('data-row') && allText(r.childNodes[0]) === 'IV1_UPPER_LIMIT');
  assert.ok(row, 'the row is shown');
  const issueCell = allText(row.childNodes[8]);
  assert.match(issueCell, /^ERROR × 1/);
  assert.equal(row.childNodes[8].className.includes('issue-error'), true);
  assert.match(allText(by().chips), /Validation: INVALID/);
  const groups = by()['validation-body'].childNodes.find((n) => n.className === 'vgroups').childNodes;
  const errors = groups[0];
  assert.match(allText(errors.childNodes[0]), /^\s*2\s+Errors/, 'one rack-level finding (limit polarity not uniform) and one row-level finding');
  assert.equal(errors.attributes.open, '', 'the Errors group is open by default');
  assert.match(allText(errors), /POLARITY_NOT_EXPLICIT — IV1_UPPER_LIMIT active polarity must be set explicitly/);
  assert.match(allText(errors), /LIMIT_POLARITY_NOT_UNIFORM/);
  for (const g of groups.slice(1)) assert.equal(g.attributes.open, undefined, 'warning groups are collapsed');
});

test('U20 no filter or grouping setting hides the error: the all, unresolved, iv and flat views all show the row and the Errors group', () => {
  const filterButton = (key) => by()['tag-filters'].childNodes.find((b) => b.attributes['data-filter'] === key);
  const showsRow = () => by()['tags-body'].childNodes.some((r) => r.className.includes('data-row') && allText(r.childNodes[0]) === 'IV1_UPPER_LIMIT' && /^ERROR × 1/.test(allText(r.childNodes[8])));
  for (const key of ['all', 'unresolved', 'iv', 'runtime']) {
    fire(filterButton(key), 'click');
    assert.ok(showsRow(), key);
  }
  fire(by()['view-flat'], 'click');
  assert.ok(showsRow(), 'flat');
  fire(by()['view-grouped'], 'click');
  fire(by()['density-compact'], 'click');
  assert.ok(showsRow(), 'compact');
  fire(by()['density-comfortable'], 'click');
  fire(filterButton('all'), 'click');
  assert.match(allText(by()['validation-body']), /Errors/);
  // Filtered-out rows are counted, never silently dropped: the visible count states the total.
  fire(filterButton('pump'), 'click');
  assert.match(by()['tags-count'].textContent, /2 of \d+/);
  fire(filterButton('all'), 'click');
});

test('U21 hostile workbook-derived strings are rendered as text: no element is created from them', () => {
  const tags = new Set();
  const texts = [];
  for (const id of Object.keys(by())) {
    walk(by()[id], (n) => {
      if (n.tagName && n.tagName !== '#text') tags.add(n.tagName);
      if (n.tagName === '#text') texts.push(n.textContent);
      if (n.textContent) texts.push(n.textContent);
    });
  }
  for (const t of ['b', 'img', 'script', 'iframe', 'a', 'svg']) assert.equal(tags.has(t), false, t);
  const all = texts.join('\n');
  assert.ok(all.includes(HOSTILE), 'the reserved tag string is present as text');
  assert.ok(all.includes(HOSTILE_IMG), 'the import string is present as text');
  assert.ok(Object.values(by()).every((n) => !(n instanceof FakeNode) || !('innerHTML' in n)), 'no markup assignment');
});

test('U22 no forbidden control or device word appears as a control or in the rendered DOM text', () => {
  const rendered = [];
  for (const id of Object.keys(by())) walk(by()[id], (n) => { if (n.textContent) rendered.push(n.textContent); });
  const text = rendered.join('\n');
  assert.doesNotMatch(text, /\b(Connect|Poll|Read device|Acknowledge|Write value|Force)\b/);
  const buttons = [];
  for (const id of Object.keys(by())) walk(by()[id], (n) => { if (n.tagName === 'button') buttons.push(allText(n)); });
  assert.ok(buttons.length >= 9);
  assert.doesNotMatch(buttons.join('|'), /connect|poll|write|activate|start|stop|acknowledge|override|force/i);
});
