#!/usr/bin/env node
// WJSS Stage 0.2.1A — environment record (no secrets, no host names, no addresses).
//   node measurements/environment-record.mjs --label arena
// Writes results/environment/<label>-environment.json.
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..');
const label = process.argv.includes('--label') ? process.argv[process.argv.indexOf('--label') + 1] : 'arena';
const run = (cmd, a) => {
  try {
    return execFileSync(cmd, a, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'], shell: process.platform === 'win32' }).trim();
  } catch {
    return null;
  }
};
const lock = JSON.parse(fs.readFileSync(path.join(root, 'react-ui', 'package-lock.json'), 'utf8'));
const entries = Object.entries(lock.packages).filter(([k]) => k);
const installed = entries.filter(([k]) => fs.existsSync(path.join(root, 'react-ui', k, 'package.json')));

const record = {
  label: `${label} environment record - Stage 0.2.1A synthetic spike`,
  recordedAt: new Date().toISOString(),
  os: { platform: os.platform(), release: os.release(), type: os.type(), arch: os.arch() },
  cpu: { model: os.cpus()[0]?.model ?? null, logicalCores: os.cpus().length },
  memoryGb: Number((os.totalmem() / 1024 ** 3).toFixed(1)),
  node: process.version,
  npm: run('npm', ['--version']),
  git: { head: run('git', ['rev-parse', 'HEAD']), branch: run('git', ['rev-parse', '--abbrev-ref', 'HEAD']) },
  dependencies: {
    lockfileVersion: lock.lockfileVersion,
    lockfileEntries: entries.length,
    installedOnThisPlatform: installed.length,
    direct: { ...lock.packages[''].dependencies, ...lock.packages[''].devDependencies },
  },
  browserAvailable: label === 'arena' ? false : null,
  notes: label === 'arena' ? ['No browser, no WebView2, no .NET, no Windows in Arena.', 'Harness and SSE measurements only.'] : [],
};
const out = path.join(root, 'results', 'environment', `${label}-environment.json`);
fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify(record, null, 2) + '\n');
console.log(`written ${path.relative(root, out)}`);
