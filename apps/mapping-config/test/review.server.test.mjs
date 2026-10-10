// Stage 0.4B-2 — the new browser-safe modules are served read-only; the synthetic test helper is not served.
import { test, before, after } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createUiServer, loadConfiguration } from '../server.mjs';

let server;
let base;
before(async () => {
  server = createUiServer({ loader: () => loadConfiguration({}) });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  base = origin.origin;
});
after(() => server.close());

test('U23 presentation, evidence and report modules are served by GET and HEAD, byte-identical to the package source', async () => {
  for (const name of ['presentation.mjs', 'processImageEvidence.mjs', 'evidenceReport.mjs']) {
    const get = await fetch(`${base}/pkg/${name}`);
    assert.equal(get.status, 200, name);
    assert.match(get.headers.get('content-type'), /javascript/);
    assert.equal(await get.text(), readFileSync(new URL(`../../../packages/mapping-config/src/${name}`, import.meta.url), 'utf8'));
    const head = await fetch(`${base}/pkg/${name}`, { method: 'HEAD' });
    assert.equal(head.status, 200, `HEAD ${name}`);
  }
});

test('U24 every write-style method is refused with 405 on the new routes, and the synthetic helper is not served', async () => {
  for (const method of ['POST', 'PUT', 'PATCH', 'DELETE']) {
    for (const path of ['/pkg/presentation.mjs', '/pkg/processImageEvidence.mjs', '/pkg/evidenceReport.mjs', '/api/configuration']) {
      const r = await fetch(`${base}${path}`, { method, body: method === 'DELETE' ? undefined : '{}' });
      assert.equal(r.status, 405, `${method} ${path}`);
      assert.equal(r.headers.get('allow'), 'GET, HEAD');
    }
  }
  for (const path of ['/pkg/syntheticEvidence.mjs', '/pkg/helpers/syntheticEvidence.mjs', '/pkg/../test/helpers/syntheticEvidence.mjs', '/test/helpers/syntheticEvidence.mjs', '/pkg/workbookImport.mjs']) {
    const r = await fetch(`${base}${path}`);
    assert.equal(r.status, 404, path);
  }
});

test('U25 the served evidence module carries no synthetic test rule and the configuration route exposes no evidence set', async () => {
  const body = await (await fetch(`${base}/pkg/processImageEvidence.mjs`)).text();
  assert.equal(/SYNTHETIC_TEST_EVIDENCE/.test(body), false);
  const cfg = JSON.stringify(await (await fetch(`${base}/api/configuration`)).json());
  assert.equal(/SYNTHETIC TEST RULE/.test(cfg), false);
  assert.equal(/evidenceSetId/.test(cfg), false);
});
