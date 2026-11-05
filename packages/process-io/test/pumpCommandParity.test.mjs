// Focused tests — Node-to-C# parity for the PUMP-COMMAND-PRODUCT-INTEGRATION contract.
//
// The C# Pump command contract is the Product contract, and no .NET SDK is available in Arena. These
// tests therefore read the C# source and pin it to the Owner-commissioned contract restated as data
// in pumpCommandEvidence.mjs. They catch the one defect class a text review cannot: a wrong tag, a
// wrong order, a wrong constant or a reintroduced superseded Stop order.
//
// They prove nothing about runtime behaviour. The Owner-local build and the focused Pump lifecycle
// .NET suite remain the behavioural validation of record.

import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

import { ROW_BINDINGS, ANALOG_OUTPUT_CONVERSION } from '../src/legacyEvidence.mjs';
import {
  PUMP_COMMAND_OUTPUTS, PUMP_ANALOG_OUTPUT, PUMP_FEEDBACK_INPUTS, PUMP_PRESSURE_INPUTS,
  MAINTAINED_SEMANTICS, SELECTION_SEQUENCES, START_SEQUENCE, STOP_SEQUENCE, SUPERSEDED_STOP_SEQUENCE,
  PUMP_READY_CONFIGURATION, FEEDBACK_POLARITY, PUMP_FAILURE_STATES, PUMP_PROGRESSION_STATES,
  PUMP_STOP_REASONS,
} from '../src/pumpCommandEvidence.mjs';

const ROOT = new URL('../../../', import.meta.url);
const read = (path) => readFileSync(new URL(path, ROOT), 'utf8');

const CONTRACTS = read('packages/contracts/PumpCommandContracts.cs');

// ---------------------------------------------------------------- C# extraction helpers

/** Reads one `public const string NAME = "VALUE";` declaration from the C# contract. */
function constString(source, name) {
  const match = new RegExp(`public const string ${name} = "([^"]+)";`).exec(source);
  assert.ok(match, `C# is missing const string ${name}`);
  return match[1];
}

/** Reads one `public const double NAME = VALUE;` or `public const int NAME = VALUE;` declaration. */
function constNumber(source, name) {
  const match = new RegExp(`public const (?:double|int) ${name} = ([0-9.]+);`).exec(source);
  assert.ok(match, `C# is missing numeric const ${name}`);
  return Number(match[1]);
}

/**
 * Extracts an ordered command sequence from the C# contract. The tags are declared as named
 * constants there, so each identifier is resolved through its own declaration rather than assumed.
 */
function sequence(source, property) {
  const anchor = `public static IReadOnlyList<PumpCommandStep> ${property}`;
  const start = source.indexOf(anchor);
  assert.ok(start >= 0, `C# is missing sequence ${property}`);
  const end = source.indexOf('};', start);
  assert.ok(end > start, `C# sequence ${property} is not terminated`);

  const block = source.slice(start, end);
  const steps = [...block.matchAll(/new PumpCommandStep\(\s*(\d+),\s*([A-Za-z0-9_]+),\s*(true|false)\s*\)/g)];
  assert.ok(steps.length > 0, `C# sequence ${property} declares no steps`);

  return steps.map((match) => ({
    order: Number(match[1]),
    tag: constString(source, match[2]),
    value: match[3] === 'true',
  }));
}

/** Extracts the body of one C# enum declaration, so a member name can be counted in isolation. */
function enumBody(source, name) {
  const start = source.indexOf(`public enum ${name}`);
  assert.ok(start >= 0, `C# is missing enum ${name}`);
  const end = source.indexOf('\n}', start);
  assert.ok(end > start, `C# enum ${name} is not terminated`);
  return source.slice(start, end);
}

// ---------------------------------------------------------------- identities

test('pump parity: every command tag is declared exactly and matches the adopted inventory', () => {
  assert.equal(constString(CONTRACTS, 'SelectPumpOneTag'), PUMP_COMMAND_OUTPUTS.SELECT_PUMP_1.tag);
  assert.equal(constString(CONTRACTS, 'SelectPumpTwoTag'), PUMP_COMMAND_OUTPUTS.SELECT_PUMP_2.tag);
  assert.equal(constString(CONTRACTS, 'EnableInverterTag'), PUMP_COMMAND_OUTPUTS.ENABLE_INVERTER.tag);
  assert.equal(constString(CONTRACTS, 'RunPumpTag'), PUMP_COMMAND_OUTPUTS.RUN_PUMP.tag);
  assert.equal(constString(CONTRACTS, 'SpeedReferenceTag'), PUMP_ANALOG_OUTPUT.tag);
  assert.equal(constString(CONTRACTS, 'RunningFeedbackTag'), PUMP_FEEDBACK_INPUTS.RUNNING.tag);
  assert.equal(constString(CONTRACTS, 'FaultFeedbackTag'), PUMP_FEEDBACK_INPUTS.FAULT.tag);
});

