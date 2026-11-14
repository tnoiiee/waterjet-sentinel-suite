// Source and behaviour contracts for the WJSS Operator presentation.
//
// Run with:  node --test apps/ui/test/*.test.mjs
//
// This suite has no dependencies, no build step and no browser: it reads the shipped files and
// asserts the rules that keep the presentation a read-only, offline, dependency-free surface, then
// imports the two DOM-free modules to test their logic directly. A failure here means a product rule
// was broken, never that a rendering preference changed.

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { join, basename } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const HERE = fileURLToPath(import.meta.url);
const UI = join(HERE, '..', '..');
const PUBLIC = join(UI, 'public');

// The modules the contract rules run over. Four of them are new in the 1920x1080 overhaul: the one
// topology table, the shared canvas kit, the command bar and the tab panel set. Anything that must not
// appear anywhere in the presentation is checked against this list, so a new module is covered the
// moment it is shipped - which is also why the inventory test below keeps it honest.
const MODULES = ['app.mjs', 'feed.mjs', 'topology.mjs', 'chartKit.mjs', 'shellBar.mjs', 'tabs.mjs',
  'wallMap.mjs', 'sensorDetail.mjs', 'thermal.mjs', 'dispatchPanel.mjs', 'opsPanel.mjs'];

function read(name) {
  return readFileSync(join(PUBLIC, name), 'utf8');
}

function readAll() {
  const map = new Map();
  for (const entry of readdirSync(PUBLIC)) map.set(entry, read(entry));
  return map;
}

const html = read('index.html');
const css = read('global.css');
const sources = readAll();
const code = MODULES.map((name) => sources.get(name)).join('\n');
const every = [...sources.entries()].map(([name, text]) => [name, text]);

// ------------------------------------------------------------------- inventory

test('the workspace ships browser-native modules only: no manifest, no lockfile, no build tooling', () => {
  for (const forbidden of ['package.json', 'package-lock.json', 'tsconfig.json', 'vite.config.ts',
    'node_modules', 'dist']) {
    assert.equal(existsSync(join(UI, forbidden)), false, `apps/ui must not carry ${forbidden}`);
  }

  const shipped = readdirSync(PUBLIC).sort();
  assert.deepEqual(shipped, ['app.mjs', 'chartKit.mjs', 'dispatchPanel.mjs', 'feed.mjs', 'global.css',
    'index.html', 'opsPanel.mjs', 'sensorDetail.mjs', 'shellBar.mjs', 'tabs.mjs', 'thermal.mjs',
    'topology.mjs', 'wallMap.mjs']
    .sort(), 'the shipped file set is exactly the files the Runtime serves');

  for (const name of shipped) {
    const extension = name.slice(name.lastIndexOf('.'));
    assert.equal(['.mjs', '.css', '.html'].includes(extension), true,
      `${name} carries ${extension}, which would need a compiler, a bundler or a font pipeline`);
  }
});

test('every module resolves to a file that exists and every shipped file is reachable', () => {
  const referenced = new Set();
  for (const [name, text] of every) {
    for (const match of text.matchAll(/from '\.\/([a-zA-Z0-9._-]+)'/g)) referenced.add(match[1]);
    for (const match of text.matchAll(/from '\/ui\/[a-zA-Z0-9._-]+'/g)) {
      assert.fail(`${name} imports an absolute path: a browser-native module graph stays relative`);
    }
    for (const match of text.matchAll(/href="\/ui\/([a-zA-Z0-9._-]+)"/g)) referenced.add(match[1]);
    for (const match of text.matchAll(/src="\/ui\/([a-zA-Z0-9._-]+)"/g)) referenced.add(match[1]);
  }
  for (const name of referenced) {
    assert.equal(existsSync(join(PUBLIC, name)), true, `${name} is referenced but not shipped`);
  }
  for (const name of MODULES) {
    if (name === 'app.mjs') continue;
    assert.equal(referenced.has(name), true, `${name} is shipped but nothing loads it`);
  }
  assert.equal(referenced.has('index.html'), false, 'index.html is the document, not a module');
});

