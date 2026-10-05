#!/usr/bin/env node
// WJSS Stage 0.2.1A — Arena scenario runner: the 28 planned scenarios at the runtime / SSE
// level against the Node synthetic harness on 127.0.0.1. Browser-only aspects are marked
// OWNER-LOCAL and are covered by react-ui/e2e/operations.spec.ts (installed Edge).
//
// SYNTHETIC SPIKE EVIDENCE — NOT PRODUCTION VALIDATION.
// Output: results/raw/arena-scenarios-<ts>.json (ignored) and
//         results/summary/arena-scenarios.{json,md} (committed summary).

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHarness } from '../runtime-harness/src/server.mjs';
import { validateDelta, validateSnapshot } from '../contracts/validate.mjs';
import { CANNON_IDS, EXPECTED, getSensorMap } from '../contracts/sensorMap.mjs';
import { isOscillationSample } from '../runtime-harness/src/config.mjs';
import { SseMirror } from './sse-client.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..');
const TOKEN = 'arena-scenario-runner';
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const results = [];
let h;

const api = {
  cmd: async (command, params) => (await fetch(`${h.url}/api/spike/scenario`, { method: 'POST', headers: { 'content-type': 'application/json', 'x-spike-token': TOKEN }, body: JSON.stringify({ command, params }) })).json(),
  metrics: async () => (await fetch(`${h.url}/api/spike/metrics`)).json(),
  snapshot: async () => (await fetch(`${h.url}/api/snapshot`)).json(),
  close: async () => (await fetch(`${h.url}/api/spike/close-request`, { method: 'POST' })).json(),
};

async function scenario(id, name, fn) {
  const t0 = Date.now();
  let status = 'PASS';
  let evidence = {};
  let error = null;
  try {
    const r = await fn();
    if (r && r.status) {
      status = r.status;
      evidence = r.evidence ?? {};
    } else evidence = r ?? {};
  } catch (e) {
    status = 'FAIL';
    error = String(e?.message ?? e);
  }
  results.push({ id, name, status, durationMs: Date.now() - t0, evidence, error });
  console.log(`${status.padEnd(11)} ${id} ${name}${error ? ' :: ' + error : ''}`);
}
function check(cond, msg) {
  if (!cond) throw new Error(msg);
}

h = createHarness({ port: 0, token: TOKEN });
await h.start();
await sleep(1500);
const mirror = new SseMirror(`${h.url}/api/stream`);
await sleep(300);
const intervals = [];
let lastDeltaAt = null;
mirror.onEvent = (ev) => {
  if (ev.type === 'delta') {
    if (lastDeltaAt) intervals.push(ev.at - lastDeltaAt);
    lastDeltaAt = ev.at;
  }
};

await scenario('S01', 'Initial Snapshot', async () => {
  const first = mirror.events[0];
  check(first?.type === 'snapshot', 'first event is not a snapshot');
  const snap = await api.snapshot();
  const errs = validateSnapshot(snap);
  check(errs.length === 0, errs.join('; '));
  const channels = new Set(snap.sensors.flatMap((s) => [s.tcFrontChannel, s.tcRearChannel])).size;
  const cannons = snap.wallMap.filter((s) => s.slotType === 'CANNON');
  const perWall = Object.fromEntries(snap.walls.map((w) => [w.wall, w.total]));
  check(mirror.sensors.size === EXPECTED.sensors && channels === EXPECTED.channels, `sensors ${mirror.sensors.size} channels ${channels}`);
  check(cannons.length === 2 && !snap.sensors.some((s) => ['I7', 'I16', ...CANNON_IDS].includes(s.sensorId)), 'Cannon slots wrong or present as Sensors');
  return {
    firstEvent: first.type,
    snapshotBytes: first.bytes,
    sensors: mirror.sensors.size,
    thermocoupleChannels: channels,
    perWall,
    wallMapSlots: snap.wallMap.length,
    cannonSlots: cannons.map((c) => `${c.equipmentId}@R${c.logicalRow}C${c.logicalColumn}`),
    revision: first.revision,
  };
});

await scenario('S02', 'One-second Deltas', async () => {
  const n0 = mirror.events.length;
  await sleep(5200);
  const ds = mirror.events.slice(n0).filter((e) => e.type === 'delta');
  check(ds.length >= 4 && ds.length <= 7, `delta count ${ds.length} in 5.2 s`);
  check(mirror.gaps === 0, 'gap observed');
  const iv = intervals.slice(-ds.length + 1).sort((a, b) => a - b);
  return { deltasIn5_2s: ds.length, medianIntervalMs: iv[Math.floor(iv.length / 2)], gaps: mirror.gaps, lastValidated: validateDelta(mirror.lastMessage).length === 0 };
});

