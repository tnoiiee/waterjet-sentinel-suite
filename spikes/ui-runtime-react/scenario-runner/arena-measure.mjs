#!/usr/bin/env node
// WJSS Stage 0.2.1A — Arena harness measurement run (Node harness + SSE only; no browser).
//
//   node scenario-runner/arena-measure.mjs --minutes 10 --clients 2 --sample 10
//
// Spawns the harness as a child process (127.0.0.1), attaches N SSE clients, cycles synthetic
// scenarios every 30 s, samples /api/spike/metrics. Raw JSONL -> results/raw/ (ignored);
// summary -> results/summary/arena-harness-<label>.{json,md}.
// Measures ONLY what Arena supports: harness CPU/memory, event-loop delay, message sizes and
// rates, scheduling, Historian metrics. No browser, Edge, WebView2, or Windows metrics.

import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { SseMirror } from './sse-client.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..');
const args = process.argv.slice(2);
const arg = (n, d) => (args.includes(n) ? args[args.indexOf(n) + 1] : d);
const minutes = Number(arg('--minutes', 10));
const clients = Number(arg('--clients', 2));
const sampleS = Number(arg('--sample', 10));
const label = arg('--label', `${minutes}min`);
const port = Number(arg('--port', 5191));
const token = crypto.randomBytes(16).toString('hex');
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const pct = (arr, p) => {
  if (!arr.length) return null;
  const s = [...arr].sort((a, b) => a - b);
  return s[Math.min(s.length - 1, Math.max(0, Math.ceil((p / 100) * s.length) - 1))];
};
const stats = (arr, d = 2) => {
  const f = (v) => (v === null ? null : Number(v.toFixed(d)));
  return arr.length ? { n: arr.length, p50: f(pct(arr, 50)), p95: f(pct(arr, 95)), max: f(Math.max(...arr)), min: f(Math.min(...arr)) } : { n: 0 };
};

const child = spawn(process.execPath, [path.join(root, 'runtime-harness', 'src', 'main.mjs'), '--port', String(port)], { env: { ...process.env, WJSS_SPIKE_TOKEN: token }, stdio: ['ignore', 'pipe', 'pipe'] });
let childOut = '';
child.stdout.on('data', (d) => (childOut += d));
child.stderr.on('data', (d) => (childOut += d));
const base = `http://127.0.0.1:${port}`;
for (let i = 0; i < 50; i += 1) {
  try {
    if ((await fetch(`${base}/healthz`)).ok) break;
  } catch {
    /* starting */
  }
  await sleep(100);
}
const cmd = async (command, params) => (await fetch(`${base}/api/spike/scenario`, { method: 'POST', headers: { 'content-type': 'application/json', 'x-spike-token': token }, body: JSON.stringify({ command, params }) })).json();
const metrics = async () => (await fetch(`${base}/api/spike/metrics`)).json();

const deltaBytes = [];
const snapshotBytes = [];
const intervals = [];
const lags = [];
let lastAt = null;
const mirrors = [];
for (let i = 0; i < clients; i += 1) {
  const m = new SseMirror(`${base}/api/stream`, {
    resyncOnGap: true,
    onEvent: (ev, msg) => {
      if (i !== 0) return;
      if (ev.type === 'snapshot') snapshotBytes.push(ev.bytes);
      if (ev.type === 'delta') {
        deltaBytes.push(ev.bytes);
        lags.push(ev.at - Date.parse(msg.generatedAt));
        if (lastAt) intervals.push(ev.at - lastAt);
        lastAt = ev.at;
      }
    },
  });
  mirrors.push(m);
}

const CYCLE = [
  ['set-dirty-mode', { mode: 'dirty30' }],
  ['raise-alarm', { sensorId: 'SYN-REAR-07' }],
  ['set-dirty-mode', { mode: 'oscillate' }],
  ['clear-alarm', {}],
  ['device-timeout', { deviceId: 'SYN-TC-06', enabled: true }],
  ['set-dirty-mode', { mode: 'dirty70' }],
  ['device-timeout', { deviceId: 'SYN-TC-06', enabled: false }],
  ['ack-alarm', {}],
  ['historian-delay', { delayMs: 5000 }],
  ['inject-revision-gap', {}],
  ['pump-stop', {}],
  ['historian-delay', { delayMs: 20 }],
  ['pump-start', {}],
  ['set-dirty-mode', { mode: 'normal' }],
];

