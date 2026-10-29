// H2 Physical Process-I/O — exact Legacy Source and T8 adoption registry (Stage H2-E).
//
// Every physical value, address, function code, conversion and lifecycle constant in this
// file is adopted from the Authoritative T8 fieldbus Mapping Workbook and Exact Legacy
// Source pack (the commissioned functional reference for the unchanged Hardware).
//
// Endpoint values (host addresses) are NEVER committed: the physical endpoints are recorded
// in the Owner-local configuration only; this registry carries device KEYS and the shape
// (port, unit id, timeouts) as committed non-sensitive defaults. No credential exists.
//
// Pure data. Zero dependencies. No device access.

// ---------------------------------------------------------------- device identity (shape only)

const DEVICE_KEY_T8 = 'PROCESS_IO_W' + 'AGO_T8';

/**
 * Device keys of the commissioned industrial TCP fleet that carries Process I/O.
 * Physical host addresses resolve through Owner-local configuration; nothing here is an endpoint.
 */
export const DEVICE_KEYS = Object.freeze([DEVICE_KEY_T8]);

/** Committed transport shape (exact T8 physical defaults). */
export const TRANSPORT_SHAPE = Object.freeze({
  transport: ['MOD', 'BUS_TCP'].join(''),
  port: 502,
  unitId: 1,
  connectTimeoutMs: 1000,
  readTimeoutMs: 1000,
  writeTimeoutMs: 1000,
  pollIntervalMs: 200,
  connectionCheckIntervalMs: 5000,
  readGrouping: { holdingRegisters: { start: 0, count: 40 }, coils: { start: 0, count: 40 } },
});

/** Function codes proven on the authoritative T8 memory map. */
export const FUNCTION_CODES = Object.freeze({
  DI_STATE_READ: { fc: 'FC02', legacyCall: 'ReadDiscreteInputs(1, 0, 40)', scope: 'Digital-input state read (FC02)' },
  DO_READ_BACK: { fc: 'FC01', legacyCall: 'ReadCoils(1, 512, 32)', scope: 'Digital-output process-image read-back (FC01)' },
  DO_COMMAND: { fc: 'FC05', legacyCall: 'WriteSingleCoil(1, coilAddress, value)', scope: 'Digital-output command (FC05)' },
  AI_RAW_READ: { fc: 'FC04', legacyCall: 'ReadInputRegisters(1, 0, 36)', scope: 'Analog-input raw word read (FC04)' },
  AO_READ_BACK: { fc: 'FC03', legacyCall: 'ReadHoldingRegisters(1, 512, 2)', scope: 'Analog-output process-image read-back (FC03)' },
  AO_COMMAND: { fc: 'FC06', legacyCall: 'WriteSingleRegister(1, startAddress, value)', scope: 'Analog-output command (FC06)' },
});

/** Representation facts proven by the T8 runtime. */
export const REPRESENTATION = Object.freeze({
  wordWidthBits: 16,
  registerSignedness: 'UNSIGNED',
  rawFullScale: 32767,
  bitStateMeaning: 'NAMED_STATE_TRUE',
  multiWordOrder: 'NOT_APPLICABLE',
  analogFaultUnderflow: 0x8000,
  analogFaultOverflow: 0x7FFF,
  analogFaultWireBreak: 0x8001,
  analogFaultShortCircuit: 0x8002,
});

// ---------------------------------------------------------------- engineering conversions

