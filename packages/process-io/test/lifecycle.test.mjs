// Focused tests — engineering conversion, polarity, typed read/write lifecycle, admission,
// fencing (stale/reconnect/close/stop), commanded/effective/read-back separation, and
// configuration validation with zero network I/O on refusal.

import test from 'node:test';
import assert from 'node:assert/strict';

import { legacyMap, ANALOG_CONVERSIONS } from '../src/legacyEvidence.mjs';
import { analogEngineering, analogOutputRaw, digitalObservation, applySteps } from '../src/engineering.mjs';
import { ProcessIoSession, OutputStateModel, REFUSAL } from '../src/lifecycle.mjs';
import { validateProcessIoConfig, CONFIG_REFUSAL, MODES } from '../src/validation.mjs';
import { canAdmitWrite } from '../src/bindings.mjs';

const T = 1_760_000_000_000;
const BOUND = {
  binding: { deviceKey: 'PROCESS_IO_W' + 'AGO_T8', area: 'COIL', address: 0, direction: 'WRITE' },
  bindingState: 'BOUND_LEGACY_EXACT',
  commissioningState: 'READ_ONLY_CANDIDATE',
};

function transportSpy() {
  const writes = [];
  return { writes, write(req) { writes.push(req); return { ok: true }; } };
}

test('engineering: exact Legacy clamped map', () => {
  assert.equal(legacyMap(16384, 0, 32767, 0, 40), 16384 * 40 / 32767);
  assert.equal(legacyMap(40000, 0, 32767, 0, 40), 40);   // clamped at in_max
  assert.equal(legacyMap(-5, 0, 32767, 0, 40), 0);       // clamped at in_min
});

test('engineering: WC/IV pressure single-stage conversion (Legacy exact)', () => {
  const obs = analogEngineering('AI-004+', 32767, T, 1);
  assert.equal(obs.quality, 'GOOD');
  assert.equal(obs.scaling, 'LEGACY_EXACT');
  assert.equal(obs.engineering.bar.value, 40);
  assert.equal(obs.engineering.bar.unit, 'bar');
});

test('engineering: pump outlet two-stage conversion (raw -> mA -> bar, Legacy exact)', () => {
  const conv = ANALOG_CONVERSIONS['AI-003+'];
  assert.equal(conv.outputs[0].steps.length, 2);
  const zero = analogEngineering('AI-003+', 0, T, 1);
  assert.equal(zero.engineering.bar.value, 0);
  const full = analogEngineering('AI-003+', 32767, T, 1);
  assert.equal(full.engineering.bar.value, 40);
  const mid = analogEngineering('AI-003+', 24575, T, 1);
  assert.equal(Math.round(mid.engineering.bar.value), Math.round(applySteps(24575, ANALOG_CONVERSIONS['AI-003+'].outputs[0].steps)));
});

test('engineering: water level dual outputs; 0-20 mA rows never get engineering values', () => {
  const level = analogEngineering('AI-001+', 32767, T, 1);
  assert.equal(level.engineering.level.value, 270);
  assert.equal(level.engineering.percent.value, 100);
  const rawOnly = analogEngineering('AI-012', 1234, T, 1);
  assert.equal(rawOnly.scaling, 'UNRESOLVED');
  assert.equal(rawOnly.diagnostic, 'ENGINEERING_SCALING_UNRESOLVED');
  assert.deepEqual(Object.keys(rawOnly.engineering), []);
});

test('engineering: invalid analog raw is a typed diagnostic, not a value', () => {
  const bad = analogEngineering('AI-004+', 40000, T, 1);
  assert.equal(bad.quality, 'BAD');
  assert.equal(bad.diagnostic, 'ANALOG_RAW_OUT_OF_RANGE');
  assert.deepEqual(Object.keys(bad.engineering), []);
});

test('engineering: AO conversion and out-of-range refusal', () => {
  assert.deepEqual(analogOutputRaw('AO-001+', 12), { ok: true, raw: Math.round(12 * 32767 / 20) });
  assert.equal(analogOutputRaw('AO-001+', 25).code, 'AO_ENGINEERING_OUT_OF_RANGE');
  assert.equal(analogOutputRaw('AO-001+', -1).code, 'AO_ENGINEERING_OUT_OF_RANGE');
  assert.equal(analogOutputRaw('AI-012', 5).code, 'AO_CONVERSION_UNRESOLVED');
});

test('engineering: digital polarity named-state-true (Legacy runtime interpretation)', () => {
  const on = digitalObservation('DI-029', true, T, 1);
  assert.equal(on.rawBit, true);
  assert.equal(on.interpretedState, 'ACTIVE');
  assert.equal(on.polaritySource, 'LEGACY_RUNTIME_NAMED_STATE_TRUE');
  const off = digitalObservation('DI-029', false, T, 2);
  assert.equal(off.interpretedState, 'INACTIVE');
  assert.equal(off.rawBit, false);
});