const rawDir = path.join(root, 'results', 'raw', `arena-harness-${label}-${new Date().toISOString().replace(/[:.]/g, '-')}`);
fs.mkdirSync(rawDir, { recursive: true });
const rawFile = path.join(rawDir, 'harness-samples.jsonl');
const samples = [];
const startedAt = new Date().toISOString();
const t0 = Date.now();
let step = 0;
let nextScenario = t0 + 30_000;
await metrics(); // reset CPU window
while (Date.now() - t0 < minutes * 60_000) {
  await sleep(sampleS * 1000);
  const m = await metrics();
  const s = { t: Date.now(), elapsedS: Math.round((Date.now() - t0) / 1000), cpu: m.cpuPercentSinceLastRead, rssMb: m.memory.rssMb, heapUsedMb: m.memory.heapUsedMb, loopP99: m.eventLoopDelayMs.p99, loopMax: m.eventLoopDelayMs.max, revision: m.revision, historianDepth: m.historian.depth, nearOverflow: m.historian.nearOverflow, violations: m.invariants.violations, sseClients: m.sse.clients };
  samples.push(s);
  fs.appendFileSync(rawFile, JSON.stringify(s) + '\n');
  if (Date.now() >= nextScenario) {
    const [c, p] = CYCLE[step % CYCLE.length];
    await cmd(c, p);
    step += 1;
    nextScenario = Date.now() + 30_000;
  }
}
const final = await metrics();
fs.writeFileSync(path.join(rawDir, 'final-metrics.json'), JSON.stringify(final, null, 2));
for (const m of mirrors) m.close();
child.kill('SIGTERM');
await sleep(300);