test('pump parity: the pressure identities are the adopted rows, not restated bindings', () => {
  // The contract must ALIAS the single Process-Io authority rather than re-declare the row.
  assert.match(
    CONTRACTS,
    /public const string PumpOutletPressureTag = ProcessIoIsolationValves\.PumpOutletPressureTag;/);
  assert.match(
    CONTRACTS,
    /public const string PumpInletPressureTag = ProcessIoIsolationValves\.PumpInletPressureTag;/);

  assert.equal(PUMP_PRESSURE_INPUTS.OUTLET.tag, 'AI-003+');
  assert.equal(PUMP_PRESSURE_INPUTS.INLET.tag, 'AI-002+');
});

test('pump parity: every adopted binding address matches the commissioned contract', () => {
  for (const entry of Object.values(PUMP_COMMAND_OUTPUTS)) {
    const binding = ROW_BINDINGS[entry.tag];
    assert.ok(binding, `no adopted binding for ${entry.tag}`);
    assert.equal(binding.area, 'COIL');
    assert.equal(binding.direction, 'WRITE');
    assert.equal(binding.address, entry.coil, `${entry.tag} coil address drifted`);
  }

  const running = ROW_BINDINGS[PUMP_FEEDBACK_INPUTS.RUNNING.tag];
  assert.equal(running.area, 'DISCRETE_INPUT');
  assert.equal(running.direction, 'READ');
  assert.equal(running.address, PUMP_FEEDBACK_INPUTS.RUNNING.discreteInput);

  const fault = ROW_BINDINGS[PUMP_FEEDBACK_INPUTS.FAULT.tag];
  assert.equal(fault.area, 'DISCRETE_INPUT');
  assert.equal(fault.direction, 'READ');
  assert.equal(fault.address, PUMP_FEEDBACK_INPUTS.FAULT.discreteInput);

  const outlet = ROW_BINDINGS[PUMP_PRESSURE_INPUTS.OUTLET.tag];
  assert.equal(outlet.area, 'INPUT_REGISTER');
  assert.equal(outlet.address, PUMP_PRESSURE_INPUTS.OUTLET.inputRegister);

  const inlet = ROW_BINDINGS[PUMP_PRESSURE_INPUTS.INLET.tag];
  assert.equal(inlet.area, 'INPUT_REGISTER');
  assert.equal(inlet.address, PUMP_PRESSURE_INPUTS.INLET.inputRegister);

  const analog = ROW_BINDINGS[PUMP_ANALOG_OUTPUT.tag];
  assert.equal(analog.area, 'HOLDING_REGISTER');
  assert.equal(analog.direction, 'WRITE');
  assert.equal(analog.address, PUMP_ANALOG_OUTPUT.register);
});

// ---------------------------------------------------------------- command semantics and order

test('pump parity: the command semantics are maintained with no pulse duration', () => {
  assert.equal(constString(CONTRACTS, 'CommandSemantics'), MAINTAINED_SEMANTICS.command);
  assert.equal(constString(CONTRACTS, 'PulseSemantics'), MAINTAINED_SEMANTICS.pulse);
  assert.equal(MAINTAINED_SEMANTICS.pulse, 'NO_PULSE_DURATION_APPLIES');

  // There is no pulse-width member anywhere in the contract.
  assert.doesNotMatch(CONTRACTS, /PulseDuration|PulseWidth|PulseMilliseconds/);
});

test('pump parity: the Start order is enable then run, both energised', () => {
  const start = sequence(CONTRACTS, 'StartSequence');
  assert.deepEqual(start, START_SEQUENCE.map((step) => ({ ...step })));
  assert.deepEqual(start.map((step) => step.tag), ['DO-003', 'DO-007']);
  assert.deepEqual(start.map((step) => step.value), [true, true]);
});

test('pump parity: the Stop order is run then enable, both de-energised', () => {
  const stop = sequence(CONTRACTS, 'StopSequence');
  assert.deepEqual(stop, STOP_SEQUENCE.map((step) => ({ ...step })));
  assert.deepEqual(stop.map((step) => step.tag), ['DO-007', 'DO-003']);
  assert.deepEqual(stop.map((step) => step.value), [false, false]);
});

test('pump parity: the superseded Stop order is absent from the Product contract', () => {
  const stop = sequence(CONTRACTS, 'StopSequence');
  assert.notDeepEqual(stop, SUPERSEDED_STOP_SEQUENCE.map((step) => ({ ...step })));
  assert.notEqual(stop[0].tag, 'DO-003');

  // Enable may never be cleared before Run anywhere in the C# Pump surface.
  assert.doesNotMatch(CONTRACTS, /EnableInverterTag, false\)[\s\S]{0,160}RunPumpTag, false\)/);
});

