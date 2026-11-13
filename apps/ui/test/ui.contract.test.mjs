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

const MODULES = ['app.mjs', 'feed.mjs', 'wallMap.mjs', 'sensorDetail.mjs', 'thermal.mjs',
  'dispatchPanel.mjs', 'opsPanel.mjs'];

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
  assert.deepEqual(shipped, ['app.mjs', 'dispatchPanel.mjs', 'feed.mjs', 'global.css', 'index.html',
    'opsPanel.mjs', 'sensorDetail.mjs', 'thermal.mjs', 'wallMap.mjs']
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
