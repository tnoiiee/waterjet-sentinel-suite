// Stage 0.4C-K2 - focused tests of the canonical TC Node / Cabinet / Test IP configuration.
//
// The approved Test addresses are NOT repeated here. They are read from the canonical source and
// cross-checked against the boundary-scan S2 policy, so the exact set has one authority per side.
// Negative-case inputs are built from octets: the boundary exception permits the approved addresses
// only inside the canonical source file.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import * as cfg from '../src/tcNodeConfiguration.mjs';
import { PROTECTED_BASELINE } from '../src/schedule.mjs';
import { s2ExceptionAddresses, S2_EXCEPTION_PATH } from '../../../tools/boundary-scan/s2Exception.mjs';
import * as pkg from '../src/index.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const SRC = join(HERE, '..', 'src');

const CANON = cfg.CANONICAL_TC_NODE_CONFIGURATION;
const clone = () => structuredClone(CANON);
const ip = (...octets) => octets.join('.');
const sha256 = (text) => createHash('sha256').update(text, 'utf8').digest('hex');
const codesOf = (config) => cfg.validateTcNodeConfiguration(config).issues.map((i) => i.code);
const hasCode = (config, code) => codesOf(config).includes(code);
const approved = s2ExceptionAddresses();

// ---------------------------------------------------------------------------
// A. Canonical valid configuration
// ---------------------------------------------------------------------------

test('A: exactly six Nodes and six unique Cabinets, with the explicit one-to-one ordinal relationship', () => {
  assert.equal(CANON.nodes.length, 6);
  assert.equal(new Set(CANON.nodes.map((n) => n.nodeId)).size, 6);
  assert.equal(new Set(CANON.nodes.map((n) => n.cabinetId)).size, 6);
  CANON.nodes.forEach((n, i) => {
    const ordinal = i + 1;
    assert.equal(n.ordinal, ordinal);
    assert.equal(n.nodeId, `TC-NODE-0${ordinal}`);
    assert.equal(n.cabinetId, `CAB${ordinal}`);
  });
  assert.deepEqual(cfg.APPROVED_NODE_IDS, CANON.nodes.map((n) => n.nodeId));
  assert.deepEqual(cfg.APPROVED_CABINET_IDS, CANON.nodes.map((n) => n.cabinetId));
});

test('A: the six approved Test addresses are exact and are the S2 policy set, one per Node', () => {
  assert.equal(cfg.APPROVED_TEST_ADDRESSES.length, 6);
  assert.equal(new Set(cfg.APPROVED_TEST_ADDRESSES).size, 6);
  assert.deepEqual([...cfg.APPROVED_TEST_ADDRESSES].sort(), [...approved].sort());
  assert.equal(S2_EXCEPTION_PATH, 'packages/commissioning/src/tcNodeConfiguration.mjs');
  CANON.nodes.forEach((n, i) => assert.equal(n.ipAddress, approved[i], n.nodeId));
});

test('A: exact approved topology per Node (Coupler, TC module, count, capacity, end module)', () => {
  for (const n of CANON.nodes) {
    assert.equal(n.expectedCouplerModel, '750-362');
    assert.equal(n.expectedThermocoupleModuleModel, '750-498');
    assert.equal(n.expectedThermocoupleModuleCount, 5);
    assert.equal(n.expectedChannelsPerThermocoupleModule, 8);
    assert.equal(n.expectedPhysicalChannelCount, 40);
    assert.equal(n.expectedEndModuleModel, '750-600');
  }
});

test('A: the topology constants are the protected baseline, never restated', () => {
  assert.equal(cfg.EXPECTED_TOPOLOGY.thermocoupleModuleCount, PROTECTED_BASELINE.MODULES_PER_NODE);
  assert.equal(cfg.EXPECTED_TOPOLOGY.channelsPerThermocoupleModule, PROTECTED_BASELINE.CHANNELS_PER_MODULE);
  assert.equal(cfg.EXPECTED_SYSTEM.thermocoupleModules, 30);
  assert.equal(cfg.EXPECTED_SYSTEM.physicalTcChannels, 240);
  assert.equal(cfg.EXPECTED_SYSTEM.physicalTcChannels, PROTECTED_BASELINE.PHYSICAL_CHANNEL_COUNT);
});

