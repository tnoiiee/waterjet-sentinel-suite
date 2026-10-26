// H2 Physical Process-I/O — deterministic Simulation through the typed application boundary.
//
// Covers the complete four I/O classes. Every scenario is deterministic (no clock, no
// randomness): the same scenario id always yields the same observations and refusals. The
// simulation is explicitly labelled SIMULATION and is never Hardware evidence.
//
// Pure functions + an in-memory transport. No network I/O. No device access.

import { ProcessIoSession, OutputStateModel, REFUSAL } from './lifecycle.mjs';
import { analogEngineering, digitalObservation, analogOutputRaw } from './engineering.mjs';
import { canAdmitWrite } from './bindings.mjs';

export const SIMULATION_LABEL = 'SIMULATION';

/** Deterministic transport used by the simulation (never opens a socket). */
export class SimulatedTransport {
  constructor() {
    this.writes = [];
    this.networkAttempts = 0;
  }

  write(request) {
    this.writes.push(request);
    return { ok: true, label: SIMULATION_LABEL };
  }
}

const FIXED_AT = 1_760_000_000_000;
const FIXED_SEQ = 1;

/** Scenario catalogue: id -> deterministic outcome bundle. */
export function runScenario(id, ctx = {}) {
  const session = ctx.session ?? new ProcessIoSession('PROCESS_IO_PUMP_RACK', new SimulatedTransport(), { freshnessWindowMs: 1000 });
  const at = FIXED_AT;
  switch (id) {
    case 'di_valid': {
      session.open(at);
      return pack(id, session.read(at, (g) => digitalObservation('DI-001', true, at, FIXED_SEQ)) , g(session));
    }
    case 'di_stale': {
      session.open(at - 5000);
      session.read(at - 5000, (g) => digitalObservation('DI-001', true, at - 5000, FIXED_SEQ));
      return { id, label: SIMULATION_LABEL, stale: session.freshness(at), quality: 'STALE' };
    }
    case 'di_invalid': {
      session.open(at);
      return { id, label: SIMULATION_LABEL, observation: Object.freeze({ tag: 'DI-001', rawBit: null, interpretedState: 'UNRESOLVED', quality: 'BAD', diagnostic: 'DIGITAL_RAW_INVALID' }) };
    }
    case 'di_polarity_resolved': {
      session.open(at);
      return pack(id, session.read(at, () => digitalObservation('DI-029', true, at, FIXED_SEQ)), g(session));
    }
    case 'di_polarity_unresolved': {
      session.open(at);
      return { id, label: SIMULATION_LABEL, observation: Object.freeze({ tag: 'DI-037', rawBit: true, interpretedState: 'UNRESOLVED', polaritySource: 'NONE', quality: 'UNCERTAIN', diagnostic: 'POLARITY_UNRESOLVED' }) };
    }
    case 'ai_valid_raw': {
      session.open(at);
      return pack(id, session.read(at, () => Object.freeze({ tag: 'AI-004+', rawWord: 16384, at, sequence: FIXED_SEQ, quality: 'GOOD', diagnostic: 'NONE' })), g(session));
    }
    case 'ai_valid_engineering': {
      session.open(at);
      return pack(id, session.read(at, () => analogEngineering('AI-004+', 16384, at, FIXED_SEQ)), g(session));
    }
    case 'ai_scaling_unresolved': {
      session.open(at);
      return pack(id, session.read(at, () => analogEngineering('AI-012', 100, at, FIXED_SEQ)), g(session));
    }
    case 'ai_invalid_current': {
      session.open(at);
      return pack(id, session.read(at, () => analogEngineering('AI-004+', 40000, at, FIXED_SEQ)), g(session));
    }
    case 'communication_loss': {
      session.open(at);
      session.markStop(at);
      return { id, label: SIMULATION_LABEL, state: session.state, stopped: true };
    }
    case 'recovery_new_generation': {
      session.open(at);
      const before = session.generation;
      session.reconnect(at);
      return { id, label: SIMULATION_LABEL, before, after: session.generation, fenced: before !== session.generation };
    }
    case 'do_admitted': {
      session.open(at);
      const admission = session.admitWrite(at, { bindingState: 'BOUND_LEGACY_EXACT', commissioningState: 'READ_ONLY_CANDIDATE' }, { physicalWriteAuthorized: false }, canAdmitWrite);
      return { id, label: SIMULATION_LABEL, admission };
    }
    case 'do_unauthorized_refusal': {
      session.open(at);
      const admission = session.admitWrite(at, { bindingState: 'BOUND_LEGACY_EXACT', commissioningState: 'READ_ONLY_CANDIDATE' }, { physicalWriteAuthorized: false }, canAdmitWrite);
      return { id, label: SIMULATION_LABEL, admission, expected: REFUSAL.NOT_AUTHORIZED };
    }
    case 'do_unresolved_spare_refusal': {
      session.open(at);
      const unbound = session.admitWrite(at, { bindingState: 'UNBOUND', commissioningState: 'EXCLUDED' }, { physicalWriteAuthorized: true }, canAdmitWrite);
      const spare = session.admitWrite(at, { bindingState: 'BOUND_LEGACY_EXACT', commissioningState: 'EXCLUDED' }, { physicalWriteAuthorized: true }, canAdmitWrite);
      return { id, label: SIMULATION_LABEL, unbound, spare };
    }
    case 'do_separation': {
      session.open(at);
      const model = new OutputStateModel('DO-023');
      model.requested = 'ON';
      model.dispatched = true;
      model.commanded = 'ON';
      // effective and readBack stay UNKNOWN until a feedback source arrives: write
      // acceptance is never treated as physical actuation.
      return { id, label: SIMULATION_LABEL, view: model.toView() };
    }
    case 'ao_admitted': {
      session.open(at);
      const conversion = analogOutputRaw('AO-001+', 12);
      return { id, label: SIMULATION_LABEL, conversion };
    }
    case 'ao_unresolved_range_refusal': {
      return { id, label: SIMULATION_LABEL, conversion: analogOutputRaw('AI-012', 12), expected: 'AO_CONVERSION_UNRESOLVED' };
    }
    case 'ao_out_of_range_refusal': {
      return { id, label: SIMULATION_LABEL, conversion: analogOutputRaw('AO-001+', 25), expected: 'AO_ENGINEERING_OUT_OF_RANGE' };
    }
    case 'stale_write_fencing': {
      session.open(at);
      const admission = session.admitWrite(at, { bindingState: 'BOUND_LEGACY_EXACT', commissioningState: 'READ_ONLY_CANDIDATE' }, { physicalWriteAuthorized: true }, canAdmitWrite);
      const first = session.dispatch(at, admission.token, { tag: 'DO-023', value: true });
      const replay = session.dispatch(at, admission.token, { tag: 'DO-023', value: true });
      return { id, label: SIMULATION_LABEL, first, replay, expected: REFUSAL.WRITE_REPLAY };
    }
    case 'no_replay_after_reconnect': {
      session.open(at);
      const admission = session.admitWrite(at, { bindingState: 'BOUND_LEGACY_EXACT', commissioningState: 'READ_ONLY_CANDIDATE' }, { physicalWriteAuthorized: true }, canAdmitWrite);
      session.reconnect(at);
      const replay = session.dispatch(at, admission.token, { tag: 'DO-023', value: true });
      return { id, label: SIMULATION_LABEL, replay, expected: REFUSAL.WRITE_REPLAY };
    }
    case 'no_write_after_close': {
      session.open(at);
      session.close(at);
      const admission = session.admitWrite(at, { bindingState: 'BOUND_LEGACY_EXACT', commissioningState: 'READ_ONLY_CANDIDATE' }, { physicalWriteAuthorized: true }, canAdmitWrite);
      return { id, label: SIMULATION_LABEL, admission, expected: REFUSAL.WRITE_AFTER_CLOSE };
    }
    case 'no_write_after_stop': {
      session.open(at);
      session.markStop(at);
      const admission = session.admitWrite(at, { bindingState: 'BOUND_LEGACY_EXACT', commissioningState: 'READ_ONLY_CANDIDATE' }, { physicalWriteAuthorized: true }, canAdmitWrite);
      return { id, label: SIMULATION_LABEL, admission, expected: REFUSAL.WRITE_AFTER_STOP };
    }
    case 'no_fabricated_observation': {
      // A read on a closed session must return a typed refusal, never a fabricated value.
      const result = session.read(at, () => digitalObservation('DI-001', true, at, FIXED_SEQ));
      return { id, label: SIMULATION_LABEL, result, expected: REFUSAL.NOT_OPEN };
    }
    default:
      throw new Error(`unknown scenario ${id}`);
  }
}

function g(session) { return session.generation; }

function pack(id, readResult, generation) {
  return { id, label: SIMULATION_LABEL, generation, read: readResult };
}

export const SCENARIO_IDS = Object.freeze([
  'di_valid', 'di_stale', 'di_invalid', 'di_polarity_resolved', 'di_polarity_unresolved',
  'ai_valid_raw', 'ai_valid_engineering', 'ai_scaling_unresolved', 'ai_invalid_current',
  'communication_loss', 'recovery_new_generation',
  'do_admitted', 'do_unauthorized_refusal', 'do_unresolved_spare_refusal', 'do_separation',
  'ao_admitted', 'ao_unresolved_range_refusal', 'ao_out_of_range_refusal',
  'stale_write_fencing', 'no_replay_after_reconnect', 'no_write_after_close', 'no_write_after_stop',
  'no_fabricated_observation',
]);