const dirtyGood = () => mirror.all().filter((s) => s.quality === 'GOOD' && s.classification === 'DIRTY').length;

await scenario('S03', '30% Dirty', async () => {
  await api.cmd('set-dirty-mode', { mode: 'dirty30' });
  await sleep(2500);
  const n = dirtyGood();
  const expected = Math.round(EXPECTED.sensors * 0.3);
  check(n >= expected - 8 && n <= expected, `dirty count ${n}`);
  return { dirty: n, expectedApprox: expected, population: EXPECTED.sensors, note: 'one sensor may be under active cleaning' };
});

await scenario('S04', '70% Dirty', async () => {
  await api.cmd('set-dirty-mode', { mode: 'dirty70' });
  await sleep(2500);
  const n = dirtyGood();
  const expected = Math.round(EXPECTED.sensors * 0.7);
  check(n >= expected - 8 && n <= expected, `dirty count ${n}`);
  return { dirty: n, expectedApprox: expected, population: EXPECTED.sensors };
});

await scenario('S05', 'Threshold oscillation around 50', async () => {
  await api.cmd('set-dirty-mode', { mode: 'oscillate' });
  const ids = mirror.all().filter(isOscillationSample).map((s) => s.sensorId);
  const prev = new Map();
  let flips = 0;
  for (let i = 0; i < 7; i += 1) {
    await sleep(1000);
    for (const id of ids) {
      const c = mirror.sensor(id).classification;
      if (prev.has(id) && prev.get(id) !== c) flips += 1;
      prev.set(id, c);
    }
  }
  check(flips >= 4, `only ${flips} flips`);
  await api.cmd('set-dirty-mode', { mode: 'normal' });
  return { oscillatingSensors: ids.length, classificationFlipsIn7s: flips };
});

await scenario('S06', 'All quality states', async () => {
  await api.cmd('quality-showcase', { enabled: true });
  await sleep(1300);
  const showcase = getSensorMap().sensors.filter((x) => x.wall === 'LEFT').slice(0, 5).map((x) => x.sensorId); // G+201..G+205
  const q = Object.fromEntries(showcase.map((id) => [id, mirror.sensor(id).quality]));
  const seen = new Set(mirror.all().map((s) => s.quality));
  check(['GOOD', 'UNCERTAIN', 'BAD', 'STALE', 'DISABLED'].every((x) => seen.has(x)), `missing quality: ${[...seen]}`);
  for (const s of mirror.all()) {
    if (['BAD', 'STALE', 'DISABLED'].includes(s.quality)) check(s.classification === 'NOT_CLASSIFIED', `${s.sensorId} classified`);
    if (s.quality === 'UNCERTAIN') check(s.classificationBasis !== 'CURRENT', `${s.sensorId} uncertain classified from current`);
  }
  await api.cmd('quality-showcase', { enabled: false });
  return { qualities: q, uncertainBasis: mirror.sensor(showcase[0]).classificationBasis };
});

await scenario('S07', 'Selection during updates', async () => {
  const snapText = JSON.stringify(await api.snapshot());
  check(!/"selected"/.test(snapText), 'selection present on the wire');
  return { status: 'PASS+OWNER', evidence: { runtimePart: 'selection is not on the wire (UI-local)', jsdom: 'renderIsolation.test.tsx: selection survives Deltas and Snapshots', browserPart: 'OWNER-LOCAL e2e S07' } };
});

await scenario('S08', 'Queue badge changes', async () => {
  await api.cmd('set-dirty-mode', { mode: 'dirty30' });
  const seen = new Map();
  for (let i = 0; i < 4; i += 1) {
    await sleep(1000);
    for (const s of mirror.all()) (seen.get(s.sensorId) ?? seen.set(s.sensorId, new Set()).get(s.sensorId)).add(s.queueState);
  }
  const changed = [...seen.values()].filter((v) => v.size > 1).length;
  const states = new Set([...seen.values()].flatMap((v) => [...v]));
  check(changed > 0, 'no queue badge change observed');
  return { sensorsWithQueueStateChange: changed, statesSeen: [...states] };
});