test('A: the canonical configuration validates with zero issues, and the totals are recomputed to 6 / 30 / 240', () => {
  const v = cfg.validateTcNodeConfiguration(CANON);
  assert.equal(v.valid, true, JSON.stringify(v.issues));
  assert.equal(v.issueCount, 0);
  const totals = cfg.systemTotals(CANON.nodes);
  assert.deepEqual({ ...totals }, { nodes: 6, cabinets: 6, thermocoupleModules: 30, physicalTcChannels: 240 });
});

test('A: the canonical representation and the fingerprint are deterministic and key-order independent', () => {
  const text = cfg.canonicalTcNodeConfigurationText(CANON);
  assert.equal(text, cfg.canonicalTcNodeConfigurationText(structuredClone(CANON)));
  const reordered = (c) => ({ nodes: c.nodes, configurationId: c.configurationId, addressEnvironment: c.addressEnvironment,
    configurationMode: c.configurationMode, schemaVersion: c.schemaVersion });
  assert.equal(cfg.tcNodeConfigurationFingerprint(CANON, sha256), cfg.tcNodeConfigurationFingerprint(reordered(CANON), sha256));
  assert.match(cfg.tcNodeConfigurationFingerprint(CANON, sha256), /^[0-9a-f]{64}$/);
  const changed = clone();
  changed.nodes[0].displayName = 'TC Node 01 (CAB1) changed';
  assert.notEqual(cfg.tcNodeConfigurationFingerprint(CANON, sha256), cfg.tcNodeConfigurationFingerprint(changed, sha256));
});

test('A: the configuration is frozen, so no caller can mutate the canonical source in place', () => {
  assert.equal(Object.isFrozen(CANON), true);
  assert.equal(Object.isFrozen(CANON.nodes[0]), true);
});

// ---------------------------------------------------------------------------
// B. IP validation
// ---------------------------------------------------------------------------

test('B: malformed IPv4 forms are refused', () => {
  for (const bad of ['not-an-address', '192.0.2', `${approved[0]}.1`, '192.0.2.300', '192.0.2.a1', '192..2.11']) {
    const c = clone();
    c.nodes[0].ipAddress = bad;
    assert.equal(hasCode(c, 'MALFORMED_IPV4'), true, bad);
    assert.equal(cfg.validateTcNodeConfiguration(c).valid, false, bad);
  }
});

test('B: a duplicate IP address is refused on every occurrence', () => {
  const c = clone();
  c.nodes[1].ipAddress = c.nodes[0].ipAddress;
  const issues = cfg.validateTcNodeConfiguration(c).issues.filter((i) => i.code === 'DUPLICATE_IP_ADDRESS');
  assert.deepEqual(issues.map((i) => i.index), [0, 1]);
});

test('B: a missing IP address is refused and is never filled from a neighbour', () => {
  const c = clone();
  delete c.nodes[2].ipAddress;
  assert.equal(hasCode(c, 'MISSING_IP_ADDRESS'), true);
  assert.equal(cfg.buildTcNodeConfigurationView(c).nodes[2].ipAddress, null);
});

test('B: a wrong approved address for a Node is refused (the address belongs to another Node)', () => {
  const c = clone();
  c.nodes[0].ipAddress = approved[1];
  assert.equal(hasCode(c, 'IP_ASSIGNED_TO_WRONG_NODE'), true);
});

