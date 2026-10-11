// Stage 0.4C-K2 - the canonical TC Node / Cabinet / Test IP configuration foundation.
//
// This module is the ONE canonical source of the six TC Node identities, the six Cabinet identities
// and the six approved TEST_CONFIGURATION IPv4 addresses. Every projection (the Preview route, the
// read-only Node Configuration section and the tests) is derived from CANONICAL_TC_NODE_CONFIGURATION
// through the functions below. No other Product file may repeat these address literals.
//
// Boundary: configuration evidence only. This module opens no socket, probes no address or port,
// contacts no device, reads no process image, carries no credential and invents no network port or
// protocol. The status values it reports are honest by construction: a valid configuration means
// CONFIGURED and nothing more. It is the ONLY Product file that may contain the approved non-loopback
// IPv4 literals (boundary-scan S2 exception, Stage 0.4C-K2, Owner approved, pinned to this exact path).
//
// Browser-safe: no Node built-in imports and no I/O. The fingerprint digest is supplied by the caller.

import { PROTECTED_BASELINE } from './schedule.mjs';

export const TC_NODE_CONFIGURATION_SCHEMA_VERSION = 'wjss.tc-node-configuration/1';
export const TC_NODE_CONFIGURATION_ID = 'TC-NODE-CONFIGURATION-TEST-REV1';
export const CONFIGURATION_MODE = 'TEST_CONFIGURATION';
export const ADDRESS_ENVIRONMENT = 'TEST';
export const IP_ADDRESS_BASIS = 'OWNER_APPROVED_TEST_CONFIGURATION';
export const PORT_STATUS = 'PORT_UNRESOLVED';
export const PROTOCOL_STATUS = 'PROTOCOL_UNRESOLVED';

/** The configuration status is DERIVED from validation. It is never stored in the source. */
export const CONFIGURATION_STATUS = Object.freeze({
  CONFIGURED: 'CONFIGURED',
  VALIDATION_FAILED: 'VALIDATION_FAILED',
});

/** No network probe exists in this Stage, so no reachability claim can be made. */
export const REACHABILITY_STATUS = Object.freeze({ NOT_TESTED: 'REACHABILITY_NOT_TESTED' });
export const ACQUISITION_STATUS = Object.freeze({ NOT_IMPLEMENTED: 'ACQUISITION_NOT_IMPLEMENTED' });
export const HARDWARE_VERIFICATION_STATUS = Object.freeze({ PENDING: 'HARDWARE_VERIFICATION_PENDING' });
export const FIELD_VERIFICATION_STATUS = Object.freeze({ PENDING: 'FIELD_VERIFICATION_PENDING' });
export const HARDWARE_ACCESS_STATUS = Object.freeze({ NOT_AUTHORIZED: 'HARDWARE_ACCESS_NOT_AUTHORIZED' });

/** Topology expectations, taken from the protected baseline (never restated in this file). */
export const EXPECTED_TOPOLOGY = Object.freeze({
  couplerModel: PROTECTED_BASELINE.COUPLER_MODEL,
  thermocoupleModuleModel: PROTECTED_BASELINE.MODULE_MODEL,
  thermocoupleModuleCount: PROTECTED_BASELINE.MODULES_PER_NODE,
  channelsPerThermocoupleModule: PROTECTED_BASELINE.CHANNELS_PER_MODULE,
  physicalChannelCount: PROTECTED_BASELINE.MODULES_PER_NODE * PROTECTED_BASELINE.CHANNELS_PER_MODULE,
  endModuleModel: PROTECTED_BASELINE.END_MODEL,
});

export const EXPECTED_SYSTEM = Object.freeze({
  nodes: PROTECTED_BASELINE.NODE_COUNT,
  cabinets: PROTECTED_BASELINE.NODE_COUNT,
  thermocoupleModules: PROTECTED_BASELINE.MODULE_COUNT,
  physicalTcChannels: PROTECTED_BASELINE.PHYSICAL_CHANNEL_COUNT,
});

/**
 * The approved identity table. The ordinal is recorded EXPLICITLY on every entry, so the
 * Node-to-Cabinet relationship is a recorded fact and never an inference from array position.
 */