await scenario('S09', 'GlobalQueue changes', async () => {
  const q1 = mirror.single.queue;
  await api.cmd('hold-queue', { held: true });
  await sleep(1200);
  const q2 = mirror.single.queue;
  await api.cmd('hold-queue', { held: false });
  await api.cmd('enqueue', { sensorId: 'H17' });
  await sleep(1200);
  const q3 = mirror.single.queue;
  for (const q of [q1, q2, q3]) {
    check(q.entries.length <= 8, 'preview > 8');
    q.entries.forEach((e, i) => check(e.position === i + 1, 'not FIFO positions'));
  }
  check(q2.entries.every((e) => e.status !== 'READY'), 'held queue still READY');
  return { totalQueued: q3.totalQueued, previewLength: q3.entries.length, heldStatuses: [...new Set(q2.entries.map((e) => e.status))], fields: Object.keys(q3.entries[0] ?? {}) };
});

await scenario('S10', 'Single Job progression', async () => {
  await api.cmd('set-dirty-mode', { mode: 'normal' });
  const m0 = await api.metrics();
  const phases = new Set();
  let maxActive = 0;
  const t0 = Date.now();
  while (Date.now() - t0 < 30000) {
    await sleep(500);
    const j = mirror.single.activeJob;
    if (j) phases.add(j.phase);
    maxActive = Math.max(maxActive, (await api.metrics()).jobs.active);
    if (phases.size === 6 && !mirror.single.activeJob) break;
  }
  const m1 = await api.metrics();
  check(phases.size === 6, `phases seen ${[...phases]}`);
  check(m1.jobs.completed > m0.jobs.completed, 'no job completed');
  check(maxActive <= 1, 'more than one active job');
  return { phasesSeen: [...phases].sort(), jobsCompleted: m1.jobs.completed, maxActiveJobs: maxActive };
});

let alarmId;
await scenario('S11', 'Active Alarm', async () => {
  const before = mirror.sensor('H7').classification;
  const r = await api.cmd('raise-alarm', { sensorId: 'H7' });
  alarmId = r.detail.alarmId;
  await sleep(400);
  const s = mirror.sensor('H7');
  check(s.alarmState === 'ACTIVE_UNACK', `alarmState ${s.alarmState}`);
  check(['DIRTY', 'CLEANER', 'NOT_CLASSIFIED'].includes(s.classification), 'classification missing');
  return { alarmState: s.alarmState, classificationBefore: before, classificationAfter: s.classification, note: 'classification is independent of alarm state' };
});

await scenario('S12', 'Cleared Ack Required', async () => {
  await api.cmd('clear-alarm', { alarmId });
  await sleep(400);
  const a = mirror.sensor('H7').alarmState;
  check(a === 'CLEARED_UNACK', `after clear ${a}`);
  const strip = mirror.single.alarms.clearedUnack;
  await api.cmd('ack-alarm', { alarmId });
  await sleep(400);
  const b = mirror.sensor('H7').alarmState;
  check(b === 'NONE', `after ack ${b}`);
  return { afterClear: a, clearedUnackCount: strip, afterAck: b };
});

const devSensors = () => mirror.all().filter((s) => s.deviceId === 'SYN-TC-03');
let pollsBefore;
await scenario('S13', 'Device timeout', async () => {
  pollsBefore = Object.fromEntries((await api.metrics()).scheduler.sessions.map((s) => [s.deviceId, s.pollsOk]));
  await api.cmd('device-timeout', { deviceId: 'SYN-TC-03', enabled: true });
  const seen = new Set();
  for (let i = 0; i < 10; i += 1) {
    await sleep(500);
    devSensors().forEach((s) => seen.add(s.quality));
  }
  const comm = mirror.single.communication.devices.find((d) => d.deviceId === 'SYN-TC-03');
  check(seen.has('UNCERTAIN') && seen.has('BAD'), `qualities seen ${[...seen]}`);
  check(devSensors().every((s) => s.classification === 'NOT_CLASSIFIED'), 'BAD sensors classified');
  check(mirror.single.alarms.items.some((a) => a.code === 'SYN-COMM-TIMEOUT' && a.deviceId === 'SYN-TC-03'), 'no comm alarm');
  return { affectedSensors: devSensors().length, qualitiesSeen: [...seen], deviceState: comm.state, consecutiveTimeouts: comm.consecutiveTimeouts };
});

