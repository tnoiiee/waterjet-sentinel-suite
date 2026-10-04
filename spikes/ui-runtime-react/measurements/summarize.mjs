#!/usr/bin/env node
// WJSS Stage 0.2.1A — summarise an OWNER-LOCAL soak run (raw -> committed summary).
//   node measurements/summarize.mjs ../results/raw/<label>-<timestamp> [--processes <csv>]
// Reads browser-samples.jsonl, harness-samples.jsonl, final.json (written by e2e/soak.spec.ts)
// and optionally the CSV written by sample-processes.ps1. Writes
// results/summary/owner-local-<label>.{json,md}. Raw files stay outside Git.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..');
const dir = path.resolve(process.argv[2] ?? '');
if (!process.argv[2] || !fs.existsSync(dir)) {
  console.error('usage: node measurements/summarize.mjs <raw run dir> [--processes <csv>]');
  process.exit(2);
}
const procArg = process.argv.includes('--processes') ? process.argv[process.argv.indexOf('--processes') + 1] : null;
const readJsonl = (f) => (fs.existsSync(f) ? fs.readFileSync(f, 'utf8').trim().split('\n').filter(Boolean).map((l) => JSON.parse(l)) : []);
const pct = (a, p) => {
  if (!a.length) return null;
  const s = [...a].sort((x, y) => x - y);
  return s[Math.min(s.length - 1, Math.max(0, Math.ceil((p / 100) * s.length) - 1))];
};
const st = (a) => {
  const v = a.filter((x) => typeof x === 'number' && Number.isFinite(x));
  return v.length ? { n: v.length, p50: pct(v, 50), p95: pct(v, 95), max: Math.max(...v), first: v[0], last: v.at(-1) } : { n: 0 };
};

const run = JSON.parse(fs.readFileSync(path.join(dir, 'run.json'), 'utf8'));
const b = readJsonl(path.join(dir, 'browser-samples.jsonl'));
const h = readJsonl(path.join(dir, 'harness-samples.jsonl'));
const final = fs.existsSync(path.join(dir, 'final.json')) ? JSON.parse(fs.readFileSync(path.join(dir, 'final.json'), 'utf8')) : null;

let processes = null;
if (procArg && fs.existsSync(procArg)) {
  const [head, ...lines] = fs.readFileSync(procArg, 'utf8').trim().split(/\r?\n/);
  const cols = head.split(',').map((c) => c.replace(/"/g, ''));
  const rows = lines.map((l) => Object.fromEntries(l.split(',').map((v, i) => [cols[i], v.replace(/"/g, '')])));
  const by = {};
  for (const r of rows) (by[r.Group] ??= []).push(r);
  processes = Object.fromEntries(Object.entries(by).map(([g, rs]) => [g, { cpuPercent: st(rs.map((r) => Number(r.CpuPercent))), workingSetMb: st(rs.map((r) => Number(r.WorkingSetMb))), privateMb: st(rs.map((r) => Number(r.PrivateMb))), handles: st(rs.map((r) => Number(r.Handles))) }]));
}

const summary = {
  label: 'OWNER-LOCAL SYNTHETIC SPIKE RUN SUMMARY - installed Microsoft Edge (not WebView2, not kiosk)',
  run,
  samples: { browser: b.length, harness: h.length },
  browser: {
    jsHeapUsedMb: st(b.map((x) => x.jsHeapUsedMb)),
    domNodes: st(b.map((x) => x.domNodes)),
    lastLagMs: st(b.map((x) => x.lastLagMs)),
    longTasksTotal: b.at(-1)?.longTasks ?? null,
    longTaskMsTotal: b.at(-1)?.longTaskMsTotal ?? null,
    gaps: b.at(-1)?.gaps ?? null,
    reconnects: b.at(-1)?.reconnects ?? null,
    snapshots: b.at(-1)?.snapshots ?? null,
    deltas: b.at(-1)?.deltas ?? null,
    trendPointsMax: st(b.map((x) => x.trendPoints)).max ?? null,
  },
  harness: {
    cpuPercent: st(h.map((x) => x.cpu)),
    rssMb: st(h.map((x) => x.memory?.rssMb)),
    eventLoopP99Ms: st(h.map((x) => x.eventLoopDelayMs?.p99)),
    invariantViolationsFinal: final?.harness?.invariants?.violations ?? null,
    acceptedSecondJobsFinal: final?.harness?.jobs?.acceptedSecondJobs ?? null,
  },
  windowsProcesses: processes,
  hardGates: final
    ? {
        connectionLiveAtEnd: final.browser.connection === 'LIVE',
        invariantViolationsZero: final.harness.invariants.violations === 0,
        acceptedSecondJobsZero: final.harness.jobs.acceptedSecondJobs === 0,
        trendBounded: final.browser.trendPoints <= 600,
      }
    : 'final.json missing - run incomplete',
};
const out = path.join(root, 'results', 'summary', `owner-local-${run.label}.json`);
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify(summary, null, 2) + '\n');
const row = (k, s) => `| ${k} | ${s.n ? `${s.p50} / ${s.p95} / ${s.max} (first ${s.first}, last ${s.last})` : 'n/a'} |`;
fs.writeFileSync(
  out.replace(/\.json$/, '.md'),
  [
    `# Owner-local run summary — ${run.label} (${run.minutes} min)`,
    '',
    `> ${summary.label}`,
    '',
    '| Metric | p50 / p95 / max |',
    '| --- | --- |',
    row('Edge JS heap used MB', summary.browser.jsHeapUsedMb),
    row('DOM nodes', summary.browser.domNodes),
    row('Delta lag ms (generatedAt→applied)', summary.browser.lastLagMs),
    row('Harness CPU %', summary.harness.cpuPercent),
    row('Harness RSS MB', summary.harness.rssMb),
    row('Harness event-loop p99 ms', summary.harness.eventLoopP99Ms),
    ...(processes ? Object.entries(processes).flatMap(([g, p]) => [row(`${g} CPU %`, p.cpuPercent), row(`${g} working set MB`, p.workingSetMb)]) : []),
    '',
    `Long tasks: ${summary.browser.longTasksTotal} (${summary.browser.longTaskMsTotal} ms) · gaps ${summary.browser.gaps} · reconnects ${summary.browser.reconnects} · snapshots ${summary.browser.snapshots} · deltas ${summary.browser.deltas}`,
    '',
    `Hard gates: ${JSON.stringify(summary.hardGates)}`,
    '',
  ].join('\n'),
);
console.log(`written ${path.relative(root, out)}`);
