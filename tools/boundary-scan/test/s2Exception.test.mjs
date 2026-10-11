// Stage 0.4C-K2 - self-tests of the narrowly scoped S2 exception and the inherited rules it must not weaken.
//
// Each case runs the REAL boundary scanner against a temporary root outside the repository. Every
// address and every rule-violating token is generated at runtime from fragments, so this test file
// itself contains no non-loopback address literal and no governed vocabulary. Nothing is written
// into the repository.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { isS2ExceptionAddress, s2ExceptionAddresses, S2_EXCEPTION_PATH } from '../s2Exception.mjs';
import { APPROVED_TEST_ADDRESSES } from '../../../packages/commissioning/src/tcNodeConfiguration.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const SCANNER = join(HERE, '..', 'boundary-scan.mjs');
const REPO = join(HERE, '..', '..', '..');
const CANON = S2_EXCEPTION_PATH;
const approved = s2ExceptionAddresses();
const ip = (...octets) => octets.join('.');
const SPIKE = 'spi' + 'kes/';

/** Runs the real scanner on a temporary tree and returns its exit status and finding lines. */
function scan(files) {
  const root = mkdtempSync(join(tmpdir(), 's2-selftest-'));
  try {
    for (const [rel, content] of Object.entries(files)) {
      const full = join(root, rel);
      mkdirSync(dirname(full), { recursive: true });
      writeFileSync(full, content, 'utf8');
    }
    const run = spawnSync(process.execPath, [SCANNER, root], { encoding: 'utf8' });
    const findings = run.stdout.split('\n').filter((line) => /^S\d /.test(line));
    return { status: run.status, findings, stdout: run.stdout };
  } finally {
    rmSync(root, { recursive: true, force: true });
  }
}

const countFor = (findings, rel, rule = 'S2') => findings.filter((f) => f.startsWith(`${rule} ${rel}:`)).length;
const sixLines = () => approved.map((a) => `const a = '${a}';`).join('\n') + '\n';

test('S2: all six approved Test addresses pass in the canonical source path', () => {
  const result = scan({ [CANON]: sixLines() });
  assert.deepEqual(result.findings, []);
  assert.equal(result.status, 0);
  assert.match(result.stdout, /S1-S9 clean/);
});

test('S2: each approved address fails in every other Product path, and documentation stays unaffected', () => {
  const others = [
    'packages/commissioning/src/schedule.mjs',
    'packages/commissioning/test/fixture.test.mjs',
    'apps/ops-preview/server.mjs',
    'apps/ops-preview/public/app.mjs',
    'tests/config.tests/Fixture.cs',
    'tools/boundary-scan/fixture.mjs',
    'config/examples/fixture.json',
    'Directory.Build.props',
  ];
  for (const rel of others) {
    const result = scan({ [rel]: sixLines() });
    assert.equal(countFor(result.findings, rel), 6, `${rel} must keep all six S2 findings`);
  }
  const docs = scan({ 'docs/fixture.md': sixLines() });
  assert.equal(docs.findings.length, 0, 'documentation may discuss the addresses');
});

test('S2: addresses immediately outside the approved set fail, including the ambiguous leading-zero form', () => {
  const outside = [ip(192, 0, 2, 10), ip(192, 0, 2, 17), ip(192, 0, 2, 0), ip(192, 0, 2, 255), ip(192, 0, 2, 1), ip(192, 0, 2, 0) + approved[0].split('.')[3]];
  for (const address of outside) {
    const result = scan({ [CANON]: `const a = '${address}';\n` });
    assert.equal(countFor(result.findings, CANON), 1, `${address} must fail in the canonical path`);
  }
});

test('S2: another non-loopback IPv4 address fails in the canonical path', () => {
  const others = [ip(10, 0, 0, 5), ip(198, 51, 100, 11), ip(203, 0, 113, 11), ip(172, 16, 0, 1)];
  for (const address of others) {
    const result = scan({ [CANON]: `const a = '${address}';\n` });
    assert.equal(countFor(result.findings, CANON), 1, address);
  }
});

test('S2: similar, prefix-matching and relocated paths receive no exception', () => {
  const variants = [
    'packages/commissioning/src/tcNodeConfiguration.mjs.bak',
    'packages/commissioning/src/tcNodeConfiguration.mj',
    'packages/commissioning/src/tcNodeConfigurationX.mjs',
    'packages/commissioning/src/sub/tcNodeConfiguration.mjs',
    'packages/commissioning/src/TcNodeConfiguration.mjs',
    'packages/commissioning/tcNodeConfiguration.mjs',
    'apps/ops-preview/src/tcNodeConfiguration.mjs',
  ];
  const files = Object.fromEntries(variants.map((rel) => [rel, sixLines()]));
  const result = scan(files);
  for (const rel of variants) assert.equal(countFor(result.findings, rel), 6, rel);
});