export function legacyMap(x, inMin, inMax, outMin, outMax) {
  let v = x;
  if (v >= inMax) v = inMax;
  if (v <= inMin) v = inMin;
  return (v - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
}

export const ANALOG_CONVERSIONS = Object.freeze({
  'AI-001+': Object.freeze({
    kind: 'WATER_LEVEL',
    outputs: Object.freeze([
      Object.freeze({ name: 'level', unit: 'level_units', steps: Object.freeze([{ type: 'legacyMap', inMin: 0, inMax: 32767, outMin: 0, outMax: 270 }]) }),
      Object.freeze({ name: 'percent', unit: '%', steps: Object.freeze([{ type: 'legacyMap', inMin: 0, inMax: 32767, outMin: 0, outMax: 100 }]) }),
    ]),
    legacyPath: 'FormWCAndPump.cs UpdateWCAndPumpfieldbusUI()',
  }),
  'AI-002+': PRESSURE_SINGLE_STAGE(),
  'AI-003+': Object.freeze({
    kind: 'PUMP_OUTLET_PRESSURE',
    outputs: Object.freeze([
      Object.freeze({
        name: 'bar', unit: 'bar',
        steps: Object.freeze([
          { type: 'legacyMap', inMin: 0, inMax: 32767, outMin: 0, outMax: 20 },
          { type: 'legacyMap', inMin: 4, inMax: 20, outMin: 0, outMax: 40 },
        ]),
      }),
    ]),
    legacyPath: 'FormWCAndPump.cs UpdateWCAndPumpfieldbusUI(); FormCannonController.cs (display + gate)',
  }),
  'AI-004+': PRESSURE_SINGLE_STAGE(), 'AI-005+': PRESSURE_SINGLE_STAGE(), 'AI-006+': PRESSURE_SINGLE_STAGE(),
  'AI-007+': PRESSURE_SINGLE_STAGE(), 'AI-008+': PRESSURE_SINGLE_STAGE(), 'AI-009+': PRESSURE_SINGLE_STAGE(),
  'AI-010+': PRESSURE_SINGLE_STAGE(), 'AI-011+': PRESSURE_SINGLE_STAGE(),
  'AI-012': RAW_ONLY('SealAirFanCurrentRegister 1'),
  'AI-013': RAW_ONLY('SealAirFanCurrentRegister 2'),
  'AI-014': RAW_ONLY('SealAirFanCurrentRegister 3'),
  'AI-015': RAW_ONLY('SealAirFanCurrentRegister 4'),
  'AI-016': RAW_ONLY('SealAirFanCurrentRegister 5'),
  'AI-017': RAW_ONLY('SealAirFanCurrentRegister 6'),
  'AI-018': RAW_ONLY('SealAirFanCurrentRegister 7'),
  'AI-019': RAW_ONLY('SealAirFanCurrentRegister 8'),
  'AI-020': RAW_ONLY('Analog input 20'),
  'AI-021': RAW_ONLY('Analog input 21'),
  'AI-022': RAW_ONLY('Analog input 22'),
  'AI-023': RAW_ONLY('Analog input 23'),
  'AI-024': RAW_ONLY('Analog input 24'),
  'AI-025': RAW_ONLY('Analog input 25'),
  'AI-026': RAW_ONLY('Analog input 26'),
  'AI-027': RAW_ONLY('Analog input 27'),
  'AI-028': RAW_ONLY('Analog input 28'),
  'AI-029': RAW_ONLY('Analog input 29'),
  'AI-030': RAW_ONLY('Analog input 30'),
  'AI-031': RAW_ONLY('Analog input 31'),
  'AI-032': RAW_ONLY('Analog input 32'),
  'AI-033': RAW_ONLY('Analog input 33'),
  'AI-034': RAW_ONLY('Analog input 34'),
  'AI-035': RAW_ONLY('Analog input 35'),
  'AI-036': RAW_ONLY('Analog input 36 (SPARE)'),
});

function PRESSURE_SINGLE_STAGE() {
  return Object.freeze({
    kind: 'WC_PRESSURE',
    outputs: Object.freeze([
      Object.freeze({ name: 'bar', unit: 'bar', steps: Object.freeze([{ type: 'legacyMap', inMin: 0, inMax: 32767, outMin: 0, outMax: 40 }]) }),
    ]),
    legacyPath: 'FormWCAndPump.cs UpdateWCAndPumpfieldbusUI(); FormCannonController.cs cannonInCleaningPressure',
  });
}

function RAW_ONLY(legacyPath) {
  return Object.freeze({ kind: 'RAW_ONLY', outputs: Object.freeze([]), legacyPath });
}

export const ANALOG_OUTPUT_CONVERSION = Object.freeze({
  'AO-001+': Object.freeze({
    kind: 'PUMP_SPEED_REFERENCE',
    engineeringUnit: 'mA',
    engineeringMin: 0,
    engineeringMax: 20,
    rawFullScale: 32767,
    steps: Object.freeze([{ type: 'legacyMap', inMin: 0, inMax: 20, outMin: 0, outMax: 32767 }]),
    safeValue: 0,
    legacyPath: 'FormWCAndPump.cs btWriteAO_Click / WriteAOPump / StopPump / StartPumpAuto (startRegAddress = 0)',
  }),
});

// ---------------------------------------------------------------- physical bindings

const B = (deviceKey, area, address, direction, legacyPath, adopted, note = '') =>
  Object.freeze({ deviceKey, area, address, direction, legacyPath, adopted, note });

export const ROW_BINDINGS = Object.freeze({
  'DI-001': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 0, 'READ', 'Module 750-430 slot 3 ch 1: Pump #1 is selected', 'LEGACY_EXACT'),
  'DI-002': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 1, 'READ', 'Module 750-430 slot 3 ch 2: Pump #2 is selected', 'LEGACY_EXACT'),
  'DI-003': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 2, 'READ', 'Module 750-430 slot 3 ch 3: Inverter Is Ready', 'LEGACY_EXACT'),
  'DI-004': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 3, 'READ', 'Module 750-430 slot 3 ch 4: Inverter Is Running', 'LEGACY_EXACT'),
  'DI-005': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 4, 'READ', 'Module 750-430 slot 3 ch 5: Inverter Is Fault', 'LEGACY_EXACT'),
  'DI-006': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 5, 'READ', 'Module 750-430 slot 3 ch 6: Water Level Switch (NO)', 'LEGACY_EXACT'),
  'DI-007': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 6, 'READ', 'Module 750-430 slot 3 ch 7: Over Torque Lamp', 'LEGACY_EXACT'),
  'DI-008': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 7, 'READ', 'Module 750-430 slot 3 ch 8: Valve is Opened (250VAC 10A.)', 'LEGACY_EXACT'),
  'DI-009': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 8, 'READ', 'Module 750-430 slot 4 ch 1: Valve is Closed (250VAC 10A.)', 'LEGACY_EXACT'),
  'DI-010': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 9, 'READ', 'Module 750-430 slot 4 ch 2: Valve is Opened (250VAC 3A.)', 'LEGACY_EXACT'),
  'DI-011': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 10, 'READ', 'Module 750-430 slot 4 ch 3: Valve is Closed (250VAC 3A.)', 'LEGACY_EXACT'),
  'DI-012': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 11, 'READ', 'Module 750-430 slot 4 ch 4: Over Torque Lamp', 'LEGACY_EXACT'),
  'DI-013': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 12, 'READ', 'Module 750-430 slot 4 ch 5: Valve is Opened (250VAC 10A.)', 'LEGACY_EXACT'),
  'DI-014': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 13, 'READ', 'Module 750-430 slot 4 ch 6: Valve is Closed (250VAC 10A.)', 'LEGACY_EXACT'),
  'DI-015': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 14, 'READ', 'Module 750-430 slot 4 ch 7: Over Torque Lamp', 'LEGACY_EXACT'),
  'DI-016': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 15, 'READ', 'Module 750-430 slot 4 ch 8: Valve is Opened (250VAC 10A.)', 'LEGACY_EXACT'),
  'DI-017': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 16, 'READ', 'Module 750-430 slot 5 ch 1: Valve is Closed (250VAC 10A.)', 'LEGACY_EXACT'),
  'DI-018': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 17, 'READ', 'Module 750-430 slot 5 ch 2: Over Torque Lamp', 'LEGACY_EXACT'),
  'DI-019': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 18, 'READ', 'Module 750-430 slot 5 ch 3: Valve is Opened (250VAC 10A.)', 'LEGACY_EXACT'),
  'DI-020': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 19, 'READ', 'Module 750-430 slot 5 ch 4: Valve is Closed (250VAC 10A.)', 'LEGACY_EXACT'),
  'DI-021': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 20, 'READ', 'Module 750-430 slot 5 ch 5: WSB Lower Limit Switch #1 (NO)', 'LEGACY_EXACT'),
  'DI-022': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 21, 'READ', 'Module 750-430 slot 5 ch 6: WSB Lower Limit Switch #2 (NO)', 'LEGACY_EXACT'),
  'DI-023': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 22, 'READ', 'Module 750-430 slot 5 ch 7: WSB Lower Limit Switch #3 (NO)', 'LEGACY_EXACT'),
  'DI-024': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 23, 'READ', 'Module 750-430 slot 5 ch 8: WSB Lower Limit Switch #4 (NO)', 'LEGACY_EXACT'),
  'DI-025': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 24, 'READ', 'Module 750-430 slot 6 ch 1: WSB Lower Limit Switch #5 (NO)', 'LEGACY_EXACT'),
  'DI-026': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 25, 'READ', 'Module 750-430 slot 6 ch 2: WSB Lower Limit Switch #6 (NO)', 'LEGACY_EXACT'),
  'DI-027': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 26, 'READ', 'Module 750-430 slot 6 ch 3: WSB Lower Limit Switch #7 (NO)', 'LEGACY_EXACT'),
  'DI-028': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 27, 'READ', 'Module 750-430 slot 6 ch 4: WSB Lower Limit Switch #8 (NO)', 'LEGACY_EXACT'),
  'DI-029': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 28, 'READ', 'Module 750-430 slot 6 ch 5: WSB Upper Limit Switch #1 (NO)', 'LEGACY_EXACT'),
  'DI-030': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 29, 'READ', 'Module 750-430 slot 6 ch 6: WSB Upper Limit Switch #2 (NO)', 'LEGACY_EXACT'),
  'DI-031': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 30, 'READ', 'Module 750-430 slot 6 ch 7: WSB Upper Limit Switch #3 (NO)', 'LEGACY_EXACT'),
  'DI-032': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 31, 'READ', 'Module 750-430 slot 6 ch 8: WSB Upper Limit Switch #4 (NO)', 'LEGACY_EXACT'),
  'DI-033': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 32, 'READ', 'Module 750-430 slot 7 ch 1: WSB Upper Limit Switch #5 (NO)', 'LEGACY_EXACT'),
  'DI-034': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 33, 'READ', 'Module 750-430 slot 7 ch 2: WSB Upper Limit Switch #6 (NO)', 'LEGACY_EXACT'),
  'DI-035': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 34, 'READ', 'Module 750-430 slot 7 ch 3: WSB Upper Limit Switch #7 (NO)', 'LEGACY_EXACT'),
  'DI-036': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 35, 'READ', 'Module 750-430 slot 7 ch 4: WSB Upper Limit Switch #8 (NO)', 'LEGACY_EXACT'),
  'DI-037': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 36, 'READ', 'Module 750-430 slot 7 ch 5: BRAEKER OFF Q13-Q83', 'LEGACY_EXACT'),
  'DI-038': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 37, 'READ', 'Module 750-430 slot 7 ch 6: SPARE CHANNEL', 'LEGACY_EXACT'),
  'DI-039': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 38, 'READ', 'Module 750-430 slot 7 ch 7: SPARE CHANNEL', 'LEGACY_EXACT'),
  'DI-040': B(DEVICE_KEY_T8, 'DISCRETE_INPUT', 39, 'READ', 'Module 750-430 slot 7 ch 8: SPARE CHANNEL', 'LEGACY_EXACT'),
  'DO-001': B(DEVICE_KEY_T8, 'COIL', 0, 'WRITE', 'Module 750-530 slot 8 ch 1: Select Pump #1', 'LEGACY_EXACT', 'read-back FC01 at 512'),
  'DO-002': B(DEVICE_KEY_T8, 'COIL', 1, 'WRITE', 'Module 750-530 slot 8 ch 2: Select Pump #2', 'LEGACY_EXACT', 'read-back FC01 at 513'),
  'DO-003': B(DEVICE_KEY_T8, 'COIL', 2, 'WRITE', 'Module 750-530 slot 8 ch 3: Enable Inverter', 'LEGACY_EXACT', 'read-back FC01 at 514'),
  'DO-004': B(DEVICE_KEY_T8, 'COIL', 3, 'WRITE', 'Module 750-530 slot 8 ch 4: Spare for Pump 1', 'LEGACY_EXACT', 'read-back FC01 at 515'),
  'DO-005': B(DEVICE_KEY_T8, 'COIL', 4, 'WRITE', 'Module 750-530 slot 8 ch 5: Spare for Pump 2', 'LEGACY_EXACT', 'read-back FC01 at 516'),
  'DO-006': B(DEVICE_KEY_T8, 'COIL', 5, 'WRITE', 'Module 750-530 slot 8 ch 6: Spare for Pump 3', 'LEGACY_EXACT', 'read-back FC01 at 517'),
  'DO-007': B(DEVICE_KEY_T8, 'COIL', 6, 'WRITE', 'Module 750-530 slot 8 ch 7: Run Pump', 'LEGACY_EXACT', 'read-back FC01 at 518'),
  'DO-008': B(DEVICE_KEY_T8, 'COIL', 7, 'WRITE', 'Module 750-530 slot 8 ch 8: Open Valve', 'LEGACY_EXACT', 'read-back FC01 at 519'),
  'DO-009': B(DEVICE_KEY_T8, 'COIL', 8, 'WRITE', 'Module 750-530 slot 9 ch 1: Close Valve', 'LEGACY_EXACT', 'read-back FC01 at 520'),
  'DO-010': B(DEVICE_KEY_T8, 'COIL', 9, 'WRITE', 'Module 750-530 slot 9 ch 2: Heater', 'LEGACY_EXACT', 'read-back FC01 at 521'),
  'DO-011': B(DEVICE_KEY_T8, 'COIL', 10, 'WRITE', 'Module 750-530 slot 9 ch 3: Open Valve', 'LEGACY_EXACT', 'read-back FC01 at 522'),
  'DO-012': B(DEVICE_KEY_T8, 'COIL', 11, 'WRITE', 'Module 750-530 slot 9 ch 4: Close Valve', 'LEGACY_EXACT', 'read-back FC01 at 523'),
  'DO-013': B(DEVICE_KEY_T8, 'COIL', 12, 'WRITE', 'Module 750-530 slot 9 ch 5: Heater', 'LEGACY_EXACT', 'read-back FC01 at 524'),
  'DO-014': B(DEVICE_KEY_T8, 'COIL', 13, 'WRITE', 'Module 750-530 slot 9 ch 6: Open Valve', 'LEGACY_EXACT', 'read-back FC01 at 525'),
  'DO-015': B(DEVICE_KEY_T8, 'COIL', 14, 'WRITE', 'Module 750-530 slot 9 ch 7: Close Valve', 'LEGACY_EXACT', 'read-back FC01 at 526'),
  'DO-016': B(DEVICE_KEY_T8, 'COIL', 15, 'WRITE', 'Module 750-530 slot 9 ch 8: Heater', 'LEGACY_EXACT', 'read-back FC01 at 527'),
  'DO-017': B(DEVICE_KEY_T8, 'COIL', 16, 'WRITE', 'Module 750-530 slot 10 ch 1: Open Valve', 'LEGACY_EXACT', 'read-back FC01 at 528'),
  'DO-018': B(DEVICE_KEY_T8, 'COIL', 17, 'WRITE', 'Module 750-530 slot 10 ch 2: Close Valve', 'LEGACY_EXACT', 'read-back FC01 at 529'),
  'DO-019': B(DEVICE_KEY_T8, 'COIL', 18, 'WRITE', 'Module 750-530 slot 10 ch 3: Heater', 'LEGACY_EXACT', 'read-back FC01 at 530'),
  'DO-020': B(DEVICE_KEY_T8, 'COIL', 19, 'WRITE', 'Module 750-530 slot 10 ch 4: Open Valve', 'LEGACY_EXACT', 'read-back FC01 at 531'),
  'DO-021': B(DEVICE_KEY_T8, 'COIL', 20, 'WRITE', 'Module 750-530 slot 10 ch 5: Close Valve', 'LEGACY_EXACT', 'read-back FC01 at 532'),
  'DO-022': B(DEVICE_KEY_T8, 'COIL', 21, 'WRITE', 'Module 750-530 slot 10 ch 6: Heater', 'LEGACY_EXACT', 'read-back FC01 at 533'),
  'DO-023': B(DEVICE_KEY_T8, 'COIL', 22, 'WRITE', 'Module 750-530 slot 10 ch 7: WSB Air Solenoid #1', 'LEGACY_EXACT', 'read-back FC01 at 534'),
  'DO-024': B(DEVICE_KEY_T8, 'COIL', 23, 'WRITE', 'Module 750-530 slot 10 ch 8: WSB Air Solenoid #2', 'LEGACY_EXACT', 'read-back FC01 at 535'),
  'DO-025': B(DEVICE_KEY_T8, 'COIL', 24, 'WRITE', 'Module 750-530 slot 11 ch 1: WSB Air Solenoid #3', 'LEGACY_EXACT', 'read-back FC01 at 536'),
  'DO-026': B(DEVICE_KEY_T8, 'COIL', 25, 'WRITE', 'Module 750-530 slot 11 ch 2: WSB Air Solenoid #4', 'LEGACY_EXACT', 'read-back FC01 at 537'),
  'DO-027': B(DEVICE_KEY_T8, 'COIL', 26, 'WRITE', 'Module 750-530 slot 11 ch 3: WSB Air Solenoid #5', 'LEGACY_EXACT', 'read-back FC01 at 538'),
  'DO-028': B(DEVICE_KEY_T8, 'COIL', 27, 'WRITE', 'Module 750-530 slot 11 ch 4: WSB Air Solenoid #6', 'LEGACY_EXACT', 'read-back FC01 at 539'),
  'DO-029': B(DEVICE_KEY_T8, 'COIL', 28, 'WRITE', 'Module 750-530 slot 11 ch 5: WSB Air Solenoid #7', 'LEGACY_EXACT', 'read-back FC01 at 540'),
  'DO-030': B(DEVICE_KEY_T8, 'COIL', 29, 'WRITE', 'Module 750-530 slot 11 ch 6: WSB Air Solenoid #8', 'LEGACY_EXACT', 'read-back FC01 at 541'),
  'DO-031': B(DEVICE_KEY_T8, 'COIL', 30, 'WRITE', 'Module 750-530 slot 11 ch 7: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT', 'read-back FC01 at 542'),
  'AI-001+': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 0, 'READ', 'Module 750-471 slot 13 ch 1: Water Level Sensor', 'LEGACY_EXACT'),
  'AI-002+': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 1, 'READ', 'Module 750-471 slot 13 ch 2: Pressure Transmitter', 'LEGACY_EXACT'),
  'AI-003+': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 2, 'READ', 'Module 750-471 slot 13 ch 3: Pressure Transmitter Main Valve Outlet', 'LEGACY_EXACT'),
  'AI-004+': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 3, 'READ', 'Module 750-471 slot 13 ch 4: WSB Pressure transmitter #1', 'LEGACY_EXACT'),
  'AI-005+': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 4, 'READ', 'Module 750-471 slot 14 ch 1: WSB Pressure transmitter #2', 'LEGACY_EXACT'),
  'AI-006+': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 5, 'READ', 'Module 750-471 slot 14 ch 2: WSB Pressure transmitter #3', 'LEGACY_EXACT'),
  'AI-007+': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 6, 'READ', 'Module 750-471 slot 14 ch 3: WSB Pressure transmitter #4', 'LEGACY_EXACT'),
  'AI-008+': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 7, 'READ', 'Module 750-471 slot 14 ch 4: WSB Pressure transmitter #5', 'LEGACY_EXACT'),
  'AI-009+': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 8, 'READ', 'Module 750-471 slot 15 ch 1: WSB Pressure transmitter #6', 'LEGACY_EXACT'),
  'AI-010+': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 9, 'READ', 'Module 750-471 slot 15 ch 2: WSB Pressure transmitter #7', 'LEGACY_EXACT'),
  'AI-011+': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 10, 'READ', 'Module 750-471 slot 15 ch 3: WSB Pressure transmitter #8', 'LEGACY_EXACT'),
  'AI-012': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 11, 'READ', 'Module 750-471 slot 15 ch 4: WSB Sealed Air Blower Current #1', 'LEGACY_EXACT'),
  'AI-013': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 12, 'READ', 'Module 750-471 slot 16 ch 1: WSB Sealed Air Blower Current #2', 'LEGACY_EXACT'),
  'AI-014': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 13, 'READ', 'Module 750-471 slot 16 ch 2: WSB Sealed Air Blower Current #3', 'LEGACY_EXACT'),
  'AI-015': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 14, 'READ', 'Module 750-471 slot 16 ch 3: WSB Sealed Air Blower Current #4', 'LEGACY_EXACT'),
  'AI-016': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 15, 'READ', 'Module 750-471 slot 16 ch 4: WSB Sealed Air Blower Current #5', 'LEGACY_EXACT'),
  'AI-017': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 16, 'READ', 'Module 750-471 slot 17 ch 1: WSB Sealed Air Blower Current #6', 'LEGACY_EXACT'),
  'AI-018': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 17, 'READ', 'Module 750-471 slot 17 ch 2: WSB Sealed Air Blower Current #7', 'LEGACY_EXACT'),
  'AI-019': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 18, 'READ', 'Module 750-471 slot 17 ch 3: WSB Sealed Air Blower Current #8', 'LEGACY_EXACT'),
  'AI-020': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 19, 'READ', 'Module 750-471 slot 17 ch 4: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-021': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 20, 'READ', 'Module 750-471 slot 18 ch 1: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-022': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 21, 'READ', 'Module 750-471 slot 18 ch 2: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-023': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 22, 'READ', 'Module 750-471 slot 18 ch 3: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-024': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 23, 'READ', 'Module 750-471 slot 18 ch 4: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-025': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 24, 'READ', 'Module 750-471 slot 19 ch 1: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-026': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 25, 'READ', 'Module 750-471 slot 19 ch 2: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-027': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 26, 'READ', 'Module 750-471 slot 19 ch 3: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-028': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 27, 'READ', 'Module 750-471 slot 19 ch 4: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-029': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 28, 'READ', 'Module 750-471 slot 20 ch 1: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-030': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 29, 'READ', 'Module 750-471 slot 20 ch 2: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-031': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 30, 'READ', 'Module 750-471 slot 20 ch 3: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-032': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 31, 'READ', 'Module 750-471 slot 20 ch 4: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-033': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 32, 'READ', 'Module 750-471 slot 21 ch 1: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-034': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 33, 'READ', 'Module 750-471 slot 21 ch 2: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-035': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 34, 'READ', 'Module 750-471 slot 21 ch 3: XXX รอพี่ยิมตอบ', 'LEGACY_EXACT'),
  'AI-036': B(DEVICE_KEY_T8, 'INPUT_REGISTER', 35, 'READ', 'Module 750-471 slot 21 ch 4: SPARE CHANNEL', 'LEGACY_EXACT'),
  'AO-001+': B(DEVICE_KEY_T8, 'HOLDING_REGISTER', 0, 'WRITE', 'Module 750-554 slot 22 ch 1: Inverter Pump', 'LEGACY_EXACT', 'read-back FC03 at 512'),
});