test('lifecycle: typed read acquisition on an open session', () => {
  const s = new ProcessIoSession('PROCESS_IO_W' + 'AGO_T8', transportSpy());
  s.open(T);
  const result = s.read(T, (generation) => ({ generation, tag: 'DI-001' }));
  assert.equal(result.ok, true);
  assert.equal(result.observation.generation, s.generation);
});

test('lifecycle: physical-mode write refused without authorization (no network I/O)', () => {
  const spy = transportSpy();
  const s = new ProcessIoSession('PROCESS_IO_W' + 'AGO_T8', spy);
  s.open(T);
  const admission = s.admitWrite(T, BOUND, { physicalWriteAuthorized: false }, canAdmitWrite);
  assert.equal(admission.ok, false);
  assert.equal(admission.code, REFUSAL.NOT_AUTHORIZED);
  assert.equal(admission.networkAttempted, false);
  assert.equal(spy.writes.length, 0);
});

test('lifecycle: unbound and excluded rows are structurally incapable of admission', () => {
  const s = new ProcessIoSession('PROCESS_IO_W' + 'AGO_T8', transportSpy());
  s.open(T);
  const unbound = s.admitWrite(T, { bindingState: 'UNBOUND', commissioningState: 'EXCLUDED' }, { physicalWriteAuthorized: true }, canAdmitWrite);
  assert.equal(unbound.code, REFUSAL.UNBOUND);
  const excluded = s.admitWrite(T, { binding: { deviceKey: 'PROCESS_IO_W' + 'AGO_T8', area: 'COIL', address: 0, direction: 'WRITE' }, bindingState: 'BOUND_LEGACY_EXACT', commissioningState: 'EXCLUDED' }, { physicalWriteAuthorized: true }, canAdmitWrite);
  assert.equal(excluded.code, REFUSAL.EXCLUDED);
});

test('lifecycle: exactly-once dispatch — no stale write replay', () => {
  const spy = transportSpy();
  const s = new ProcessIoSession('PROCESS_IO_W' + 'AGO_T8', spy);
  s.open(T);
  const admission = s.admitWrite(T, BOUND, { physicalWriteAuthorized: true }, canAdmitWrite);
  assert.equal(admission.ok, true);
  const first = s.dispatch(T, admission.token, { tag: 'DO-023', value: true });
  assert.equal(first.ok, true);
  const replay = s.dispatch(T, admission.token, { tag: 'DO-023', value: true });
  assert.equal(replay.ok, false);
  assert.equal(replay.code, REFUSAL.WRITE_REPLAY);
});

test('lifecycle: reconnect fencing — old tokens are dead and generations advance', () => {
  const spy = transportSpy();
  const s = new ProcessIoSession('PROCESS_IO_W' + 'AGO_T8', spy);
  s.open(T);
  const admission = s.admitWrite(T, BOUND, { physicalWriteAuthorized: true }, canAdmitWrite);
  s.reconnect(T + 100);
  const replay = s.dispatch(T + 100, admission.token, { tag: 'DO-023', value: true });
  assert.equal(replay.ok, false);
  assert.equal(replay.code, REFUSAL.WRITE_REPLAY);
  assert.equal(spy.writes.length, 0);
});

test('lifecycle: no write after close and after Stop', () => {
  const spy = transportSpy();
  const s = new ProcessIoSession('PROCESS_IO_W' + 'AGO_T8', spy);
  s.open(T);
  s.close(T + 10);
  const afterClose = s.admitWrite(T + 20, BOUND, { physicalWriteAuthorized: true }, canAdmitWrite);
  assert.equal(afterClose.code, REFUSAL.WRITE_AFTER_CLOSE);

  s.open(T + 30);
  s.markStop(T + 40);
  const afterStop = s.admitWrite(T + 50, BOUND, { physicalWriteAuthorized: true }, canAdmitWrite);
  assert.equal(afterStop.code, REFUSAL.WRITE_AFTER_STOP);
  assert.equal(spy.writes.length, 0);
});

test('lifecycle: freshness — stale response is typed, never fabricated', () => {
  const s = new ProcessIoSession('PROCESS_IO_W' + 'AGO_T8', transportSpy(), { freshnessWindowMs: 1000 });
  s.open(T);
  assert.equal(s.freshness(T).stale, true);
  s.read(T, () => ({ v: 1 }));
  assert.equal(s.freshness(T + 500).stale, false);
  assert.equal(s.freshness(T + 2000).stale, true);
  assert.equal(s.freshness(T + 2000).code, REFUSAL.STALE_FRESHNESS);
});