test('S2: path spellings that could map onto the canonical file are refused by the policy', () => {
  const spellings = [
    'packages/commissioning/src/../src/tcNodeConfiguration.mjs',
    './packages/commissioning/src/tcNodeConfiguration.mjs',
    '/packages/commissioning/src/tcNodeConfiguration.mjs',
    'packages\\commissioning\\src\\tcNodeConfiguration.mjs',
    'packages/commissioning/src/tcNodeConfiguration.mjs ',
    'packages/commissioning//src/tcNodeConfiguration.mjs',
    'PACKAGES/commissioning/src/tcNodeConfiguration.mjs',
    'packages/commissioning/src/tcNodeConfiguration.mjs/',
    '',
    null,
    undefined,
  ];
  for (const spelling of spellings) {
    for (const address of approved) {
      assert.equal(isS2ExceptionAddress(spelling, address), false, `${String(spelling)} + ${address}`);
    }
  }
});

test('S2: address spellings and near-matches never receive the exception, even at the canonical path', () => {
  const near = [` ${approved[0]}`, `${approved[0]} `, `${approved[0]}.0`, `${approved[0]}0`, ip(192, 0, 2, 0) + approved[0].split('.')[3], '', null, 7];
  for (const address of near) {
    assert.equal(isS2ExceptionAddress(CANON, address), false, JSON.stringify(address));
  }
  for (const address of approved) assert.equal(isS2ExceptionAddress(CANON, address), true, address);
});

test('S2: no prefix, subnet or wildcard form is accepted, even at the canonical path', () => {
  for (const address of [ip(192, 0, 2, 1), ip(192, 0, 2, 10), ip(192, 0, 2, 17), ip(192, 0, 2, 0), '192.0.2.', ip(192, 0, 2, 0) + '/24', '192.0.2.*']) {
    assert.equal(isS2ExceptionAddress(CANON, address), false, address);
  }
});

test('S2: the policy names exactly the six canonical addresses, once each, and nothing else', () => {
  assert.equal(approved.length, 6);
  assert.equal(new Set(approved).size, 6);
  assert.deepEqual([...approved].sort(), [...APPROVED_TEST_ADDRESSES].sort(), 'one authority: the canonical source');
});

test('S2: the policy module holds no dotted address literal, so the approved set is not duplicated', () => {
  const source = readFileSync(join(HERE, '..', 's2Exception.mjs'), 'utf8');
  assert.equal(/\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b/.test(source), false);
});

test('S2: loopback handling is unchanged: the loopback address passes and the other 127 addresses still fail', () => {
  const loopback = scan({ 'apps/fixture/a.mjs': `const a = '${ip(127, 0, 0, 1)}';\n` });
  assert.equal(loopback.findings.length, 0);
  const other = scan({ 'apps/fixture/b.mjs': `const a = '${ip(127, 0, 0, 2)}';\n` });
  assert.equal(countFor(other.findings, 'apps/fixture/b.mjs'), 1);
});

test('S1, S3, S4, S5 and S6 still fire inside the canonical path, alongside the approved addresses', () => {
  const planted = {
    S1: `const u = '${'ht' + 'tps://example.invalid/x'}';\n`,
    S3: `// ${'mo' + 'dbus'} vocabulary\n`,
    S4: `const x = '${'pass' + 'word = "abc123"'}';\n`,
    S5: `import x from '../${SPIKE}ui-runtime-react/y.mjs';\n`,
    S6a: `const h = '${'Listen' + 'Any'}';\n`,
    S6b: `const h = process.${'env.BIND' + '_HOST'};\n`,
  };
  for (const [rule, line] of Object.entries(planted)) {
    const result = scan({ [CANON]: sixLines() + line });
    const expected = rule.slice(0, 2);
    assert.ok(result.findings.some((f) => f.startsWith(`${expected} ${CANON}:`)), `${rule} must still fire`);
    assert.equal(countFor(result.findings, CANON), 0, `${rule} fixture must not add S2 findings`);
  }
});

test('S7, S8 and S9 still fire under the unchanged rules', () => {
  const s7 = scan({ 'WaterJetSentinelSuite.sln': 'Microsoft Visual Studio Solution File\n', 'apps/fixture/X.csproj': '<Project />\n' });
  assert.ok(s7.findings.some((f) => f.startsWith('S7 ')));
  const s8 = scan({ 'apps/fixture/Bad.csproj': '<Project><PropertyGroup></Project>\n' });
  assert.ok(s8.findings.some((f) => f.startsWith('S8 ')));
  const s9 = scan({ 'apps/kiosk/app.manifest': '<assembly><windowsSettings><dpiAware>true</dpiAware></windowsSettings></assembly>\n' });
  assert.ok(s9.findings.some((f) => f.startsWith('S9 ')));
});

test('the real repository remains clean under the scanner with the exception in place', () => {
  const run = spawnSync(process.execPath, [SCANNER, REPO], { encoding: 'utf8' });
  assert.equal(run.status, 0, run.stdout);
  assert.match(run.stdout, /boundary-scan: 0 findings \(S1-S9 clean\)/);
});