const APPROVED_NODE_TABLE = Object.freeze([
  Object.freeze({ ordinal: 1, nodeId: 'TC-NODE-01', cabinetId: 'CAB1', ipAddress: '192.0.2.11' }),
  Object.freeze({ ordinal: 2, nodeId: 'TC-NODE-02', cabinetId: 'CAB2', ipAddress: '192.0.2.12' }),
  Object.freeze({ ordinal: 3, nodeId: 'TC-NODE-03', cabinetId: 'CAB3', ipAddress: '192.0.2.13' }),
  Object.freeze({ ordinal: 4, nodeId: 'TC-NODE-04', cabinetId: 'CAB4', ipAddress: '192.0.2.14' }),
  Object.freeze({ ordinal: 5, nodeId: 'TC-NODE-05', cabinetId: 'CAB5', ipAddress: '192.0.2.15' }),
  Object.freeze({ ordinal: 6, nodeId: 'TC-NODE-06', cabinetId: 'CAB6', ipAddress: '192.0.2.16' }),
]);

/** Every key a Node entry may carry. Anything else is refused, never silently dropped. */
export const NODE_FIELDS = Object.freeze([
  'ordinal', 'nodeId', 'cabinetId', 'displayName', 'ipAddress', 'ipAddressBasis', 'addressEnvironment',
  'configurationMode', 'expectedCouplerModel', 'expectedThermocoupleModuleModel',
  'expectedThermocoupleModuleCount', 'expectedChannelsPerThermocoupleModule', 'expectedPhysicalChannelCount',
  'expectedEndModuleModel', 'portStatus', 'protocolStatus', 'reachabilityStatus', 'acquisitionStatus',
  'hardwareVerificationStatus', 'fieldVerificationStatus',
]);

/** Every key the top-level configuration may carry. */
export const CONFIGURATION_FIELDS = Object.freeze([
  'schemaVersion', 'configurationId', 'configurationMode', 'addressEnvironment', 'nodes',
]);

function deepFreeze(value) {
  if (value && typeof value === 'object' && !Object.isFrozen(value)) {
    Object.values(value).forEach(deepFreeze);
    Object.freeze(value);
  }
  return value;
}

/**
 * THE canonical configuration. Its shape is what the validator checks and what the fingerprint is
 * computed over. The declared status fields hold the only honest values; the validator refuses any
 * other value, so a claim cannot be smuggled into the source.
 */
export const CANONICAL_TC_NODE_CONFIGURATION = deepFreeze({
  schemaVersion: TC_NODE_CONFIGURATION_SCHEMA_VERSION,
  configurationId: TC_NODE_CONFIGURATION_ID,
  configurationMode: CONFIGURATION_MODE,
  addressEnvironment: ADDRESS_ENVIRONMENT,
  nodes: APPROVED_NODE_TABLE.map((entry) => ({
    ordinal: entry.ordinal,
    nodeId: entry.nodeId,
    cabinetId: entry.cabinetId,
    displayName: `TC Node ${String(entry.ordinal).padStart(2, '0')} (${entry.cabinetId})`,
    ipAddress: entry.ipAddress,
    ipAddressBasis: IP_ADDRESS_BASIS,
    addressEnvironment: ADDRESS_ENVIRONMENT,
    configurationMode: CONFIGURATION_MODE,
    expectedCouplerModel: EXPECTED_TOPOLOGY.couplerModel,
    expectedThermocoupleModuleModel: EXPECTED_TOPOLOGY.thermocoupleModuleModel,
    expectedThermocoupleModuleCount: EXPECTED_TOPOLOGY.thermocoupleModuleCount,
    expectedChannelsPerThermocoupleModule: EXPECTED_TOPOLOGY.channelsPerThermocoupleModule,
    expectedPhysicalChannelCount: EXPECTED_TOPOLOGY.physicalChannelCount,
    expectedEndModuleModel: EXPECTED_TOPOLOGY.endModuleModel,
    portStatus: PORT_STATUS,
    protocolStatus: PROTOCOL_STATUS,
    reachabilityStatus: REACHABILITY_STATUS.NOT_TESTED,
    acquisitionStatus: ACQUISITION_STATUS.NOT_IMPLEMENTED,
    hardwareVerificationStatus: HARDWARE_VERIFICATION_STATUS.PENDING,
    fieldVerificationStatus: FIELD_VERIFICATION_STATUS.PENDING,
  })),
});

