// WJSS Stage 0.2.1A — synthetic configuration, model, and poll plan.
//
// EVERY VALUE IN THIS FILE IS A SYNTHETIC SPIKE PARAMETER — NOT A PRODUCTION VALUE.
// No Production Sensor ID, device address, register map, setpoint, threshold, or timeout is
// represented here. Addresses are synthetic offsets inside a simulated device only.
// Do not copy these values into Production requirements or configuration.

export const SYNTHETIC_LABEL = 'SYNTHETIC SPIKE PARAMETER - NOT A PRODUCTION VALUE';

/** Synthetic spike parameters (Owner-approved planning values, Stage 0.2.1A). */
export const DEFAULT_PARAMS = Object.freeze({
  label: SYNTHETIC_LABEL,
  seed: 2101,
  concurrency: 4,
  requestTimeoutMs: 750,
  fastMs: 1000,
  mediumMs: 5000,
  slowMs: 30000,
  staleThresholdMs: 5000,
  dirtyThreshold: 50,
  uncertainAfterTimeouts: 1,
  badAfterTimeouts: 3,
  deviceLatencyMinMs: 5,
  deviceLatencyMaxMs: 40,
  historianCapacity: 50000,
  historianBatchSize: 500,
  historianNearOverflowRatio: 0.8,
  historianDelayMs: 20,
  jobPhaseMs: 4000,
  /** Mandatory Safe Return (synthetic proof): minimum time per step, nominal feedback time after a
   *  command, and the feedback timeout that ends in SAFE_RETURN_FAILED. Not Production timings. */
  safeReturnStepMs: 1500,
  safeReturnFeedbackMs: 3000,
  safeReturnTimeoutMs: 20000,
  /** Delay applied by the synthetic "feedback delay" review controls. */
  safeReturnReviewDelayMs: 6000,
  pumpStartMs: 3000,
  pumpStopMs: 2000,
  /** Normalised synthetic pressure units (syn-units). Not a pressure setpoint. */
  pumpSetpoint: 100,
  pumpReadyBand: 10,
  publishIntervalMs: 1000,
  trendCapacity: 600,
  alarmItemLimit: 20,
  sseMaxBufferedBytes: 1_000_000,
  heartbeatMs: 15000,
  autoJobs: true,
});

// Sensor map, wall groups, Cannon slots, device distribution, and Thermocouple channel identifiers
// come from the single canonical mapping source. This file never generates Sensor IDs itself.
import { EXPECTED, JET_COUNT, TC_DEVICE_COUNT, WALL_GROUPS, getSensorMap } from '../../contracts/sensorMap.mjs';

export { JET_COUNT, TC_DEVICE_COUNT };
/** [wall, expectedSensorCount] in logical-column order (LEFT, REAR, RIGHT, FRONT). */
export const WALL_COUNTS = Object.freeze(WALL_GROUPS.map((g) => Object.freeze([g.wall, EXPECTED.perWall[g.wall]])));
export const MOTION_CONTROLLER_COUNT = 4;

/** Synthetic 'oscillate' dirty-mode sample: wall column 1, logical rows 1-2 of every wall (8 Sensors). */
export const isOscillationSample = (s) => s.wallColumn === 1 && s.logicalRow <= 2;

const pad2 = (n) => String(n).padStart(2, '0');

/**
 * 106 synthetic Sensor locations (canonical map), each folded from two synthetic Thermocouple
 * channels (TC_F, TC_R). Adds harness-only fields (register offsets, job target IDs).
 */
export function buildSensors() {
  return getSensorMap().sensors.map((m) => ({
    ...m,
    channelOffsets: [m.tcFrontOffset, m.tcRearOffset],
    jetId: m.assignedWaterJet,
    valveId: m.assignedIsolationValve,
  }));
}

export function buildDevices() {
  const devices = [];
  for (let d = 1; d <= TC_DEVICE_COUNT; d += 1) devices.push({ deviceId: `SYN-TC-${pad2(d)}`, kind: 'TC' });
  devices.push({ deviceId: 'SYN-PIO-01', kind: 'PIO' });
  devices.push({ deviceId: 'SYN-STS-01', kind: 'STS' });
  return devices;
}