test('B: an unknown or out-of-set address is refused, including the neighbours of the approved range', () => {
  for (const unknown of [ip(192, 0, 2, 10), ip(192, 0, 2, 17), ip(192, 0, 2, 0), ip(192, 0, 2, 255), ip(10, 0, 0, 5)]) {
    const c = clone();
    c.nodes[0].ipAddress = unknown;
    assert.equal(hasCode(c, 'NON_APPROVED_TEST_ADDRESS'), true, unknown);
  }
});

test('B: loopback, unspecified, multicast, broadcast and link-local addresses are each refused', () => {
  const cases = [
    [ip(127, 0, 0, 1), 'LOOPBACK_ADDRESS_REFUSED'],
    [ip(0, 0, 0, 0), 'UNSPECIFIED_ADDRESS_REFUSED'],
    [ip(224, 0, 0, 1), 'MULTICAST_ADDRESS_REFUSED'],
    [ip(255, 255, 255, 255), 'BROADCAST_ADDRESS_REFUSED'],
    [ip(169, 254, 1, 1), 'LINK_LOCAL_ADDRESS_REFUSED'],
  ];
  for (const [address, code] of cases) {
    assert.deepEqual(cfg.ipv4ClassificationCodes(address), [code], address);
    const c = clone();
    c.nodes[0].ipAddress = address;
    assert.equal(hasCode(c, code), true, address);
  }
});

test('B: leading or trailing whitespace is refused, never silently normalised', () => {
  for (const spaced of [` ${approved[0]}`, `${approved[0]} `, `\t${approved[0]}`]) {
    const c = clone();
    c.nodes[0].ipAddress = spaced;
    assert.equal(hasCode(c, 'IP_ADDRESS_WHITESPACE_REFUSED'), true, JSON.stringify(spaced));
    assert.equal(cfg.validateTcNodeConfiguration(c).valid, false);
  }
});

test('B: an ambiguous leading-zero form is refused and does not become the canonical address', () => {
  const c = clone();
  c.nodes[0].ipAddress = [192, 0, 2, 0].join('.') + approved[0].split('.')[3];
  assert.equal(hasCode(c, 'AMBIGUOUS_LEADING_ZERO_REFUSED'), true);
  assert.equal(cfg.validateTcNodeConfiguration(c).valid, false);
});

test('B: IPv6 and non-text addresses are refused for this IPv4 profile', () => {
  for (const v6 of ['2001:db8::11', '::1', 'fe80::1']) {
    const c = clone();
    c.nodes[0].ipAddress = v6;
    assert.equal(hasCode(c, 'IPV6_NOT_ALLOWED'), true, v6);
  }
  const c = clone();
  c.nodes[0].ipAddress = 3232235531;
  assert.equal(hasCode(c, 'IP_ADDRESS_NOT_TEXT'), true);
});

test('B: a refused address never yields a CONFIGURED view, and the submitted text is not echoed', () => {
  const c = clone();
  c.nodes[0].ipAddress = `${approved[0]}zz`;
  const view = cfg.buildTcNodeConfigurationView(c);
  assert.equal(view.configurationStatus, 'VALIDATION_FAILED');
  assert.equal(view.nodes.every((n) => n.configurationStatus === 'VALIDATION_FAILED'), true);
  assert.equal(JSON.stringify(cfg.validateTcNodeConfiguration(c)).includes('zz'), false);
});

// ---------------------------------------------------------------------------
// C. Identity validation
// ---------------------------------------------------------------------------

test('C: a duplicate Node ID is refused, and the missing Node it displaces is reported', () => {
  const c = clone();
  c.nodes[1].nodeId = 'TC-NODE-01';
  const codes = codesOf(c);
  assert.ok(codes.includes('DUPLICATE_NODE_ID'));
  assert.ok(codes.includes('MISSING_NODE'));
});

test('C: a missing Node is refused, with no neighbour promoted into its place', () => {
  const c = clone();
  c.nodes.splice(2, 1);
  const codes = codesOf(c);
  assert.ok(codes.includes('NODE_COUNT_INVALID'));
  assert.ok(codes.includes('MISSING_NODE'));
  const view = cfg.buildTcNodeConfigurationView(c);
  assert.equal(view.nodes.some((n) => n.nodeId === 'TC-NODE-03'), false);
  assert.equal(view.nodes.length, 5);
});

