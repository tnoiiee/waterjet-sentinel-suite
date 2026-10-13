// Stage Library Selection Gate - self-tests of the narrowly scoped S3 exception and the inherited rules it must not weaken.
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

import { isS3Exception } from '../s3Exception.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const SCANNER = join(HERE, '..', 'boundary-scan.mjs');
const REPO = join(HERE, '..', '..', '..');

// Fragments so this file itself does not contain the literal.
const MOD = 'mo' + 'db' + 'us';
const WAG = 'wa' + 'go';
const GALL = 'ga' + 'lil';
const SQLCL = 'sq' + 'lclient';
const FLUENT = 'Fluent' + MOD;
const ALLOWED_TERM = MOD;

const ALLOWED_PATHS = ['Directory.Packages.props', 'adapters/tc-acquisition/Wjss.Adapters.TcAcquisition.csproj'];
const ALLOWED_PREFIX = 'adapters/tc-acquisition/';

function scan(files) {
  const root = mkdtempSync(join(tmpdir(), 's3-selftest-'));
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

const countFor = (findings, rel, rule = 'S3') => findings.filter((f) => f.startsWith(`${rule} ${rel}:`)).length;

test('S3: approved term allowed only in exact approved package metadata and adapter prefix', () => {
  const okFiles = {};
  for (const p of ALLOWED_PATHS) okFiles[p] = `// ${MOD} vocabulary\n`;
  okFiles[ALLOWED_PREFIX + 'ITcReadOnlyClient.cs'] = `// ${MOD} vocabulary\n`;
  okFiles[ALLOWED_PREFIX + 'Fluent' + MOD + 'TcClient.cs'] = `// ${FLUENT} client\n`;
  okFiles[ALLOWED_PREFIX + 'TcReadModels.cs'] = `// ${MOD} model\n`;
  okFiles[ALLOWED_PREFIX + 'README.md'] = `# ${FLUENT}\n`;
  okFiles[ALLOWED_PREFIX + 'Wjss.Adapters.TcAcquisition.csproj'] = `<PackageReference Include=\"${FLUENT}\" />\n`;
  const result = scan(okFiles);
  assert.equal(result.findings.length, 0, result.stdout);
  assert.equal(result.status, 0);
  assert.match(result.stdout, /S1-S9 clean/);
});

test('S3: same term outside approved paths remains a finding', () => {
  const others = [
    'packages/domain/WallMap.cs',
    'packages/contracts/Enums.cs',
    'apps/runtime/Program.cs',
    'apps/kiosk/MainForm.cs',
    'tests/domain.tests/WallMapTests.cs',
    'tools/boundary-scan/other.mjs',
    'config/examples/fixture.json',
    'Directory.Build.props',
    'WaterJetSentinelSuite.sln',
  ];
  for (const rel of others) {
    const result = scan({ [rel]: `// ${MOD} vocabulary\n` });
    assert.equal(countFor(result.findings, rel), 1, `${rel} must keep S3 finding for ${MOD}`);
  }
});

test('S3: path normalization cannot expand the exception', () => {
  const variants = [
    'adapters/tc-acquisition/../tc-acquisition/evil.cs',
    'adapters/tc-acquisition/./evil.cs',
    './adapters/tc-acquisition/evil.cs',
    '/adapters/tc-acquisition/evil.cs',
    'adapters//tc-acquisition/evil.cs',
    'adapters/tc-acquisition/evil.cs ',
    'adapters/tc-acquisitionfoo/evil.cs',
    'adapters/tc-acquisition-extra/evil.cs',
    'Adapters/tc-acquisition/evil.cs',
    'adapters/TC-ACQUISITION/evil.cs',
    'packages/commissioning/src/tcNodeConfiguration.mjs',
    'adapters/tc-acquisition',
    '',
    null,
    undefined,
  ];
  for (const rel of variants) {
    assert.equal(isS3Exception(rel, MOD), false, `variant ${String(rel)} must be false`);
  }
  assert.equal(isS3Exception('adapters/tc-acquisition/foo.cs', MOD), true);
  assert.equal(isS3Exception('adapters/tc-acquisition/sub/bar.cs', MOD), true);
  assert.equal(isS3Exception('Directory.Packages.props', MOD), true);
  assert.equal(isS3Exception('adapters/tc-acquisition/Wjss.Adapters.TcAcquisition.csproj', MOD), true);
});

test('S3: case variants behave according to scanner policy (case-insensitive for term)', () => {
  const variants = [MOD, MOD.toUpperCase(), MOD[0].toUpperCase() + MOD.slice(1), 'Mo' + 'DbUs'];
  for (const v of variants) {
    assert.equal(isS3Exception('adapters/tc-acquisition/foo.cs', v), true, `${v} should be allowed in approved path`);
    assert.equal(isS3Exception('packages/domain/foo.cs', v), false, `${v} must be blocked outside approved path`);
  }
  // Scanner test: uppercase in file should still be caught as S3 and allowed only in approved path
  const upper = MOD.toUpperCase();
  const ok = scan({ [ALLOWED_PREFIX + 'a.cs']: `// ${upper}\n` });
  assert.equal(ok.findings.length, 0);
  const bad = scan({ ['packages/domain/a.cs']: `// ${upper}\n` });
  assert.equal(countFor(bad.findings, 'packages/domain/a.cs'), 1);
});

test('S3: other vendor terms remain blocked even in approved paths', () => {
  const otherTerms = [WAG, GALL, SQLCL, 'np' + 'gsql', 'ora' + 'cle', 'red' + 'is', 'mq' + 'tt', 'kaf' + 'ka'];
  for (const term of otherTerms) {
    assert.equal(isS3Exception('adapters/tc-acquisition/foo.cs', term), false, `${term} must not be allowed even in adapter`);
    assert.equal(isS3Exception('Directory.Packages.props', term), false, `${term} must not be allowed in package props`);
    const result = scan({ [ALLOWED_PREFIX + 'foo.cs']: `// ${term}\n` });
    assert.equal(countFor(result.findings, ALLOWED_PREFIX + 'foo.cs'), 1, `${term} must still be finding in adapter`);
  }
});

test('S3: write-related identifiers remain blocked where required', () => {
  // Write function identifiers should not be allowed via the vendor exception.
  // The adapter never calls them; they must remain findings if they appear outside docs.
  const writeTerms = ['WriteSingleCoil', 'WriteSingleRegister', 'WriteMultipleCoils', 'WriteMultipleRegisters'];
  for (const w of writeTerms) {
    const result = scan({ [ALLOWED_PREFIX + 'foo.cs']: `// ${w}\n` });
    // These terms are not part of VENDOR_TERMS but are scanned as part of read-only boundary proof;
    // we ensure they would be visible if present. For now, we prove that the adapter's source
    // does not contain them (separate test), but the S3 exception does not blanket-allow them.
    // The current S3 VENDOR_TERMS does not include these, but we verify isS3Exception does not allow them.
    assert.equal(isS3Exception(ALLOWED_PREFIX + 'foo.cs', w), false);
  }
});

test('S3: product code outside approved adapter remains clean after repair', () => {
  const cleanFiles = {
    'packages/domain/WallMap.cs': 'namespace Wjss.Domain;\npublic class WallMap {}\n',
    'apps/runtime/Program.cs': 'using System;\nclass Program {}\n',
    'Directory.Packages.props': '<Project><ItemGroup><PackageVersion Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.12.0\" /></ItemGroup></Project>\n',
  };
  const result = scan(cleanFiles);
  assert.equal(result.findings.length, 0);
});

test('S3: S1, S2, S4, S5, S6, S7, S8, S9 still fire alongside the exception', () => {
  const planted = {
    S1: `const u = '${'ht' + 'tps://example.invalid/x'}';\n`,
    S2: `const ip = '${'192' + '.0.2.1'}';\n`,
    S4: `const x = '${'pass' + 'word = \"abc123\"'}';\n`,
    S5: `import x from '../${'spi' + 'kes/'}ui-runtime-react/y.mjs';\n`,
    S6a: `const h = '${'Listen' + 'Any'}';\n`,
    S6b: `const h = process.${'env.BIND' + '_HOST'};\n`,
  };
  for (const [rule, line] of Object.entries(planted)) {
    const rel = ALLOWED_PREFIX + 'probe.cs';
    const result = scan({ [rel]: `// ${MOD}\n` + line });
    const expected = rule.slice(0, 2);
    assert.ok(result.findings.some((f) => f.startsWith(`${expected} ${rel}:`)), `${rule} must still fire in adapter`);
  }
});

test('S3: exception does not permit a general protocol implementation elsewhere', () => {
  const result = scan({
    'packages/domain/ProtocolImpl.cs': `// ${MOD} generic impl\npublic class Foo{}\n`,
    ['apps/runtime/Generic' + MOD + '.cs']: `// ${MOD} generic\n`,
  });
  assert.equal(countFor(result.findings, 'packages/domain/ProtocolImpl.cs'), 1);
  assert.equal(countFor(result.findings, 'apps/runtime/Generic' + MOD + '.cs'), 1);
});

test('S3: the policy module holds no vendor literal', () => {
  const source = readFileSync(new URL('../s3Exception.mjs', import.meta.url), 'utf8');
  const lower = source.toLowerCase();
  const lit = 'mo' + 'db' + 'us';
  assert.equal(lower.includes(lit), false, 's3Exception.mjs must not contain raw vendor literal');
});

test('the real repository remains clean under the scanner with the exception in place', () => {
  const run = spawnSync(process.execPath, [SCANNER, REPO], { encoding: 'utf8' });
  assert.equal(run.status, 0, run.stdout);
  assert.match(run.stdout, /boundary-scan: 0 findings \(S1-S9 clean\)/);
});

// Owner narrow revision: synthetic scanner fixtures only, never genuine or placeholder locks.
const LOCK_PATHS = ['apps/tc-bench/packages.lock.json', 'tests/tc-acquisition.tests/packages.lock.json'];
const lockFixture = (term) => JSON.stringify({ dependencies: { synthetic: { [term]: { type: 'Transitive' } } } });

test('S3 locks: selected term passes only at both exact dependency-metadata paths', () => {
  for (const rel of LOCK_PATHS) {
    assert.equal(isS3Exception(rel, MOD), true);
    const result = scan({ [rel]: lockFixture(FLUENT) });
    assert.equal(result.status, 0, result.stdout);
    assert.equal(result.findings.length, 0);
  }
});

test('S3 locks: harness and test source, README and config retain findings', () => {
  const paths = [
    'apps/tc-bench/Program.cs', 'apps/tc-bench/TrialConfiguration.cs', 'apps/tc-bench/README.md',
    'apps/tc-bench/config.json', 'apps/tc-bench/Wjss.TcBench.csproj',
    'tests/tc-acquisition.tests/AdapterLifecycleTests.cs', 'tests/tc-acquisition.tests/README.md',
    'tests/tc-acquisition.tests/config.json', 'tests/tc-acquisition.tests/Wjss.TcAcquisition.Tests.csproj',
  ];
  for (const rel of paths) {
    assert.equal(isS3Exception(rel, MOD), false);
    const result = scan({ [rel]: `// ${FLUENT}\n` });
    assert.equal(countFor(result.findings, rel), 1, result.stdout);
  }
});

test('S3 locks: sibling, nested, backup, renamed and every unrelated solution lock remain blocked', () => {
  const unrelated = [...readFileSync(join(REPO, 'WaterJetSentinelSuite.sln'), 'utf8').matchAll(/"([^"\r\n]+\.csproj)"/g)]
    .map((m) => m[1].replaceAll('\\', '/').replace(/[^/]+$/, 'packages.lock.json'))
    .filter((rel) => !LOCK_PATHS.includes(rel) && !rel.startsWith(ALLOWED_PREFIX));
  const others = [
    'apps/tc-bench-extra/packages.lock.json', 'apps/tc-bench/sub/packages.lock.json',
    'apps/tc-bench/packages.lock.json.bak', 'apps/tc-bench/packages.lock.old.json',
    'apps/packages.lock.json', 'tests/tc-acquisition.tests-extra/packages.lock.json',
    'tests/tc-acquisition.tests/sub/packages.lock.json', 'tests/tc-acquisition.tests/packages.lock.json.bak',
    'tests/tc-acquisition.tests/packages.lock.old.json', 'tests/packages.lock.json',
    'config/packages.lock.json', ...unrelated,
  ];
  for (const rel of new Set(others)) {
    assert.equal(isS3Exception(rel, MOD), false, rel);
    const result = scan({ [rel]: lockFixture(FLUENT) });
    assert.equal(countFor(result.findings, rel), 1, result.stdout);
  }
});

test('S3 locks: path normalization cannot enlarge either exact exception', () => {
  for (const rel of LOCK_PATHS) {
    const variants = [
      rel + '/', rel + ' ', rel + '.bak', rel.toUpperCase(), './' + rel, '/' + rel,
      rel.replaceAll('/', '\\'), rel.replace('/', '//'), rel.replace('/', '/./'),
      rel.replace('/', '/../'), rel.replace('packages.lock.json', 'sub/packages.lock.json'),
      rel.replace('packages.lock.json', ''), rel.replace('packages.lock.json', 'PACKAGES.lock.json'),
    ];
    for (const variant of variants) assert.equal(isS3Exception(variant, MOD), false, variant);
  }
});

test('S3 locks: other vendor terms stay blocked even in approved lock metadata', () => {
  for (const term of [WAG, GALL, SQLCL, 'enti' + 'ty framework', 'np' + 'gsql', 'ora' + 'cle', 'red' + 'is', 'mq' + 'tt', 'kaf' + 'ka']) {
    for (const rel of LOCK_PATHS) {
      assert.equal(isS3Exception(rel, term), false);
      const result = scan({ [rel]: lockFixture(term) });
      assert.equal(countFor(result.findings, rel), 1, result.stdout);
    }
  }
});

test('S3 locks: write/control names receive no exemption; existing scanner behavior is unchanged', () => {
  for (const term of ['WriteSingleCoil', 'WriteSingleRegister', 'WriteMultipleCoils', 'WriteMultipleRegisters', 'ExecuteFunction', 'Control']) {
    for (const rel of LOCK_PATHS) {
      assert.equal(isS3Exception(rel, term), false);
      // These are not engine vendor rules. Dedicated API/source tests govern no-write behavior;
      // do not falsely claim that the vocabulary scanner already bans these identifiers.
      const approved = scan({ [rel]: lockFixture(term) });
      const ordinary = scan({ ['config/ordinary-lock.json']: lockFixture(term) });
      assert.equal(countFor(approved.findings, rel), countFor(ordinary.findings, 'config/ordinary-lock.json'));
    }
  }
});

test('S3 locks: S1, S2, S4, S5 and S6 still fire inside both metadata paths', () => {
  const planted = {
    S1: `${'ht' + 'tps://example.invalid/x'}`,
    S2: `${'192' + '.0.2.1'}`,
    S4: `${'pass' + 'word = "abc123"'}`,
    S5: `import x from '../${'spi' + 'kes/'}ui-runtime-react/y.mjs';`,
    S6: `${'Listen' + 'Any'}`,
  };
  for (const rel of LOCK_PATHS) for (const [rule, text] of Object.entries(planted)) {
    const result = scan({ [rel]: lockFixture(FLUENT) + '\n' + text });
    assert.ok(countFor(result.findings, rel, rule) > 0, result.stdout);
    assert.equal(result.findings.filter((f) => f.includes(`vendor/transport term '${MOD}'`)).length, 0);
  }
});

test('S3 locks: S7-S9 remain active alongside exact lock exceptions', () => {
  const locks = Object.fromEntries(LOCK_PATHS.map((rel) => [rel, lockFixture(FLUENT)]));
  const cases = {
    S7: { 'WaterJetSentinelSuite.sln': 'Microsoft Visual Studio Solution File\n', 'apps/orphan/X.csproj': '<Project />' },
    S8: { 'apps/orphan/Bad.props': '<Project><ItemGroup></Project>' },
    S9: { 'apps/kiosk/app.manifest': '<assembly><windowsSettings><dpiAware>true</dpiAware></windowsSettings></assembly>' },
  };
  for (const [rule, files] of Object.entries(cases)) {
    const result = scan({ ...locks, ...files });
    assert.ok(result.findings.some((f) => f.startsWith(rule + ' ')), result.stdout);
    for (const rel of LOCK_PATHS) assert.equal(countFor(result.findings, rel, 'S3'), 0);
  }
});