await scenario('S14', 'Other Devices continue', async () => {
  const now = Object.fromEntries((await api.metrics()).scheduler.sessions.map((s) => [s.deviceId, s.pollsOk]));
  const others = Object.keys(now).filter((d) => d !== 'SYN-TC-03' && d.startsWith('SYN-TC'));
  check(others.every((d) => now[d] - pollsBefore[d] >= 3), 'another device stalled');
  check(mirror.all().filter((s) => s.deviceId !== 'SYN-TC-03').every((s) => s.quality === 'GOOD'), 'other sensors degraded');
  return { pollsDuringTimeout: Object.fromEntries(others.map((d) => [d, now[d] - pollsBefore[d]])) };
});

await scenario('S15', 'Device recovery', async () => {
  await api.cmd('device-timeout', { deviceId: 'SYN-TC-03', enabled: false });
  const t0 = Date.now();
  while (Date.now() - t0 < 5000 && !devSensors().every((s) => s.quality === 'GOOD')) await sleep(100);
  check(devSensors().every((s) => s.quality === 'GOOD'), 'not recovered');
  const alarm = mirror.single.alarms.items.find((a) => a.code === 'SYN-COMM-TIMEOUT' && a.deviceId === 'SYN-TC-03');
  await api.cmd('ack-alarm', {});
  return { recoveredWithinMs: Date.now() - t0, commAlarmAfterRecovery: alarm?.state ?? 'none' };
});

let lastRevBeforeDrop;
let mirror2;
await scenario('S16', 'UI disconnect', async () => {
  lastRevBeforeDrop = mirror.revision;
  const r = await api.cmd('drop-clients');
  await sleep(300);
  check(mirror.ended, 'stream did not end');
  return { droppedClients: r.detail.dropped, lastRevisionSeen: lastRevBeforeDrop };
});

await scenario('S17', 'UI reconnect', async () => {
  await sleep(1200);
  mirror2 = new SseMirror(`${h.url}/api/stream`, { headers: { 'Last-Event-ID': String(lastRevBeforeDrop) } });
  await sleep(500);
  check(mirror2.events[0]?.type === 'snapshot', 'reconnect did not start with a snapshot');
  return { firstEventAfterReconnect: mirror2.events[0].type, sentLastEventId: lastRevBeforeDrop };
});

await scenario('S18', 'Authoritative re-snapshot', async () => {
  const snapRev = mirror2.events[0].revision;
  check(snapRev > lastRevBeforeDrop, 'snapshot is not newer than last seen');
  const m = await api.metrics();
  check(m.sse.reconnectsWithLastEventId >= 1, 'Last-Event-ID not observed');
  return { snapshotRevision: snapRev, lastSeenBeforeDrop: lastRevBeforeDrop, missedDeltasReplayed: 0, reconnectsWithLastEventId: m.sse.reconnectsWithLastEventId };
});

const m2 = () => mirror2;
await scenario('S19', 'Historian slowdown', async () => {
  await api.cmd('historian-delay', { delayMs: 600000 });
  const n0 = m2().events.length;
  const r0 = (await api.metrics()).revision;
  await sleep(5200);
  const ds = m2().events.slice(n0).filter((e) => e.type === 'delta');
  const iv = ds.slice(1).map((e, i) => e.at - ds[i].at);
  const r1 = (await api.metrics()).revision;
  check(ds.length >= 4, `deltas during slowdown ${ds.length}`);
  check(Math.max(...iv) < 1600, `max delta interval ${Math.max(...iv)} ms`);
  return { deltasIn5_2s: ds.length, maxIntervalMs: Math.max(...iv), revisionsAdvanced: r1 - r0 };
});