/** The approved sets, derived from the canonical table. Read-only. */
export const APPROVED_NODE_IDS = Object.freeze(APPROVED_NODE_TABLE.map((e) => e.nodeId));
export const APPROVED_CABINET_IDS = Object.freeze(APPROVED_NODE_TABLE.map((e) => e.cabinetId));
export const APPROVED_TEST_ADDRESSES = Object.freeze(APPROVED_NODE_TABLE.map((e) => e.ipAddress));

const NODE_ID_PATTERN = /^TC-NODE-0([1-6])$/;
const CABINET_ID_PATTERN = /^CAB([1-6])$/;
const FORBIDDEN_NODE_FIELD_RULES = Object.freeze([
  [/^(port|tcpPort|udpPort|portNumber)$/i, 'INVENTED_NETWORK_PORT'],
  [/protocol/i, 'INVENTED_PROTOCOL'],
  [/^(pass|pwd|secret|token|credential|apiKey|accessToken)/i, 'CREDENTIAL_FIELD_REFUSED'],
  [/^(production|productionAddress|productionEndpoint|productionMode)$/i, 'PRODUCTION_FIELD_REFUSED'],
  [/^(hardwareReady|hardwareConnected|acquisitionReady|acquisitionVerified|fieldVerified|online|connected|reachable|verified)$/i,
    'CLAIM_FIELD_REFUSED'],
  [/^configurationStatus$/i, 'DERIVED_STATUS_STORED_REFUSED'],
]);

function forbiddenFieldCode(key) {
  for (const [pattern, code] of FORBIDDEN_NODE_FIELD_RULES) {
    if (pattern.test(key)) return code;
  }
  return 'UNAPPROVED_FIELD';
}

/**
 * Strict dotted-decimal IPv4 classification. No normalisation: whitespace, leading zeros, IPv6 and
 * every malformed form are refused, and nothing is repaired. Returns the issue codes only, never the
 * submitted text, so a hostile value can never be reflected into the Operator screen.
 */
export function ipv4ClassificationCodes(value) {
  if (value === undefined || value === null || value === '') return ['MISSING_IP_ADDRESS'];
  if (typeof value !== 'string') return ['IP_ADDRESS_NOT_TEXT'];
  if (value !== value.trim()) return ['IP_ADDRESS_WHITESPACE_REFUSED'];
  if (value.includes(':')) return ['IPV6_NOT_ALLOWED'];
  const parts = value.split('.');
  if (parts.length !== 4 || !parts.every((p) => /^[0-9]{1,3}$/.test(p))) return ['MALFORMED_IPV4'];
  if (parts.some((p) => p.length > 1 && p.startsWith('0'))) return ['AMBIGUOUS_LEADING_ZERO_REFUSED'];
  const octets = parts.map(Number);
  if (octets.some((o) => o > 255)) return ['MALFORMED_IPV4'];
  if (octets[0] === 127) return ['LOOPBACK_ADDRESS_REFUSED'];
  if (octets.every((o) => o === 0)) return ['UNSPECIFIED_ADDRESS_REFUSED'];
  if (octets[0] >= 224 && octets[0] <= 239) return ['MULTICAST_ADDRESS_REFUSED'];
  if (octets.every((o) => o === 255)) return ['BROADCAST_ADDRESS_REFUSED'];
  if (octets[0] === 169 && octets[1] === 254) return ['LINK_LOCAL_ADDRESS_REFUSED'];
  return [];
}

/** The approved Node for an approved Test address, or null. Exact string match only. */
export function approvedNodeForAddress(address) {
  const entry = APPROVED_NODE_TABLE.find((e) => e.ipAddress === address);
  return entry ? entry.nodeId : null;
}

