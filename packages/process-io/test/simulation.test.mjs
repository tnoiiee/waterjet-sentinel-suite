// Focused tests — deterministic Simulation (complete I/O classes) and commissioning-surface
// composition (complete catalogue visibility + filtering).

import test from 'node:test';
import assert from 'node:assert/strict';

import { runScenario, SCENARIO_IDS, SIMULATION_LABEL, SimulatedTransport } from '../src/simulation.mjs';
import { buildCommissioningView, equipmentGroupOf } from '../src/presentation.mjs';
import { loadCatalogue } from '../src/catalogue.mjs';
import { buildBindings } from '../src/bindings.mjs';

const catalogue = loadCatalogue();
const bindings = buildBindings(catalogue);

test('simulation: every scenario runs and is labelled SIMULATION', () => {
  for (const id of SCENARIO_IDS) {
    const result = runScenario(id);
    assert.equal(result.label, SIMULATION_LABEL);
    assert.equal(result.id, id);
  }
});

test('simulation: deterministic — two runs of every scenario are identical', () => {
  for (const id of SCENARIO_IDS) {
    assert.deepEqual(runScenario(id), runScenario(id));
  }
});

test('simulation: valid DI / stale DI / invalid DI / polarity states', () => {
  assert.equal(runScenario('di_valid').read.observation.interpretedState, 'ACTIVE');
  assert.equal(runScenario('di_stale').stale.stale, true);
  assert.equal(runScenario('di_invalid').observation.quality, 'BAD');
  assert.equal(runScenario('di_polarity_resolved').read.observation.polaritySource, 'LEGACY_RUNTIME_NAMED_STATE_TRUE');
  assert.equal(runScenario('di_polarity_unresolved').observation.interpretedState, 'UNRESOLVED');
});

test('simulation: valid AI raw / engineering / unresolved scaling / invalid input', () => {
  assert.equal(runScenario('ai_valid_raw').read.observation.rawWord, 16384);
  const eng = runScenario('ai_valid_engineering');
  assert.equal(eng.read.observation.engineering.bar.value, 16384 * 40 / 32767);
  assert.equal(runScenario('ai_scaling_unresolved').read.observation.scaling, 'UNRESOLVED');
  assert.equal(runScenario('ai_invalid_current').read.observation.diagnostic, 'ANALOG_RAW_OUT_OF_RANGE');
});

test('simulation: communication loss and recovery with new session generation', () => {
  assert.equal(runScenario('communication_loss').stopped, true);
  const rec = runScenario('recovery_new_generation');
  assert.equal(rec.fenced, true);
  assert.equal(rec.after, rec.before + 1);
});

test('simulation: DO admission, unauthorized refusal, spare/unresolved refusal, separation', () => {
  // do_admitted runs WITHOUT physical authorization -> admitted must be false (fail-closed)
  assert.equal(runScenario('do_admitted').admission.ok, false);
  assert.equal(runScenario('do_unauthorized_refusal').admission.code, 'WJSS_WRITE_REFUSED_NOT_AUTHORIZED');
  const spare = runScenario('do_unresolved_spare_refusal');
  assert.equal(spare.unbound.code, 'WJSS_WRITE_REFUSED_UNBOUND');
  assert.equal(spare.spare.code, 'WJSS_WRITE_REFUSED_EXCLUDED');
  const sep = runScenario('do_separation').view;
  assert.equal(sep.commanded, 'ON');
  assert.equal(sep.effective, 'UNKNOWN');
  assert.equal(sep.readBack, 'UNKNOWN');
});

test('simulation: AO admission and refusals', () => {
  assert.equal(runScenario('ao_admitted').conversion.ok, true);
  assert.equal(runScenario('ao_unresolved_range_refusal').conversion.code, 'AO_CONVERSION_UNRESOLVED');
  assert.equal(runScenario('ao_out_of_range_refusal').conversion.code, 'AO_ENGINEERING_OUT_OF_RANGE');
});

test('simulation: stale write fencing, no replay after reconnect, no write after close/Stop', () => {
  assert.equal(runScenario('stale_write_fencing').first.ok, true);
  assert.equal(runScenario('stale_write_fencing').replay.code, 'WJSS_STALE_WRITE_REPLAY');
  assert.equal(runScenario('no_replay_after_reconnect').replay.code, 'WJSS_STALE_WRITE_REPLAY');
  assert.equal(runScenario('no_write_after_close').admission.code, 'WJSS_WRITE_AFTER_CLOSE');
  assert.equal(runScenario('no_write_after_stop').admission.code, 'WJSS_WRITE_AFTER_STOP');
});

