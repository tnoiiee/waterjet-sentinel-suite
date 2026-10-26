#!/usr/bin/env node
// WJSS Process-I/O Hardware Bench Runner (CLI host).
// Supports deterministic SIMULATION and explicit PHYSICAL_TEST modes.
// Zero dependencies.

import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { runProcessIoBench, REQUIRED_CONSENT_PHRASE } from '../../packages/process-io/src/benchRunner.mjs';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const REPO_ROOT = join(__dirname, '../..');
const CSV_PATH = join(REPO_ROOT, 'docs/evidence/process-io-inventory-2026-10-22.csv');

function parseArgs(args) {
  const options = {
    mode: 'SIMULATION',
    consentPhrase: null,
    endpoints: {},
  };

  for (const arg of args) {
    if (arg.startsWith('--mode=')) {
      options.mode = arg.slice(7).toUpperCase();
    } else if (arg.startsWith('--consent=')) {
      options.consentPhrase = arg.slice(10);
    } else if (arg.startsWith('--pump-rack-endpoint=')) {
      options.endpoints.PROCESS_IO_PUMP_RACK = { targetAddress: arg.slice(21), port: 502, unitId: 1, timeoutMs: 1000 };
    } else if (arg.startsWith('--zone-a-endpoint=')) {
      options.endpoints.PROCESS_IO_ZONE_A = { targetAddress: arg.slice(18), port: 502, unitId: 1, timeoutMs: 1000 };
    } else if (arg.startsWith('--zone-b-endpoint=')) {
      options.endpoints.PROCESS_IO_ZONE_B = { targetAddress: arg.slice(18), port: 502, unitId: 1, timeoutMs: 1000 };
    }
  }

  // Environment fallback (non-HOST variable names)
  if (!options.consentPhrase && process.env.WJSS_PROCESS_IO_CONSENT) {
    options.consentPhrase = process.env.WJSS_PROCESS_IO_CONSENT;
  }
  if (options.mode === 'SIMULATION' && process.env.WJSS_PROCESS_IO_MODE) {
    options.mode = process.env.WJSS_PROCESS_IO_MODE.toUpperCase();
  }
  if (!options.endpoints.PROCESS_IO_PUMP_RACK && process.env.WJSS_PROCESS_IO_PUMP_RACK_ENDPOINT) {
    options.endpoints.PROCESS_IO_PUMP_RACK = { targetAddress: process.env.WJSS_PROCESS_IO_PUMP_RACK_ENDPOINT, port: 502, unitId: 1, timeoutMs: 1000 };
  }
  if (!options.endpoints.PROCESS_IO_ZONE_A && process.env.WJSS_PROCESS_IO_ZONE_A_ENDPOINT) {
    options.endpoints.PROCESS_IO_ZONE_A = { targetAddress: process.env.WJSS_PROCESS_IO_ZONE_A_ENDPOINT, port: 502, unitId: 1, timeoutMs: 1000 };
  }
  if (!options.endpoints.PROCESS_IO_ZONE_B && process.env.WJSS_PROCESS_IO_ZONE_B_ENDPOINT) {
    options.endpoints.PROCESS_IO_ZONE_B = { targetAddress: process.env.WJSS_PROCESS_IO_ZONE_B_ENDPOINT, port: 502, unitId: 1, timeoutMs: 1000 };
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