test('the document boots from exactly one module element and carries no inline script or style', () => {
  const scripts = [...html.matchAll(/<script\b[^>]*>/g)].map((m) => m[0]);
  assert.equal(scripts.length, 1, 'exactly one script element is allowed');
  assert.match(scripts[0], /^<script type="module" src="\/ui\/app\.mjs">$/, 'the only script is the module boot');
  assert.equal(/<script\b(?![^>]*\bsrc=)/.test(html), false, 'no inline script block');
  assert.equal(/<style\b/.test(html + code), false, 'no inline style element');
  assert.equal(/\sstyle="/.test(html), false, 'no inline style attribute in the document');
  assert.match(html, /<link rel="stylesheet" href="\/ui\/global\.css">/);
  assert.match(html, /<noscript>/);
  assert.match(html, /id="boot-error"[^>]*role="alert"[^>]*hidden/);
  assert.match(html, /<body[^>]*data-app-state="booting"/);
  assert.match(html, /aria-live="assertive"/, 'the critical dialog announces itself');
});

test('the stylesheet fetches nothing and declares no font: the UI is offline by construction', () => {
  assert.equal(/url\(/.test(css), false, 'no url() means no second request for any asset');
  assert.equal(/@import/.test(css), false, 'no @import means no transitive fetch');
  assert.equal(/@font-face/.test(css), false, 'no webfont: the presentation uses the system stack');
  assert.match(css, /font-family: var\(--font\)/);
  assert.match(css, /--font: "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;/);
  assert.match(css, /overflow-x: hidden;/, 'the page never scrolls sideways');
});

// ------------------------------------------------------- the read-only boundary

test('the presentation issues GET requests against same-origin relative Runtime paths, and nothing else', () => {
  const paths = [...sources.get('feed.mjs').matchAll(/^export const [A-Z_]+ = '(\/[a-z0-9/._-]+)';$/gm)]
    .map((m) => m[1]);
  assert.deepEqual(paths, ['/api/v1/snapshot', '/api/v1/deltas', '/api/v1/runtime',
    '/api/v1/dispatch-workspace', '/api/health'], 'the whole request surface of the UI');
  for (const path of paths) {
    assert.equal(/^\/\//.test(path), false, `${path} must never be a protocol-relative URL`);
  }

  assert.equal(sources.get('feed.mjs').includes("method: 'GET'"), true);
  assert.equal(/method:\s*'(?!GET)/.test(code), false, 'no other method exists');
  for (const verb of ['POST', 'PUT', 'PATCH', 'DELETE']) {
    assert.equal(new RegExp(`['"]${verb}['"]`).test(code), false, `${verb} appears in the sources`);
  }
  for (const forbidden of ['XMLHttpRequest', 'WebSocket', 'EventSource', 'sendBeacon', 'navigator.clipboard.write']) {
    assert.equal(code.includes(forbidden), false, `${forbidden} is not part of a read-only surface`);
  }

  // Network reach is confined to one module, so a reviewer reads one file to audit the surface.
  for (const [name, text] of every) {
    if (name === 'feed.mjs') continue;
    assert.equal(/\bfetch\s*\(/.test(text), false, `${name} must not reach the network itself`);
  }
  assert.equal((code.match(/localStorage\.setItem/g) || []).length, 1,
    'storage is written exactly once: the density preference, and nothing else');
});

test('no module reads an address, a host or a port from anywhere', () => {
  for (const [name, text] of every) {
    assert.equal(/location\.(host|hostname|port)\b/.test(text), false, `${name} reads a network identity`);
    assert.equal(/process\.env/.test(text), false, `${name} reads the process environment`);
    assert.equal(/import\.meta\.env/.test(text), false, `${name} reads a build-time environment`);
    assert.equal(/\b(https?|ftp|ws|wss):\/\//.test(text), false, `${name} names an absolute URL`);
    assert.equal(/\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b/.test(text), false,
      `${name} carries an address literal: the UI never names a host`);
    assert.equal(/spikes\//.test(text), false, `${name} references reference material`);
  }
  // location.origin is read for display only, which is a fact about the frame, not an input.
  assert.match(sources.get('app.mjs'), /window\.location\.origin/);
});

test('the sources are ASCII, markup-safe and never write raw HTML', () => {
  for (const [name, text] of every) {
    assert.equal(/[^\x00-\x7F]/.test(text), false, `${name} carries a non-ASCII byte`);
    assert.equal(/\.innerHTML\s*=/.test(text), false, `${name} writes raw HTML`);
    assert.equal(/outerHTML|insertAdjacentHTML|document\.write/.test(text), false, `${name} injects markup`);
    assert.equal(/createElementNS|xmlns|<svg/.test(text), false,
      `${name} would introduce a namespace literal: the trend is drawn on a canvas instead`);
    assert.equal(/eval\(|new Function\(/.test(text), false, `${name} evaluates code`);
  }
});

// ------------------------------------------------------------------ the topology

test('the wall map carries the approved geometry: 108 Sensors, rows 1-3 UPPER, rows 4-6 LOWER', async () => {
  const map = await import(pathToFileURL(join(PUBLIC, 'wallMap.mjs')).href);
  assert.deepEqual([...map.WALL_ORDER], ['LEFT', 'REAR', 'RIGHT', 'FRONT']);
  assert.equal(map.WALL_ROWS, 6);
  assert.deepEqual([...map.UPPER_ROWS], [1, 2, 3]);
  assert.deepEqual([...map.LOWER_ROWS], [4, 5, 6]);
  assert.equal(map.EXPECTED_TOTAL, 108);
  assert.deepEqual({ ...map.EXPECTED_PER_WALL }, { LEFT: 24, REAR: 30, RIGHT: 24, FRONT: 30 });
  assert.deepEqual({ ...map.WALL_COLUMNS }, { LEFT: 4, REAR: 5, RIGHT: 4, FRONT: 5 });

  assert.equal(map.tierOfRow(1), 'UPPER');
  assert.equal(map.tierOfRow(3), 'UPPER');
  assert.equal(map.tierOfRow(4), 'LOWER');
  assert.equal(map.tierOfRow(6), 'LOWER');
  assert.equal(map.tierOfRow(0), 'UNPLACED');
  assert.equal(map.tierOfRow(7), 'UNPLACED');

  // Comment markers and hard wraps are removed before prose is compared, so re-wrapping a sentence
  // can never break a gate and a gate can never pin a line width.
  const source = sources.get('wallMap.mjs').replace(/\/\*|\*\/|\/\/\/?/g, ' ').replace(/\s+/g, ' ');
  assert.match(source, /I7 and I16 are normal Product Sensors/);
  assert.match(source, /never a gap, never an anchor and never a Water Jet identity/);
  assert.match(source, /logical rows 1 to 3 are the UPPER tier and logical rows 4 to 6 are the LOWER tier/);
  assert.equal(/\bNON_SENSOR_GAP\b/.test(source), false, 'no superseded gap position may return');
});

test('buildModel accepts the published matrix and refuses a topology it cannot prove', async () => {
  const map = await import(pathToFileURL(join(PUBLIC, 'wallMap.mjs')).href);

  const wallSlots = (wall, count, from) => Array.from({ length: count }, (unused, index) => ({
    slotId: `SLOT-${wall}-${from + index}`,
    positionKind: 'SENSOR',
    wall,
    logicalRow: Math.floor((from + index) / 18) + 1,
    logicalColumn: ((from + index) % 18) + 1,
    sensorId: `${wall}${from + index}`,
  }));
  const slots = [
    ...wallSlots('LEFT', 24, 0), ...wallSlots('REAR', 30, 0),
    ...wallSlots('RIGHT', 24, 0), ...wallSlots('FRONT', 30, 0),
  ];
  const sensors = slots.map((slot) => ({ sensorId: slot.sensorId, wall: slot.wall }));
  const ok = map.buildModel({ wallMap: slots, sensors });
  assert.equal(ok.ok, true, JSON.stringify(ok.error));
  assert.equal(ok.sensorCount, 108);
  assert.equal(ok.ordered.length, 108);
  assert.equal(ok.ordered[0], slots[0].sensorId, 'the order is wall, then logical row, then column');

  const short = map.buildModel({ wallMap: slots.slice(1), sensors: sensors.slice(1) });
  assert.equal(short.ok, false);
  assert.match(String(short.error), /protected total is 108/);

  const wallWrong = map.buildModel({
    wallMap: [ ...wallSlots('LEFT', 23, 0), ...wallSlots('REAR', 31, 0),
      ...wallSlots('RIGHT', 24, 0), ...wallSlots('FRONT', 30, 0) ]
      .map((slot, index, all) => ({ ...slot, sensorId: all[index].sensorId })),
    sensors: sensors.map((sensor, index) => ({ ...sensor })),
  });
  assert.equal(wallWrong.ok, false, 'a wall with the wrong count refuses the map');

  const duplicate = map.buildModel({
    wallMap: slots.map((slot, index) => (index === 1 ? { ...slot, sensorId: slots[0].sensorId } : slot)),
    sensors,
  });
  assert.equal(duplicate.ok, false, 'one identity on two slots is a contradiction');

  assert.equal(map.buildModel({}).ok, false, 'no matrix at all is a refusal, not an empty wall');
});

test('the step navigation walks only the published Sensor identities', async () => {
  const map = await import(pathToFileURL(join(PUBLIC, 'wallMap.mjs')).href);
  const ordered = ['A1', 'A2', 'A3'];
  assert.equal(map.stepSelection(ordered, 'A1', 1), 'A2');
  assert.equal(map.stepSelection(ordered, 'A3', 1), 'A1', 'the walk wraps inside the published list');
  assert.equal(map.stepSelection(ordered, 'A1', -1), 'A3');
  assert.equal(map.stepSelection(ordered, 'MISSING', 1), 'A1', 'an unknown selection starts at the head');
  assert.equal(map.stepSelection([], 'A1', 1), null, 'nothing published means nothing to select');
  assert.equal(map.visualOf(null).key, 'unknown');
  assert.equal(map.visualOf({ classification: 'DIRTY', quality: 'GOOD' }).key, 'dirty');
  assert.equal(map.visualOf({ classification: 'CLEANER', quality: 'GOOD' }).key, 'clean');
  assert.equal(map.visualOf({ classification: 'DIRTY', quality: 'STALE' }).key, 'stale',
    'a stale reading is reported as stale on top of its classification');
  assert.equal(map.visualOf({ classification: 'NOT_CLASSIFIED', quality: 'GOOD' }).key, 'watch');
  assert.equal(map.alarmOf({ alarmState: 'ACTIVE_UNACK', alarmSeverity: 'HIGH' }), 'critical');
  assert.equal(map.alarmOf({ alarmState: 'ACTIVE_ACK', alarmSeverity: 'LOW' }), 'alarm');
  assert.equal(map.alarmOf({ alarmState: 'NONE' }), 'none');
  assert.equal(map.queueOf({ queueState: 'ACTIVE' }), 'next');
  assert.equal(map.queueOf({ queueState: 'QUEUED' }), 'queued');
  assert.equal(map.queueOf({ queueState: 'NONE' }), 'none');
});

// ------------------------------------------------------------- the feed behaviour

test('the cadence and the bounds are the accepted Inspector values, stated as constants', () => {
  assert.match(sources.get('feed.mjs'), /export const POLL_INTERVAL_MS = 1000;/);
  assert.match(sources.get('feed.mjs'), /export const MAX_DELTA_ROWS = 60;/);
  assert.match(sources.get('feed.mjs'), /export const OFFLINE_AFTER_FAILURES = 3;/);
  assert.match(sources.get('feed.mjs'), /export const HEALTH_EVERY_TICKS = 10;/);
  assert.match(sources.get('feed.mjs'), /cache: 'no-store'/, 'a poll must never be served from cache');
});

test('a Snapshot is adopted whole or refused: the shape rules are executable, not prose', async () => {
  const feed = await import(pathToFileURL(join(PUBLIC, 'feed.mjs')).href);
  assert.equal(feed.validateSnapshot(null) !== null, true);
  assert.match(String(feed.validateSnapshot({})), /discriminator/);
  assert.match(String(feed.validateSnapshot({ kind: 'snapshot' })), /revision is not an integer/);
  assert.match(String(feed.validateSnapshot({ kind: 'snapshot', revision: 1 })), /Sensor set is absent/);
  assert.equal(feed.validateSnapshot({ kind: 'snapshot', revision: 3, sensors: [], wallMap: [] }), null);
  assert.match(String(feed.validateSnapshot({ kind: 'snapshot', revision: 1.5, sensors: [], wallMap: [] })),
    /not an integer/);

  assert.equal(feed.countFeedGaps(null), 0);
  assert.equal(feed.countFeedGaps({ items: [{ appliesCleanly: true }] }), 0);
  assert.equal(feed.countFeedGaps({ items: [{ hasRevisionGap: true }, { hasRevisionGap: false }, { hasRevisionGap: true }] }), 2);
});

test('reconnect is a fresh full Snapshot and a revision discontinuity is counted, never smoothed over', () => {
  const feed = sources.get('feed.mjs');
  assert.match(feed, /next <= previous/);
  assert.match(feed, /state\.resyncs \+= 1/);
  assert.match(feed, /const previous = state\.revision;/);
  assert.match(feed, /revision: null/);
  assert.match(feed, /state\.snapshot = snapshotResult\.document;/, 'the document replaces the document');
  assert.equal(/\bapplyDelta|\bmergeDelta|\.patch\(/.test(feed + sources.get('app.mjs')), false,
    'no partial Delta is applied: the host exposes feed metadata only, so the Snapshot is the state');
  assert.match(sources.get('app.mjs'), /renderWallMap\(mapHost, model, snapshot, view\.selectedId, select\)/);
});

test('the missing paired difference is stated, not computed', async () => {
  const thermal = await import(pathToFileURL(join(PUBLIC, 'thermal.mjs')).href);
  assert.equal(thermal.DFR_UNAVAILABLE, 'DFR_NOT_PUBLISHED_BY_RUNTIME');
  assert.equal(thermal.dfrStatement(null, null).present, false);
  assert.equal(thermal.dfrStatement({ sensors: [] }, 'I7').value, thermal.DFR_UNAVAILABLE);
  const sensorless = thermal.dfrStatement({ sensors: [{ sensorId: 'I7' }] }, 'I7');
  assert.equal(sensorless.value, thermal.DFR_UNAVAILABLE);
  const withChannels = thermal.dfrStatement({
    sensors: [{ sensorId: 'I7', tcFrontChannel: 'TC-F-01', tcRearChannel: 'TC-R-01' }],
  }, 'I7');
  assert.equal(withChannels.present, false, 'two channel identities are not two temperatures');
  assert.match(withChannels.value, /TC-F-01/);
  assert.match(withChannels.value, /TC-R-01/);

  const source = sources.get('thermal.mjs');
  for (const forbidden of ['FrontTemperature', 'RearTemperature', 'frontTemperature', 'rearTemperature']) {
    assert.equal(source.includes(forbidden), false, `${forbidden} is not a contract field`);
  }
  assert.equal(/temperature\s*-/.test(source), false, 'no subtraction of a temperature happens here');
  assert.match(source, /NOT_VERIFIED/);
});

test('the trend renders the published window and draws a null series value as a gap', () => {
  const trend = sources.get('sensorDetail.mjs');
  assert.match(trend, /getContext\('2d'\)/);
  assert.match(trend, /canvas\.getContext/);
  assert.match(trend, /drawn = false;/, 'a missing value ends the drawn segment');
  assert.match(trend, /never interpolated/);
  assert.match(trend, /String\(points\.length\) \+ ' of ' \+ String\(capacity\)/);
  assert.equal(/setLineDash\(\[4, 3\]\)/.test(trend), true, 'the setpoint is visibly a setpoint');
});

test('the panels name the contract fields they read, so a renamed field fails here first', () => {
  const dispatch = sources.get('dispatchPanel.mjs');
  const ops = sources.get('opsPanel.mjs');
  const detail = sources.get('sensorDetail.mjs');
  const thermal = sources.get('thermal.mjs');

  for (const field of ['workspace.kind', 'queue.entries', 'queue.capacity', 'queue.totalQueued',
    'queue.lastDispatch', 'countdown.lifecycle', 'countdown.remainingSeconds', 'countdown.frozen',
    'countdown.dispatchAdmissible', 'countdown.decision', 'timeline.steps', 'preview.appliedDelaySeconds',
    'preview.zoneFactor', 'preview.mappedDelaySeconds', 'preview.clampedRhTemperature',
    'preview.boilerCondition', 'decision.appliedDelaySeconds', 'decision.countdownGeneration',
    'decision.nextSensorOrderWall', 'decision.effectiveWaterJetProvenance', 'decision.rhInput']) {
    assert.equal(dispatch.includes(field), true, `${field} is not read by the dispatch panel`);
  }
  assert.match(dispatch, /WORKSPACE REFUSED/, 'a foreign discriminator is refused on screen');

  for (const field of ['job.targetSensorId', 'job.phaseLabel', 'job.phaseProgress',
    'job.pumpOutletPressureBar', 'job.pumpPressureQuality', 'job.pumpPressureSourceId',
    'job.valveOutletPressureBar', 'job.valveOpenCommand', 'pump.readyBandLow', 'pump.readyBandHigh',
    'pump.pressure', 'pump.setpoint', 'pump.ready', 'alarms.activeUnack', 'alarms.items',
    'item.severity', 'device.state', 'device.consecutiveTimeouts', 'sequence.equipmentFaults']) {
    assert.equal(ops.includes(field), true, `${field} is not read by the ops panel`);
  }

  for (const field of ['sensor.sensorId', 'sensor.deviceId', 'sensor.logicalColumn', 'sensor.scanOrder',
    'sensor.assignedWaterJetId', 'sensor.assignedIsolationValveId', 'sensor.tcFrontChannel',
    'sensor.tcRearChannel', 'sensor.dirtyScore', 'sensor.lastValidatedScore', 'sensor.classification',
    'sensor.classificationBasis', 'sensor.quality', 'sensor.qualityReason', 'sensor.sourceTimestamp',
    'sensor.queueState', 'sensor.alarmState', 'sensor.alarmSeverity', 'trend.points', 'trend.capacity',
    'trend.seriesNames', 'point.setpoint', 'point.series']) {
    assert.equal(detail.includes(field), true, `${field} is not read by the detail panel`);
  }

  for (const field of ['tc.acquisitionState', 'tc.copiedWordCount', 'tc.decodeStatus',
    'tc.mappingStatus', 'tc.diagnosticState', 'tc.notVerified', 'tc.physicalEvidence', 'tc.quarantined']) {
    assert.equal(thermal.includes(field), true, `${field} is not read by the thermal panel`);
  }
});

test('no control affordance exists: the screen observes and never actuates', () => {
  for (const [name, text] of every) {
    assert.equal(/<form|type="submit"|\.submit\(\)|\.click\(\)|dispatchEvent\(/.test(text), false,
      `${name} carries a control affordance`);
  }
  for (const match of code.matchAll(/addEventListener\('([a-zA-Z]+)'/g)) {
    assert.equal(['click', 'keydown', 'DOMContentLoaded', 'beforeunload'].includes(match[1]), true,
      `${match[1]} is not a presentation-only listener`);
  }
  assert.match(html, /READ-ONLY OBSERVATION - NO DEVICE WRITE - NO COMMAND SURFACE/);
  assert.match(html, /no control affordance exists on this screen/);
  assert.equal(/addEventListener\('wheel'|addEventListener\('scroll'/.test(code), false);
});

test('the lifecycle is published on the body dataset and a refused topology is not hidden', () => {
  const app = sources.get('app.mjs');
  assert.match(app, /document\.body\.dataset\.appState = state/);
  for (const state of ['booting', 'ready', 'failed']) {
    assert.equal(app.includes(`'${state}'`), true, `the ${state} lifecycle state is unreachable`);
  }
  assert.match(sources.get('wallMap.mjs'), /WALL MAP REFUSED/);
  assert.match(sources.get('app.mjs'), /HOST UNAVAILABLE - RECONNECTING/);
  assert.match(sources.get('app.mjs'), /FRAME IS NOT CURRENT - RETRYING/);
});

// ------------------------------------------------------------------ boot safety

test('every import specifier names a served file exactly, case-sensitively', () => {
  const served = new Set(readdirSync(PUBLIC));

  for (const [name, text] of every) {
    if (!name.endsWith('.mjs')) continue;
    // Only a real module specifier counts. A bare "from" inside a string is prose, and a test that
    // matched it would be matching the file rather than its imports.
    for (const match of text.matchAll(/^[ \t]*(?:import|export)[ \t]+(?:[^'\n]*?[ \t]from[ \t]+)?'(?<spec>[^']+)'[ \t]*$/gm)) {
      const spec = match.groups.spec;
      assert.equal(spec.startsWith('./'), true,
        `${name} imports '${spec}': a root-absolute specifier breaks resolution under node --test and a `
        + 'parent path would leave the served directory');
      const file = spec.slice(2);
      assert.equal(basename(file), file, `${name} imports '${spec}', which leaves /ui/`);
      assert.equal(served.has(file), true, `${name} imports '${file}', which the host does not serve`);
      assert.equal(file.endsWith('.mjs'), true,
        `${name} imports '${file}': a module the browser must compile is not a browser-native file`);
    }
  }

  // The document reaches the same set through its two asset elements.
  for (const match of html.matchAll(/(?:src|href)="\/ui\/(?<file>[^"]+)"/g)) {
    assert.equal(served.has(match.groups.file), true, `the document requests ${match.groups.file}, which is not shipped`);
  }
  assert.equal(html.includes('src="/ui/app.mjs"'), true, 'the document must boot the entry module the host serves');
  assert.equal(html.includes('href="/ui/global.css"'), true, 'the document must load the stylesheet the host serves');
});

test('no module looks up an element the document does not carry, so first paint cannot throw', () => {
  const htmlIds = new Set([...html.matchAll(/\bid="(?<id>[^"]+)"/g)].map((m) => m.groups.id));
  assert.equal(htmlIds.size, [...html.matchAll(/\bid="[^"]*"/g)].length,
    'a duplicated id would make one of the two unreachable by getElementById');

  for (const name of MODULES) {
    const text = read(name).replace(/\/\*[\s\S]*?\*\//g, '').replace(/^\/\/.*$/gm, '');
    const created = new Set([...text.matchAll(/\.id\s*=\s*['"](?<id>[a-z0-9_-]+)['"]/g)].map((m) => m.groups.id));
    const looked = new Set();
    for (const re of [/getElementById\(['"](?<id>[a-z0-9_-]+)['"]\)/g, /querySelector(?:All)?\(['"]#(?<id>[a-z0-9_-]+)['"]\)/g,
      /\bhost\(['"](?<id>[a-z0-9_-]+)['"]\)/g, /\bset\(['"](?<id>[a-z0-9_-]+)['"]/g]) {
      for (const m of text.matchAll(re)) looked.add(m.groups.id);
    }
    for (const id of looked) {
      assert.equal(htmlIds.has(id) || created.has(id), true,
        `${name} looks up '${id}', which no shipped document or module provides: the lookup returns null and the paint throws`);
    }
  }
});

// ------------------------------------------------------- contract wire shape

test('every payload property the modules read is a published contract property', () => {
  // The serialiser in use is ContractJson.Options: camelCase properties, UPPER_SNAKE enums. A module
  // that reads a name the contract does not publish does not crash - it quietly prints a fallback,
  // which is worse on an operator screen. So the property graph, not prose, is the check here.
  // HERE is a file path, so the first '..' leaves the test directory and the fourth reaches the root.
  const root = join(HERE, '..', '..', '..', '..');
  const csFiles = [];
  for (const dir of [join(root, 'packages', 'contracts'), join(root, 'apps', 'runtime')]) {
    for (const entry of readdirSync(dir)) if (entry.endsWith('.cs')) csFiles.push(join(dir, entry));
  }
  assert.equal(csFiles.length > 20, true, 'the contract tree must be readable for this check to mean anything');

  const types = new Map();
  for (const file of csFiles) {
    const text = readFileSync(file, 'utf8');
    for (const match of text.matchAll(/public sealed (?:record|class)\s+(?<name>\w+)[^{}]*\{/g)) {
      let index = match.index + match[0].length;
      let depth = 1;
      while (index < text.length && depth > 0) {
        if (text[index] === '{') depth += 1;
        else if (text[index] === '}') depth -= 1;
        index += 1;
      }
      const body = text.slice(match[0].length + match.index, index - 1);
      const props = types.get(match.groups.name) || {};
      for (const prop of body.matchAll(/public\s+(?:required\s+|virtual\s+|override\s+|static\s+|const\s+)?(?<type>[A-Za-z_][\w<>,\[\]?.\s]*?)\s+(?<name>[A-Z]\w*)\s*(?:\{\s*get;|=>)/g)) {
        if (prop.groups.type.includes('(')) continue;
        const key = prop.groups.name[0].toLowerCase() + prop.groups.name.slice(1);
        props[key] = prop.groups.type.trim();
      }
      types.set(match.groups.name, props);
    }
    for (const enumMatch of text.matchAll(/public enum\s+(?<name>\w+)\s*\{/g)) {
      if (!types.has(enumMatch.groups.name)) types.set(enumMatch.groups.name, null);
    }
  }

  const element = (declared) => {
    const clean = declared.replace(/\s+/g, ' ').trim();
    const list = clean.match(/^(?:IReadOnlyList|IReadOnlyCollection|IList|List|IEnumerable)<\s*(?<el>[\w.?]+)\s*>$/);
    if (list) return { list: true, el: list.groups.el.replace(/\?$/, '') };
    const nullable = clean.match(/^(?<el>[\w.]+)\?$/);
    if (nullable) return { list: false, el: nullable.groups.el };
    return { list: false, el: clean };
  };

  const ARRAY_METHODS = new Set(['map', 'filter', 'forEach', 'find', 'findIndex', 'some', 'every', 'slice',
    'join', 'length', 'indexOf', 'includes', 'reduce', 'flatMap', 'at', 'concat', 'sort', 'reverse']);
  const roots = new Map(Object.entries({
    snapshot: 'OperationalSnapshot', status: 'RuntimeStatus', health: 'KioskHealthPayload',
    workspace: 'DispatchWorkspace',
  }));

  const resolve = (expression, table) => {
    const parts = expression.split('.').filter(Boolean);
    let type = table.get(parts[0]);
    if (!type) return null;
    let isList = false;
    for (let index = 1; index < parts.length; index += 1) {
      if (isList && ARRAY_METHODS.has(parts[index])) return { leaf: true };
      const outer = element(type);
      const owner = types.get(outer.list ? outer.el : type);
      if (!owner) return { leaf: true };
      if (!(parts[index] in owner)) return { missing: parts[index], type: outer.list ? outer.el : type, path: parts.slice(0, index + 1).join('.') };
      const next = element(owner[parts[index]]);
      isList = next.list;
      type = next.el;
    }
    return { type, list: isList, path: parts.join('.') };
  };

  for (const name of MODULES) {
    // Comment first, then literals: an apostrophe in prose ("the host's label") would otherwise open a
    // string that swallows the code that follows it, and the check would quietly see less than it says.
    const text = read(name)
      .replace(/\/\*[\s\S]*?\*\//g, '')
      .replace(/\/\/.*$/gm, '')
      .replace(/'(?:[^'\\]|\\.)*'|"(?:[^"\\]|\\.)*"|`(?:[^`\\]|\\.)*`/g, '""');
    const table = new Map(roots);

    // A panel helper receives a sub-object of the Snapshot, and most field reads live inside it, so a
    // check limited to top-level roots would cover almost nothing. Each local function's parameters
    // therefore take the type of the chain at its first call site - nothing more is claimed than what
    // the caller actually passes.
    const inferParameters = () => {
      for (const fn of text.matchAll(/function\s+(?<name>[a-zA-Z_$][\w$]*)\s*\((?<params>[^)]*)\)/g)) {
        const params = fn.groups.params.split(',').map((part) => part.trim().split(/[\s=]/)[0]).filter(Boolean);
        if (!params.length) continue;
        const calls = new RegExp('\\b' + fn.groups.name + '\\((?<args>[^()]*(?:\\([^()]*\\)[^()]*)*)\\)', 'g');
        for (const call of text.matchAll(calls)) {
          const args = call.groups.args.split(',');
          for (let index = 0; index < params.length; index += 1) {
            if (table.has(params[index])) continue;
            const chain = (args[index] || '').match(/[a-z_$][\w$]*(?:\.[a-zA-Z_]\w*){0,6}/);
            if (!chain) continue;
            const result = resolve(chain[0], table);
            if (result && result.type && !result.list && types.has(result.type)) table.set(params[index], result.type);
          }
          break;
        }
      }
    };

    for (let pass = 0; pass < 4; pass += 1) {
      inferParameters();
      for (const match of text.matchAll(/(?:const|let)\s+(?<name>[a-z_$][\w$]*)\s*=\s*(?<init>[^;]+)/g)) {
        const callIndex = match.groups.init.indexOf('(');
        const chainIndex = match.groups.init.search(/[a-z_$][\w$]*\.[a-zA-Z_]\w*/);
        if (callIndex >= 0 && callIndex < chainIndex) continue;
        const chains = [...match.groups.init.matchAll(/[a-z_$][\w$]*(?:\.[a-zA-Z_]\w*){1,6}/g)].map((c) => c[0]);
        chains.sort((a, b) => b.split('.').length - a.split('.').length);
        if (!chains.length) continue;
        const result = resolve(chains[0], table);
        if (result && result.missing) {
          assert.fail(`${name}: ${result.path} reads '${result.missing}', which ${result.type} does not publish`);
        }
        if (result && result.type && !result.list && types.has(result.type) && !table.has(match.groups.name)) {
          table.set(match.groups.name, result.type);
        }
      }
      for (const match of text.matchAll(/[a-z_$][\w$]*(?:\.[a-zA-Z_]\w*){1,6}/g)) {
        const result = resolve(match[0], table);
        if (result && result.missing && !ARRAY_METHODS.has(result.missing)) {
          assert.fail(`${name}: ${result.path} reads '${result.missing}', which ${result.type} does not publish`);
        }
      }
    }
  }
});

test('the Safe Return row reads the published leg fields and no other shape', () => {
  const ops = read('opsPanel.mjs');
  const fields = ['safeReturn.step', 'safeReturn.trigger', 'safeReturn.pendingOutcome',
    'safeReturn.startedAt', 'safeReturn.valve', 'safeReturn.axis', 'safeReturn.failure',
    'valve.valveId', 'valve.command', 'valve.feedback', 'valve.resolution',
    'axis.command', 'axis.standby', 'failure.reason', 'failure.at'];
  for (const field of fields) {
    const pattern = new RegExp(field.replace('.', '\\.') + '\\b(?!\\w)');
    assert.equal(pattern.test(ops), true, `the Safe Return row no longer reads exactly ${field}`);
  }

  // SafeReturnState has no step-list alias: reading a name it does not publish would fall through to
  // the fallback string and the row would look answered while it was not.
  for (const forbidden of ['safeReturn.state', 'safeReturn.Phase', 'safeReturn.status', 'safeReturn.events[0]']) {
    assert.equal(ops.includes(forbidden), false, `the Safe Return row reads ${forbidden}, which is not published`);
  }
  assert.match(ops, /no Mandatory Safe Return is running for this job/);
});

// ------------------------------------------- the 1920x1080 overhaul: one table, six tabs, no invention

const ROOT = join(HERE, '..', '..', '..', '..');
const csharp = (relative) => readFileSync(join(ROOT, relative), 'utf8');

test('the single canonical topology module agrees with the two .NET catalogs it mirrors', async () => {
  const catalog = csharp('packages/domain/WaterJetTopologyCatalog.cs');
  const motion = csharp('packages/application/Motion/MotionTopologyCatalog.cs');
  const topology = await import(pathToFileURL(join(PUBLIC, 'topology.mjs')).href);

  // The eight Water Jet rows, read out of the catalog's own object initializers rather than out of prose.
  const pattern = /WaterJetId = "(?<wj>WJ\d)",\s*InstalledWall = Wall\.(?<installedWall>\w+),\s*InstalledRegion = Region\.(?<installedRegion>\w+),\s*PlacementKind = WaterJetPlacementKind\.(?<placementKind>\w+),\s*PlacementAnchors = \[(?<anchors>[^\]]*)\],\s*TargetWall = Wall\.(?<targetWall>\w+),\s*TargetRegion = Region\.(?<targetRegion>\w+),\s*DedicatedIsolationValveId = "(?<iv>IV\d)"/g;
  const rows = [...catalog.matchAll(pattern)].map((match) => match.groups);
  assert.equal(rows.length, 8, 'eight Water Jet rows are read from the catalog, so this check cannot pass vacuously');

  for (const row of rows) {
    const mine = topology.WATER_JETS.find((jet) => jet.wj === row.wj);
    assert.ok(mine, `${row.wj} is missing from the presentation table`);
    assert.equal(mine.installedWall, row.installedWall, `${row.wj} installed wall`);
    assert.equal(mine.installedTier, row.installedRegion, `${row.wj} installed region`);
    assert.equal(mine.targetWall, row.targetWall, `${row.wj} target wall`);
    assert.equal(mine.targetTier, row.targetRegion, `${row.wj} target region`);
    assert.equal(mine.iv, row.iv, `${row.wj} dedicated valve`);
    assert.equal(mine.placementKind, row.placementKind, `${row.wj} placement kind`);
    assert.deepEqual(mine.placementAnchors, row.anchors.trim() === '' ? [] : row.anchors.split(','),
      `${row.wj} carries an anchor the catalog does not`);
  }
  assert.equal(catalog.includes(topology.UNRESOLVED_PLACEMENT_WORDING), true,
    'the placement wording on screen is the catalog sentence, not a paraphrase of it');

  // Tier split, read from the catalog's own RegionForLogicalRow rather than restated: if the ruling
  // moves in C#, the map and this check both stop agreeing and the failure names the row.
  assert.match(catalog, /1 or 2 or 3 => Region\.UPPER,\s*\n\s*4 or 5 or 6 => Region\.LOWER/,
    'the catalog no longer states the 1-3 UPPER / 4-6 LOWER ruling the map depends on');
  assert.match(catalog, /logical row must be 1-6/,
    'a row outside 1-6 is a refusal in the catalog, and the map reports it as UNPLACED rather than guessing');
  assert.deepEqual([...topology.UPPER_ROWS], [1, 2, 3]);
  assert.deepEqual([...topology.LOWER_ROWS], [4, 5, 6]);

  // Motion: axis roles and brake allocation, read from the Axis(...) lines of the motion catalog.
  const axes = [...motion.matchAll(/Axis\("(?<controller>MC-WSB\d)", MotionVocabulary\.Axis(?<letter>[A-D]), MotionVocabulary\.AxisRole(?<role>\w+),[^,]*,\s*MotionVocabulary\.BrakeOutput(?<brake>\d)\)/g)]
    .map((match) => match.groups);
  assert.equal(axes.length, 16, 'four cabinets by four axes are read from the motion catalog');
  for (const axis of axes) {
    assert.equal(topology.AXIS_ROLES[axis.letter], axis.role, `axis ${axis.letter} role`);
    assert.equal(topology.BRAKE_OUTPUT_ORDER[axis.letter], Number(axis.brake), `brake output of axis ${axis.letter}`);
  }
  const bindings = [...motion.matchAll(/Binding\("(?<wj>WJ\d)", "(?<wsb>WSB\d)", "(?<controller>MC-WSB\d)", MotionVocabulary\.Axis(?<first>[A-D]), MotionVocabulary\.Axis(?<second>[A-D])/g)]
    .map((match) => match.groups);
  assert.equal(bindings.length, 8, 'one binding per Water Jet, read from the motion catalog');
  for (const binding of bindings) {
    const cabinet = topology.WSB_CABINETS.find((entry) => entry.wsb === binding.wsb);
    assert.ok(cabinet, `${binding.wsb} is missing from the presentation table`);
    assert.equal(cabinet.controller, binding.controller, `${binding.wsb} controller identity`);
    const jet = cabinet.jets.find((entry) => entry.wj === binding.wj);
    assert.ok(jet, `${binding.wj} is not shown on ${binding.wsb}`);
    assert.equal(jet.horizontalAxis, binding.first, `${binding.wj} horizontal axis`);
    assert.equal(jet.verticalAxis, binding.second, `${binding.wj} vertical axis`);
  }
  // The counts the tabs print are derived from the table, never from a second copy of it.
  let assignedTotal = 0;
  for (const wall of topology.WALL_ORDER) {
    const perWall = topology.WATER_JETS
      .filter((jet) => jet.targetWall === wall)
      .reduce((sum, jet) => sum + topology.DEFAULT_ASSIGNMENTS[jet.wj], 0);
    assert.equal(perWall, topology.EXPECTED_PER_WALL[wall],
      `the default assignments aimed at ${wall} no longer account for that wall's Sensors`);
    assignedTotal += perWall;
  }
  assert.equal(assignedTotal, topology.EXPECTED_TOTAL);
});

test('the millimetre and native scaling on screen is the contract constant, and no ratio is invented', async () => {
  const contracts = csharp('packages/domain/CleaningPathContracts.cs');
  const topology = await import(pathToFileURL(join(PUBLIC, 'topology.mjs')).href);
  assert.equal(Number(/NativePerMillimetre = (\d+)/.exec(contracts)[1]), topology.NATIVE_PER_MILLIMETRE,
    'the screen and the contract must agree on how many native units a millimetre is');
  assert.equal(Number(/MinimumMillimetres = (\d+)/.exec(contracts)[1]), topology.WORKSPACE_MIN_MM);
  assert.equal(Number(/MaximumMillimetres = (\d+)/.exec(contracts)[1]), topology.WORKSPACE_MAX_MM);
  assert.equal(Number(/MaximumNative = (\d+)/.exec(contracts)[1]), topology.MAX_NATIVE);
  assert.equal(Number(/SafeReturnCentreHorizontalMillimetres = (\d+)/.exec(contracts)[1]),
    topology.SAFE_RETURN_CENTRE_MM.horizontal);
  assert.equal(Number(/SafeReturnCentreVerticalMillimetres = (\d+)/.exec(contracts)[1]),
    topology.SAFE_RETURN_CENTRE_MM.vertical);
  assert.equal(topology.nativeToMm(topology.SAFE_RETURN_CENTRE_NATIVE.horizontal), 125);
  assert.equal(topology.coordinatePair(281).ok, false, 'a coordinate outside the contract range is refused');
  assert.equal(topology.coordinatePair(0).ok, true);

  // The conversion factor is stated in words on the panel that shows millimetres, so a reader of the
  // screen and a reader of the contract are given the same sentence.
  assert.match(sources.get('tabs.mjs'), /1 mm is never rendered as a pulse count/);

  // A tpToRpRatio belongs to a manual motion profile, not to this product: the Runtime publishes none in
  // any payload the screen reads, so the screen must not print one - and must not print a 2:1 either.
  // Comments are stripped first: the file that explains why no ratio is printed has to name the ratio to
  // explain it, and a check that punished that would end up deleting the explanation instead of the bug.
  for (const [name, source] of every) {
    const text = source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/.*$/gm, '');
    assert.equal(/tpToRpRatio/.test(text), false, `${name} reads a profile ratio the payload never publishes`);
    assert.equal(/\b2:1\b|TP:RP/.test(text), false, `${name} states a ratio nobody published`);
  }
});

test('six tabs, one panel each, and the topology table is written in exactly one file', () => {
  const shell = sources.get('shellBar.mjs');
  const tabs = [...shell.matchAll(/\{ key: (?<key>\d), id: '(?<id>\w+)', label: '(?<label>[^']+)'/g)]
    .map((match) => match.groups);
  assert.deepEqual(tabs.map((tab) => tab.label),
    ['Main Control', 'Cleaning Queue', 'WJ & Pump', 'Sensor & Path', 'Alarms', 'System & Trends']);
  assert.deepEqual(tabs.map((tab) => tab.key), ['1', '2', '3', '4', '5', '6']);

  const panels = [...html.matchAll(/data-tab-panel="(?<id>[a-z]+)"/g)].map((match) => match.groups.id);
  assert.deepEqual(panels, tabs.map((tab) => tab.id), 'a tab without a panel is a button that does nothing');
  assert.equal(new Set(panels).size, panels.length, 'a panel named twice is a panel that never clears');

  // Single Topology Source Rule: only topology.mjs may write the table. A panel that restates a row is a
  // row that can drift, and drift on a wall screen looks like data.
  for (const [name, text] of every) {
    if (name === 'topology.mjs') continue;
    assert.equal(/\bWJ1\b/.test(text), false,
      `${name} names WJ1 directly: the identity table belongs in topology.mjs and is imported from there`);
  }
  // A module that uses the table takes it from the canonical module; the router, which reads none, is
  // checked by what it does import instead. That is the rule as it can actually be enforced.
  for (const [name, source] of every) {
    if (name === 'topology.mjs' || !name.endsWith('.mjs')) continue;
    if (/\bWALL_ORDER\b|\btierOfRow\b|\bEXPECTED_TOTAL\b|\bEXPECTED_PER_WALL\b|\bWATER_JETS\b|\bSB_CABINETS\b/.test(source)) {
      assert.equal(source.includes("from './topology.mjs'"), true,
        `${name} uses the topology table without importing the canonical module`);
    }
  }
  for (const name of ['app.mjs', 'wallMap.mjs', 'tabs.mjs', 'shellBar.mjs']) {
    assert.equal(sources.get(name).includes("from './topology.mjs'")
      || sources.get(name).includes("from './tabs.mjs'")
      || sources.get(name).includes("from './shellBar.mjs'")
      || sources.get(name).includes("from './wallMap.mjs'")
      || sources.get(name).includes("from './topology.mjs'"), true,
      `${name} is not wired into the module graph the document boots`);
  }
});

test('the camera viewport is a container: it carries the refusal and makes no request', () => {
  assert.equal(/<img|poster=|srcset=/.test(html), false, 'the document carries no image element to load');
  assert.match(html, /id="camera-panel"/);
  assert.match(sources.get('tabs.mjs'), /NO CAMERA SOURCE - THE READ-ONLY RUNTIME HAS NO IMAGE ROUTE/);
  // The sentence is the whole of the viewport's claim about itself; anything warmer would be a mock.
  const camera = sources.get('tabs.mjs').split('export function renderCameraPanel')[1] || '';
  assert.equal(/http|\/\/|\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}/.test(camera), false,
    'the camera panel names an address: the read-only host has no image route to name');
  assert.match(camera, /zero - no image element, no source, no address, no request/);
});

test('the summary pills add up to the protected total, and a wall summary that does not is reported', async () => {
  const topology = await import(pathToFileURL(join(PUBLIC, 'topology.mjs')).href);
  const walls = [
    { wall: 'LEFT', total: 24, dirty: 6, cleaner: 12, notClassified: 3, uncertain: 3, maxScore: 91.5 },
    { wall: 'REAR', total: 30, dirty: 8, cleaner: 15, notClassified: 4, uncertain: 3, maxScore: 95 },
    { wall: 'RIGHT', total: 24, dirty: 5, cleaner: 13, notClassified: 3, uncertain: 3, maxScore: 88 },
    { wall: 'FRONT', total: 30, dirty: 9, cleaner: 14, notClassified: 4, uncertain: 3, maxScore: 99 },
  ];
  // The fixture is the arithmetic the pill claims: every wall's parts add to its own total, the four
  // walls add to 108, and 12 + 14 of them are classified neither dirty nor cleaner. A function that
  // quietly dropped one of the two components could not reproduce these numbers.
  for (const wall of walls) {
    assert.equal(wall.cleaner + wall.dirty + wall.uncertain + wall.notClassified, wall.total,
      `the fixture for ${wall.wall} is not self-consistent`);
  }
  assert.equal(walls.reduce((sum, wall) => sum + wall.total, 0), 108);
  const result = topology.summaryPills(walls);
  assert.equal(result.state, 'ok', JSON.stringify(result.note));
  assert.equal(result.total, 108);
  assert.equal(result.pills.length, 3);
  assert.deepEqual(result.pills.map((pill) => pill.key), ['cleaner', 'dirty', 'uncertain']);
  assert.equal(result.pills[0].value, 54);
  assert.equal(result.pills[1].value, 28);
  assert.equal(result.pills[2].value, 26,
    'pill three is the published uncertain count plus the published not-classified count');
  assert.match(result.breakdown, /uncertain 12 \+ not classified 14/);
  assert.equal(result.perWall.length, 4, 'the breakdown the bar points at has one row per wall');

  const short = topology.summaryPills([walls[0], walls[1]]);
  assert.equal(short.state, 'mismatch');
  assert.match(short.note, /TOPOLOGY_COUNT_MISMATCH/);
  assert.equal(topology.summaryPills(null).state, 'no data', 'no summary is reported as no summary');
  assert.match(topology.summaryPills([]).note, /WALL SUMMARY NOT PUBLISHED IN THIS SNAPSHOT/);
});

// ------------------------------------------- Hydrogen 1920x1080 Industrial HMI architecture

test('the Hydrogen 4-band 1920x1080 shell pins exact vertical tracks and a fixed diagnostics popover', () => {
  // Exact 4-band shell in HTML: topbar (48px) + status-strip (26px) + main-workspace + event-ticker (24px),
  // plus the out-of-flow fixed diagnostics popover overlay.
  assert.match(html, /class="app-shell"/);
  assert.match(html, /class="topbar"/);
  assert.match(html, /class="status-strip"/);
  assert.match(html, /class="main-workspace"/);
  assert.match(html, /class="event-ticker\b/);
  assert.match(html, /class="diag-popover"\s+id="diag-popover"/);

  // Exact CSS geometry pins for full-bleed 1920x1080 fit (48 + 26 + 16 + 770 + 8 + 188 + 24 = 1080px).
  assert.match(css, /\.topbar\s*\{[^}]*[;\s]height:\s*48px;/);
  assert.match(css, /\.status-strip\s*\{[^}]*[;\s]height:\s*26px;/);
  assert.match(css, /\.event-ticker\s*\{[^}]*[;\s]height:\s*24px;/);
  assert.match(css, /\.diag-popover\s*\{[^}]*[;\s]position:\s*fixed;/);
  assert.match(css, /@media\s*\(min-width:\s*1280px\)\s*\{\s*\.app-shell\s*\{[^}]*[;\s]height:\s*100vh;[^}]*[;\s]overflow:\s*hidden;/);
});

test('Tab 1 pins the 2x2 6fr/4fr architectural grid, 3-column U-shape boiler wall map and vivid fills', () => {
  assert.match(css, /\.main-control-grid\s*\{[^}]*grid-template-columns:\s*minmax\(0,\s*6fr\)\s+minmax\(0,\s*4fr\);/);
  assert.match(css, /\.main-control-grid\s*\{[^}]*grid-template-rows:\s*minmax\(0,\s*1fr\)\s+188px;/);
  assert.match(css, /\.boiler-map-body\s*\{[^}]*grid-template-columns:\s*minmax\(0,\s*4fr\)\s+minmax\(0,\s*5\.2fr\)\s+minmax\(0,\s*4fr\);/);
  assert.match(css, /\.split-card\s*\{[^}]*[;\s]height:\s*178px;/);
  assert.match(css, /--clean-fill:\s*#1f6a49;/);
  assert.match(css, /--dirty-fill:\s*#b0382e;/);
  assert.match(css, /--uncertain-fill:\s*#3e454f;/);
  assert.match(css, /--bg-canvas:\s*#0d1014;/);

  const mapSource = sources.get('wallMap.mjs');
  assert.match(mapSource, /u-col-side/);
  assert.match(mapSource, /u-col-center/);
  assert.match(mapSource, /u-center-hub/);
  assert.match(mapSource, /sds-cell/);
  assert.match(mapSource, /wall-bar/);
});

test('the Hydrogen U-shape map and canvas panels render cleanly with finite coordinates and authoritative WSB2 C/D', async () => {
  const prevDoc = globalThis.document;
  const nonFinite = [];
  const checkFinite = (fnName, args) => {
    for (const arg of args) {
      if (typeof arg === 'number' && !Number.isFinite(arg)) nonFinite.push(`${fnName}(${arg})`);
    }
  };
  const makeNode = (tag) => {
    const node = {
      tagName: String(tag).toUpperCase(),
      className: '',
      id: '',
      title: '',
      type: '',
      disabled: false,
      hidden: false,
      width: 680,
      height: 190,
      dataset: {},
      style: { setProperty() {} },
      children: [],
      attributes: new Map(),
      listeners: [],
      get firstChild() { return this.children[0] || null; },
      set textContent(v) { this._text = String(v); if (v === '') this.children = []; },
      get textContent() {
        if (this.children.length === 0) return this._text || '';
        return this.children.map((c) => c.textContent).join(' ');
      },
      appendChild(child) { this.children.push(child); return child; },
      removeChild(child) {
        const idx = this.children.indexOf(child);
        if (idx >= 0) this.children.splice(idx, 1);
        return child;
      },
      setAttribute(k, v) { this.attributes.set(k, String(v)); },
      getAttribute(k) { return this.attributes.get(k) || null; },
      addEventListener(type, fn) { this.listeners.push({ type, fn }); },
      getContext() {
        const ctx = {
          fillStyle: '', strokeStyle: '', lineWidth: 1, font: '', textAlign: '', textBaseline: '',
          save() {}, restore() {}, beginPath() {}, stroke() {}, fill() {}, setLineDash() {},
          clearRect(...a) { checkFinite('clearRect', a); },
          fillRect(...a) { checkFinite('fillRect', a); },
          strokeRect(...a) { checkFinite('strokeRect', a); },
          moveTo(...a) { checkFinite('moveTo', a); },
          lineTo(...a) { checkFinite('lineTo', a); },
          arc(...a) { checkFinite('arc', a); },
          fillText(_t, ...a) { checkFinite('fillText', a); },
        };
        return ctx;
      },
    };
    return node;
  };
  globalThis.document = { createElement: makeNode };
  try {
    const mapMod = await import(pathToFileURL(join(PUBLIC, 'wallMap.mjs')).href);
    const tabsMod = await import(pathToFileURL(join(PUBLIC, 'tabs.mjs')).href);
    const detailMod = await import(pathToFileURL(join(PUBLIC, 'sensorDetail.mjs')).href);
    const topoMod = await import(pathToFileURL(join(PUBLIC, 'topology.mjs')).href);

    const wallSpec = [
      ['LEFT', 4], ['REAR', 5], ['RIGHT', 4], ['FRONT', 5],
    ];
    const wallMap = [];
    const sensors = [];
    let scan = 1;
    for (const [wall, cols] of wallSpec) {
      for (let r = 1; r <= 6; r += 1) {
        for (let c = 1; c <= cols; c += 1) {
          const sensorId = `${wall}-${r}-${c}`;
          wallMap.push({ slotId: `S-${sensorId}`, positionKind: 'SENSOR', wall, logicalRow: r, logicalColumn: c, sensorId });
          sensors.push({
            sensorId, deviceId: 'DEV1', wall, logicalRow: r, logicalColumn: c, scanOrder: scan,
            dirtyScore: scan % 3 === 0 ? 72.5 : 41.2, lastValidatedScore: 50.0,
            classification: scan % 3 === 0 ? 'DIRTY' : 'CLEANER', classificationBasis: 'SCORE',
            quality: 'GOOD', queueState: scan === 1 ? 'ACTIVE' : 'NONE', alarmState: 'NONE',
            isActiveJobTarget: scan === 1, assignedWaterJetId: topoMod.WATER_JETS[0].wj,
            assignedIsolationValveId: topoMod.WATER_JETS[0].iv,
            tcFrontChannel: `TCF-${scan}`, tcRearChannel: `TCR-${scan}`,
          });
          scan += 1;
        }
      }
    }
    const snapshot = {
      kind: 'snapshot', revision: 7, generatedAt: '2026-11-14T12:00:00Z',
      wallMap, sensors,
      walls: [
        { wall: 'LEFT', total: 24, cleaner: 16, dirty: 8, uncertain: 0, notClassified: 0, maxScore: 72.5 },
        { wall: 'REAR', total: 30, cleaner: 20, dirty: 10, uncertain: 0, notClassified: 0, maxScore: 72.5 },
        { wall: 'RIGHT', total: 24, cleaner: 16, dirty: 8, uncertain: 0, notClassified: 0, maxScore: 72.5 },
        { wall: 'FRONT', total: 30, cleaner: 20, dirty: 10, uncertain: 0, notClassified: 0, maxScore: 72.5 },
      ],
      waterJets: topoMod.WATER_JETS.map((j) => ({
        waterJetId: j.wj, installedWall: j.installedWall, installedRegion: j.installedTier,
        targetWall: j.targetWall, targetRegion: j.targetTier, dedicatedIsolationValveId: j.iv,
        placementKind: j.placementKind, placementAnchors: [],
      })),
      activeJob: {
        jobId: 'JOB-1', targetSensorId: sensors[0].sensorId, jetId: topoMod.WATER_JETS[0].wj,
        valveId: topoMod.WATER_JETS[0].iv, phase: 'P3', phaseLabel: 'P3', phaseIndex: 4,
        phaseProgress: 0.5, valveOpenCommand: 'OPEN', valveOutletPressureBar: 21.4,
        pumpOutletPressureBar: 22.1, pumpReadySetpointBar: 15.0,
        pumpPressureQuality: 'GOOD', pumpPressureSourceId: 'AI-003', pumpPressureInputValid: true,
        valvePressureQuality: 'GOOD', valvePressureSourceId: 'IV-P', valvePressureInputValid: true,
      },
      pump: { state: 'RUNNING', pressure: 22.1, setpoint: 15.0, readyBandLow: 15.0, readyBandHigh: 25.0, ready: true },
      config: { dirtyThreshold: 65 },
      trend: {
        capacity: 60, seriesNames: ['pressure', 'setpoint'],
        points: [
          { t: '2026-11-14T11:59:58Z', setpoint: 15.0, series: [22.0, 15.0] },
          { t: '2026-11-14T11:59:59Z', setpoint: 15.0, series: [null, 15.0] },
          { t: '2026-11-14T12:00:00Z', setpoint: 15.0, series: [22.1, 15.0] },
        ],
      },
    };
    const workspace = {
      kind: 'dispatch-workspace', revision: 7,
      configuration: {
        activeBoilerCondition: 'NORMAL',
        boilerRanges: [
          { condition: 'LOW', autoDelayMinSeconds: 180, autoDelayMaxSeconds: 300 },
          { condition: 'NORMAL', autoDelayMinSeconds: 60, autoDelayMaxSeconds: 180 },
        ],
      },
      preview: { appliedDelaySeconds: 90, mode: 'AUTO', zoneFactor: 0.75 },
    };

    const mapHost = makeNode('div');
    const model = mapMod.buildModel(snapshot);
    assert.equal(model.ok, true);
    mapMod.renderWallMap(mapHost, model, snapshot, sensors[0].sensorId, () => {});
    assert.equal(mapHost.children.length, 3, 'U-shape wall map must render exactly 3 columns');
    const allCells = [];
    const walk = (n) => {
      if (n.className && n.className.includes('sds-cell')) allCells.push(n);
      for (const c of n.children || []) walk(c);
    };
    walk(mapHost);
    assert.equal(allCells.length, 108, 'U-shape wall map must render all 108 SDS cells');
    assert.match(mapHost.textContent, /WJ3 \| IV3 \(WSB2 C\/D\)/, 'Right nozzle strip must obey authoritative WSB2 C/D topology');

    const camHost = makeNode('div');
    tabsMod.renderCameraPanel(camHost, snapshot, 'AUTO', () => {});
    assert.match(camHost.textContent, /zero - no image element, no source, no address, no request/);

    const trendCanvas = makeNode('canvas');
    detailMod.drawTrend(trendCanvas, makeNode('span'), makeNode('span'), snapshot, snapshot.pump);
    tabsMod.renderDelayBands(makeNode('canvas'), makeNode('p'), snapshot, workspace, sensors[0].sensorId);
    tabsMod.renderWorkspace(makeNode('canvas'), makeNode('p'), snapshot, sensors[0].sensorId, sensors[0].sensorId);
    assert.deepEqual(nonFinite, [], 'zero non-finite canvas coordinates allowed');
  } finally {
    globalThis.document = prevDoc;
  }
});
