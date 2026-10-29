// Focused tests — physical bindings, address derivation, function codes, Authoritative T8 adoption
// completeness, spare/unresolved exclusion, duplicate/capacity/type refusal.

import test from 'node:test';
import assert from 'node:assert/strict';

import { loadCatalogue } from '../src/catalogue.mjs';
import {
  ROW_BINDINGS, FUNCTION_CODES, TRANSPORT_SHAPE, DEVICE_KEYS, OUTPUT_COMMAND_SHAPE,
  READ_BACK_SOURCES, ANALOG_CONVERSIONS, ANALOG_OUTPUT_CONVERSION, OWNER_RULINGS, LIFECYCLE,
} from '../src/legacyEvidence.mjs';
import { buildBindings, canAdmitWrite, BindingError, MODULE_CAPACITY, deriveAddress } from '../src/bindings.mjs';

const catalogue = loadCatalogue();
const bindings = buildBindings(catalogue);
const byTag = new Map(bindings.rows.map((r) => [r.tag, r]));

test('bindings: every Legacy/T8 binding resolves to a catalogue row (no orphan bindings)', () => {
  for (const tag of Object.keys(ROW_BINDINGS)) {
    assert.ok(catalogue.byTag[tag] !== undefined, `binding for unknown tag ${tag}`);
    const row = byTag.get(tag);
    assert.ok(row.bindingState.startsWith('BOUND_'), `${tag} not bound`);
    assert.equal(row.address.state, 'DERIVED_FROM_LEGACY_EVIDENCE');
  }
});

test('bindings: exact T8 process-image derivation (device/area/offset/function code)', () => {
  const ai003 = byTag.get('AI-003+');
  assert.equal(ai003.address.deviceKey, 'PROCESS_IO_W' + 'AGO_T8');
  assert.equal(ai003.address.area, 'INPUT_REGISTER');
  assert.equal(ai003.address.offset, 2);
  assert.equal(ai003.address.functionCode, 'FC04');
  const do023 = byTag.get('DO-023');
  assert.equal(do023.address.deviceKey, 'PROCESS_IO_W' + 'AGO_T8');
  assert.equal(do023.address.area, 'COIL');
  assert.equal(do023.address.offset, 22);
  assert.equal(do023.address.functionCode, 'FC05');
  const ao001 = byTag.get('AO-001+');
  assert.equal(ao001.address.deviceKey, 'PROCESS_IO_W' + 'AGO_T8');
  assert.equal(ao001.address.area, 'HOLDING_REGISTER');
  assert.equal(ao001.address.offset, 0);
  assert.equal(ao001.address.functionCode, 'FC06');
  const di001 = byTag.get('DI-001');
  assert.equal(di001.address.deviceKey, 'PROCESS_IO_W' + 'AGO_T8');
  assert.equal(di001.address.area, 'DISCRETE_INPUT');
  assert.equal(di001.address.offset, 0);
  assert.equal(di001.address.functionCode, 'FC02');
});

test('bindings: exact function-code mapping per signal class', () => {
  assert.equal(FUNCTION_CODES.AI_RAW_READ.fc, 'FC04');
  assert.equal(FUNCTION_CODES.DI_STATE_READ.fc, 'FC02');
  assert.equal(FUNCTION_CODES.DO_COMMAND.fc, 'FC05');
  assert.equal(FUNCTION_CODES.DO_READ_BACK.fc, 'FC01');
  assert.equal(FUNCTION_CODES.AO_COMMAND.fc, 'FC06');
  assert.equal(FUNCTION_CODES.AO_READ_BACK.fc, 'FC03');
  for (const row of bindings.rows) {
    if (!row.binding) continue;
    if (row.ioClass === 'AI') assert.equal(row.address.functionCode, 'FC04');
    if (row.ioClass === 'DI') assert.equal(row.address.functionCode, 'FC02');
    if (row.ioClass === 'DO') assert.equal(row.address.functionCode, 'FC05');
    if (row.ioClass === 'AO') assert.equal(row.address.functionCode, 'FC06');
  }
});

test('bindings: exact T8 endpoint/config shape adoption', () => {
  assert.equal(TRANSPORT_SHAPE.port, 502);
  assert.equal(TRANSPORT_SHAPE.unitId, 1);
  assert.equal(TRANSPORT_SHAPE.connectTimeoutMs, 1000);
  assert.equal(TRANSPORT_SHAPE.readTimeoutMs, 1000);
  assert.equal(TRANSPORT_SHAPE.writeTimeoutMs, 1000);
  assert.equal(TRANSPORT_SHAPE.pollIntervalMs, 200);
  assert.deepEqual(TRANSPORT_SHAPE.readGrouping, { holdingRegisters: { start: 0, count: 40 }, coils: { start: 0, count: 40 } });
  assert.deepEqual([...DEVICE_KEYS], ['PROCESS_IO_W' + 'AGO_T8']);
});

test('bindings: unbound rows stay ADDRESS_UNRESOLVED and inactive (spare/unresolved exclusion)', () => {
  const unbound = bindings.rows.filter((r) => r.bindingState === 'UNBOUND');
  assert.equal(unbound.length, 3);
  for (const row of unbound) {
    assert.equal(row.address.state, 'ADDRESS_UNRESOLVED');
    assert.equal(row.commissioningState, 'EXCLUDED');
  }
  // spare rows are classified as EXCLUDED
  for (const tag of ['DI-038', 'DI-039', 'DI-040', 'AI-036']) {
    const row = byTag.get(tag);
    assert.ok(row.bindingState.startsWith('BOUND_'));
    assert.equal(row.commissioningState, 'EXCLUDED');
  }
});

