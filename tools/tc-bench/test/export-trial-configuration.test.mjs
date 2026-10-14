import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { CANONICAL_TC_NODE_CONFIGURATION as canonical } from '../../../packages/commissioning/src/tcNodeConfiguration.mjs';
import { exportCatalog, digest } from '../export-trial-configuration.mjs';

const read = (relative) => readFileSync(new URL(relative, import.meta.url), 'utf8');

test('catalog is byte deterministic and independently hashes manifest and catalog', () => {
  const a = exportCatalog();
  assert.deepEqual(a, exportCatalog());
  assert.equal(a.sha256, digest(a.bytes));
  assert.equal(a.manifestSha256, digest(a.manifest));
  const c = JSON.parse(a.bytes);
  assert.equal(c.schemaVersion, 'wjss.tc-bench-catalog/2');
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
  assert.equal(canonical.nodes[0].portStatus, 'PORT_UNRESOLVED');
});
test('catalog carries no execution authority and no authorization digest', () => {
  const c = JSON.parse(exportCatalog().bytes);
  assert.deepEqual(Object.keys(c.trials[0]).sort(), ['manifestSha256', 'trial']);
  assert.equal(JSON.stringify(c).includes('executionAuthorized'), false);
  assert.equal(JSON.stringify(c).includes('authorizationSha256'), false);
  assert.equal(JSON.stringify(c).includes('authorization'), false);
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
  const text = read('../export-trial-configuration.mjs');
  assert.doesNotMatch(text, /from ['"]node:(net|http|https|dns|dgram)/);
  assert.doesNotMatch(text, /--(ip|port|unit|function|start|quantity)\b/);
});

test('physical entry is gated before any factory and supports no environment activation (static source)', () => {
  const program = read('../../../apps/tc-bench/Program.cs');
  assert.match(program, /PhysicalTrialAuthorization\.Authorize/);
  assert.match(program, /PhysicalTrialRefusals\.DeploymentNotAuthorized/);
  assert.doesNotMatch(program, /GetEnvironmentVariable|Dns\.|new TcpClient|Socket\(/);
  // Exactly two client factory references, one per real execution class, and neither constructs a
  // client in Program: the factory is handed to the runner, which validates before invoking it.
  // Local TCP Simulation: the factory appears only after the fail-closed loopback invocation
  // configuration. Physical: the factory appears only after the authorization gate.
  const factories = [...program.matchAll(/TcReadOnlyClientFactory/g)].map((m) => m.index);
  assert.equal(factories.length, 2);
  const localBranchStart = program.indexOf('BenchMode.LOCAL_TCP_SIMULATION');
  const physicalBranchStart = program.indexOf('BenchMode.PHYSICAL_TEST');
  const dryRunStart = program.indexOf('.DryRun(');
  assert.ok(localBranchStart > -1 && localBranchStart < physicalBranchStart && physicalBranchStart < dryRunStart);
  const localBranch = program.slice(localBranchStart, physicalBranchStart);
  const physicalBranch = program.slice(physicalBranchStart, dryRunStart);
  const dryRun = program.slice(dryRunStart);
  assert.ok(localBranch.indexOf('LocalSimulation.InvocationConfiguration(') < localBranch.indexOf('TcReadOnlyClientFactory'));
  assert.equal((localBranch.match(/RunLocalSimulationAsync\(/g) ?? []).length, 1);
  assert.doesNotMatch(localBranch, /Authoriz|Signature|PhysicalTrial|RunPhysicalTrialAsync|RunSoftwareTrialAsync/);
  assert.ok(physicalBranch.indexOf('PhysicalTrialAuthorization.Authorize') < physicalBranch.indexOf('TcReadOnlyClientFactory'));
  assert.equal((physicalBranch.match(/RunPhysicalTrialAsync\(/g) ?? []).length, 1);
  assert.doesNotMatch(physicalBranch, /LocalSimulation|RunLocalSimulationAsync/);
  // The default dry run still constructs nothing.
  assert.doesNotMatch(dryRun, /TcReadOnlyClientFactory|RunLocalSimulationAsync|RunPhysicalTrialAsync/);
  const policy = read('../../../apps/tc-bench/TrialConfiguration.cs');
  assert.doesNotMatch(policy, /ExecutionAuthorized|AuthorizationSha256/);
  assert.match(policy, /MANIFEST_HASH_MISMATCH/);
  assert.match(policy, /supplied != trusted\.Trial/);
  assert.match(policy, /"--signature"/);
  assert.match(policy, /"--local-simulation"/);
  assert.doesNotMatch(policy, /SoftwareTrial|TcReadOnlyClient|ConnectAsync/);
});

test('local simulation class accepts only the literal loopback text and validates before any client (static source)', () => {
  const local = read('../../../apps/tc-bench/LocalSimulation.cs');
  assert.match(local, /public const string Provenance = "LOCAL_TCP_SIMULATION";/);
  assert.match(local, /public const string LoopbackAddress = "127\.0\.0\.1";/);
  assert.match(local, /public const int SimulatorPort = 502;/);
  // Exact ordinal text comparison: no parsing, resolution, normalization or correction of the address.
  assert.match(local, /string\.Equals\(invocation\.Address, LoopbackAddress, StringComparison\.Ordinal\)/);
  assert.match(local, /LOCAL_SIMULATION_ADDRESS_REJECTED/);
  assert.match(local, /LOCAL_SIMULATION_PORT_REJECTED/);
  assert.match(local, /LOCAL_SIMULATION_REQUEST_REJECTED/);
  assert.match(local, /invocation with \{ Address = trusted\.Address, Port = trusted\.Port \} != trusted/);
  assert.doesNotMatch(local, /IPAddress\.|Dns\.|TcpClient|Socket\(|GetEnvironmentVariable|PhysicalTrialAuthorization|TcReadOnlyClientFactory|ITcReadOnlyClient/);
  // The default (template) address is read from the catalog, never typed as a second literal.
  assert.equal((local.match(/\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b/g) ?? []).length, 1);
  const runner = read('../../../apps/tc-bench/TrialRunner.cs');
  assert.match(runner, /public async Task<TrialEvidence> RunLocalSimulationAsync\(TrialCatalog catalog, TrialConfiguration invocation, Func<ITcReadOnlyClient> factory, CancellationToken cancellationToken\)/);
  const localEntry = runner.slice(runner.indexOf('RunLocalSimulationAsync('), runner.indexOf('RunSoftwareTrialAsync('));
  assert.ok(localEntry.indexOf('LocalSimulation.RequireValid(catalog, invocation)') < localEntry.indexOf('ExecuteAsync('));
  assert.match(localEntry, /Provenance = LocalSimulation\.Provenance/);
  assert.doesNotMatch(localEntry, /PhysicalTrialPlan|Authoriz|PHYSICAL_DIAGNOSTIC|SYNTHETIC_TEST_ONLY/);
  const evidence = read('../../../apps/tc-bench/TrialEvidence.cs');
  assert.match(evidence, /evidence\.Provenance == LocalSimulation\.Provenance/);
  assert.match(evidence, /LOCAL_SIMULATION_ENDPOINT_REJECTED/);
});

test('authorization verification is detached and uses platform cryptography only (static source)', () => {
  const authorization = read('../../../apps/tc-bench/PhysicalTrialAuthorization.cs');
  assert.match(authorization, /ImportSubjectPublicKeyInfo/);
  assert.match(authorization, /HashAlgorithmName\.SHA256/);
  assert.match(authorization, /DSASignatureFormat\.IeeeP1363FixedFieldConcatenation/);
  assert.match(authorization, /FileMode\.CreateNew/);
  assert.equal((authorization.match(/FileMode\.CreateNew/g) ?? []).length, 1);
  // Verification only: no key generation, no private-key import, no export of private material.
  assert.doesNotMatch(authorization, /ImportECPrivateKey|ImportPkcs8PrivateKey|ExportECPrivateKey|ExportPkcs8PrivateKey|GenerateKey|ECDsa\.Create\(ECCurve/);
  assert.equal((authorization.match(/VerifyData\(/g) ?? []).length, 1);
  // No endpoint may be resolved or contacted by the gate.
  assert.doesNotMatch(authorization, /IPAddress\.Parse|Dns\.|TcpClient|Socket\(|Connect\(/);
});

test('build identity defaults to physically disabled and is generated, not committed', () => {
  const project = read('../../../apps/tc-bench/Wjss.TcBench.csproj');
  assert.match(project, /<WjssPhysicalCapabilityEnabled[^>]*>false<\/WjssPhysicalCapabilityEnabled>/);
  assert.match(project, /<WjssImplementationSha[^>]*>NOT_PROVIDED<\/WjssImplementationSha>/);
  assert.match(project, /<WjssSourceRef[^>]*>NOT_PROVIDED<\/WjssSourceRef>/);
  assert.match(project, /<WjssDeploymentProof[^>]*>NOT_PROVIDED<\/WjssDeploymentProof>/);
  assert.match(project, /<WjssOwnerPublicKey[^>]*>NOT_PROVIDED<\/WjssOwnerPublicKey>/);
  assert.match(project, /LogicalName="TcBenchBuildIdentity\.txt"/);
  assert.match(project, /WriteLinesToFile/);
  // Build metadata is written into ignored obj output; no package, key material or secret is added.
  assert.match(project, /<TrialCatalogDirectory>[^<]*obj\/tc-bench<\/TrialCatalogDirectory>/);
  assert.match(project, /<TrialBuildIdentityFile>[^<]*build-identity\.txt<\/TrialBuildIdentityFile>/);
  assert.doesNotMatch(project, /<PackageReference|-----BEGIN|MFkwEwYHKo|sessionKey|PrivateKey/i);
});
test('the physical-capable build requires explicit trust metadata and fails closed', () => {
  const identity = read('../../../apps/tc-bench/BuildIdentity.cs');
  assert.match(identity, /ValidateForPhysicalUse/);
  assert.match(identity, /DeploymentProofClean/);
  assert.match(identity, /LoadTrusted/);
  assert.doesNotMatch(identity, /GetEnvironmentVariable|Process\.Start|git |Dns\./);
});

test('one-shot runner seam is loopback-only; no detached retry or automatic acceptance (static source)', () => {
  const runner = read('../../../apps/tc-bench/TrialRunner.cs');
  assert.match(runner, /internal async Task<TrialEvidence> RunSoftwareTrialAsync/);
  assert.match(runner, /loopback\.IpAddress != "127\.0\.0\.1"/);
  assert.match(runner, /loopback\.Port == 502/);
  assert.doesNotMatch(runner, /TrialAcceptance\.ACCEPTED|while\s*\(|PeriodicTimer/);
  assert.equal((runner.match(/client\.ConnectAsync\(/g) ?? []).length, 1);
  assert.equal((runner.match(/client\.ReadAsync\(/g) ?? []).length, 1);
  // The synchronous handshake is isolated from scheduling cancellation and always observed.
  assert.match(runner, /Task\.Run\(\(\) => client\.ConnectAsync\(execution\.Endpoint, operationCts\.Token\), CancellationToken\.None\)/);
  // Cleanup is always awaited to terminal completion; only the observation budget flags it overdue.
  assert.match(runner, /CompleteWithinBudgetAsync\(client\.DisconnectAsync\(\)/);
  assert.match(runner, /CompleteWithinBudgetAsync\(client\.DisposeAsync\(\)\.AsTask\(\)/);
  assert.match(runner, /await task\.ConfigureAwait\(false\)/);
  assert.match(runner, /public async Task<TrialEvidence> RunPhysicalTrialAsync\(PhysicalTrialPlan plan/);
});
test('adapter endpoint/count checks and copied memory are present (static source, not execution)', () => {
  const adapter = read('../../../adapters/tc-acquisition/' + ['Fluent', 'Mo', 'dbus', 'TcClient.cs'].join(''));
  assert.match(adapter, /request\.Endpoint != connectedEndpoint/);
  assert.match(adapter, /words\.Length != request\.Quantity/);
  assert.equal((adapter.match(/words = CopyBigEndianWords/g) ?? []).length, 2);
  assert.match(adapter, /var ownedBytes = payload\.ToArray\(\)/);
  assert.equal((adapter.match(/payload\.Length != request\.Quantity \* 2/g) ?? []).length, 2);
  assert.doesNotMatch(adapter, /\.WriteSingle|\.WriteMultiple|\.ReadWriteMultiple/);
});