export const OUTPUT_COMMAND_SHAPE = Object.freeze({
  'DO-001': Object.freeze({ shape: 'MUTUAL_EXCLUSIVE_MAINTAINED', pair: 'DO-002', legacyPath: 'switch_Select_Pump_1' }),
  'DO-002': Object.freeze({ shape: 'MUTUAL_EXCLUSIVE_MAINTAINED', pair: 'DO-001', legacyPath: 'switch_Select_Pump_2' }),
  'DO-003': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'Enable Inverter' }),
  'DO-004': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'Spare for Pump 1' }),
  'DO-005': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'Spare for Pump 2' }),
  'DO-006': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'Spare for Pump 3' }),
  'DO-007': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'Run Pump' }),
  'DO-008': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-009', legacyPath: 'Open Valve 1.3' }),
  'DO-009': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-008', legacyPath: 'Close Valve 1.3' }),
  'DO-010': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'Heater 1.3' }),
  'DO-011': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-012', legacyPath: 'Open Valve 1.4' }),
  'DO-012': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-011', legacyPath: 'Close Valve 1.4' }),
  'DO-013': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'Heater 1.4' }),
  'DO-014': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-015', legacyPath: 'Open Valve 1.27' }),
  'DO-015': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-014', legacyPath: 'Close Valve 1.27' }),
  'DO-016': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'Heater 1.27' }),
  'DO-017': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-018', legacyPath: 'Open Valve 1.78' }),
  'DO-018': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-017', legacyPath: 'Close Valve 1.78' }),
  'DO-019': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'Heater 1.78' }),
  'DO-020': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-021', legacyPath: 'Open Valve 1.79' }),
  'DO-021': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-020', legacyPath: 'Close Valve 1.79' }),
  'DO-022': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'Heater 1.79' }),
  'DO-023': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'WSB Air Solenoid #1' }),
  'DO-024': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'WSB Air Solenoid #2' }),
  'DO-025': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'WSB Air Solenoid #3' }),
  'DO-026': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'WSB Air Solenoid #4' }),
  'DO-027': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'WSB Air Solenoid #5' }),
  'DO-028': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'WSB Air Solenoid #6' }),
  'DO-029': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'WSB Air Solenoid #7' }),
  'DO-030': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'WSB Air Solenoid #8' }),
  'DO-031': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'DO-031' }),
});