test('pump parity: selection is maintained and mutually exclusive in the inverse route', () => {
  const pump1 = sequence(CONTRACTS, 'SelectionSequence');
  assert.ok(pump1.length > 0);

  const source = CONTRACTS;
  const first = source.indexOf('if (selection == 1)');
  const second = source.indexOf('if (selection == 2)');
  assert.ok(first >= 0 && second > first);

  const blockOne = source.slice(first, second);
  const blockTwo = source.slice(second, source.indexOf('return Array.Empty<PumpCommandStep>();', second));

  assert.match(blockOne, /new PumpCommandStep\(1, SelectPumpOneTag, true\)/);
  assert.match(blockOne, /new PumpCommandStep\(2, SelectPumpTwoTag, false\)/);
  assert.match(blockTwo, /new PumpCommandStep\(1, SelectPumpTwoTag, true\)/);
  assert.match(blockTwo, /new PumpCommandStep\(2, SelectPumpOneTag, false\)/);

  assert.deepEqual(SELECTION_SEQUENCES[1].map((step) => step.tag), ['DO-001', 'DO-002']);
  assert.deepEqual(SELECTION_SEQUENCES[2].map((step) => step.tag), ['DO-002', 'DO-001']);
});

// ---------------------------------------------------------------- analog command

test('pump parity: the analog ramp is the commissioned 1000 ms interval and step of 2', () => {
  assert.equal(constNumber(CONTRACTS, 'RampIntervalMilliseconds'), PUMP_ANALOG_OUTPUT.rampIntervalMilliseconds);
  assert.equal(constNumber(CONTRACTS, 'RampStepEngineering'), PUMP_ANALOG_OUTPUT.rampStepEngineering);
  assert.equal(constNumber(CONTRACTS, 'SafeZeroEngineeringCommand'), PUMP_ANALOG_OUTPUT.safeZero);
  assert.equal(constNumber(CONTRACTS, 'EngineeringMinimum'), PUMP_ANALOG_OUTPUT.engineeringMin);
  assert.equal(constNumber(CONTRACTS, 'EngineeringMaximum'), PUMP_ANALOG_OUTPUT.engineeringMax);

  assert.equal(PUMP_ANALOG_OUTPUT.rampIntervalMilliseconds, 1000);
  assert.equal(PUMP_ANALOG_OUTPUT.rampStepEngineering, 2);
});

test('pump parity: the analog command keeps its commissioned milliamp unit and conversion', () => {
  assert.equal(constString(CONTRACTS, 'EngineeringUnit'), 'mA');
  assert.equal(PUMP_ANALOG_OUTPUT.engineeringUnit, 'mA');

  // The conversion itself is NOT restated in the Pump contract: the existing boundary owns it.
  assert.match(CONTRACTS, /ProcessIoEngineering\.TryPumpSpeedRaw/);

  const conversion = ANALOG_OUTPUT_CONVERSION['AO-001+'];
  assert.equal(conversion.engineeringUnit, 'mA');
  assert.equal(conversion.engineeringMin, PUMP_ANALOG_OUTPUT.engineeringMin);
  assert.equal(conversion.engineeringMax, PUMP_ANALOG_OUTPUT.engineeringMax);
  assert.equal(conversion.rawFullScale, PUMP_ANALOG_OUTPUT.rawFullScale);
  assert.equal(conversion.safeValue, PUMP_ANALOG_OUTPUT.safeZero);
});

// ---------------------------------------------------------------- readiness

test('pump parity: the Owner-initialized readiness configuration is exact and explicit', () => {
  assert.equal(constNumber(CONTRACTS, 'OwnerInitialPumpReadySetpointBar'), PUMP_READY_CONFIGURATION.setpointBar);
  assert.equal(
    constNumber(CONTRACTS, 'OwnerInitialFreshnessBudgetMilliseconds'),
    PUMP_READY_CONFIGURATION.freshnessBudgetMilliseconds);

  assert.equal(PUMP_READY_CONFIGURATION.setpointBar, 15);
  assert.equal(PUMP_READY_CONFIGURATION.freshnessBudgetMilliseconds, 2000);

  // Strictly greater than the setpoint: the comparison operator is `>` in the shared rule, and the
  // Pump contract supplies no comparison of its own.
  assert.doesNotMatch(CONTRACTS, /Bar >= thresholds\.PumpReadySetpointBar|Bar >= PumpReadySetpointBar/);
});

