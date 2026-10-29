// Focused tests — Node-to-C# parity: the typed Product contracts in
// packages/contracts/ProcessIoContracts.cs must mirror the adopted Authoritative T8
// registry in legacyEvidence.mjs exactly (device keys, transport shape, function codes, offsets).
// The C# records are the Product contracts; this parity pins both directions.

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

import {
  ROW_BINDINGS, TRANSPORT_SHAPE, FUNCTION_CODES, DEVICE_KEYS,
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

test('parity: engineering formulas match the Legacy map chains', () => {
  assert.ok(CS.includes('if (v >= inMax) v = inMax;'));
  assert.ok(CS.includes('return (v - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;'));
  assert.ok(CS.includes('=> LegacyMap(rawWord, 0, ProcessIoRepresentation.RawFullScale, 0, 40);'));
  assert.ok(CS.includes('double milliamps = LegacyMap(rawWord, 0, ProcessIoRepresentation.RawFullScale, 0, 20);'));
  assert.ok(CS.includes('return LegacyMap(milliamps, 4, 20, 0, 40);'));
});
