// Stage 0.4B-1 — UI shell tests: routes, method refusals, control whitelist, id coverage and the local Excel path.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createUiServer, loadConfiguration } from '../server.mjs';
import { buildWorkbook, exampleRows, exampleSeed } from '../../../packages/mapping-config/test/helpers/syntheticWorkbook.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const PUBLIC = join(HERE, '..', 'public');
const html = readFileSync(join(PUBLIC, 'index.html'), 'utf8');
const appSrc = readFileSync(join(PUBLIC, 'app.mjs'), 'utf8');
const css = readFileSync(join(PUBLIC, 'styles.css'), 'utf8');

let server;
let base;
let tmp;

before(async () => {
  tmp = mkdtempSync(join(tmpdir(), 'wjss-mapping-ui-'));
  server = createUiServer();
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  base = origin.origin;
});

after(() => {
  server.close();
  rmSync(tmp, { recursive: true, force: true });
});

test('the shell and its assets are served with a restrictive policy', async () => {
  const r = await fetch(`${base}/`);
  assert.equal(r.status, 200);
  assert.match(r.headers.get('content-type'), /text\/html/);
  assert.match(r.headers.get('content-security-policy'), /default-src 'self'/);
  assert.equal(r.headers.get('x-content-type-options'), 'nosniff');
  assert.match(await r.text(), /id="btn-undo"/);
  for (const path of ['/app.mjs', '/styles.css', '/pkg/index.mjs', '/pkg/rack.mjs']) {
    const a = await fetch(`${base}${path}`);
    assert.equal(a.status, 200, path);
  }
  const head = await fetch(`${base}/`, { method: 'HEAD' });
  assert.equal(head.status, 200);
});

test('Node-only modules and anything outside the whitelist are not served', async () => {
  for (const path of ['/pkg/workbookImport.mjs', '/pkg/nodeImport.mjs', '/pkg/../server.mjs', '/server.mjs', '/test/ui.test.mjs', '/api/unknown']) {
    const r = await fetch(`${base}${path}`);
    assert.equal(r.status, 404, path);
  }
});

test('every method other than GET and HEAD is refused with 405', async () => {
  for (const method of ['POST', 'PUT', 'PATCH', 'DELETE']) {
    for (const path of ['/', '/api/configuration', '/pkg/index.mjs']) {
      const r = await fetch(`${base}${path}`, { method, body: method === 'DELETE' ? undefined : '{}' });
      assert.equal(r.status, 405, `${method} ${path}`);
      assert.equal(r.headers.get('allow'), 'GET, HEAD');
    }
  }
});

test('the configuration endpoint returns the SYNTHETIC EXAMPLE by default, without records', async () => {
  const r = await fetch(`${base}/api/configuration`);
  assert.equal(r.status, 200);
  const body = await r.json();
  assert.equal(body.mode, 'SYNTHETIC EXAMPLE');
  assert.equal(body.importReport, null);
  assert.equal(body.configuration.label, 'SYNTHETIC EXAMPLE');
  assert.equal(body.configuration.modules.length, 10);
  assert.equal(body.configuration.bindings.length, 26);
  assert.doesNotMatch(JSON.stringify(body), /"records"|"rowNumber"/);
});

test('the Excel path is refused unless both local inputs are set, and refused inside the repository', async () => {
  const seedPath = join(tmp, 'seed.json');
  writeFileSync(seedPath, JSON.stringify(exampleSeed()));
  await assert.rejects(loadConfiguration({ MAPPING_EXCEL_DEFAULT_PATH: join(tmp, 'x.xlsx') }), /must be set together/);
  await assert.rejects(loadConfiguration({ MAPPING_BINDING_SEED_PATH: seedPath }), /must be set together/);
  const inRepo = join(HERE, '..', '..', '..', 'no-such-local-input.xlsx');
  await assert.rejects(loadConfiguration({ MAPPING_EXCEL_DEFAULT_PATH: inRepo, MAPPING_BINDING_SEED_PATH: seedPath }), /outside the repository/);
});

test('a local workbook outside the repository loads as DEFAULT FROM EXCEL with a count-only report', async () => {
  const xlsxPath = join(tmp, 'local.xlsx');
  const seedPath = join(tmp, 'seed-local.json');
  writeFileSync(xlsxPath, buildWorkbook({ rows: exampleRows({ placeholder: true }) }));
  writeFileSync(seedPath, JSON.stringify(exampleSeed()));
  const loaded = await loadConfiguration({ MAPPING_EXCEL_DEFAULT_PATH: xlsxPath, MAPPING_BINDING_SEED_PATH: seedPath });
  assert.equal(loaded.mode, 'DEFAULT FROM EXCEL');
  assert.equal(loaded.importReport.counts.USED, 40);
  assert.equal(loaded.importReport.counts.SPARE, 1);
  assert.equal(loaded.configuration.label, 'DEFAULT FROM EXCEL');
  assert.equal(loaded.importReport.issueSummary.SIGNAL_IDENTITY_UNRESOLVED, 1);
  assert.doesNotMatch(JSON.stringify(loaded.importReport), /"records"|"rowNumber"/);
});

test('the browser assets expose only the Undo, Redo and Reset controls', () => {
  const buttons = [...html.matchAll(/<button\b[^>]*>([\s\S]*?)<\/button>/g)].map((m) => m[1].trim());
  assert.deepEqual(buttons, ['Undo', 'Redo', 'Reset']);
  for (const tag of ['input', 'select', 'textarea', 'form']) {
    assert.doesNotMatch(html, new RegExp(`<${tag}\\b`, 'i'), tag);
  }
  assert.doesNotMatch(appSrc, /addEventListener\('(submit|change|input)'/);
});

test('no browser asset names a control or a device action, and none uses a non-GET request', () => {
  const forbiddenControl = /\b(connect|poll|write|force|activate|command|test_hardware|production|read device)\b/i;
  const buttonLabels = [...html.matchAll(/<button\b[^>]*>([\s\S]*?)<\/button>/g)].map((m) => m[1]);
  for (const label of buttonLabels) assert.doesNotMatch(label, forbiddenControl, label);
  const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map((m) => m[1]);
  for (const id of ids) assert.doesNotMatch(id, /connect|poll|write|force|activate|command/i, id);
  assert.doesNotMatch(appSrc, /method\s*:/);
  assert.doesNotMatch(appSrc, /\b(POST|PUT|PATCH|DELETE)\b/);
  assert.doesNotMatch(appSrc, /WebSocket|EventSource|XMLHttpRequest|sendBeacon/);
  assert.doesNotMatch(appSrc + html + css, /https?:\/\//);
  assert.doesNotMatch(appSrc + html + css, /localhost|127\.0\.0\.1|0\.0\.0\.0/);
});

test('every element id that app.mjs looks up exists exactly once in index.html', () => {
  const ids = [...html.matchAll(/\bid="([^"]+)"/g)].map((m) => m[1]);
  assert.equal(new Set(ids).size, ids.length, 'duplicate ids');
  const used = new Set([...appSrc.matchAll(/\$\('([^']+)'\)/g)].map((m) => m[1]));
  assert.ok(used.size > 5);
  for (const id of used) assert.ok(ids.includes(id), `missing #${id}`);
});

test('the UI never renders values as HTML: app.mjs writes text, not markup, from data', () => {
  assert.doesNotMatch(appSrc, /innerHTML|outerHTML|insertAdjacentHTML|document\.write/);
});