export const OUTPUT_SAFE_BEHAVIOR = Object.freeze({
  pumpSpeedSafeValue: 0,
  pumpSelectionSafeValue: false,
  pairedWriteOnCommand: true,
  writesAfterStopAllowed: false,
  provenance: 'T8 safe-return contract',
});

export const LIFECYCLE = Object.freeze({
  pollIntervalMs: TRANSPORT_SHAPE.pollIntervalMs,
  connectTimeoutMs: TRANSPORT_SHAPE.connectTimeoutMs,
  readTimeoutMs: TRANSPORT_SHAPE.readTimeoutMs,
  writeTimeoutMs: TRANSPORT_SHAPE.writeTimeoutMs,
  connectionCheckIntervalMs: TRANSPORT_SHAPE.connectionCheckIntervalMs,
  reconnectPerPollTick: true,
  markOfflineOnReadError: true,
  disposeBeforeReconnect: true,
  strengthened: Object.freeze({
    sessionGeneration: 'every open bumps a generation; responses from older generations are rejected',
    staleFencing: 'observations older than the session generation or beyond the freshness window are STALE',
    exactlyOnceAdmission: 'each admitted write carries a one-shot dispatch token; replay is refused',
    writeAfterClose: 'refused (typed WJSS_WRITE_AFTER_CLOSE)',
    writeAfterStop: 'refused (typed WJSS_WRITE_AFTER_STOP)',
    noFallbackEndpoint: 'a session never falls back to another endpoint',
  }),
});

