#!/usr/bin/env node
// WJSS Stage 0.2.1A — SHA-256 manifest of committed result files.
//   node measurements/manifest.mjs
// Hashes results/{environment,summary,manifests}/* (excluding the manifest itself) and writes
// results/manifests/SHA256SUMS.txt (sha256sum format) and sha256-manifest.json.
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..');
const res = path.join(root, 'results');
const files = [];
for (const sub of ['environment', 'summary', 'manifests']) {
  const d = path.join(res, sub);
  if (!fs.existsSync(d)) continue;
  for (const f of fs.readdirSync(d).sort()) {
    if (f === 'SHA256SUMS.txt' || f === 'sha256-manifest.json') continue;
    files.push(path.join(d, f));
  }
}
const entries = files.map((f) => ({ path: path.relative(res, f).split(path.sep).join('/'), bytes: fs.statSync(f).size, sha256: crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex') }));
fs.writeFileSync(path.join(res, 'manifests', 'SHA256SUMS.txt'), entries.map((e) => `${e.sha256}  ${e.path}`).join('\n') + '\n');
fs.writeFileSync(path.join(res, 'manifests', 'sha256-manifest.json'), JSON.stringify({ generatedAt: new Date().toISOString(), base: 'spikes/ui-runtime-react/results', entries }, null, 2) + '\n');
console.log(`${entries.length} files hashed`);