test('C: an unknown Node identity is refused', () => {
  const c = clone();
  c.nodes[0].nodeId = 'TC-NODE-07';
  assert.equal(hasCode(c, 'UNKNOWN_NODE_ID'), true);
  const c2 = clone();
  c2.nodes[0].nodeId = 'tc-node-01';
  assert.equal(hasCode(c2, 'UNKNOWN_NODE_ID'), true, 'identity matching is exact, never case-folded');
});

test('C: a duplicate Cabinet ID is refused and the displaced Cabinet is reported missing', () => {
  const c = clone();
  c.nodes[1].cabinetId = 'CAB1';
  const codes = codesOf(c);
  assert.ok(codes.includes('DUPLICATE_CABINET_ID'));
  assert.ok(codes.includes('MISSING_CABINET'));
});

test('C: a missing Cabinet and an unknown Cabinet are each refused', () => {
  const missing = clone();
  delete missing.nodes[3].cabinetId;
  assert.equal(hasCode(missing, 'MISSING_CABINET_ID'), true);
  const unknown = clone();
  unknown.nodes[3].cabinetId = 'CAB7';
  assert.equal(hasCode(unknown, 'UNKNOWN_CABINET_ID'), true);
});

test('C: a wrong Node-to-Cabinet ordinal relationship is refused', () => {
  const c = clone();
  c.nodes[0].cabinetId = 'CAB2';
  assert.equal(hasCode(c, 'NODE_CABINET_ORDINAL_MISMATCH'), true);
});

test('C: an ordinal is never clamped to the nearest valid value', () => {
  const c = clone();
  c.nodes[5].ordinal = 7;
  assert.equal(hasCode(c, 'ORDINAL_MISMATCH'), true);
  assert.equal(c.nodes[5].ordinal, 7, 'the refused value is left as submitted, never repaired');
});

test('C: no fallback to a neighbouring Node occurs for a missing identity', () => {
  const c = clone();
  c.nodes[4].nodeId = undefined;
  assert.equal(hasCode(c, 'MISSING_NODE_ID'), true);
  assert.equal(c.nodes[4].nodeId, undefined);
});

// ---------------------------------------------------------------------------
// D. Topology validation
// ---------------------------------------------------------------------------

test('D: a wrong Coupler, TC module, module count, Channel capacity and end module are each refused', () => {
  const cases = [
    ['expectedCouplerModel', '750-361', 'WRONG_COUPLER_MODEL'],
    ['expectedThermocoupleModuleModel', '750-499', 'WRONG_THERMOCOUPLE_MODULE_MODEL'],
    ['expectedThermocoupleModuleCount', 4, 'WRONG_THERMOCOUPLE_MODULE_COUNT'],
    ['expectedChannelsPerThermocoupleModule', 7, 'WRONG_CHANNEL_CAPACITY'],
    ['expectedEndModuleModel', '750-601', 'WRONG_END_MODULE_MODEL'],
  ];
  for (const [field, value, code] of cases) {
    const c = clone();
    c.nodes[2][field] = value;
    assert.equal(hasCode(c, code), true, `${field} -> ${code}`);
    assert.equal(cfg.validateTcNodeConfiguration(c).valid, false, field);
  }
});

test('D: a wrong expected physical Channel count is refused, including an internally inconsistent count', () => {
  const wrong = clone();
  wrong.nodes[0].expectedPhysicalChannelCount = 32;
  assert.equal(hasCode(wrong, 'WRONG_PHYSICAL_CHANNEL_COUNT'), true);
});

