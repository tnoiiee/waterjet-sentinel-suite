#!/usr/bin/env node
// WJSS Stage 0.2.1A — regenerate the golden contract fixtures from the synthetic harness.
// Output: contracts/fixtures/snapshot.example.json, delta.example.json (SYNTHETIC EXAMPLES).
// The trend window is truncated to keep the fixture small. Timestamps are real run times.

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHarness } from '../runtime-harness/src/server.mjs';
import { validateDelta, validateSnapshot } from '../contracts/validate.mjs';

const here = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.resolve(here, '../contracts/fixtures');
const h = createHarness({ port: 0, token: 'fixture' });
await h.start();
await new Promise((r) => setTimeout(r, 2600));
// Exercise several presentation states so the fixture shows them.
h.runtime.command('quality-showcase', { enabled: true });
h.runtime.command('raise-alarm', { sensorId: 'SYN-REAR-05' });
await new Promise((r) => setTimeout(r, 1100));
const { snap } = h.runtime.snapshot();
const delta = await new Promise((resolve) => h.runtime.once('delta', (d) => resolve(d)));
await h.stop();
snap.trend.points = snap.trend.points.slice(-3);
const e1 = validateSnapshot(snap);
const e2 = validateDelta(delta);
if (e1.length || e2.length) {
  console.error('fixture validation failed', e1, e2);
  process.exit(1);
}
fs.mkdirSync(outDir, { recursive: true });
fs.writeFileSync(path.join(outDir, 'snapshot.example.json'), JSON.stringify(snap, null, 1) + '\n');
fs.writeFileSync(path.join(outDir, 'delta.example.json'), JSON.stringify(delta, null, 1) + '\n');
console.log(`fixtures written: snapshot rev ${snap.revision}, delta ${delta.previousRevision}->${delta.revision} (${delta.sensors?.length ?? 0} sensors)`);