/**
 * Validates the WHOLE configuration atomically. It returns every issue it can prove and never
 * repairs, substitutes, clamps or infers. Issues carry a code, a field and a Node index or identity,
 * never a submitted value.
 */
export function validateTcNodeConfiguration(config) {
  const issues = [];
  const add = (code, field, extra = {}) => issues.push(Object.freeze({ code, field, ...extra }));

  if (!config || typeof config !== 'object' || Array.isArray(config)) {
    add('NOT_A_CONFIGURATION_OBJECT', 'configuration');
    return summarise(issues);
  }
  for (const key of Object.keys(config)) {
    if (!CONFIGURATION_FIELDS.includes(key)) add(forbiddenFieldCode(key), key);
  }
  if (config.schemaVersion !== TC_NODE_CONFIGURATION_SCHEMA_VERSION) add('SCHEMA_VERSION_MISMATCH', 'schemaVersion');
  if (config.configurationId !== TC_NODE_CONFIGURATION_ID) add('CONFIGURATION_ID_MISMATCH', 'configurationId');
  if (config.configurationMode === 'PRODUCTION') add('PRODUCTION_MODE_REFUSED', 'configurationMode');
  else if (config.configurationMode !== CONFIGURATION_MODE) add('CONFIGURATION_MODE_NOT_TEST', 'configurationMode');
  if (config.addressEnvironment === 'PRODUCTION') add('PRODUCTION_ENVIRONMENT_REFUSED', 'addressEnvironment');
  else if (config.addressEnvironment !== ADDRESS_ENVIRONMENT) add('ADDRESS_ENVIRONMENT_NOT_TEST', 'addressEnvironment');

  if (!Array.isArray(config.nodes)) {
    add('NODES_NOT_AN_ARRAY', 'nodes');
    return summarise(issues);
  }
  if (config.nodes.length !== APPROVED_NODE_TABLE.length) {
    add('NODE_COUNT_INVALID', 'nodes', { expected: APPROVED_NODE_TABLE.length, found: config.nodes.length });
  }

  const seenNodeIds = new Map();
  const seenCabinetIds = new Map();
  const ipCounts = new Map();
  config.nodes.forEach((node, index) => {
    if (!node || typeof node !== 'object' || Array.isArray(node)) {
      add('NODE_NOT_AN_OBJECT', 'nodes', { index });
      return;
    }
    for (const key of Object.keys(node)) {
      if (!NODE_FIELDS.includes(key)) add(forbiddenFieldCode(key), key, { index });
    }
    const nodeIdText = node.nodeId;
    const nodeMatch = typeof nodeIdText === 'string' ? NODE_ID_PATTERN.exec(nodeIdText) : null;
    if (nodeIdText === undefined || nodeIdText === null || nodeIdText === '') add('MISSING_NODE_ID', 'nodeId', { index });
    else if (!nodeMatch) add('UNKNOWN_NODE_ID', 'nodeId', { index });
    else if (seenNodeIds.has(nodeIdText)) add('DUPLICATE_NODE_ID', 'nodeId', { index, nodeId: nodeIdText });
    else seenNodeIds.set(nodeIdText, index);

    const cabinetText = node.cabinetId;
    const cabinetMatch = typeof cabinetText === 'string' ? CABINET_ID_PATTERN.exec(cabinetText) : null;
    if (cabinetText === undefined || cabinetText === null || cabinetText === '') add('MISSING_CABINET_ID', 'cabinetId', { index });
    else if (!cabinetMatch) add('UNKNOWN_CABINET_ID', 'cabinetId', { index });
    else if (seenCabinetIds.has(cabinetText)) add('DUPLICATE_CABINET_ID', 'cabinetId', { index, cabinetId: cabinetText });
    else seenCabinetIds.set(cabinetText, index);

    // The ordinal is recorded explicitly. It must equal BOTH the Node number and the Cabinet number.
    // There is no clamping: an ordinal outside 1..6 is a mismatch, never a neighbour.
    if (nodeMatch && node.ordinal !== Number(nodeMatch[1])) add('ORDINAL_MISMATCH', 'ordinal', { index });
    if (nodeMatch && cabinetMatch && cabinetMatch[1] !== nodeMatch[1]) {
      add('NODE_CABINET_ORDINAL_MISMATCH', 'cabinetId', { index });
    }

    if (node.displayName === undefined || typeof node.displayName !== 'string' || node.displayName.trim() !== node.displayName
      || node.displayName.length === 0 || node.displayName.length > 64) {
      add('DISPLAY_NAME_INVALID', 'displayName', { index });
    }

    // IP address: strict parse, then approved-set membership, then the approved owner of that address.
    const ipCodes = ipv4ClassificationCodes(node.ipAddress);
    for (const code of ipCodes) add(code, 'ipAddress', { index });
    if (ipCodes.length === 0) {
      ipCounts.set(node.ipAddress, (ipCounts.get(node.ipAddress) ?? 0) + 1);
      const owner = approvedNodeForAddress(node.ipAddress);
      if (owner === null) add('NON_APPROVED_TEST_ADDRESS', 'ipAddress', { index });
      else if (owner !== nodeIdText) add('IP_ASSIGNED_TO_WRONG_NODE', 'ipAddress', { index });
    }
    if (node.ipAddressBasis !== IP_ADDRESS_BASIS) add('IP_ADDRESS_BASIS_NOT_APPROVED', 'ipAddressBasis', { index });
    if (node.addressEnvironment === 'PRODUCTION') add('PRODUCTION_ENVIRONMENT_REFUSED', 'addressEnvironment', { index });
    else if (node.addressEnvironment !== ADDRESS_ENVIRONMENT) add('ADDRESS_ENVIRONMENT_NOT_TEST', 'addressEnvironment', { index });
    if (node.configurationMode === 'PRODUCTION') add('PRODUCTION_MODE_REFUSED', 'configurationMode', { index });
    else if (node.configurationMode !== CONFIGURATION_MODE) add('CONFIGURATION_MODE_NOT_TEST', 'configurationMode', { index });

    // Topology: each expectation is compared exactly. A missing value is a mismatch, never a default.
    if (node.expectedCouplerModel !== EXPECTED_TOPOLOGY.couplerModel) add('WRONG_COUPLER_MODEL', 'expectedCouplerModel', { index });
    if (node.expectedThermocoupleModuleModel !== EXPECTED_TOPOLOGY.thermocoupleModuleModel) {
      add('WRONG_THERMOCOUPLE_MODULE_MODEL', 'expectedThermocoupleModuleModel', { index });
    }
    if (node.expectedThermocoupleModuleCount !== EXPECTED_TOPOLOGY.thermocoupleModuleCount) {
      add('WRONG_THERMOCOUPLE_MODULE_COUNT', 'expectedThermocoupleModuleCount', { index });
    }
    if (node.expectedChannelsPerThermocoupleModule !== EXPECTED_TOPOLOGY.channelsPerThermocoupleModule) {
      add('WRONG_CHANNEL_CAPACITY', 'expectedChannelsPerThermocoupleModule', { index });
    }
    if (node.expectedPhysicalChannelCount !== EXPECTED_TOPOLOGY.physicalChannelCount
      || node.expectedPhysicalChannelCount !== node.expectedThermocoupleModuleCount * node.expectedChannelsPerThermocoupleModule) {
      add('WRONG_PHYSICAL_CHANNEL_COUNT', 'expectedPhysicalChannelCount', { index });
    }
    if (node.expectedEndModuleModel !== EXPECTED_TOPOLOGY.endModuleModel) add('WRONG_END_MODULE_MODEL', 'expectedEndModuleModel', { index });

    // Port and protocol: both are UNRESOLVED. Any port or protocol value is an invented value.
    if (node.portStatus !== PORT_STATUS) add('PORT_STATUS_NOT_UNRESOLVED', 'portStatus', { index });
    if (node.protocolStatus !== PROTOCOL_STATUS) add('PROTOCOL_STATUS_NOT_UNRESOLVED', 'protocolStatus', { index });

    // Declared statuses: only the honest values are accepted. Any other value is a claim.
    if (node.reachabilityStatus !== REACHABILITY_STATUS.NOT_TESTED) add('REACHABILITY_CLAIM_REFUSED', 'reachabilityStatus', { index });
    if (node.acquisitionStatus !== ACQUISITION_STATUS.NOT_IMPLEMENTED) add('ACQUISITION_CLAIM_REFUSED', 'acquisitionStatus', { index });
    if (node.hardwareVerificationStatus !== HARDWARE_VERIFICATION_STATUS.PENDING) {
      add('HARDWARE_VERIFICATION_CLAIM_REFUSED', 'hardwareVerificationStatus', { index });
    }
    if (node.fieldVerificationStatus !== FIELD_VERIFICATION_STATUS.PENDING) add('FIELD_VERIFICATION_CLAIM_REFUSED', 'fieldVerificationStatus', { index });
  });

  // Completeness: every approved Node and every approved Cabinet must be present exactly once.
  for (const expected of APPROVED_NODE_TABLE) {
    if (!seenNodeIds.has(expected.nodeId)) add('MISSING_NODE', 'nodeId', { nodeId: expected.nodeId });
    if (!seenCabinetIds.has(expected.cabinetId)) add('MISSING_CABINET', 'cabinetId', { cabinetId: expected.cabinetId });
  }
  // Duplicate Test addresses are reported on every occurrence, so none can hide behind the first.
  config.nodes.forEach((node, index) => {
    if (node && typeof node === 'object' && ipCounts.get(node.ipAddress) > 1) add('DUPLICATE_IP_ADDRESS', 'ipAddress', { index });
  });

  // System totals are RECOMPUTED from the Node entries on every validation, never read from a cache.
  const totals = systemTotals(config.nodes);
  if (totals.thermocoupleModules !== EXPECTED_SYSTEM.thermocoupleModules) {
    add('SYSTEM_TC_MODULE_TOTAL_MISMATCH', 'nodes', { expected: EXPECTED_SYSTEM.thermocoupleModules, found: totals.thermocoupleModules });
  }
  if (totals.physicalTcChannels !== EXPECTED_SYSTEM.physicalTcChannels) {
    add('SYSTEM_PHYSICAL_CHANNEL_TOTAL_MISMATCH', 'nodes', { expected: EXPECTED_SYSTEM.physicalTcChannels, found: totals.physicalTcChannels });
  }
  return summarise(issues);
}