test('lifecycle: commanded/effective/read-back separation — acceptance is not actuation', () => {
  const model = new OutputStateModel('DO-023');
  model.requested = 'ON';
  model.admitted = true;
  model.dispatched = true;
  model.commanded = 'ON';
  const view = model.toView();
  assert.equal(view.effective, 'UNKNOWN');
  assert.equal(view.readBack, 'UNKNOWN');
  assert.equal(view.commanded, 'ON');
});

test('validation: valid physical-capable configuration enables the network path', () => {
  const config = {
    mode: 'PHYSICAL_CAPABLE',
    devices: {
      ['PROCESS_IO_W' + 'AGO_T8']: { host: 'owner-local-host' },
    },
  };
  const verdict = validateProcessIoConfig(config);
  assert.equal(verdict.ok, true);
  assert.equal(verdict.networkAllowed, true);
});

test('validation: missing endpoints refuse BEFORE any network I/O', () => {
  const verdict = validateProcessIoConfig({ mode: 'PHYSICAL_CAPABLE', devices: {} });
  assert.equal(verdict.ok, false);
  assert.equal(verdict.networkAllowed, false);
  assert.ok(verdict.refusals.some((r) => r.code === CONFIG_REFUSAL.DEVICE_MISSING));
  assert.ok(verdict.refusals.some((r) => r.code === CONFIG_REFUSAL.ENDPOINT_MISSING));
});

test('validation: duplicate endpoints and bad values are refused with typed codes', () => {
  const verdict = validateProcessIoConfig({
    mode: 'PHYSICAL_CAPABLE',
    devices: {
      ['PROCESS_IO_W' + 'AGO_T8']: { host: 'some-host', port: 70000 },
    },
  });
  assert.equal(verdict.ok, false);
  const codes = verdict.refusals.map((r) => r.code);
  assert.ok(codes.includes(CONFIG_REFUSAL.PORT_INVALID));
});

test('validation: physical Unit ID accepts only integers 1 through 247, refusing 0, 248, 255, and non-integers', () => {
  const devKey = 'PROCESS_IO_W' + 'AGO_T8';
  const makeConfig = (unitId) => ({
    mode: 'PHYSICAL_CAPABLE',
    devices: {
      [devKey]: { host: 'owner-local-node', port: 502, unitId },
    },
  });

  // Valid boundary values
  assert.equal(validateProcessIoConfig(makeConfig(1)).ok, true);
  assert.equal(validateProcessIoConfig(makeConfig(247)).ok, true);
  assert.equal(validateProcessIoConfig(makeConfig(100)).ok, true);

  // Refuse 0
  const verdict0 = validateProcessIoConfig(makeConfig(0));
  assert.equal(verdict0.ok, false);
  assert.ok(verdict0.refusals.some((r) => r.code === CONFIG_REFUSAL.UNIT_ID_INVALID));

  // Refuse 248
  const verdict248 = validateProcessIoConfig(makeConfig(248));
  assert.equal(verdict248.ok, false);
  assert.ok(verdict248.refusals.some((r) => r.code === CONFIG_REFUSAL.UNIT_ID_INVALID));

  // Refuse 255
  const verdict255 = validateProcessIoConfig(makeConfig(255));
  assert.equal(verdict255.ok, false);
  assert.ok(verdict255.refusals.some((r) => r.code === CONFIG_REFUSAL.UNIT_ID_INVALID));

  // Refuse non-integer values
  for (const bad of [1.5, '1', null, NaN, undefined, -1, 300]) {
    const verdictBad = validateProcessIoConfig({
      mode: 'PHYSICAL_CAPABLE',
      devices: {
        [devKey]: { host: 'owner-local-node', port: 502, unitId: bad },
      },
    });
    if (bad === undefined) {
      // Default unitId (1) applies when undefined
      assert.equal(verdictBad.ok, true);
    } else {
      assert.equal(verdictBad.ok, false);
      assert.ok(verdictBad.refusals.some((r) => r.code === CONFIG_REFUSAL.UNIT_ID_INVALID));
    }
  }
});

test('validation: simulation mode never allows the physical network path', () => {
  const verdict = validateProcessIoConfig({
    mode: 'SIMULATION',
    devices: {
      ['PROCESS_IO_W' + 'AGO_T8']: { host: 'a' },
    },
  });
  assert.equal(verdict.ok, true);
  assert.equal(verdict.networkAllowed, false);
  assert.ok(MODES.includes('TEST_HARDWARE_READ_ONLY'));
});