test('bindings: spare rows cannot receive write authority (structural)', () => {
  for (const row of bindings.rows.filter((r) => r.commissioningState === 'EXCLUDED')) {
    const verdict = canAdmitWrite(row, { physicalWriteAuthorized: true });
    assert.equal(verdict.admitted, false);
    assert.ok(['WJSS_WRITE_REFUSED_UNBOUND', 'WJSS_WRITE_REFUSED_EXCLUDED'].includes(verdict.code));
  }
});

test('bindings: uniqueness — no two rows share one device/area/offset/direction', () => {
  const seen = new Map();
  for (const row of bindings.rows) {
    if (!row.binding) continue;
    const key = `${row.binding.deviceKey}|${row.binding.area}|${row.binding.address}|${row.binding.direction}`;
    assert.ok(!seen.has(key), `${row.tag} duplicates ${seen.get(key)}`);
    seen.set(key, row.tag);
  }
});

test('bindings: capacity and signal-type validation refuses bad rows', () => {
  const mutated = {
    ...catalogue,
    rows: catalogue.rows.map((r, i) => (i === 0 ? { ...r, source: { ...r.source, Channel: '9' } } : r)),
  };
  assert.throws(() => buildBindings(mutated), (e) => e instanceof BindingError && e.code === 'CHANNEL_CAPACITY');
  const wrongClass = {
    ...catalogue,
    rows: catalogue.rows.map((r, i) => (i === 0 ? { ...r, derived: { ...r.derived, ioClass: 'AI' } } : r)),
  };
  assert.throws(() => buildBindings(wrongClass), (e) => e instanceof BindingError && e.code === 'SIGNAL_TYPE_MISMATCH');
});

test('bindings: module capacity profile matches the adopted rack', () => {
  assert.equal(MODULE_CAPACITY['750-430'].channels, 8);
  assert.equal(MODULE_CAPACITY['750-530'].ioClass, 'DO');
  assert.equal(MODULE_CAPACITY['750-471'].channels, 4);
  assert.equal(MODULE_CAPACITY['750-554'].channels, 2);
});

test('bindings: deriveAddress never invents an offset for unbound rows', () => {
  const unresolved = deriveAddress(null, 'AI');
  assert.equal(unresolved.state, 'ADDRESS_UNRESOLVED');
  assert.ok(unresolved.reasons.includes('NO_LEGACY_BINDING'));
});

test('bindings: owner rulings preserved (pressure identities and ordinal rules)', () => {
  assert.equal(OWNER_RULINGS['AI-002+'].role, 'DIAGNOSTIC_TREND_ONLY');
  assert.equal(OWNER_RULINGS['AI-002+'].pumpReady, false);
  assert.equal(OWNER_RULINGS['AI-003+'].role, 'SOLE_PUMP_READY_PRESSURE');
  assert.equal(OWNER_RULINGS['AI-003+'].pumpReady, true);
  assert.deepEqual([...OWNER_RULINGS.ivPressureRows], ['AI-004+', 'AI-005+', 'AI-006+', 'AI-007+', 'AI-008+', 'AI-009+', 'AI-010+', 'AI-011+']);
  assert.equal(OWNER_RULINGS.ivLowerLimitRows.length, 8);
  assert.equal(OWNER_RULINGS.ivUpperLimitRows.length, 8);
});

test('bindings: command shapes and read-back sources adopted', () => {
  assert.equal(OUTPUT_COMMAND_SHAPE['DO-008'].shape, 'PAIRED_COMPLEMENT');
  assert.equal(OUTPUT_COMMAND_SHAPE['DO-008'].pair, 'DO-009');
  assert.equal(OUTPUT_COMMAND_SHAPE['DO-001'].shape, 'MUTUAL_EXCLUSIVE_MAINTAINED');
  assert.equal(OUTPUT_COMMAND_SHAPE['DO-023'].shape, 'MAINTAINED');
  assert.equal(READ_BACK_SOURCES['DO-023'].readBackRow, 'DI-029');
  assert.equal(READ_BACK_SOURCES['AO-001+'].readBackRow, null);
});

test('bindings: every bound AI row has an adopted conversion or explicit raw-only state', () => {
  for (const tag of Object.keys(ROW_BINDINGS)) {
    if (!tag.startsWith('AI-')) continue;
    const conv = ANALOG_CONVERSIONS[tag];
    assert.ok(conv, `${tag} missing conversion record`);
    if (conv.kind === 'RAW_ONLY') assert.equal(conv.outputs.length, 0);
    else assert.ok(conv.outputs.length > 0);
  }
  assert.equal(ANALOG_OUTPUT_CONVERSION['AO-001+'].safeValue, 0);
  assert.equal(ANALOG_OUTPUT_CONVERSION['AO-001+'].engineeringUnit, 'mA');
});

test('bindings: lifecycle strengthening recorded with functional equivalence', () => {
  assert.equal(LIFECYCLE.pollIntervalMs, 200);
  assert.ok(LIFECYCLE.strengthened.sessionGeneration.includes('generation'));
  assert.ok(LIFECYCLE.strengthened.exactlyOnceAdmission.includes('one-shot'));
});
