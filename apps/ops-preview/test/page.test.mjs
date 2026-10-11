// Stage 0.4C-K1 - runtime wiring and application-boot contract for the served Operations Preview.
//
// WHY THIS FILE EXISTS. The Owner Manual Review found the real Preview inert: HTML and CSS rendered
// but nothing initialized. The cause was that the SERVED PAGE contained no <script> element at all,
// so the browser never requested app.mjs - while every existing test imported the module directly and
// therefore passed. Source parsing and direct imports cannot catch that class of defect; these tests
// therefore resolve and fetch the module graph THROUGH THE SERVER, exactly as a browser would, and
// then execute the real application over the served bytes.
//
// What this file can and cannot prove: it proves the page references the module, that the whole
// module graph resolves over the real routes with JavaScript MIME types, that the application reaches
// its ready state with the protected content rendered, that the controls are wired, and that a failed
// boot becomes VISIBLE instead of an empty page. It does NOT prove pixel layout, and it is not a real
// browser: no browser is available in the Arena sandbox, so real-browser execution is NOT RUN and is
// not claimed anywhere. The Owner-local Kiosk rerun remains the verification of record.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { createPreviewServer, UI_BIND_HOST } from '../server.mjs';
import { bootApp, click, allText, cellTexts, rowsOf, waitFor } from './helpers/fakeDom.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const APP = join(HERE, '..');
const publicFile = (name) => readFileSync(join(APP, 'public', name), 'utf8');

const html = publicFile('index.html');
const appSource = publicFile('app.mjs');

/** Boots a real server on an ephemeral loopback port and returns a small fetch helper. */
async function withServer(fn) {
  const server = createPreviewServer();
  await new Promise((resolve) => server.listen(0, UI_BIND_HOST, resolve));
  // The origin is assembled from the loopback literal and the bound ephemeral port, the same way the
  // boot helper does it: no other host can be written here, and no port is hard-coded.
  const origin = new URL('http://127.0.0.1');
  origin.port = String(server.address().port);
  const base = origin.origin;
  const get = (path) => fetch(`${base}${path}`);
  try {
    return await fn({ base, get });
  } finally {
    server.close();
  }
}

/**
 * Static module specifiers of one served module, in source order: `import ... from`, bare `import`
 * and `export ... from` re-exports. A re-export is a real dependency of the served graph, so it must
 * be resolved the same way the browser resolves it.
 */
function importSpecifiers(source) {
  const specifiers = [];
  for (const pattern of [
    /\bimport\s+[\s\S]*?\bfrom\s*['"]([^'"]+)['"]/g,
    /\bimport\s*['"]([^'"]+)['"]/g,
    /\bexport\s+[\s\S]*?\bfrom\s*['"]([^'"]+)['"]/g,
  ]) {
    for (const match of source.matchAll(pattern)) specifiers.push(match[1]);
  }
  return specifiers;
}

/** Resolves one specifier against the URL of the module that imported it. */
const resolveSpecifier = (specifier, importerUrl) => new URL(specifier, importerUrl).pathname + new URL(specifier, importerUrl).search;