test('pump parity: the feedback polarity starts NOT_CONFIGURED and gates the command path', () => {
  assert.equal(FEEDBACK_POLARITY.initialState, 'NOT_CONFIGURED');

  const running = /public PumpFeedbackPolarity RunningPolarity \{ get; init; \} = PumpFeedbackPolarity\.([A-Z_]+);/
    .exec(CONTRACTS);
  const fault = /public PumpFeedbackPolarity FaultPolarity \{ get; init; \} = PumpFeedbackPolarity\.([A-Z_]+);/
    .exec(CONTRACTS);

  assert.ok(running, 'C# does not default RunningPolarity');
  assert.ok(fault, 'C# does not default FaultPolarity');
  assert.equal(running[1], FEEDBACK_POLARITY.initialState);
  assert.equal(fault[1], FEEDBACK_POLARITY.initialState);

  assert.match(CONTRACTS, /public const string NotCommissionedCode = "WJSS_PUMP_FEEDBACK_POLARITY_NOT_COMMISSIONED";/);
  assert.match(CONTRACTS, /public const string RefusedPolarityNotCommissioned = "WJSS_PUMP_COMMAND_REFUSED_POLARITY_NOT_COMMISSIONED";/);
});

// ---------------------------------------------------------------- state machine surface

test('pump parity: the state machine declares every progression and failure state exactly once', () => {
  const lifecycle = read('packages/application/ProcessIo/ProcessIoPumpLifecycle.cs');

  const states = enumBody(CONTRACTS, 'PumpLifecycleState');

  for (const state of PUMP_PROGRESSION_STATES) {
    const declarations = [...states.matchAll(new RegExp(`^\\s*${state} = \\d+,`, 'gm'))];
    assert.equal(declarations.length, 1, `${state} must be declared exactly once in PumpLifecycleState`);
  }

  for (const state of PUMP_FAILURE_STATES) {
    const declarations = [...states.matchAll(new RegExp(`^\\s*${state} = \\d+,`, 'gm'))];
    assert.equal(declarations.length, 1, `${state} must be declared exactly once in PumpLifecycleState`);
  }

  assert.equal(PUMP_PROGRESSION_STATES.length, 14);
  assert.equal(PUMP_FAILURE_STATES.length, 15);

  // Every Stop reason is modelled, and every one routes through the one Stop path.
  const reasons = enumBody(CONTRACTS, 'PumpStopReason');
  for (const reason of PUMP_STOP_REASONS) {
    assert.equal(
      [...reasons.matchAll(new RegExp(`^\\s*${reason} = \\d+,`, 'gm'))].length,
      1,
      `${reason} must be declared exactly once in PumpStopReason`);
  }

  // One lifecycle owns the correction: the correction is expressed once and consumed, not copied.
  assert.match(lifecycle, /_runCleared = true;/);
  assert.match(lifecycle, /_enableCleared = true;/);
  assert.ok(
    lifecycle.indexOf('_runCleared = true;') < lifecycle.indexOf('_enableCleared = true;'),
    'the lifecycle must clear Run before Enable');
});

test('pump parity: the one lifecycle exposes a single reusable surface, not a per-consumer machine', () => {
  const lifecycle = read('packages/application/ProcessIo/ProcessIoPumpLifecycle.cs');

  // Exactly one public lifecycle type and one public step method.
  assert.equal([...lifecycle.matchAll(/public sealed class ProcessIoPumpLifecycle/g)].length, 1);
  assert.equal([...lifecycle.matchAll(/public void Step\(\)/g)].length, 1);

  // The lifecycle commits to the corrected order through the shared contract, not a local copy.
  assert.match(lifecycle, /PumpCommandContract\.StopSequence|SetRunPump\(false\)/);
  assert.doesNotMatch(lifecycle, /new PumpCommandStep\(1, PumpCommandContract\.EnableInverterTag, false\)/);
});

test('pump parity: no raw transport, no arbitrary write and no UI command surface is added', () => {
  const files = [
    'packages/contracts/PumpCommandContracts.cs',
    'packages/application/ProcessIo/ProcessIoPumpCommandBoundary.cs',
    'packages/application/ProcessIo/ProcessIoPumpLifecycle.cs',
    'packages/application/CleaningPath/Composition/CleaningPathPumpLifecycleGate.cs',
  ];

  for (const path of files) {
    const source = read(path);
    // No new transport is constructed, and no write bypasses the session lifecycle.
    assert.doesNotMatch(source, /new ProcessIoPhysicalTransport|new TcpClient|Socket\(/);
    assert.doesNotMatch(source, /MapPost|MapPut|HttpClient/);
  }

  const boundary = read('packages/application/ProcessIo/ProcessIoPumpCommandBoundary.cs');
  assert.match(boundary, /_session\.AdmitWrite/);
  assert.match(boundary, /_session\.DispatchDigitalOutput/);
  assert.match(boundary, /_session\.DispatchAnalogOutput/);
});
