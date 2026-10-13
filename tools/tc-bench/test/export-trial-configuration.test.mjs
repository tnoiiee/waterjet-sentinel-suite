import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { CANONICAL_TC_NODE_CONFIGURATION as canonical } from '../../../packages/commissioning/src/tcNodeConfiguration.mjs';
import { exportCatalog, digest } from '../export-trial-configuration.mjs';

test('catalog is byte deterministic and independently hashes manifest and catalog', () => {
  const a = exportCatalog();
  assert.deepEqual(a, exportCatalog());
  assert.equal(a.sha256, digest(a.bytes));
  assert.equal(a.manifestSha256, digest(a.manifest));
  const c = JSON.parse(a.bytes);
  assert.equal(c.schemaVersion, 'wjss.tc-bench-catalog/1');
  assert.equal(c.trials.length, 1);
  assert.equal(c.trials[0].manifestSha256, digest(a.manifest));
});
test('tuple comes from canonical source; T01 has no execution authority', () => {
  const t = JSON.parse(exportCatalog().manifest);
  assert.equal(t.address, canonical.nodes[0].ipAddress);
  assert.equal(t.nodeId, canonical.nodes[0].nodeId);
  assert.equal(t.cabinetId, canonical.nodes[0].cabinetId);
  assert.equal(t.port, 502);
  assert.equal(t.functionCode, 3);
  assert.equal(t.unitId, 0);
  assert.equal(t.start, 0);
  assert.equal(t.quantity, 40);
  assert.equal(t.endianness, 'BigEndian');
  assert.equal(t.physicalVerification, 'NOT_VERIFIED');
  assert.equal(t.executionAuthorized, false);
  assert.equal(canonical.nodes[0].portStatus, 'PORT_UNRESOLVED');
});
for (const [name, mutate] of [
  ['unknown field', (c) => { c.extra = true; }],
  ['unknown node field', (c) => { c.nodes[0].extra = true; }],
  ['wrong address', (c) => { c.nodes[0].ipAddress = c.nodes[1].ipAddress; }],
  ['wrong node', (c) => { c.nodes[0].nodeId = c.nodes[1].nodeId; }],
  ['wrong cabinet', (c) => { c.nodes[0].cabinetId = c.nodes[1].cabinetId; }],
  ['numeric port field', (c) => { c.nodes[0].port = 502; }],
  ['multiple copies', (c) => { c.nodes.push(c.nodes[0]); }],
  ['missing node', (c) => { c.nodes.pop(); }],
]) test(`reject ${name}`, () => {
  const c = structuredClone(canonical); mutate(c);
  assert.throws(() => exportCatalog(c));
});
test('source keeps offline imports and no endpoint arguments', () => {
  const text = readFileSync(new URL('../export-trial-configuration.mjs', import.meta.url), 'utf8');
  assert.doesNotMatch(text, /from ['"]node:(net|http|https|dns|dgram)/);
  assert.doesNotMatch(text, /--(ip|port|unit|function|start|quantity)\b/);
});

test('CLI activation is blocked before any factory and supports no environment activation (static source)', () => {
  const program = readFileSync(new URL('../../../apps/tc-bench/Program.cs', import.meta.url), 'utf8');
  assert.match(program, /RequirePhysicalAuthorization/);
  assert.match(program, /PHYSICAL_DEPLOYMENT_NOT_AUTHORIZED/);
  assert.doesNotMatch(program, /TcReadOnlyClientFactory|ConnectAsync|ReadAsync|GetEnvironmentVariable|Dns\./);
  const policy = readFileSync(new URL('../../../apps/tc-bench/TrialConfiguration.cs', import.meta.url), 'utf8');
  assert.match(policy, /!entry\.Trial\.ExecutionAuthorized/);
  assert.match(policy, /AUTHORIZATION_HASH_MISMATCH/);
  assert.match(policy, /MANIFEST_HASH_MISMATCH/);
  assert.match(policy, /supplied != trusted\.Trial/);
});
test('one-shot runner seam is loopback-only; no detached retry or automatic acceptance (static source)', () => {
  const runner = readFileSync(new URL('../../../apps/tc-bench/TrialRunner.cs', import.meta.url), 'utf8');
  assert.match(runner, /internal async Task<TrialEvidence> RunSoftwareTrialAsync/);
  assert.match(runner, /loopback\.IpAddress != "127\.0\.0\.1"/);
  assert.match(runner, /loopback\.Port == 502/);
  assert.doesNotMatch(runner, /TrialAcceptance\.ACCEPTED|while\s*\(|PeriodicTimer/);
  assert.equal((runner.match(/client\.ConnectAsync\(/g) ?? []).length, 1);
  assert.equal((runner.match(/client\.ReadAsync\(/g) ?? []).length, 1);
  assert.match(runner, /await Task\.Run/);
  assert.match(runner, /await client\.DisconnectAsync/);
  assert.match(runner, /await client\.DisposeAsync/);
});
test('adapter endpoint/count checks and copied memory are present (static source, not execution)', () => {
  const adapter = readFileSync(new URL('../../../adapters/tc-acquisition/' + ['Fluent', 'Mo', 'dbus', 'TcClient.cs'].join(''), import.meta.url), 'utf8');
  assert.match(adapter, /request\.Endpoint != connectedEndpoint/);
  assert.match(adapter, /words\.Length != request\.Quantity/);
  assert.equal((adapter.match(/words = CopyBigEndianWords/g) ?? []).length, 2);
  assert.match(adapter, /var ownedBytes = payload\.ToArray\(\)/);
  assert.equal((adapter.match(/payload\.Length != request\.Quantity \* 2/g) ?? []).length, 2);
  assert.doesNotMatch(adapter, /\.WriteSingle|\.WriteMultiple|\.ReadWriteMultiple/);
});