await scenario('S20', 'Near-overflow behavior', async () => {
  await api.cmd('historian-burst', { count: 45000 });
  await sleep(1200);
  let m = await api.metrics();
  check(m.historian.nearOverflow === true, 'nearOverflow not reported');
  await api.cmd('historian-burst', { count: 10000 });
  m = await api.metrics();
  check(m.historian.rejected > 0, 'no rejected samples');
  check(m.historian.gapMarkers >= 1, 'no gap marker');
  check(m.historian.depth <= m.historian.capacity, 'depth over capacity');
  const t0 = performance.now();
  const stop = await api.cmd('pump-stop');
  const stopMs = performance.now() - t0;
  check(stop.accepted && stopMs < 250, `pump stop ${stopMs} ms`);
  // Restore the write delay. A slow batch already in flight (from S19) completes first, so the
  // drain is polled for up to 12 s and the observed time is recorded (simulator behaviour).
  const tRestore = performance.now();
  await api.cmd('historian-delay', { delayMs: 20 });
  await api.cmd('pump-start');
  let m3 = await api.metrics();
  while (m3.historian.nearOverflow && performance.now() - tRestore < 12000) {
    await sleep(250);
    m3 = await api.metrics();
  }
  const drainMs = Math.round(performance.now() - tRestore);
  check(!m3.historian.nearOverflow, `historian still near overflow ${drainMs} ms after delay restore`);
  return { nearOverflow: true, depthAtFull: m.historian.depth, capacity: m.historian.capacity, rejected: m.historian.rejected, gapMarkers: m.historian.gapMarkers, pumpStopRoundTripMs: Number(stopMs.toFixed(2)), nearOverflowClearedAfterRestoreMs: drainMs, depthAtClear: m3.historian.depth, pumpAfterRestart: (await api.snapshot()).pump.state };
});

await scenario('S21', 'Bounded Trend', async () => {
  // Accelerated, separately labelled test parameters: 100 ms ticks, capacity 50.
  const fast = createHarness({ port: 0, token: 'x', params: { publishIntervalMs: 100, trendCapacity: 50 } });
  await fast.start();
  await sleep(9000);
  const s = await (await fetch(`${fast.url}/api/snapshot`)).json();
  const ticks = fast.runtime.tickCount;
  await fast.stop();
  check(ticks > 50 && s.trend.points.length === 50, `trend ${s.trend.points.length} after ${ticks} ticks`);
  const main = await api.snapshot();
  check(main.trend.points.length <= main.trend.capacity, 'main trend exceeds capacity');
  return { acceleratedTicks: ticks, acceleratedCapacity: 50, pointsRetained: s.trend.points.length, mainTrendPoints: main.trend.points.length, mainCapacity: main.trend.capacity, note: 'accelerated parameters are test-only, not the planning values' };
});