test('D: the system Channel total is recomputed, never read from a cached value', () => {
  const c = clone();
  c.nodes[0].expectedThermocoupleModuleCount = 4;
  c.nodes[0].expectedPhysicalChannelCount = 32;
  const codes = codesOf(c);
  assert.ok(codes.includes('SYSTEM_TC_MODULE_TOTAL_MISMATCH'));
  assert.ok(codes.includes('SYSTEM_PHYSICAL_CHANNEL_TOTAL_MISMATCH'));
  assert.equal(cfg.systemTotals(c.nodes).physicalTcChannels, 232); // 4x8 + 5x(5x8)
  assert.equal(cfg.systemTotals(CANON.nodes).physicalTcChannels, 240, 'the canonical source itself is untouched');
});

// ---------------------------------------------------------------------------
// E. Status honesty
// ---------------------------------------------------------------------------

test('E: a valid committed configuration means CONFIGURED only, never ONLINE or CONNECTED', () => {
  const view = cfg.buildTcNodeConfigurationView(CANON);
  assert.equal(view.configurationStatus, 'CONFIGURED');
  assert.equal(/ONLINE|CONNECTED|OFFLINE|CONNECTION_FAILED/.test(JSON.stringify(view)), false);
});

test('E: reachability, acquisition, hardware and field verification remain at their honest values', () => {
  const view = cfg.buildTcNodeConfigurationView(CANON);
  for (const n of view.nodes) {
    assert.equal(n.reachabilityStatus, 'REACHABILITY_NOT_TESTED');
    assert.equal(n.acquisitionStatus, 'ACQUISITION_NOT_IMPLEMENTED');
    assert.equal(n.hardwareVerificationStatus, 'HARDWARE_VERIFICATION_PENDING');
    assert.equal(n.fieldVerificationStatus, 'FIELD_VERIFICATION_PENDING');
    assert.equal(n.hardwareAccessStatus, 'HARDWARE_ACCESS_NOT_AUTHORIZED');
  }
  assert.equal(view.totals.reachabilityTested, 0);
  assert.equal(view.totals.acquisitionVerified, 0);
  assert.equal(view.totals.fieldVerified, 0);
});

test('E: no port and no protocol is invented: both are UNRESOLVED on every Node', () => {
  const view = cfg.buildTcNodeConfigurationView(CANON);
  for (const n of view.nodes) {
    assert.equal(n.portStatus, 'PORT_UNRESOLVED');
    assert.equal(n.protocolStatus, 'PROTOCOL_UNRESOLVED');
    assert.equal(Object.keys(n).some((k) => /^(port|protocol|tcpPort|udpPort)$/i.test(k)), false);
  }
});

test('E: an invented port or protocol value is refused, never stored or projected', () => {
  const c = clone();
  c.nodes[0].port = 1234;
  assert.equal(hasCode(c, 'INVENTED_NETWORK_PORT'), true);
  const d = clone();
  d.nodes[0].protocol = 'EXAMPLE_PROTOCOL';
  assert.equal(hasCode(d, 'INVENTED_PROTOCOL'), true);
  const e = clone();
  e.nodes[0].portStatus = 'OPEN';
  assert.equal(hasCode(e, 'PORT_STATUS_NOT_UNRESOLVED'), true);
});

test('E: fabricated connection, acquisition, hardware or field claims are each refused', () => {
  const cases = [
    ['reachabilityStatus', 'ONLINE', 'REACHABILITY_CLAIM_REFUSED'],
    ['reachabilityStatus', 'CONNECTED', 'REACHABILITY_CLAIM_REFUSED'],
    ['acquisitionStatus', 'ACQUISITION_READY', 'ACQUISITION_CLAIM_REFUSED'],
    ['hardwareVerificationStatus', 'VERIFIED', 'HARDWARE_VERIFICATION_CLAIM_REFUSED'],
    ['fieldVerificationStatus', 'FIELD_VERIFIED', 'FIELD_VERIFICATION_CLAIM_REFUSED'],
  ];
  for (const [field, value, code] of cases) {
    const c = clone();
    c.nodes[0][field] = value;
    assert.equal(hasCode(c, code), true, `${field}=${value}`);
    assert.equal(cfg.buildTcNodeConfigurationView(c).configurationStatus, 'VALIDATION_FAILED');
  }
});