/**
 * Synthetic Modbus-like poll plan. Fields: deviceId, functionCategory, startAddress,
 * quantity, decodeInstructions, pollGroup, updateTargets. Offsets are synthetic.
 */
export function buildPollPlan(sensors) {
  const plan = [];
  for (let d = 1; d <= TC_DEVICE_COUNT; d += 1) {
    const deviceId = `SYN-TC-${pad2(d)}`;
    const own = sensors.filter((s) => s.deviceId === deviceId);
    const quantity = own.length * 2;
    plan.push({
      id: `${deviceId}/fast/tc`,
      deviceId,
      functionCategory: 'INPUT_REGISTERS',
      startAddress: 0,
      quantity,
      decodeInstructions: Array.from({ length: quantity }, (_, i) => ({
        offset: i,
        type: 'INT16_SCALED',
        scale: 0.1,
        unit: 'syn-degC',
        target: `${deviceId}:CH${pad2(i)}`,
      })),
      pollGroup: 'FAST',
      updateTargets: own.map((s) => `sensor:${s.sensorId}`),
    });
    plan.push({
      id: `${deviceId}/slow/info`,
      deviceId,
      functionCategory: 'HOLDING_REGISTERS',
      startAddress: 100,
      quantity: 4,
      decodeInstructions: [{ offset: 0, type: 'UINT16', scale: 1, unit: 'none', target: 'device:info' }],
      pollGroup: 'SLOW',
      updateTargets: [`device:${deviceId}`],
    });
  }
  plan.push({
    id: 'SYN-PIO-01/fast/pressure',
    deviceId: 'SYN-PIO-01',
    functionCategory: 'INPUT_REGISTERS',
    startAddress: 0,
    quantity: 4,
    decodeInstructions: [0, 1, 2, 3].map((i) => ({ offset: i, type: 'INT16_SCALED', scale: 0.1, unit: 'syn-units', target: `pressure:${i}` })),
    pollGroup: 'FAST',
    updateTargets: ['pump', 'trend'],
  });
  plan.push({
    id: 'SYN-PIO-01/medium/jets-valves',
    deviceId: 'SYN-PIO-01',
    functionCategory: 'COILS',
    startAddress: 0,
    quantity: 16,
    decodeInstructions: [{ offset: 0, type: 'BITS', scale: 1, unit: 'none', target: 'jets-valves' }],
    pollGroup: 'MEDIUM',
    updateTargets: ['jets', 'valves'],
  });
  plan.push({
    id: 'SYN-PIO-01/slow/info',
    deviceId: 'SYN-PIO-01',
    functionCategory: 'HOLDING_REGISTERS',
    startAddress: 100,
    quantity: 4,
    decodeInstructions: [{ offset: 0, type: 'UINT16', scale: 1, unit: 'none', target: 'device:info' }],
    pollGroup: 'SLOW',
    updateTargets: ['device:SYN-PIO-01'],
  });
  plan.push({
    id: 'SYN-STS-01/medium/motion',
    deviceId: 'SYN-STS-01',
    functionCategory: 'DISCRETE_INPUTS',
    startAddress: 0,
    quantity: 4 * MOTION_CONTROLLER_COUNT,
    decodeInstructions: [{ offset: 0, type: 'BITS', scale: 1, unit: 'none', target: 'motion-status' }],
    pollGroup: 'MEDIUM',
    updateTargets: ['SYN-MC-1', 'SYN-MC-2', 'SYN-MC-3', 'SYN-MC-4'],
  });
  plan.push({
    id: 'SYN-STS-01/slow/info',
    deviceId: 'SYN-STS-01',
    functionCategory: 'HOLDING_REGISTERS',
    startAddress: 100,
    quantity: 4,
    decodeInstructions: [{ offset: 0, type: 'UINT16', scale: 1, unit: 'none', target: 'device:info' }],
    pollGroup: 'SLOW',
    updateTargets: ['device:SYN-STS-01'],
  });
  return plan;
}

/** Loopback-only bind guard. The harness must never listen on a non-loopback interface. */
export function assertLoopbackHost(host) {
  if (host !== '127.0.0.1' && host !== '::1') {
    throw new Error(`Refusing to bind to non-loopback host "${host}". The spike harness binds to 127.0.0.1 only.`);
  }
  return host;
}
