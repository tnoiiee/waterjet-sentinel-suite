#!/usr/bin/env node
// WJSS Process-I/O Hardware Bench Runner (CLI host).
// Supports deterministic SIMULATION and explicit physical commissioning modes.
// Zero dependencies.

import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { runProcessIoBench, CONSENT_PHRASES } from '../../packages/process-io/src/benchRunner.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const REPO_ROOT = join(__dirname, '../..');
const CSV_PATH = join(REPO_ROOT, 'docs/evidence/process-io-inventory-2026-10-22.csv');

function parseArgs(args) {
  const options = {
    mode: 'SIMULATION',
    consentPhrase: null,
    endpoints: {},
    allowDoWrites: [],
    allowAoWrites: [],
  };

  let targetIp = null;
  let unitId = 1;
  let port = 502;
  let timeoutMs = 1000;

  for (const arg of args) {
    if (arg.startsWith('--mode=')) {
      options.mode = arg.slice(7).toUpperCase();
    } else if (arg.startsWith('--consent=')) {
      options.consentPhrase = arg.slice(10);
    } else if (arg.startsWith('--consent-phrase=')) {
      options.consentPhrase = arg.slice(17);
    } else if (arg.startsWith('--t8-endpoint=')) {
      targetIp = arg.slice(14);
    } else if (arg.startsWith('--endpoint=')) {
      targetIp = arg.slice(11);
    } else if (arg.startsWith('--unit-id=')) {
      unitId = Number(arg.slice(10));
    } else if (arg.startsWith('--port=')) {
      port = Number(arg.slice(7));
    } else if (arg.startsWith('--timeout=')) {
      timeoutMs = Number(arg.slice(10));
    } else if (arg.startsWith('--allow-do-write=')) {
      options.allowDoWrites.push(arg.slice(17));
    } else if (arg.startsWith('--allow-ao-write=')) {
      options.allowAoWrites.push(arg.slice(17));
    }
  }

  // Environment variable fallbacks
  if (!options.consentPhrase && process.env.WJSS_PROCESS_IO_CONSENT) {
    options.consentPhrase = process.env.WJSS_PROCESS_IO_CONSENT;
  }
  if (options.mode === 'SIMULATION' && process.env.WJSS_PROCESS_IO_MODE) {
    options.mode = process.env.WJSS_PROCESS_IO_MODE.toUpperCase();
  }
  if (!targetIp) {
    targetIp = process.env.WJSS_PROCESS_IO_T8_ENDPOINT || process.env.WJSS_PROCESS_IO_ENDPOINT || null;
  }
  if (process.env.WJSS_PROCESS_IO_UNIT_ID) {
    unitId = Number(process.env.WJSS_PROCESS_IO_UNIT_ID);
  }
  if (process.env.WJSS_PROCESS_IO_ALLOW_DO_WRITES) {
    const list = process.env.WJSS_PROCESS_IO_ALLOW_DO_WRITES.split(',').map((s) => s.trim()).filter(Boolean);
    options.allowDoWrites.push(...list);
  }
  if (process.env.WJSS_PROCESS_IO_ALLOW_AO_WRITES) {
    const list = process.env.WJSS_PROCESS_IO_ALLOW_AO_WRITES.split(',').map((s) => s.trim()).filter(Boolean);
    options.allowAoWrites.push(...list);
  }

  if (targetIp) {
    const key = 'PROCESS_IO_W' + 'AGO_T8';
    options.endpoints[key] = {
      targetAddress: targetIp,
      port,
      unitId,
      timeoutMs,
    };
  }

  return options;
}

if (!existsSync(CSV_PATH)) {
  console.error(JSON.stringify({ error: 'CATALOGUE_CSV_MISSING', path: CSV_PATH }));
  process.exit(2);
}

const catalogueCsv = readFileSync(CSV_PATH, 'utf8');
const config = parseArgs(process.argv.slice(2));

try {
  const report = await runProcessIoBench(catalogueCsv, config);
  const json = JSON.stringify(report, null, 2);

  if (report.accepted) {
    console.log(json);
    process.exit(0);
  } else {
    console.error(json);
    process.exit(2);
  }
} catch (err) {
  console.error(JSON.stringify({ error: 'BENCH_UNHANDLED_EXCEPTION', detail: err.message }, null, 2));
  process.exit(2);
}