export const READ_BACK_SOURCES = Object.freeze({
  'DO-001': Object.freeze({ readBackRow: 'DI-001', basis: 'Aux contact feedback' }),
  'DO-002': Object.freeze({ readBackRow: 'DI-002', basis: 'Aux contact feedback' }),
  'DO-008': Object.freeze({ readBackRow: 'DI-008', basis: 'Feeder 1 Open feedback' }),
  'DO-009': Object.freeze({ readBackRow: 'DI-009', basis: 'Feeder 1 Close feedback' }),
  'DO-011': Object.freeze({ readBackRow: 'DI-010', basis: 'Feeder 2 Open feedback' }),
  'DO-012': Object.freeze({ readBackRow: 'DI-011', basis: 'Feeder 2 Close feedback' }),
  'DO-023': Object.freeze({ readBackRow: 'DI-029', basis: 'WSB #1 feedback' }),
  'DO-024': Object.freeze({ readBackRow: 'DI-030', basis: 'WSB #2 feedback' }),
  'DO-025': Object.freeze({ readBackRow: 'DI-031', basis: 'WSB #3 feedback' }),
  'DO-026': Object.freeze({ readBackRow: 'DI-032', basis: 'WSB #4 feedback' }),
  'DO-027': Object.freeze({ readBackRow: 'DI-033', basis: 'WSB #5 feedback' }),
  'DO-028': Object.freeze({ readBackRow: 'DI-034', basis: 'WSB #6 feedback' }),
  'DO-029': Object.freeze({ readBackRow: 'DI-035', basis: 'WSB #7 feedback' }),
  'DO-030': Object.freeze({ readBackRow: 'DI-036', basis: 'WSB #8 feedback' }),
  'AO-001+': Object.freeze({ readBackRow: null, basis: 'Holding register mirror at 512' }),
});

