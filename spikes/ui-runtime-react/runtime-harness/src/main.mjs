#!/usr/bin/env node
// WJSS Stage 0.2.1A — harness CLI entry. SYNTHETIC SPIKE INFRASTRUCTURE.
//
// Usage: node src/main.mjs [--port 5181] [--static ../react-ui/dist] [--synthetic-test-controls]
// Env:   WJSS_SPIKE_PORT, WJSS_SPIKE_TOKEN (optional; otherwise random per run),
//        WJSS_SPIKE_TEST_CONTROLS=1 (same as --synthetic-test-controls)
// --synthetic-test-controls enables the opt-in Owner review controls (Diagnostics drawer) by
// exposing the per-run scenario token to the same-origin spike UI. Off by default.
// Binds to 127.0.0.1 only. The run token is written to .run-token (git-ignored).

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHarness } from './server.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const arg = (name, fallback) => {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : fallback;
};

const port = Number(arg('--port', process.env.WJSS_SPIKE_PORT ?? 5181));
const defaultStatic = path.resolve(here, '../../react-ui/dist');
const staticDir = arg('--static', fs.existsSync(defaultStatic) ? defaultStatic : null);

const testControls = args.includes('--synthetic-test-controls') || process.env.WJSS_SPIKE_TEST_CONTROLS === '1';
const harness = createHarness({ port, staticDir, token: process.env.WJSS_SPIKE_TOKEN || undefined, testControls });
await harness.start();
fs.writeFileSync(path.resolve(here, '../.run-token'), harness.token + '\n', { mode: 0o600 });
console.log(`[wjss-spike] SYNTHETIC runtime harness listening on ${harness.url} (loopback only)`);
console.log(`[wjss-spike] static: ${staticDir ?? '(none - run npm run build in react-ui)'}`);
console.log('[wjss-spike] scenario token written to runtime-harness/.run-token (git-ignored)');
console.log(`[wjss-spike] synthetic test controls: ${testControls ? 'ENABLED (spike review tooling, loopback only)' : 'off'}`);

const shutdown = async () => {
  await harness.stop();
  process.exit(0);
};
process.on('SIGINT', shutdown);
process.on('SIGTERM', shutdown);
