// Stage 0.4B-1 — canonical JSON / SHA-256 parity, and the package safety boundary.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { sha256Hex, canonicalJson, fingerprintOf } from '../src/canonical.mjs';

const SRC = join(dirname(fileURLToPath(import.meta.url)), '..', 'src');
const NODE_ONLY = new Set(['workbookImport.mjs', 'nodeImport.mjs']);
const files = readdirSync(SRC).filter((f) => f.endsWith('.mjs'));
const read = (f) => readFileSync(join(SRC, f), 'utf8');

test('the pure-JS SHA-256 matches Node’s digest for every padding boundary and for Unicode', () => {
  const cases = ['', 'abc', 'a'.repeat(3), 'a'.repeat(55), 'a'.repeat(56), 'a'.repeat(63), 'a'.repeat(64),
    'a'.repeat(300), 'ไทย ✓ 𝔘𝔫𝔦', '{"k":[1,2,{"x":null}]}', 'é'.repeat(40)];
  for (const c of cases) {
    assert.equal(sha256Hex(c), createHash('sha256').update(c, 'utf8').digest('hex'), JSON.stringify(c).slice(0, 40));
  }
});

test('canonicalJson is independent of object key order and keeps array order', () => {
  assert.equal(canonicalJson({ b: 1, a: { d: 2, c: 3 } }), canonicalJson({ a: { c: 3, d: 2 }, b: 1 }));
  assert.notEqual(canonicalJson([1, 2]), canonicalJson([2, 1]));
});

test('fingerprintOf is a 64-hex digest and changes with any value change', () => {
  const a = fingerprintOf({ x: 1 });
  assert.match(a, /^[0-9a-f]{64}$/);
  assert.notEqual(a, fingerprintOf({ x: 2 }));
});

test('browser-safe modules import no Node built-ins (only relative imports)', () => {
  for (const f of files.filter((x) => !NODE_ONLY.has(x))) {
    const specs = [...read(f).matchAll(/(?:from|import)\s*\(?\s*['"]([^'"]+)['"]/g)].map((m) => m[1]);
    for (const s of specs) {
      assert.ok(s.startsWith('./'), `${f} imports '${s}'`);
    }
  }
});

test('the Node-only importer is the only module that uses node:zlib or node:crypto', () => {
  const users = files.filter((f) => /node:(zlib|crypto)/.test(read(f)));
  assert.deepEqual(users.sort(), ['workbookImport.mjs'].sort());
  assert.ok(NODE_ONLY.has('nodeImport.mjs'));
  // Check the import/export specifiers only; comments may name the Node-only entry.
  const entrySpecs = [...read('index.mjs').matchAll(/(?:from|import)\s*['"]([^'"]+)['"]/g)].map((m) => m[1]);
  assert.ok(entrySpecs.length > 0);
  assert.equal(entrySpecs.some((x) => /workbookImport|nodeImport|^node:/.test(x)), false, 'the browser entry must not load the importer');
});

test('no module in the package reaches the network, a child process, the environment or a socket', () => {
  const forbidden = [
    /\bfetch\s*\(/, /XMLHttpRequest/, /WebSocket/, /node:(net|http|https|tls|dgram|child_process|worker_threads)\b/,
    /\bprocess\.env\b/, /\brequire\s*\(/, /\beval\s*\(/, /new Function\s*\(/, /\bsetInterval\b/,
  ];
  for (const f of files) {
    for (const re of forbidden) assert.doesNotMatch(read(f), re, `${f} matches ${re}`);
  }
});

test('the package defines no write, command or hardware-access identity', () => {
  const forbiddenIdentifiers = /\b(writeTag|writeCoil|writeRegister|forceOutput|commandOutput|activateConfiguration|connectDevice|pollDevice|readDevice)\b/;
  for (const f of files) assert.doesNotMatch(read(f), forbiddenIdentifiers, f);
});