test('simulation: no fabricated observation on a closed session', () => {
  const result = runScenario('no_fabricated_observation').result;
  assert.equal(result.ok, false);
  assert.equal(result.code, 'WJSS_SESSION_NOT_OPEN');
});

test('simulation: transport spy sees zero writes on every refusal path', () => {
  const spy = new SimulatedTransport();
  const { ProcessIoSession } = sessionModule;
  const s = new ProcessIoSession('PROCESS_IO_PUMP_RACK', spy);
  s.open(1);
  s.admitWrite(1, { bindingState: 'UNBOUND', commissioningState: 'EXCLUDED' }, { physicalWriteAuthorized: true }, admitFn);
  assert.equal(spy.writes.length, 0);
  assert.equal(spy.networkAttempts, 0);
});

test('presentation: complete catalogue visibility (all 111 rows)', () => {
  const view = buildCommissioningView(catalogue, bindings);
  assert.equal(view.total, 111);
  assert.equal(view.rows.length, 111);
  assert.equal(view.label, 'SIMULATION');
});

test('presentation: filtering by ioClass / source status / binding / module / slot / group / search', () => {
  assert.equal(buildCommissioningView(catalogue, bindings, {}, { ioClass: 'DI' }).total, 40);
  assert.equal(buildCommissioningView(catalogue, bindings, {}, { ioClass: 'DO' }).total, 32);
  assert.equal(buildCommissioningView(catalogue, bindings, {}, { ioClass: 'AI' }).total, 36);
  assert.equal(buildCommissioningView(catalogue, bindings, {}, { ioClass: 'AO' }).total, 2);
  assert.equal(buildCommissioningView(catalogue, bindings, {}, { sourceClassification: 'SPARE' }).total, 6);
  assert.equal(buildCommissioningView(catalogue, bindings, {}, { bindingState: 'BOUND_LEGACY_EXACT' }).total,
    bindings.rows.filter((r) => r.bindingState === 'BOUND_LEGACY_EXACT').length);
  assert.equal(buildCommissioningView(catalogue, bindings, {}, { module: '750-430' }).total, 40);
  assert.equal(buildCommissioningView(catalogue, bindings, {}, { slot: '3' }).total, 8);
  assert.equal(buildCommissioningView(catalogue, bindings, {}, { ioSlot: '10' }).total, 4);
  const iv = buildCommissioningView(catalogue, bindings, {}, { equipmentGroup: 'IV_PRESSURE' });
  assert.equal(iv.total, 8);
  const search = buildCommissioningView(catalogue, bindings, {}, { search: 'Blower' });
  assert.equal(search.total, 8);
});

test('presentation: per-row display fields carry identity, binding, address status and provenance', () => {
  const view = buildCommissioningView(catalogue, bindings, { 'AI-003+': { rawWord: 100, engineering: { bar: { value: 1, unit: 'bar' } }, quality: 'GOOD', diagnostic: 'NONE' } });
  const ai003 = view.rows.find((r) => r.tag === 'AI-003+');
  assert.equal(ai003.physicalBinding.deviceKey, 'PROCESS_IO_PUMP_RACK');
  assert.equal(ai003.derivedAddressState, 'DERIVED_FROM_LEGACY_EVIDENCE');
  assert.equal(ai003.functionCodeStatus, 'FC03');
  assert.equal(ai003.rawObservation, 100);
  assert.equal(ai003.engineeringObservation.bar.value, 1);
  assert.equal(ai003.commissioningState, 'READ_ONLY_CANDIDATE');
  assert.equal(ai003.provenance.sha256, '28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299');
  const unbound = view.rows.find((r) => r.tag === 'DO-031');
  assert.equal(unbound.derivedAddressState, 'ADDRESS_UNRESOLVED');
  assert.equal(unbound.functionCodeStatus, 'UNRESOLVED');
});

test('presentation: equipment groups cover every row', () => {
  const groups = new Set(catalogue.rows.map((row) => equipmentGroupOf(row)));
  for (const expected of ['PUMP_PRESSURE', 'IV_PRESSURE', 'IV_LIMITS', 'PUMP_INVERTER', 'TANK_BANKS', 'BOILER_BANKS', 'IV_ACTUATION', 'BLOWER_CURRENT', 'SPARE', 'RACK_TOPOLOGY']) {
    assert.ok(groups.has(expected), `missing group ${expected}`);
  }
});

import * as sessionModule from '../src/lifecycle.mjs';
import { canAdmitWrite as admitFn } from '../src/bindings.mjs';
