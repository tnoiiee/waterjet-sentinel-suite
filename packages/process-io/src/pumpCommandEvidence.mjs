// PUMP-COMMAND-PRODUCT-INTEGRATION: the Owner-commissioned Pump command contract, restated as data
// so that the C# Product contract can be parity-tested from Node.
//
// AUTHORITY: the latest Owner decision (command order, maintained semantics, selection exclusivity,
// CORRECTED Stop order, analog ramp shape, feedback commissioning gate) plus the authoritative
// adopted I/O inventory for every tag and address.
//
// This module holds identity only. It issues no command, opens no transport and carries no write
// authority.

/** The four Pump command outputs, in the exact selection/series shape the Owner commissioned. */
export const PUMP_COMMAND_OUTPUTS = Object.freeze({
  SELECT_PUMP_1: Object.freeze({ tag: 'DO-001', coil: 0 }),
  SELECT_PUMP_2: Object.freeze({ tag: 'DO-002', coil: 1 }),
  ENABLE_INVERTER: Object.freeze({ tag: 'DO-003', coil: 2 }),
  RUN_PUMP: Object.freeze({ tag: 'DO-007', coil: 6 }),
});

/** The analog speed reference. The engineering unit is milliamps, never pressure. */
export const PUMP_ANALOG_OUTPUT = Object.freeze({
  tag: 'AO-001+',
  register: 0,
  engineeringUnit: 'mA',
  engineeringMin: 0,
  engineeringMax: 20,
  rawFullScale: 32767,
  safeZero: 0,
  rampIntervalMilliseconds: 1000,
  rampStepEngineering: 2,
});

/** The two feedback inputs. Their physical polarity is NOT commissioned in the Product. */
export const PUMP_FEEDBACK_INPUTS = Object.freeze({
  RUNNING: Object.freeze({ tag: 'DI-004', discreteInput: 3 }),
  FAULT: Object.freeze({ tag: 'DI-005', discreteInput: 4 }),
});

/** The Pump-ready authority and the diagnostic-only inlet identity. */
export const PUMP_PRESSURE_INPUTS = Object.freeze({
  OUTLET: Object.freeze({ tag: 'AI-003+', inputRegister: 2, solePumpReadyAuthority: true }),
  INLET: Object.freeze({ tag: 'AI-002+', inputRegister: 1, solePumpReadyAuthority: false }),
});

/** Every command is a maintained level. No pulse width exists and no timer may clear one. */
export const MAINTAINED_SEMANTICS = Object.freeze({
  command: 'MAINTAINED_ACTIVE_HIGH',
  pulse: 'NO_PULSE_DURATION_APPLIES',
  functionCode: 'FC05',
});

/** Pump selection is maintained and mutually exclusive. Pump 2 is the inverse route. */
export const SELECTION_SEQUENCES = Object.freeze({
  1: Object.freeze([
    Object.freeze({ order: 1, tag: 'DO-001', value: true }),
    Object.freeze({ order: 2, tag: 'DO-002', value: false }),
  ]),
  2: Object.freeze([
    Object.freeze({ order: 1, tag: 'DO-002', value: true }),
    Object.freeze({ order: 2, tag: 'DO-001', value: false }),
  ]),
});

/** The commissioned Start order: enable the inverter, then run the pump. */
export const START_SEQUENCE = Object.freeze([
  Object.freeze({ order: 1, tag: 'DO-003', value: true }),
  Object.freeze({ order: 2, tag: 'DO-007', value: true }),
]);

/**
 * The CORRECTED Stop order. Run Pump is de-energised FIRST and Enable Inverter SECOND.
 *
 * The older Legacy source cleared Enable before Run. That order is SUPERSEDED and must never be
 * implemented, documented or tested as current behaviour.
 */
export const STOP_SEQUENCE = Object.freeze([
  Object.freeze({ order: 1, tag: 'DO-007', value: false }),
  Object.freeze({ order: 2, tag: 'DO-003', value: false }),
]);

/** The superseded order, carried ONLY so a test can prove it is absent from the Product. */
export const SUPERSEDED_STOP_SEQUENCE = Object.freeze([
  Object.freeze({ order: 1, tag: 'DO-003', value: false }),
  Object.freeze({ order: 2, tag: 'DO-007', value: false }),
]);

/** The Owner-initialized readiness configuration. Every member is explicit, never a silent default. */
export const PUMP_READY_CONFIGURATION = Object.freeze({
  setpointBar: 15,
  strictComparison: true,
  freshnessBudgetMilliseconds: 2000,
});

/** The Pump feedback commissioning states. Both inputs start uncommissioned. */
export const FEEDBACK_POLARITY = Object.freeze({
  NOT_CONFIGURED: 'NOT_CONFIGURED',
  NOT_COMMISSIONED: 'NOT_COMMISSIONED',
  ACTIVE_HIGH: 'ACTIVE_HIGH',
  ACTIVE_LOW: 'ACTIVE_LOW',
  initialState: 'NOT_CONFIGURED',
});

/** The fifteen terminal failure states of the one Pump lifecycle. */
export const PUMP_FAILURE_STATES = Object.freeze([
  'COMMAND_ADMISSION_REFUSED',
  'SELECTION_FAILED',
  'ENABLE_FAILED',
  'RUN_FAILED',
  'RUNNING_FEEDBACK_TIMEOUT',
  'FAULT_ACTIVE',
  'AI003_INVALID',
  'AI003_STALE',
  'AI003_TIMEOUT',
  'RAMP_DOWN_FAILED',
  'CLEAR_RUN_FAILED',
  'DISABLE_FAILED',
  'STOPPED_FEEDBACK_TIMEOUT',
  'CLEANUP_INCOMPLETE',
  'PHYSICAL_ABORT_REQUIRED',
]);

/** The fourteen progression states, in commissioned order. */
export const PUMP_PROGRESSION_STATES = Object.freeze([
  'IDLE',
  'SELECTING_PUMP',
  'PUMP_SELECTED',
  'ENABLE_DISPATCHED',
  'RUN_DISPATCHED',
  'WAITING_FOR_RUNNING',
  'RAMPING_OUTPUT',
  'WAITING_FOR_AI003_READY',
  'PUMP_READY',
  'RAMPING_DOWN',
  'CLEARING_RUN',
  'DISABLING_INVERTER',
  'WAITING_FOR_STOPPED',
  'STOPPED',
]);

/** Every reason that enters the same Stop path. */
export const PUMP_STOP_REASONS = Object.freeze([
  'NORMAL_COMPLETION',
  'CANCELLATION',
  'FAULT',
  'AI003_INVALID',
  'AI003_STALE',
  'AI003_TIMEOUT',
  'RUNNING_FEEDBACK_TIMEOUT',
  'DOWNSTREAM_IV_FAILURE',
  'CLEANING_JOB_FAILURE',
  'SEQUENCE_FAILURE',
  'APPLICATION_CLEANUP',
  'START_FAILURE',
]);