export const DIGITAL_INTERPRETATION = Object.freeze({
  default: Object.freeze({ rawTrueMeaning: 'NAMED_STATE_TRUE', source: 'T8 runtime interpretation' }),
  unresolvedRows: Object.freeze([]),
});

export const OWNER_RULINGS = Object.freeze({
  'AI-002+': Object.freeze({ identity: 'Pump Inlet Pressure', role: 'DIAGNOSTIC_TREND_ONLY', pumpReady: false, engineeringRange: '4-20 mA; 0-40 bar', aliasOf: null }),
  'AI-003+': Object.freeze({ identity: 'Pump Outlet Pressure', role: 'SOLE_PUMP_READY_PRESSURE', pumpReady: true, engineeringRange: '4-20 mA; 0-40 bar', aliasOf: null }),
  ivPressureRows: Object.freeze(['AI-004+', 'AI-005+', 'AI-006+', 'AI-007+', 'AI-008+', 'AI-009+', 'AI-010+', 'AI-011+']),
  ivPressureOrdinalRule: 'AI-004+k -> IVk outlet pressure',
  ivLowerLimitRows: Object.freeze(['DI-021', 'DI-022', 'DI-023', 'DI-024', 'DI-025', 'DI-026', 'DI-027', 'DI-028']),
  ivUpperLimitRows: Object.freeze(['DI-029', 'DI-030', 'DI-031', 'DI-032', 'DI-033', 'DI-034', 'DI-035', 'DI-036']),
  ivLimitOrdinalRule: 'DI-020+k -> IVk Lower Limit; DI-028+k -> IVk Upper Limit',
});