await scenario('S22', 'Camera placeholder', async () => {
  const src = path.join(root, 'react-ui', 'src');
  const files = [];
  const walk = (d) => fs.readdirSync(d, { withFileTypes: true }).forEach((e) => (e.isDirectory() ? walk(path.join(d, e.name)) : files.push(path.join(d, e.name))));
  walk(src);
  const hits = [];
  for (const f of files) {
    const t = fs.readFileSync(f, 'utf8');
    for (const m of t.matchAll(/(https?:)?\/\/[a-z0-9.-]+\.[a-z]{2,}[^\s'"`)]*/gi)) if (!/www\.w3\.org/.test(m[0])) hits.push(`${path.relative(root, f)}: ${m[0]}`);
    if (/<img|<video|<iframe/i.test(t)) hits.push(`${path.relative(root, f)}: media element`);
  }
  check(hits.length === 0, hits.join('; '));
  return { status: 'PASS+OWNER', evidence: { sourceFilesScanned: files.length, externalUrlsOrMediaElements: 0, browserPart: 'OWNER-LOCAL e2e S22 (request-host check)' } };
});

await scenario('S23', 'Synthetic close guard for Active Job', async () => {
  await api.cmd('auto-jobs', { enabled: false });
  await api.cmd('abort-job');
  await sleep(1500);
  const r = await api.cmd('second-job-attempt');
  const ev = await api.close();
  check(r.detail.activeJobs === 1, 'no active job');
  check(ev.allowed === false && ev.reasons.includes('ACTIVE_JOB'), JSON.stringify(ev));
  return { evaluation: ev };
});

await scenario('S24', 'Synthetic close guard for Pump running', async () => {
  await api.cmd('abort-job');
  const ev = await api.close();
  check(ev.allowed === false && ev.reasons.length === 1 && ev.reasons[0] === 'PUMP_RUNNING', JSON.stringify(ev));
  await api.cmd('auto-jobs', { enabled: true });
  return { evaluation: ev };
});

await scenario('S25', 'Viewport resize', async () => ({ status: 'OWNER-LOCAL', evidence: { reason: 'Browser layout cannot be exercised in Arena (no browser). Covered by react-ui/e2e/operations.spec.ts S25.' } }));

await scenario('S26', 'Synthetic config revision', async () => {
  const r0 = mirror2.single.config.revision;
  await api.cmd('publish-config', { dirtyThreshold: 99 });
  await sleep(400);
  const c = mirror2.single.config;
  check(c.revision === r0 + 1 && c.dirtyThreshold === 99, 'config revision not delivered');
  const dirty = mirror2.all().filter((s) => s.quality === 'GOOD' && s.classification === 'DIRTY').length;
  check(dirty === 0, `still dirty ${dirty}`);
  await api.cmd('publish-config', { dirtyThreshold: 50 });
  await sleep(400);
  return { configRevision: c.revision, dirtyAtThreshold99: dirty, restoredRevision: mirror2.single.config.revision };
});

await scenario('S27', 'Refused second Job', async () => {
  const r = await api.cmd('second-job-attempt');
  const m = await api.metrics();
  check(r.accepted === false && r.reason === 'ACTIVE_JOB_EXISTS', JSON.stringify(r));
  check(m.jobs.acceptedSecondJobs === 0 && m.jobs.active <= 1, 'second job accepted');
  check(m.invariants.violations === 0, 'invariant violation');
  return { result: r.reason, activeJobs: m.jobs.active, acceptedSecondJobs: m.jobs.acceptedSecondJobs, refusedSecondJobs: m.jobs.refusedSecondJobs, invariantViolations: m.invariants.violations };
});

await scenario('S28', 'Revision gap -> re-snapshot', async () => {
  const g0 = mirror2.gaps;
  await api.cmd('inject-revision-gap');
  await sleep(1600);
  check(mirror2.gaps > g0, 'gap not detected by client');
  mirror2.close();
  const m3 = new SseMirror(`${h.url}/api/stream`);
  await sleep(1500);
  check(m3.events[0].type === 'snapshot' && m3.revision === (await api.metrics()).revision, 'no fresh snapshot');
  m3.close();
  return { gapsDetected: mirror2.gaps - g0, resyncFirstEvent: m3.events[0].type };
});

const finalMetrics = await api.metrics();
mirror2.close();
await h.stop();

const counts = results.reduce((a, r) => ((a[r.status] = (a[r.status] ?? 0) + 1), a), {});
const summary = {
  label: 'SYNTHETIC SPIKE EVIDENCE - NOT PRODUCTION VALIDATION',
  stage: '0.2.1A',
  environment: 'Arena (Linux, Node harness in-process, SSE over 127.0.0.1, no browser)',
  generatedAt: new Date().toISOString(),
  counts,
  scenarios: results.map(({ id, name, status, durationMs, evidence, error }) => ({ id, name, status, durationMs, evidence, error })),
  finalInvariantViolations: finalMetrics.invariants.violations,
  finalAcceptedSecondJobs: finalMetrics.jobs.acceptedSecondJobs,
};
const rawDir = path.join(root, 'results', 'raw');
const sumDir = path.join(root, 'results', 'summary');
fs.mkdirSync(rawDir, { recursive: true });
fs.mkdirSync(sumDir, { recursive: true });
fs.writeFileSync(path.join(rawDir, `arena-scenarios-${Date.now()}.json`), JSON.stringify({ ...summary, finalMetrics }, null, 2));
fs.writeFileSync(path.join(sumDir, 'arena-scenarios.json'), JSON.stringify(summary, null, 2) + '\n');
const md = [
  '# Stage 0.2.1A — Arena scenario results (synthetic)',
  '',
  '> SYNTHETIC SPIKE EVIDENCE — NOT PRODUCTION VALIDATION. Runtime / SSE level only; no browser in Arena.',
  '> `PASS+OWNER` = runtime part passed in Arena, browser part pending Owner-local Edge run. `OWNER-LOCAL` = browser-only.',
  '',
  `Generated: ${summary.generatedAt}`,
  '',
  `Counts: ${Object.entries(counts).map(([k, v]) => `${k} ${v}`).join(' · ')} · final invariant violations ${summary.finalInvariantViolations} · accepted second jobs ${summary.finalAcceptedSecondJobs}`,
  '',
  '| ID | Scenario | Status | Duration (ms) | Key evidence |',
  '| --- | --- | --- | --- | --- |',
  ...results.map((r) => `| ${r.id} | ${r.name} | ${r.status} | ${r.durationMs} | ${(r.error ?? JSON.stringify(r.evidence)).replaceAll('|', '\\|').slice(0, 260)} |`),
  '',
].join('\n');
fs.writeFileSync(path.join(sumDir, 'arena-scenarios.md'), md);
console.log('\nCounts:', counts);
process.exit(results.some((r) => r.status === 'FAIL') ? 1 : 0);