function summarise(issues) {
  return Object.freeze({ valid: issues.length === 0, issueCount: issues.length, issues: Object.freeze(issues) });
}

/**
 * Recomputes the system totals from whatever Node entries are supplied. Counts are never cached, and
 * only integer values are summed, so a malformed entry contributes nothing rather than NaN.
 */
export function systemTotals(nodes) {
  const list = Array.isArray(nodes) ? nodes : [];
  const cabinets = new Set();
  let thermocoupleModules = 0;
  let physicalTcChannels = 0;
  for (const node of list) {
    if (!node || typeof node !== 'object') continue;
    if (typeof node.cabinetId === 'string') cabinets.add(node.cabinetId);
    const modules = Number.isInteger(node.expectedThermocoupleModuleCount) ? node.expectedThermocoupleModuleCount : 0;
    const channels = Number.isInteger(node.expectedChannelsPerThermocoupleModule) ? node.expectedChannelsPerThermocoupleModule : 0;
    thermocoupleModules += modules;
    physicalTcChannels += modules * channels;
  }
  return Object.freeze({ nodes: list.length, cabinets: cabinets.size, thermocoupleModules, physicalTcChannels });
}

/**
 * The read-only projection shown on the Preview. It is derived from the configuration AND the
 * validation result, and it reports honest statuses only: the reachability, acquisition and
 * verification values below are facts of this Stage (no probe, no acquisition, no verification),
 * not stored claims. Every projected field is a constant or a recomputed value.
 */