const rss = samples.map((s) => s.rssMb);
const third = Math.max(1, Math.floor(samples.length / 3));
const summary = {
  label: 'SYNTHETIC SPIKE MEASUREMENT - ARENA NODE HARNESS ONLY - NOT A BROWSER OR PRODUCTION RESULT',
  run: label,
  startedAt,
  durationS: Math.round((Date.now() - t0) / 1000),
  sseClients: clients,
  sampleIntervalS: sampleS,
  scenarioCycleSteps: step,
  harnessProcess: {
    cpuPercent: stats(samples.map((s) => s.cpu).filter((v) => v !== null)),
    rssMb: { ...stats(rss, 1), firstThirdMean: Number((rss.slice(0, third).reduce((a, b) => a + b, 0) / third).toFixed(1)), lastThirdMean: Number((rss.slice(-third).reduce((a, b) => a + b, 0) / third).toFixed(1)) },
    heapUsedMb: stats(samples.map((s) => s.heapUsedMb), 1),
    eventLoopDelayP99Ms: stats(samples.map((s) => s.loopP99), 1),
  },
  messages: {
    snapshotBytes: stats(snapshotBytes, 0),
    deltaBytes: stats(deltaBytes, 0),
    deltaIntervalMs: stats(intervals, 0),
    deltaLagGeneratedToReceivedMs: stats(lags, 1),
    deltasReceivedClient0: deltaBytes.length,
    deltaRatePerS: Number((deltaBytes.length / ((Date.now() - t0) / 1000)).toFixed(3)),
    clientGapsDetected: mirrors.map((m) => m.gaps),
    clientResyncs: mirrors.map((m) => m.resyncs),
    clientConnections: mirrors.map((m) => m.connections),
  },
  publish: final.publish,
  scheduler: { maxGlobalInFlight: final.scheduler.maxGlobalInFlight, maxPerDeviceInFlight: final.scheduler.maxPerDeviceInFlight, timeouts: final.scheduler.timeouts, pollLatencyMs: final.scheduler.pollLatencyMs },
  historian: { maxDepth: final.historian.maxDepth, accepted: final.historian.accepted, written: final.historian.written, rejected: final.historian.rejected, nearOverflowEvents: final.historian.nearOverflowEvents },
  jobs: final.jobs,
  pumpStopCommandMs: final.pumpStopCommandMs,
  invariantViolations: final.invariants.violations,
  harnessExited: child.exitCode ?? child.signalCode,
};
const sumDir = path.join(root, 'results', 'summary');
fs.mkdirSync(sumDir, { recursive: true });
fs.writeFileSync(path.join(sumDir, `arena-harness-${label}.json`), JSON.stringify(summary, null, 2) + '\n');
const H = summary.harnessProcess;
const M = summary.messages;
const md = `# Stage 0.2.1A — Arena harness measurement (${label})

> ${summary.label}

| Item | Value |
| --- | --- |
| Started | ${summary.startedAt} |
| Duration | ${summary.durationS} s |
| SSE clients | ${clients} |
| Scenario cycle steps | ${step} |
| Harness CPU % (p50 / p95 / max) | ${H.cpuPercent.p50} / ${H.cpuPercent.p95} / ${H.cpuPercent.max} |
| Harness RSS MB (p50 / p95 / max) | ${H.rssMb.p50} / ${H.rssMb.p95} / ${H.rssMb.max} |
| Harness RSS MB first-third mean → last-third mean | ${H.rssMb.firstThirdMean} → ${H.rssMb.lastThirdMean} |
| Heap used MB (p50 / max) | ${summary.harnessProcess.heapUsedMb.p50} / ${summary.harnessProcess.heapUsedMb.max} |
| Event-loop delay p99 ms (p50 / max of samples) | ${H.eventLoopDelayP99Ms.p50} / ${H.eventLoopDelayP99Ms.max} |
| Snapshot bytes (p50 / max) | ${M.snapshotBytes.p50} / ${M.snapshotBytes.max} |
| Delta bytes (p50 / p95 / max) | ${M.deltaBytes.p50} / ${M.deltaBytes.p95} / ${M.deltaBytes.max} |
| Delta interval ms (p50 / p95 / max) | ${M.deltaIntervalMs.p50} / ${M.deltaIntervalMs.p95} / ${M.deltaIntervalMs.max} |
| Delta lag generated→received ms (p50 / p95 / max) | ${M.deltaLagGeneratedToReceivedMs.p50} / ${M.deltaLagGeneratedToReceivedMs.p95} / ${M.deltaLagGeneratedToReceivedMs.max} |
| Delta rate (/s, client 0) | ${M.deltaRatePerS} |
| Client gaps detected (scenario-injected) / re-snapshots | ${M.clientGapsDetected.join(', ')} / ${M.clientResyncs.join(', ')} |
| Client SSE connections (incl. resyncs) | ${M.clientConnections.join(', ')} |
| Publish duration ms (p50 / p95 / max, last 3600) | ${summary.publish.publishMs.p50} / ${summary.publish.publishMs.p95} / ${summary.publish.publishMs.max} |
| Max in-flight (global / per device) | ${summary.scheduler.maxGlobalInFlight} / ${summary.scheduler.maxPerDeviceInFlight} |
| FAST poll latency ms (p50 / p95 / max) | ${summary.scheduler.pollLatencyMs.FAST.p50} / ${summary.scheduler.pollLatencyMs.FAST.p95} / ${summary.scheduler.pollLatencyMs.FAST.max} |
| Historian max depth / rejected | ${summary.historian.maxDepth} / ${summary.historian.rejected} |
| Jobs completed / accepted second jobs | ${summary.jobs.completed} / ${summary.jobs.acceptedSecondJobs} |
| Pump-stop command handling in harness ms (n / max) — not end-to-end | ${summary.pumpStopCommandMs.n} / ${summary.pumpStopCommandMs.max} |
| Invariant violations | ${summary.invariantViolations} |

Browser, Edge, WebView2, Windows-process, kiosk, and end-to-end UI metrics: **NOT MEASURED IN ARENA** (Owner-local).
`;
fs.writeFileSync(path.join(sumDir, `arena-harness-${label}.md`), md);
console.log(JSON.stringify(summary, null, 2));
if (childOut.match(/Error/)) console.error('harness output contained errors:\n' + childOut);
