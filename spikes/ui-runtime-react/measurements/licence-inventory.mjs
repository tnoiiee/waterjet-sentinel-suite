#!/usr/bin/env node
// WJSS Stage 0.2.1A — licence inventory from package-lock.json + installed package.json files.
//   node measurements/licence-inventory.mjs
// Writes results/manifests/licence-inventory.{json,md}. Lists every lockfile entry (direct and
// transitive, installed or platform-optional), its licence, scope (runtime bundle vs dev/build),
// and flags non-permissive licences (MPL-2.0 is recorded transparently, not hidden).
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, '..');
const ui = path.join(root, 'react-ui');
const lock = JSON.parse(fs.readFileSync(path.join(ui, 'package-lock.json'), 'utf8'));
const direct = new Set([...Object.keys(lock.packages[''].dependencies ?? {}), ...Object.keys(lock.packages[''].devDependencies ?? {})]);
const PERMISSIVE = new Set(['MIT', 'MIT-0', 'ISC', 'Apache-2.0', 'BSD-2-Clause', 'BSD-3-Clause', '0BSD', 'BlueOak-1.0.0', 'CC0-1.0', 'Python-2.0']);

const rows = [];
for (const [key, v] of Object.entries(lock.packages)) {
  if (!key) continue;
  const name = key.replace(/^.*node_modules\//, '');
  const pj = path.join(ui, key, 'package.json');
  const installed = fs.existsSync(pj);
  let licence = v.license ?? null;
  if (installed) {
    const p = JSON.parse(fs.readFileSync(pj, 'utf8'));
    licence = p.license ?? (Array.isArray(p.licenses) ? p.licenses.map((l) => l.type).join(' OR ') : licence);
  }
  rows.push({
    name,
    version: v.version,
    licence: licence ?? 'UNKNOWN',
    direct: direct.has(name),
    scope: v.dev ? 'dev/build' : 'runtime-bundle',
    optional: Boolean(v.optional),
    installedHere: installed,
    os: v.os ?? null,
    cpu: v.cpu ?? null,
    hasInstallScript: Boolean(v.hasInstallScript),
    integrity: v.integrity ? v.integrity.slice(0, 20) + '…' : null,
    flag: PERMISSIVE.has(licence) ? null : licence === 'MPL-2.0' ? 'MPL-2.0 (weak copyleft, file-level) - build-time only, recorded transparently' : 'REVIEW',
  });
}
rows.sort((a, b) => a.name.localeCompare(b.name));

// Bundled non-npm assets (committed files shipped in the spike bundle). Hash computed live.
const fontDir = path.join(ui, 'src', 'assets', 'fonts');
const fontFile = path.join(fontDir, 'GoogleSans-Latin-Variable.woff2');
const bundledAssets = fs.existsSync(fontFile)
  ? [
      {
        name: 'Google Sans (Latin variable subset, wght 400-700)',
        file: 'react-ui/src/assets/fonts/GoogleSans-Latin-Variable.woff2',
        bytes: fs.statSync(fontFile).size,
        sha256: crypto.createHash('sha256').update(fs.readFileSync(fontFile)).digest('hex'),
        licence: 'OFL-1.1',
        licenceFile: 'react-ui/src/assets/fonts/OFL.txt (verbatim)',
        trademarkNotice: 'react-ui/src/assets/fonts/TRADEMARKS.md (verbatim; "Google" / "Google Sans" are trademarks of Google LLC)',
        provenance: 'react-ui/src/assets/fonts/FONT_SOURCE.md (official google/fonts repository, ofl/googlesans)',
        reservedFontName: 'none declared',
        modifiedVersion: 'yes - subset + instanced + WOFF2 (OFL Modified Version; name kept because no RFN is declared)',
        scope: 'runtime-bundle (self-hosted; no CDN)',
        flag: 'OFL-1.1 font licence (permits bundling with software; must not be sold by itself; licence text bundled)',
      },
    ]
  : [];
const byLicence = rows.reduce((a, r) => ((a[r.licence] = (a[r.licence] ?? 0) + 1), a), {});
const out = {
  label: 'Stage 0.2.1A synthetic spike - react-ui dependency licence inventory',
  generatedAt: new Date().toISOString(),
  totals: { lockfileEntries: rows.length, installedHere: rows.filter((r) => r.installedHere).length, direct: rows.filter((r) => r.direct).length, runtimeBundle: rows.filter((r) => r.scope === 'runtime-bundle').length, withInstallScript: rows.filter((r) => r.hasInstallScript).map((r) => `${r.name} (${r.os?.join(',') ?? 'any'}, optional=${r.optional})`) },
  byLicence,
  flagged: rows.filter((r) => r.flag).map((r) => ({ name: r.name, version: r.version, licence: r.licence, scope: r.scope, flag: r.flag })),
  packages: rows,
  bundledAssets,
  runtimeHarness: 'runtime-harness/ has no dependencies (Node built-ins only).',
  scenarioRunnerAndMeasurements: 'Node built-ins only (Windows sampler uses built-in PowerShell cmdlets).',
};
const dir = path.join(root, 'results', 'manifests');
fs.mkdirSync(dir, { recursive: true });
fs.writeFileSync(path.join(dir, 'licence-inventory.json'), JSON.stringify(out, null, 2) + '\n');
const md = [
  '# Stage 0.2.1A — Licence inventory (react-ui)',
  '',
  `Generated ${out.generatedAt} from \`react-ui/package-lock.json\` and installed \`package.json\` files.`,
  '',
  `Lockfile entries: ${out.totals.lockfileEntries} · installed on the generating platform: ${out.totals.installedHere} · direct: ${out.totals.direct} · runtime bundle: ${out.totals.runtimeBundle}`,
  '',
  '## By licence',
  '',
  '| Licence | Count |',
  '| --- | --- |',
  ...Object.entries(byLicence).map(([k, v]) => `| ${k} | ${v} |`),
  '',
  '## Flagged (non-permissive or needing review)',
  '',
  '| Package | Version | Licence | Scope | Note |',
  '| --- | --- | --- | --- | --- |',
  ...out.flagged.map((f) => `| ${f.name} | ${f.version} | ${f.licence} | ${f.scope} | ${f.flag} |`),
  '',
  '## Bundled non-npm assets',
  '',
  '| Asset | File | Bytes | SHA-256 | Licence | Reserved Font Name | Note |',
  '| --- | --- | --- | --- | --- | --- | --- |',
  ...(bundledAssets.length ? bundledAssets.map((a) => `| ${a.name} | \`${a.file}\` | ${a.bytes} | \`${a.sha256}\` | ${a.licence} | ${a.reservedFontName} | ${a.flag}; ${a.modifiedVersion}; licence: ${a.licenceFile}; provenance: ${a.provenance} |`) : ['| none | | | | | | |']),
  '',
  '## Install scripts',
  '',
  out.totals.withInstallScript.length ? out.totals.withInstallScript.map((s) => `- ${s}`).join('\n') : '- none',
  '',
  '## All packages',
  '',
  '| Package | Version | Licence | Direct | Scope | Optional | Installed here |',
  '| --- | --- | --- | --- | --- | --- | --- |',
  ...rows.map((r) => `| ${r.name} | ${r.version} | ${r.licence} | ${r.direct ? 'yes' : ''} | ${r.scope} | ${r.optional ? 'yes' : ''} | ${r.installedHere ? 'yes' : 'no'} |`),
  '',
].join('\n');
fs.writeFileSync(path.join(dir, 'licence-inventory.md'), md);
console.log(JSON.stringify({ totals: out.totals, byLicence, flagged: out.flagged.length }, null, 2));
