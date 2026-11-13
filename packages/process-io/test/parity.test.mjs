// Focused tests — Node-to-C# parity: the typed Product contracts in
// packages/contracts/ProcessIoContracts.cs must mirror the adopted Authoritative T8
// registry in legacyEvidence.mjs exactly (device keys, transport shape, function codes, offsets).
// The C# records are the Product contracts; this parity pins both directions.

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

import {
  ROW_BINDINGS, TRANSPORT_SHAPE, FUNCTION_CODES, DEVICE_KEYS, ANALOG_CONVERSIONS,
} from '../src/legacyEvidence.mjs';

const CS_PATH = new URL('../../../packages/contracts/ProcessIoContracts.cs', import.meta.url);
const CS_APP_PATH = new URL('../../../packages/application/ProcessIo/ProcessIoFoundation.cs', import.meta.url);
const CS = readFileSync(CS_PATH, 'utf8');
const CS_APP = readFileSync(CS_APP_PATH, 'utf8');

test('parity: device keys mirror exactly', () => {
  assert.ok(CS.includes('public const string RackT8 = "PROCESS_IO_W" + "AGO_T8";'));
});

test('parity: transport shape mirrors exactly', () => {
  assert.ok(CS.includes(`public const int Port = ${TRANSPORT_SHAPE.port};`));
  assert.ok(CS.includes(`public const int UnitId = ${TRANSPORT_SHAPE.unitId};`));
  assert.ok(CS.includes(`public const int ConnectTimeoutMs = ${TRANSPORT_SHAPE.connectTimeoutMs};`));
  assert.ok(CS.includes(`public const int ReadTimeoutMs = ${TRANSPORT_SHAPE.readTimeoutMs};`));
  assert.ok(CS.includes(`public const int WriteTimeoutMs = ${TRANSPORT_SHAPE.writeTimeoutMs};`));
  assert.ok(CS.includes(`public const int PollIntervalMs = ${TRANSPORT_SHAPE.pollIntervalMs};`));
  assert.ok(CS.includes(`public const int ConnectionCheckIntervalMs = ${TRANSPORT_SHAPE.connectionCheckIntervalMs};`));
  assert.ok(CS.includes(`public const int ReadGroupStart = ${TRANSPORT_SHAPE.readGrouping.holdingRegisters.start};`));
  assert.ok(CS.includes(`public const int ReadGroupCount = ${TRANSPORT_SHAPE.readGrouping.holdingRegisters.count};`));
});

test('parity: function codes mirror exactly', () => {
  assert.ok(CS.includes(`public const string AiRawRead = "${FUNCTION_CODES.AI_RAW_READ.fc}";`));
  assert.ok(CS.includes(`public const string DiStateRead = "${FUNCTION_CODES.DI_STATE_READ.fc}";`));
  assert.ok(CS.includes(`public const string DoCommand = "${FUNCTION_CODES.DO_COMMAND.fc}";`));
  assert.ok(CS.includes(`public const string DoReadBack = "${FUNCTION_CODES.DO_READ_BACK.fc}";`));
  assert.ok(CS.includes(`public const string AoCommand = "${FUNCTION_CODES.AO_COMMAND.fc}";`));
  assert.ok(CS.includes(`public const string AoReadBack = "${FUNCTION_CODES.AO_READ_BACK.fc}";`));
});

test('parity: every ROW_BINDING entry mirrors with identical device/area/address/direction', () => {
  const areaOf = {
    HOLDING_REGISTER: 'ProcessIoArea.HoldingRegister',
    COIL: 'ProcessIoArea.Coil',
    INPUT_REGISTER: 'ProcessIoArea.InputRegister',
    DISCRETE_INPUT: 'ProcessIoArea.DiscreteInput',
  };
  const key = 'PROCESS_IO_W' + 'AGO_T8';
  for (const [tag, b] of Object.entries(ROW_BINDINGS)) {
    const deviceConst = { [key]: 'ProcessIoDevices.RackT8' }[b.deviceKey];
    const expected = `["${tag}"] = (${deviceConst}, ${areaOf[b.area]}, ${b.address}, "${b.direction}")`;
    assert.ok(CS.includes(expected), `C# missing binding: ${expected}`);
  }
});

test('parity: C# carries no more bindings than the Legacy registry (no invented rows)', () => {
  const entries = [...CS.matchAll(/\["([A-Z]{2}-\d{3}\+?)"\] = \(/g)].map((m) => m[1]);
  assert.equal(entries.length, Object.keys(ROW_BINDINGS).length);
  for (const tag of entries) assert.ok(Object.hasOwn(ROW_BINDINGS, tag), `C# invented binding ${tag}`);
});

test('parity: catalogue loader constants match the adopted inventory', () => {
  assert.ok(CS_APP.includes('public const string ExpectedProvenanceSha256 = "28245ad207a24078902fca48f4fa2622fbca631bd92f660aa4faf1681f4c5299";'));
  assert.ok(CS_APP.includes('public const int ExpectedRowCount = 111;'));
});

test('parity: engineering formulas mirror the adopted map chains', () => {
  assert.ok(CS.includes('if (v >= inMax) v = inMax;'));
  assert.ok(CS.includes('return (v - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;'));

  // Every engineered analog row in the mirror is one clamped stage over raw 0..32767. Rebuild the C# call
  // the mirror's steps denote and require it to be present: this pins both directions, not one.
  for (const [tag, conv] of Object.entries(ANALOG_CONVERSIONS)) {
    for (const out of conv.outputs) {
      assert.equal(out.steps.length, 1, `${tag}.${out.name} must be a one-stage chain after the 2026-11-07 corrections`);
      const [step] = out.steps;
      assert.deepEqual(
        { inMin: step.inMin, inMax: step.inMax, outMin: step.outMin },
        { inMin: 0, inMax: 32767, outMin: 0 },
        `${tag}.${out.name} must span raw 0..32767 from 0`);
      const call = `=> LegacyMap(rawWord, 0, ProcessIoRepresentation.RawFullScale, 0, ${step.outMax});`;
      assert.ok(CS.includes(call), `C# is missing the ${tag}.${out.name} chain: ${call}`);
    }
  }

  // The Pump Outlet and the WC/IV rows share one conversion (0..40 bar) and the water level follows Legacy
  // rev2 (0..240). Count the call sites so a second, drifting copy of either formula cannot appear.
  assert.equal([...CS.matchAll(/=> LegacyMap\(rawWord, 0, ProcessIoRepresentation\.RawFullScale, 0, 40\);/g)].length, 2);
  assert.equal([...CS.matchAll(/=> LegacyMap\(rawWord, 0, ProcessIoRepresentation\.RawFullScale, 0, 240\);/g)].length, 1);

  // The superseded two-stage pump chain (a second live-zero subtraction) must be gone from the Product.
  assert.doesNotMatch(CS, /milliamps = LegacyMap\(rawWord, 0, ProcessIoRepresentation\.RawFullScale, 0, 20\);/);
  assert.doesNotMatch(CS, /LegacyMap\(milliamps, 4, 20, 0, 40\)/);
  assert.doesNotMatch(CS, /=> LegacyMap\(rawWord, 0, ProcessIoRepresentation\.RawFullScale, 0, 270\);/);
});