export function buildTcNodeConfigurationView(config = CANONICAL_TC_NODE_CONFIGURATION) {
  const validation = validateTcNodeConfiguration(config);
  const nodes = Array.isArray(config?.nodes) ? config.nodes : [];
  const configured = validation.valid ? APPROVED_NODE_TABLE.length : 0;
  const totals = systemTotals(nodes);
  return Object.freeze({
    schemaVersion: TC_NODE_CONFIGURATION_SCHEMA_VERSION,
    configurationId: TC_NODE_CONFIGURATION_ID,
    configurationMode: CONFIGURATION_MODE,
    addressEnvironment: ADDRESS_ENVIRONMENT,
    configurationStatus: validation.valid ? CONFIGURATION_STATUS.CONFIGURED : CONFIGURATION_STATUS.VALIDATION_FAILED,
    validation: Object.freeze({
      valid: validation.valid,
      issueCount: validation.issueCount,
      issueCodes: Object.freeze([...new Set(validation.issues.map((i) => i.code))].sort()),
    }),
    nodes: Object.freeze(nodes.map((node) => Object.freeze({
      nodeId: typeof node?.nodeId === 'string' ? node.nodeId : null,
      cabinetId: typeof node?.cabinetId === 'string' ? node.cabinetId : null,
      ordinal: Number.isInteger(node?.ordinal) ? node.ordinal : null,
      displayName: typeof node?.displayName === 'string' ? node.displayName : null,
      ipAddress: typeof node?.ipAddress === 'string' ? node.ipAddress : null,
      ipAddressBasis: IP_ADDRESS_BASIS,
      addressEnvironment: ADDRESS_ENVIRONMENT,
      configurationMode: CONFIGURATION_MODE,
      expectedCouplerModel: EXPECTED_TOPOLOGY.couplerModel,
      expectedHeadStationCount: 1,
      expectedThermocoupleModuleModel: EXPECTED_TOPOLOGY.thermocoupleModuleModel,
      expectedThermocoupleModuleCount: EXPECTED_TOPOLOGY.thermocoupleModuleCount,
      expectedChannelsPerThermocoupleModule: EXPECTED_TOPOLOGY.channelsPerThermocoupleModule,
      expectedPhysicalChannelCount: EXPECTED_TOPOLOGY.physicalChannelCount,
      expectedEndModuleModel: EXPECTED_TOPOLOGY.endModuleModel,
      expectedEndModuleCount: 1,
      portStatus: PORT_STATUS,
      protocolStatus: PROTOCOL_STATUS,
      configurationStatus: validation.valid ? CONFIGURATION_STATUS.CONFIGURED : CONFIGURATION_STATUS.VALIDATION_FAILED,
      reachabilityStatus: REACHABILITY_STATUS.NOT_TESTED,
      acquisitionStatus: ACQUISITION_STATUS.NOT_IMPLEMENTED,
      hardwareVerificationStatus: HARDWARE_VERIFICATION_STATUS.PENDING,
      fieldVerificationStatus: FIELD_VERIFICATION_STATUS.PENDING,
      hardwareAccessStatus: HARDWARE_ACCESS_STATUS.NOT_AUTHORIZED,
    }))),
    totals: Object.freeze({
      nodes: totals.nodes,
      cabinets: totals.cabinets,
      thermocoupleModules: totals.thermocoupleModules,
      physicalTcChannels: totals.physicalTcChannels,
      configuredTestIps: configured,
      reachabilityTested: 0,
      acquisitionVerified: 0,
      fieldVerified: 0,
    }),
    authority: Object.freeze({
      statements: Object.freeze(['NO HARDWARE ACCESS', 'NO DEVICE WRITE']),
      hardwareAccess: false,
      networkProbe: false,
      socketOpened: false,
      deviceWrite: false,
      activation: false,
      productionMode: false,
      fieldVerified: false,
    }),
  });
}

/** Deterministic text of a configuration: object keys sorted recursively, array order kept. */
export function canonicalTcNodeConfigurationText(config) {
  const sorted = (value) => {
    if (Array.isArray(value)) return value.map(sorted);
    if (value && typeof value === 'object') {
      return Object.fromEntries(Object.keys(value).sort().map((k) => [k, sorted(value[k])]));
    }
    return value;
  };
  return JSON.stringify(sorted(config));
}

/** Deterministic fingerprint. The caller supplies a hex digest so this module stays dependency-free. */
export function tcNodeConfigurationFingerprint(config, digestHex) {
  return digestHex(canonicalTcNodeConfigurationText(config));
}