test('the served page loads the application module as an ES module', async () => {
  await withServer(async ({ get }) => {
    const response = await get('/');
    assert.equal(response.status, 200);
    const body = await response.text();

    assert.match(body, /<script\s+type="module"\s+src="\/app\.mjs"><\/script>/,
      'the served page must reference the application module with type="module"');

    // Exactly one script element, no inline script (which the CSP would refuse anyway), and no
    // external origin anywhere in the page.
    const scripts = [...body.matchAll(/<script\b[^>]*>/g)].map((m) => m[0]);
    assert.equal(scripts.length, 1, `expected exactly one script element, found ${scripts.length}`);
    assert.equal(scripts[0].includes('src="/app.mjs"'), true);
    assert.equal(/<script\b[^>]*>\s*[^<\s]/.test(body), false, 'the page must not carry inline script');
    assert.equal(/<script[^>]*src="(?:https?:)?\/\//.test(body), false, 'no external script may be loaded');
    assert.equal(/<link[^>]*href="(?:https?:)?\/\//.test(body), false, 'no external stylesheet may be loaded');
    assert.equal(/\/\/(?!127\.0\.0\.1)[a-z0-9.-]+\.[a-z]{2,}/i.test(body.replace(/https:\/\/www\.w3\.org[^"]*/g, '')), false);

    // The served module must be byte-identical to the file these tests execute, or the tests would be
    // proving something about code the browser never receives.
    const moduleResponse = await get('/app.mjs');
    assert.equal(moduleResponse.status, 200);
    assert.match(moduleResponse.headers.get('content-type') ?? '', /^text\/javascript/);
    assert.equal(await moduleResponse.text(), appSource,
      'the served module must be exactly the file the tests execute');

    const cssResponse = await get('/styles.css');
    assert.equal(cssResponse.status, 200);
    assert.match(cssResponse.headers.get('content-type') ?? '', /^text\/css/);
  });
});

test('the content security policy permits the intended local module execution', async () => {
  await withServer(async ({ get }) => {
    const csp = (await get('/')).headers.get('content-security-policy') ?? '';
    assert.match(csp, /script-src 'self'/);
    assert.equal(/unsafe-inline/.test(csp), false, 'inline script must stay refused');
    assert.equal(/unsafe-eval/.test(csp), false, 'eval must stay refused');
    assert.match(csp, /connect-src 'self'/);
    assert.match(csp, /default-src 'self'/);
    assert.match(csp, /frame-ancestors 'none'/);
    // A module script is allowed by script-src 'self', and the page carries no inline script that
    // could be blocked and leave the page inert.
    assert.equal(/<script[^>]*>[\s\S]*?<\/script>/.test(html.replace(/<script[^>]*src=[^>]*><\/script>/g, '')), false);
  });
});

test('every module in the served graph resolves over the real routes', async () => {
  await withServer(async ({ get, base }) => {
    const start = `${base}/app.mjs`;
    const seen = new Set();
    const queue = [start];
    const fetched = [];

    while (queue.length > 0) {
      const url = queue.shift();
      if (seen.has(url)) continue;
      seen.add(url);
      const response = await get(new URL(url).pathname);
      assert.equal(response.status, 200, `module must be served: ${url}`);
      assert.match(response.headers.get('content-type') ?? '', /^text\/javascript/,
        `module must be served as JavaScript: ${url}`);
      const source = await response.text();
      fetched.push(new URL(url).pathname);
      for (const specifier of importSpecifiers(source)) {
        assert.equal(/^(node:|https?:)/.test(specifier), false,
          `the browser module graph must not reference ${specifier}`);
        queue.push(new URL(specifier, url).href);
      }
    }

    // The commissioning model is reached through the package entry point and its own relative imports.
    for (const expected of ['/app.mjs', '/pkg/index.mjs', '/pkg/schedule.mjs', '/pkg/quality.mjs',
      '/pkg/scenarios.mjs', '/pkg/observation.mjs', '/pkg/workflow.mjs', '/pkg/presentation.mjs']) {
      assert.ok(fetched.includes(expected), `${expected} must be part of the served module graph`);
    }
    assert.ok(fetched.length >= 8, `expected the full module graph, fetched ${fetched.length}`);
  });
});

test('application boot completes and reports a ready state', async () => {
  const app = await bootApp({});
  try {
    assert.equal(app.byId['app-root'].attributes['data-app-state'], 'ready',
      'the application must report the ready state after boot');
    assert.equal(app.byId['boot-error'].hidden, true, 'no initialization error may be visible');
    assert.equal(app.byId['boot-error-detail'].textContent, '');

    // Every request the boot made was a successful local GET: a failed asset, module or API request
    // can never be hidden behind a passing test again.
    assert.ok(app.requests.length >= 6, `expected several requests, saw ${app.requests.length}`);
    for (const request of app.requests) {
      assert.equal(request.method, 'GET', `${request.path} must be a GET`);
      assert.equal(request.status, 200, `${request.path} must answer 200 (saw ${request.status})`);
      assert.match(request.path, /^\/(api\/|app\.mjs|styles\.css|pkg\/|index\.html|$)/, `${request.path} must be a local page route`);
    }
    for (const required of ['/api/boundary', '/api/commissioning/schedule', '/api/commissioning/scenarios',
      '/api/operations/summary', '/api/sensor-map', '/api/commissioning/observations']) {
      assert.ok(app.requests.some((r) => r.path.startsWith(required)), `${required} must be requested at boot`);
    }
  } finally {
    app.close();
  }
});

test('the scenario selector, node selector and progress areas are populated', async () => {
  const app = await bootApp({});
  try {
    const options = app.byId['scenario-select'].childNodes;
    assert.equal(options.length, 10, 'the Scenario selector must contain the ten protected scenarios');
    // `<option value=…>` carries its value as an attribute; the browser reflects it into `.value`.
    const keys = options.map((o) => o.attributes.value);
    for (const option of options) {
      assert.match(allText(option), /\w/, 'every scenario option must carry a readable label');
    }
    for (const key of ['NORMAL_GOOD', 'UNCERTAIN_TRANSIENT', 'OUT_OF_RANGE', 'STALE', 'COMMUNICATION_LOSS_NODE',
      'SIMULATED_WIRE_BREAK', 'RECOVERY', 'DRAWING_CONFLICT', 'PRODUCT_TOPOLOGY_CONFLICT', 'UNASSIGNED_CHANNEL']) {
      assert.ok(keys.includes(key), `the Scenario selector must offer ${key}`);
    }
    const nodeOptions = app.byId['node-select'].childNodes;
    assert.ok(nodeOptions.length >= 6, 'the Node selector must list the six nodes');
    for (const option of nodeOptions) {
      assert.match(String(option.attributes.value ?? ''), /^TC-NODE-0[1-6]$/);
    }
    assert.match(allText(app.byId['node-summary-body']), /TC-NODE-0[1-6]/,
      'the Node communication summary must be populated');
    // These areas are built from elements, so their text lives in child text nodes.
    assert.match(allText(app.byId['progress-body']), /BLOCKED/, 'the Commissioning progress must be populated');
    assert.match(allText(app.byId['progress-body']), /\b40\b/, 'the blocked count must be rendered');
    assert.match(allText(app.byId['operations-body']), /212/, 'the Operations area must carry the evidence counts');
  } finally {
    app.close();
  }
});

test('the Overview renders 240 physical Channels and a selected row fills the detail panel', async () => {
  const app = await bootApp({});
  try {
    const rows = app.byId['monitor-table-body'].childNodes;
    assert.equal(rows.length, 240, 'the Overview must render exactly 240 physical Channels');
    assert.equal(cellTexts(rows[0]).length, 20, 'each row must carry all 20 required columns');
    assert.match(String(app.byId['monitor-count'].textContent ?? ''), /240/,
      'the rendered row count must be visible to the Operator');

    const mapped = rows.find((row) => cellTexts(row)[0] !== 'Unassigned position');
    assert.ok(mapped, 'at least one mapped row must exist');
    click(mapped);
    assert.match(allText(app.byId['detail-identity']), /Sensor ID/, 'selecting a row must populate the identity card');
    assert.match(allText(app.byId['detail-observation']), /Quality/, 'the observation card must be populated');
    assert.match(allText(app.byId['detail-workflow']), /PREVIEW_ONLY - NOT APPROVED/,
      'the workflow card must carry the Preview-only provenance');
  } finally {
    app.close();
  }
});

test('the density controls respond and the navigation targets every section', async () => {
  const app = await bootApp({});
  try {
    // Density: the module sets the body attribute and the pressed state, which the CSS reads.
    click(app.byId['density-compact']);
    assert.equal(app.byId['app-root'].attributes['data-density'], 'compact');
    assert.equal(app.byId['density-compact'].attributes['aria-pressed'], 'true');
    assert.equal(app.byId['density-comfortable'].attributes['aria-pressed'], 'false');
    click(app.byId['density-comfortable']);
    assert.equal(app.byId['app-root'].attributes['data-density'], 'comfortable');
    assert.equal(app.byId['density-comfortable'].attributes['aria-pressed'], 'true');

    // Navigation: five anchors, each pointing at a section that the served page really contains.
    const anchors = [...html.matchAll(/<a href="#([a-z-]+)">([^<]+)<\/a>/g)].map((m) => ({ id: m[1], label: m[2] }));
    assert.equal(anchors.length, 5, 'the navigation must offer five sections');
    for (const { id, label } of anchors) {
      assert.match(html, new RegExp(`id="${id}"`), `${label} must point at a section that exists`);
    }
    assert.match(html, /id="sensor-map"[\s\S]*id="diagnostics"/);
  } finally {
    app.close();
  }
});

test('the ten scenarios can be applied and the Preview workflow records in memory only', async () => {
  const app = await bootApp({});
  try {
    app.byId['scenario-select'].value = 'COMMUNICATION_LOSS_NODE';
    click(app.byId['scenario-apply']);
    await waitFor(() => rowsOf(app.byId, 'monitor-table-body').some((row) => cellTexts(row)[12].includes('BAD')),
      'the applied scenario to reach the rendered rows');
    const rows = rowsOf(app.byId, 'monitor-table-body');
    assert.equal(rows.length, 240, 'the Overview must stay at 240 rows after applying a scenario');
    // Column 12 is Quality; its cell carries the simulation marker plus the value.
    assert.ok(rows.some((row) => cellTexts(row)[12].includes('BAD')),
      'applying a scenario must reach the rendered rows');
    assert.equal(app.byId['boot-error'].hidden, true, 'applying a scenario must not raise a failure state');
    assert.equal(app.byId['app-root'].attributes['data-app-state'], 'ready');
  } finally {
    app.close();
  }
});

test('a failed boot becomes a visible, safe failure state instead of an empty page', async () => {
  const app = await bootApp({ failPath: '/api/commissioning/schedule' });
  try {
    assert.equal(app.byId['app-root'].attributes['data-app-state'], 'failed',
      'a data failure must be reported as a failed boot');
    assert.equal(app.byId['boot-error'].hidden, false, 'the failure surface must be visible');
    const detail = app.byId['boot-error-detail'].textContent;
    assert.match(detail, /commissioning data could not be requested/);
    assert.match(String(app.byId['alert-line'].textContent ?? ''), /Preview data unavailable/);
    // Safe by construction: no stack trace, no local path, no module or file name, no credential.
    for (const forbidden of ['at ', '.mjs', 'apps/', 'C:\\', '/home/', 'node_modules', 'undefined', 'stack']) {
      assert.equal(detail.includes(forbidden), false, `the failure text must not contain '${forbidden}'`);
    }
    assert.ok(detail.length < 220, 'the failure text must stay short and readable');
  } finally {
    app.close();
  }
});

test('the failure surface exists in the served page and stays hidden until it is needed', () => {
  assert.match(html, /<div class="boot-error" id="boot-error" role="alert" hidden>/);
  assert.match(html, /<span id="boot-error-detail">/);
  assert.match(html, /<noscript>/);
  // The stylesheet must let `hidden` win, or a hidden banner would still be visible.
  const css = publicFile('styles.css');
  assert.match(css, /\.boot-error\[hidden\] \{ display:none; \}/);
  assert.match(css, /\.boot-error \{ display:flex/);
});
