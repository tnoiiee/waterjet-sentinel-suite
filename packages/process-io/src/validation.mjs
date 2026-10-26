// H2 Physical Process-I/O — physical-capable configuration validation (fail-closed).
//
// Configuration distinguishes committed non-sensitive defaults (from the commissioned
// physical topology: transport shape, unit id, timeouts, grouping) from Owner-local endpoint
// values (never committed), unresolved values and simulation values. Physical mode refuses
// BEFORE any network I/O when required configuration is incomplete or invalid. Typed
// refusal codes. No fallback endpoint is ever created. Physical availability is never
// claimed merely because a configuration can be constructed.
//
// Pure validation. No I/O.

import { TRANSPORT_SHAPE, DEVICE_KEYS } from './legacyEvidence.mjs';

export const CONFIG_REFUSAL = Object.freeze({
  MODE_UNKNOWN: 'WJSS_CONFIG_REFUSED_MODE_UNKNOWN',
  ENDPOINT_MISSING: 'WJSS_CONFIG_REFUSED_ENDPOINT_MISSING',
  ENDPOINT_INVALID: 'WJSS_CONFIG_REFUSED_ENDPOINT_INVALID',
  ENDPOINT_DUPLICATE: 'WJSS_CONFIG_REFUSED_ENDPOINT_DUPLICATE',
  PORT_INVALID: 'WJSS_CONFIG_REFUSED_PORT_INVALID',
  UNIT_ID_INVALID: 'WJSS_CONFIG_REFUSED_UNIT_ID_INVALID',
  TIMEOUT_INVALID: 'WJSS_CONFIG_REFUSED_TIMEOUT_INVALID',
  DEVICE_UNKNOWN: 'WJSS_CONFIG_REFUSED_DEVICE_UNKNOWN',
  DEVICE_MISSING: 'WJSS_CONFIG_REFUSED_DEVICE_MISSING',
  NO_NETWORK_ON_REFUSAL: 'WJSS_NO_NETWORK_IO_ON_REFUSAL',
});

export const MODES = Object.freeze(['SIMULATION', 'TEST_HARDWARE_READ_ONLY', 'PHYSICAL_CAPABLE']);

/** Endpoint shape: non-empty host string. The host VALUE is Owner-local and never committed. */
function validHost(host) {
  return typeof host === 'string' && host.trim().length > 0;
}

/**
 * Validate a process-I/O configuration object:
 *   { mode, devices: { [deviceKey]: { host, port?, unitId?, connectTimeoutMs?, ... } }, ... }
 * Returns { ok, refusals[], networkAllowed }. On any refusal networkAllowed is false and the
 * caller must perform NO network I/O (the session layer enforces the same rule).
 */
export function validateProcessIoConfig(config) {
  const refusals = [];
  const push = (code, detail) => refusals.push(Object.freeze({ code, detail }));

  if (!config || typeof config !== 'object') {
    push(CONFIG_REFUSAL.MODE_UNKNOWN, 'configuration missing');
    return { ok: false, refusals, networkAllowed: false };
  }
  if (!MODES.includes(config.mode)) {
    push(CONFIG_REFUSAL.MODE_UNKNOWN, `mode ${config.mode}`);
  }
  const devices = config.devices ?? {};
  const seenHosts = new Map();
  for (const key of DEVICE_KEYS) {
    const device = devices[key];
    if (!device || typeof device !== 'object') {
      push(CONFIG_REFUSAL.DEVICE_MISSING, key);
      push(CONFIG_REFUSAL.ENDPOINT_MISSING, key);
      continue;
    }
    if (!validHost(device.host)) push(CONFIG_REFUSAL.ENDPOINT_MISSING, key);
    else {
      if (seenHosts.has(device.host)) {
        push(CONFIG_REFUSAL.ENDPOINT_DUPLICATE, `${key} duplicates ${seenHosts.get(device.host)}`);
      }
      seenHosts.set(device.host, key);
    }
    const port = device.port ?? TRANSPORT_SHAPE.port;
    if (!Number.isInteger(port) || port < 1 || port > 65535) push(CONFIG_REFUSAL.PORT_INVALID, key);
    const unitId = device.unitId ?? TRANSPORT_SHAPE.unitId;
    if (!Number.isInteger(unitId) || unitId < 0 || unitId > 255) push(CONFIG_REFUSAL.UNIT_ID_INVALID, key);
    for (const t of ['connectTimeoutMs', 'readTimeoutMs', 'writeTimeoutMs']) {
      const v = device[t] ?? TRANSPORT_SHAPE[t];
      if (!Number.isInteger(v) || v <= 0) push(CONFIG_REFUSAL.TIMEOUT_INVALID, `${key}.${t}`);
    }
  }
  for (const key of Object.keys(devices)) {
    if (!DEVICE_KEYS.includes(key)) push(CONFIG_REFUSAL.DEVICE_UNKNOWN, key);
  }

  const ok = refusals.length === 0;
  // Network I/O is allowed only for a valid physical-capable configuration; a refusal
  // guarantees zero network activity (asserted by tests through the no-op transport).
  const networkAllowed = ok && (config.mode === 'TEST_HARDWARE_READ_ONLY' || config.mode === 'PHYSICAL_CAPABLE');
  return Object.freeze({ ok, refusals: Object.freeze(refusals), networkAllowed });
}
