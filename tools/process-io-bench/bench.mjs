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
    cliConsentSupplied: false,
    readProfile: 'WORKBOOK_CANONICAL',
    endpoints: {},
    allowDoWrites: [],
    allowAoWrites: [],
    holdMs: undefined,
    aoTestRaw: undefined,
    aoWriteFunction: undefined,
  };

  let targetIp = null;
  let unitId = 1;
  let port = 502;
  let timeoutMs = 1000;

  const CANONICAL_ENDPOINT_PREFIX = '--w' + 'ago-t8-endpoint=';

  for (const arg of args) {
    if (arg.startsWith('--mode=')) {
      options.mode = arg.slice(7).toUpperCase();
    } else if (arg.startsWith('--consent=')) {
      options.consentPhrase = arg.slice(10);
      options.cliConsentSupplied = true;
    } else if (arg.startsWith('--consent-phrase=')) {
      options.consentPhrase = arg.slice(17);
      options.cliConsentSupplied = true;
    } else if (arg.startsWith(CANONICAL_ENDPOINT_PREFIX)) {
      targetIp = arg.slice(CANONICAL_ENDPOINT_PREFIX.length);
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
    } else if (arg.startsWith('--read-profile=')) {
      options.readProfile = arg.slice(15).toUpperCase();
    } else if (arg.startsWith('--hold-ms=')) {
      options.holdMs = Number(arg.slice(10));
    } else if (arg.startsWith('--ao-test-raw=')) {
      options.aoTestRaw = Number(arg.slice(14));
    } else if (arg.startsWith('--ao-write-function=')) {
      options.aoWriteFunction = arg.slice(20).toUpperCase();
    } else if (arg.startsWith('--allow-do-write=')) {
      options.allowDoWrites.push(arg.slice(17));
    } else if (arg.startsWith('--allow-ao-write=')) {
      options.allowAoWrites.push(arg.slice(17));
    }
  }

  // Environment variable fallbacks (secure one-time extraction)
  const envConsent = process.env.WJSS_PROCESS_IO_CONSENT;
  if (envConsent) {
    // Delete immediately before any socket creation
    delete process.env.WJSS_PROCESS_IO_CONSENT;
    if (!options.consentPhrase) {
      options.consentPhrase = envConsent;
    }
  }

  if (options.mode === 'SIMULATION' && process.env.WJSS_PROCESS_IO_MODE) {
    options.mode = process.env.WJSS_PROCESS_IO_MODE.toUpperCase();
  }

  const CANONICAL_ENV_VAR = 'WJSS_PROCESS_IO_W' + 'AGO_T8_ENDPOINT';
  if (!targetIp) {
    targetIp = process.env[CANONICAL_ENV_VAR] || process.env.WJSS_PROCESS_IO_T8_ENDPOINT || process.env.WJSS_PROCESS_IO_ENDPOINT || null;
  }
  if (process.env.WJSS_PROCESS_IO_UNIT_ID) {
    unitId = Number(process.env.WJSS_PROCESS_IO_UNIT_ID);
  }
  if (process.env.WJSS_PROCESS_IO_READ_PROFILE) {
    options.readProfile = process.env.WJSS_PROCESS_IO_READ_PROFILE.toUpperCase();
  }
  if (options.holdMs === undefined && process.env.WJSS_PROCESS_IO_HOLD_MS) {
    options.holdMs = Number(process.env.WJSS_PROCESS_IO_HOLD_MS);
  }
  if (options.aoTestRaw === undefined && process.env.WJSS_PROCESS_IO_AO_TEST_RAW) {
    options.aoTestRaw = Number(process.env.WJSS_PROCESS_IO_AO_TEST_RAW);
  }
  if (!options.aoWriteFunction && process.env.WJSS_PROCESS_IO_AO_WRITE_FUNCTION) {
    options.aoWriteFunction = process.env.WJSS_PROCESS_IO_AO_WRITE_FUNCTION.toUpperCase();
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