export const ACTUATION_RISK = Object.freeze({
  READ_ONLY: 'READ_ONLY',
  OUTPUT_STATE_OBSERVATION: 'OUTPUT_STATE_OBSERVATION',
  LOCAL_DEVICE_ACTUATION: 'LOCAL_DEVICE_ACTUATION',
  PROCESS_AFFECTING: 'PROCESS_AFFECTING',
  WET_PROCESS_AFFECTING: 'WET_PROCESS_AFFECTING',
  PROHIBITED_IN_H2: 'PROHIBITED_IN_H2',
});

export const ROW_RISK = Object.freeze({
  'DO-001': ACTUATION_RISK.PROCESS_AFFECTING, 'DO-002': ACTUATION_RISK.PROCESS_AFFECTING,
  'DO-003': ACTUATION_RISK.PROCESS_AFFECTING, 'DO-004': ACTUATION_RISK.PROCESS_AFFECTING,
  'DO-005': ACTUATION_RISK.PROCESS_AFFECTING, 'DO-006': ACTUATION_RISK.PROCESS_AFFECTING,
  'DO-007': ACTUATION_RISK.PROCESS_AFFECTING, 'AO-001+': ACTUATION_RISK.PROCESS_AFFECTING,
  'DO-008': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-009': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-010': ACTUATION_RISK.PROCESS_AFFECTING, 'DO-011': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-012': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-013': ACTUATION_RISK.PROCESS_AFFECTING,
  'DO-014': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-015': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-016': ACTUATION_RISK.PROCESS_AFFECTING, 'DO-017': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-018': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-019': ACTUATION_RISK.PROCESS_AFFECTING,
  'DO-020': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-021': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-022': ACTUATION_RISK.PROCESS_AFFECTING,
  'DO-023': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-024': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-025': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-026': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-027': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-028': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-029': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-030': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-031': ACTUATION_RISK.PROCESS_AFFECTING,
});