test('E: a stored derived status or a top-level claim field is refused', () => {
  const stored = clone();
  stored.nodes[0].configurationStatus = 'CONFIGURED';
  assert.equal(hasCode(stored, 'DERIVED_STATUS_STORED_REFUSED'), true);
  const claim = clone();
  claim.nodes[0].hardwareReady = true;
  assert.equal(hasCode(claim, 'CLAIM_FIELD_REFUSED'), true);
  const topClaim = clone();
  topClaim.fieldVerified = true;
  assert.equal(hasCode(topClaim, 'CLAIM_FIELD_REFUSED'), true);
});

// ---------------------------------------------------------------------------
// F. Authority boundary (configuration only; no transport, no device, no Production)
// ---------------------------------------------------------------------------

test('F: the canonical module imports only its own schedule constants: no transport or built-in dependency', () => {
  const source = readFileSync(join(SRC, 'tcNodeConfiguration.mjs'), 'utf8');
  const imports = [...source.matchAll(/^import .* from ['"]([^'"]+)['"];?$/gm)].map((m) => m[1]);
  assert.deepEqual(imports, ['./schedule.mjs']);
  assert.equal(/\bnode:/.test(source), false);
});

test('F: the canonical module has no socket, request, child-process, file, device-write or clock API', () => {
  const source = readFileSync(join(SRC, 'tcNodeConfiguration.mjs'), 'utf8');
  const forbidden = [
    /\bfetch\s*\(/, /\bnew\s+WebSocket\b/, /\bXMLHttpRequest\b/, /\bcreateConnection\b/, /\bcreateSocket\b/,
    /\bdgram\b/, /\bchild_process\b/, /\bspawnSync\b/, /\bexecFile\b/, /\bwriteFile/, /\bDate\.now\b/, /\bsetTimeout\b/,
    /\bsetInterval\b/, /\bprocess\.env\b/, /\b502\b/,
  ];
  for (const pattern of forbidden) assert.equal(pattern.test(source), false, String(pattern));
});

test('F: the module exposes no credential, Production mode or device-write authority', () => {
  const keys = [...cfg.NODE_FIELDS, ...cfg.CONFIGURATION_FIELDS].join(' ').toLowerCase();
  for (const word of ['pass' + 'word', 'token', 'credential', 'secret', 'production', 'port', 'protocol']) {
    const hit = keys.split(' ').filter((k) => k.includes(word) && !/status$/.test(k));
    assert.deepEqual(hit, [], word);
  }
  assert.equal(cfg.CONFIGURATION_MODE, 'TEST_CONFIGURATION');
  assert.equal(cfg.ADDRESS_ENVIRONMENT, 'TEST');
  const view = cfg.buildTcNodeConfigurationView(CANON);
  for (const flag of Object.values(view.authority)) {
    if (typeof flag === 'boolean') assert.equal(flag, false);
  }
  assert.deepEqual([...view.authority.statements], ['NO HARDWARE ACCESS', 'NO DEVICE WRITE']);
});

test('F: the projection carries no control vocabulary (no connect, test, activate, poll or apply control)', () => {
  const text = JSON.stringify(cfg.buildTcNodeConfigurationView(CANON));
  assert.equal(/\b(testConnection|connect|poll|polling|activate|applyToDevice|startPolling)\b/i.test(text), false);
});

test('F: the package entry exposes the canonical source and no transport surface', () => {
  assert.equal(pkg.CANONICAL_TC_NODE_CONFIGURATION, CANON);
  assert.equal(typeof pkg.validateTcNodeConfiguration, 'function');
  for (const name of Object.keys(pkg)) assert.equal(/socket|fetch|modem|transport/i.test(name), false, name);
});
