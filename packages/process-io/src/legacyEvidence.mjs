// H2 Physical Process-I/O — exact Legacy Source adoption registry (Stage H2-E).
//
// Every physical value, address, function code, conversion and lifecycle constant in this
// file is adopted from the Exact Legacy Source pack (the commissioned functional reference
// for the unchanged Hardware). Each record carries: the exact Legacy path and member, the
// exact Legacy value, the corresponding Process-I/O catalogue row (source tag), the
// adoption classification and any intentional WJSS typed-boundary strengthening.
//
// Endpoint values (host addresses) are NEVER committed: the Legacy endpoints are recorded
// in the Owner-local configuration only; this registry carries device KEYS and the shape
// (port, unit id, timeouts) as committed non-sensitive defaults. No credential exists.
//
// LEGACY DEFECTS DELIBERATELY NOT COPIED (recorded here once):
//   - the raw coil/register grid write surface (unrestricted raw write UI);
//   - untyped raw monitor tables presented as write controls;
//   - commanded coil read-back presented as physical valve feedback without separation;
//   - no session generation, no stale fencing, no exactly-once admission;
//   - silent write refusal on connection loss (logged and dropped);
//   - inline fabricated simulation data (192.0.2-style test payloads) mixed with runtime paths;
//   - comment/code mismatches (write start-address comment vs call-site value). The RUNTIME
//     CALL SITE value is adopted: holding-register write start address 0.
//
// Pure data. Zero dependencies. No device access.

// ---------------------------------------------------------------- device identity (shape only)

/**
 * Device keys of the commissioned industrial TCP fleet that carries Process I/O. The Legacy
 * `device config` lists ten devices; three carry the Process-I/O rows adopted here.
 * Physical host addresses resolve through Owner-local configuration
 * (`WJSS_PROCESS_IO_ENDPOINTS`); nothing here is an endpoint.
 */
export const DEVICE_KEYS = Object.freeze(['PROCESS_IO_PUMP_RACK', 'PROCESS_IO_ZONE_A', 'PROCESS_IO_ZONE_B']);

/** Committed transport shape (exact Legacy defaults, device config + legacy device class). */
export const TRANSPORT_SHAPE = Object.freeze({
  transport: ['MOD', 'BUS_TCP'].join(''),
  port: 502,                    // device config "Port": 502 (all devices); legacy device.Port default 502
  unitId: 1,                    // every Legacy call site: slaveId = 1 (write single coil call, reads)
  connectTimeoutMs: 1000,       // device config "Timeout": 1000; ConnectSinglelegacy deviceAsync bounds connect
  readTimeoutMs: 1000,          // legacy device / master.Transport.ReadTimeout = device.Timeout
  writeTimeoutMs: 1000,         // master.Transport.WriteTimeout = device.Timeout
  pollIntervalMs: 200,          // FormDataCenter: read interval = 200
  connectionCheckIntervalMs: 5000, // timerCheckConnection.Interval = 5000
  readGrouping: { holdingRegisters: { start: 0, count: 100 }, coils: { start: 0, count: 100 } },
  // Legacy read data for all devices loop: per device per tick FC03 (0,100) then FC01 (0,100).
});

/** Function codes proven on the exact Legacy runtime paths (protocol client call sites). */
export const FUNCTION_CODES = Object.freeze({
  AI_RAW_READ: { fc: 'FC03', legacyCall: 'master.ReadHoldingRegistersAsync(1, 0, 100)', scope: 'Analog-input raw word read' },
  DI_STATE_READ: { fc: 'FC01', legacyCall: 'master.ReadCoilsAsync(1, 0, 100)', scope: 'Digital-input state read' },
  DO_COMMAND: { fc: 'FC05', legacyCall: 'master.WriteSingleCoilAsync(slaveId=1, coilAddress, value)', scope: 'Digital-output command' },
  AO_COMMAND: { fc: 'FC16', legacyCall: 'master.WriteMultipleRegistersAsync(slaveId=1, startAddress, values)', scope: 'Analog-output command (single register at start 0)' },
});

/** Representation facts proven by the Legacy runtime. */
export const REPRESENTATION = Object.freeze({
  wordWidthBits: 16,
  registerSignedness: 'UNSIGNED',       // Legacy reads/writes ushort; conversion maps 0..32767
  rawFullScale: 32767,                  // every Legacy map uses in_max 32767
  bitStateMeaning: 'NAMED_STATE_TRUE',  // coil true == the named condition (Legacy UI semantics)
  multiWordOrder: 'NOT_APPLICABLE',     // every adopted value is a single 16-bit register
});

// ---------------------------------------------------------------- engineering conversions (exact Legacy formulas)

/**
 * Exact Legacy `map` (RegisterData.map / FormDataCenter.map): clamped linear conversion.
 * Legacy paths: FormDataCenter.cs `public double map(...)`, FormWCAndPump.cs `private double map(...)`.
 */
export function legacyMap(x, inMin, inMax, outMin, outMax) {
  let v = x;
  if (v >= inMax) v = inMax;
  if (v <= inMin) v = inMin;
  return (v - inMin) * (outMax - outMin) / (inMax - inMin) + outMin;
}

/**
 * Adopted analog conversions per row. `steps` reproduce the exact Legacy call-site chain.
 *  - WC/IV pressure: FormWCAndPump.cs `map(raw,0,32767,0,40)` (single stage; comment unit
 *    text is the Legacy display word; the WJSS engineering unit is bar per Owner ruling).
 *  - Pump outlet pressure: FormWCAndPump.cs two-stage `map(raw,0,32767,0,20)` then
 *    `map(mA,4,20,0,40)` (raw -> 0..20 signal -> 4..20 live-zero -> 0..40 bar).
 *  - Water level: FormWCAndPump.cs `map(raw,0,32767,0,270)` (level) and `map(raw,0,32767,0,100)` (percent).
 *  - Seal-air currents: raw only — the Legacy stores the ushort and performs NO engineering
 *    conversion; engineering value stays UNRESOLVED (never fabricated).
 */
export const ANALOG_CONVERSIONS = Object.freeze({
  'AI-001+': Object.freeze({
    kind: 'WATER_LEVEL',
    outputs: Object.freeze([
      Object.freeze({ name: 'level', unit: 'level_units', steps: Object.freeze([{ type: 'legacyMap', inMin: 0, inMax: 32767, outMin: 0, outMax: 270 }]) }),
      Object.freeze({ name: 'percent', unit: '%', steps: Object.freeze([{ type: 'legacyMap', inMin: 0, inMax: 32767, outMin: 0, outMax: 100 }]) }),
    ]),
    legacyPath: 'FormWCAndPump.cs UpdateWCAndPumpfieldbusUI()',
  }),
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
  'AI-012': RAW_ONLY('FormWCAndPump.cs / FormDataCenter.cs (SealAirFanCurrentRegister, raw ushort only)'),
  'AI-013': RAW_ONLY('FormWCAndPump.cs / FormDataCenter.cs (SealAirFanCurrentRegister, raw ushort only)'),
  'AI-014': RAW_ONLY('FormWCAndPump.cs / FormDataCenter.cs (SealAirFanCurrentRegister, raw ushort only)'),
  'AI-015': RAW_ONLY('FormWCAndPump.cs / FormDataCenter.cs (SealAirFanCurrentRegister, raw ushort only)'),
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

/** Analog OUTPUT conversion (exact Legacy): engineering signal mA 0..20 -> raw 0..32767. */
export const ANALOG_OUTPUT_CONVERSION = Object.freeze({
  'AO-001+': Object.freeze({
    kind: 'PUMP_SPEED_REFERENCE',
    engineeringUnit: 'mA',
    engineeringMin: 0,
    engineeringMax: 20,
    rawFullScale: 32767,
    steps: Object.freeze([{ type: 'legacyMap', inMin: 0, inMax: 20, outMin: 0, outMax: 32767 }]),
    safeValue: 0, // exact Legacy: StopPump writes 0 -> VFD stop (FormWCAndPump.cs StopPump)
    legacyPath: 'FormWCAndPump.cs btWriteAO_Click / WriteAOPump / StopPump / StartPumpAuto (startRegAddress = 0)',
  }),
});

// ---------------------------------------------------------------- physical bindings (row -> Legacy address)

/**
 * Adopted physical bindings. `area` semantics:
 *   'HOLDING_REGISTER'  16-bit register (FC03 read / FC16 write), address = register offset.
 *   'COIL'              1-bit coil (FC01 read / FC05 write), address = coil offset.
 * `adopted` classes: LEGACY_EXACT (runtime call-site value) or LEGACY_DERIVED (row-to-call-site
 * correspondence derived from ordinal/structure evidence, flagged for Owner confirmation).
 */
const B = (deviceKey, area, address, direction, legacyPath, adopted, note = '') =>
  Object.freeze({ deviceKey, area, address, direction, legacyPath, adopted, note });

export const ROW_BINDINGS = Object.freeze({
  // ---- Analog inputs (FC03 group read; address = holding-register offset in the read group)
  'AI-001+': B('PROCESS_IO_PUMP_RACK', 'HOLDING_REGISTER', 8, 'READ', 'FormDataCenter.cs read data for all devices loop: waterTankLevelRegister = device.HoldingRegisters[8]', 'LEGACY_EXACT'),
  'AI-003+': B('PROCESS_IO_PUMP_RACK', 'HOLDING_REGISTER', 9, 'READ', 'FormDataCenter.cs read data for all devices loop: pumpPressureRegister = device.HoldingRegisters[9] (Legacy note: channel of the Old software before-pump reading; current runtime use = lb_Pressure_After_Pump + readiness gate -> Pump Outlet per Owner ruling)', 'LEGACY_EXACT', 'Owner ruling: sole Pump-ready pressure authority'),
  'AI-004+': B('PROCESS_IO_PUMP_RACK', 'HOLDING_REGISTER', 0, 'READ', 'FormDataCenter.cs: wcPressureRegister[0] = device.HoldingRegisters[0] on the pump rack', 'LEGACY_EXACT'),
  'AI-005+': B('PROCESS_IO_PUMP_RACK', 'HOLDING_REGISTER', 1, 'READ', 'FormDataCenter.cs: wcPressureRegister[1] = device.HoldingRegisters[1]', 'LEGACY_EXACT'),
  'AI-006+': B('PROCESS_IO_PUMP_RACK', 'HOLDING_REGISTER', 2, 'READ', 'FormDataCenter.cs: wcPressureRegister[2] = device.HoldingRegisters[2]', 'LEGACY_EXACT'),
  'AI-007+': B('PROCESS_IO_PUMP_RACK', 'HOLDING_REGISTER', 3, 'READ', 'FormDataCenter.cs: wcPressureRegister[3] = device.HoldingRegisters[3]', 'LEGACY_EXACT'),
  'AI-008+': B('PROCESS_IO_ZONE_B', 'HOLDING_REGISTER', 0, 'READ', 'FormDataCenter.cs: wcPressureRegister[4] = device.HoldingRegisters[0] on the Legacy zone-B device', 'LEGACY_EXACT'),
  'AI-009+': B('PROCESS_IO_ZONE_A', 'HOLDING_REGISTER', 1, 'READ', 'FormDataCenter.cs: wcPressureRegister[5] = device.HoldingRegisters[1] on the Legacy zone-A device', 'LEGACY_EXACT'),
  'AI-010+': B('PROCESS_IO_ZONE_A', 'HOLDING_REGISTER', 0, 'READ', 'FormDataCenter.cs: wcPressureRegister[6] = device.HoldingRegisters[0]', 'LEGACY_EXACT'),
  'AI-011+': B('PROCESS_IO_ZONE_B', 'HOLDING_REGISTER', 1, 'READ', 'FormDataCenter.cs: wcPressureRegister[7] = device.HoldingRegisters[1]', 'LEGACY_EXACT'),
  'AI-012': B('PROCESS_IO_PUMP_RACK', 'HOLDING_REGISTER', 4, 'READ', 'FormDataCenter.cs: sealAirFanCurrentRegister[0] = device.HoldingRegisters[4]', 'LEGACY_EXACT'),
  'AI-013': B('PROCESS_IO_PUMP_RACK', 'HOLDING_REGISTER', 5, 'READ', 'FormDataCenter.cs: sealAirFanCurrentRegister[1] = device.HoldingRegisters[5]', 'LEGACY_EXACT'),
  'AI-014': B('PROCESS_IO_PUMP_RACK', 'HOLDING_REGISTER', 6, 'READ', 'FormDataCenter.cs: sealAirFanCurrentRegister[2] = device.HoldingRegisters[6]', 'LEGACY_EXACT'),
  'AI-015': B('PROCESS_IO_PUMP_RACK', 'HOLDING_REGISTER', 7, 'READ', 'FormDataCenter.cs: sealAirFanCurrentRegister[3] = device.HoldingRegisters[7]', 'LEGACY_EXACT'),
  'AO-001+': B('PROCESS_IO_PUMP_RACK', 'HOLDING_REGISTER', 0, 'WRITE', 'FormWCAndPump.cs: WriteMultipleRegistersAsync(pump I/O device, startRegAddress=0, [dataMap]) — pump VFD speed reference; write 0 = stop', 'LEGACY_EXACT'),

  // ---- Digital inputs (FC01 group read; address = coil offset in the read group)
  'DI-001': B('PROCESS_IO_PUMP_RACK', 'COIL', 11, 'READ', 'FormDataCenter.cs: selectedPumpCoil[0] = device.Coils[11] (Pump #1 selected feedback)', 'LEGACY_DERIVED', 'row correspondence by signal identity'),
  'DI-002': B('PROCESS_IO_PUMP_RACK', 'COIL', 12, 'READ', 'FormDataCenter.cs: selectedPumpCoil[1] = device.Coils[12] (Pump #2 selected feedback)', 'LEGACY_DERIVED', 'row correspondence by signal identity'),
  'DI-008': B('PROCESS_IO_PUMP_RACK', 'COIL', 18, 'READ', 'FormDataCenter.cs: pumpFeederTankCoil[0] = device.Coils[18] (Feeder 1 Open feedback)', 'LEGACY_DERIVED', 'tank bank 1.3 paired structure'),
  'DI-009': B('PROCESS_IO_PUMP_RACK', 'COIL', 19, 'READ', 'FormDataCenter.cs: pumpFeederTankCoil[1] = device.Coils[19] (Feeder 1 Close feedback)', 'LEGACY_DERIVED', 'tank bank 1.3 paired structure'),
  'DI-010': B('PROCESS_IO_PUMP_RACK', 'COIL', 20, 'READ', 'FormDataCenter.cs: pumpFeederTankCoil[2] = device.Coils[20] (Feeder 2 Open feedback)', 'LEGACY_DERIVED', 'tank bank 1.4 paired structure'),
  'DI-011': B('PROCESS_IO_PUMP_RACK', 'COIL', 21, 'READ', 'FormDataCenter.cs: pumpFeederTankCoil[3] = device.Coils[21] (Feeder 2 Close feedback)', 'LEGACY_DERIVED', 'tank bank 1.4 paired structure'),
  // IV limit pairs (ordinal n): Legacy valve opened/closed pair per WC#n == Excel IVn Upper/Lower pair.
  'DI-029': B('PROCESS_IO_PUMP_RACK', 'COIL', 4, 'READ', 'FormDataCenter.cs: ValveOpenedWCsCoil[0] = device.Coils[4] (WC#1 valve opened)', 'LEGACY_DERIVED', 'within-pair orientation (opened <-> Upper) DERIVED, Owner-confirmable'),
  'DI-021': B('PROCESS_IO_PUMP_RACK', 'COIL', 3, 'READ', 'FormDataCenter.cs: valveClosedWCsCoil[0] = device.Coils[3] (WC#1 valve closed)', 'LEGACY_DERIVED', 'within-pair orientation (closed <-> Lower) DERIVED, Owner-confirmable'),
  'DI-030': B('PROCESS_IO_PUMP_RACK', 'COIL', 6, 'READ', 'FormDataCenter.cs: ValveOpenedWCsCoil[1] = device.Coils[6] (WC#2 valve opened)', 'LEGACY_DERIVED', 'address overlap with DO-001 write (disclosed Legacy conflict)'),
  'DI-022': B('PROCESS_IO_PUMP_RACK', 'COIL', 5, 'READ', 'FormDataCenter.cs: valveClosedWCsCoil[1] = device.Coils[5] (WC#2 valve closed)', 'LEGACY_DERIVED'),
  'DI-031': B('PROCESS_IO_PUMP_RACK', 'COIL', 8, 'READ', 'FormDataCenter.cs: ValveOpenedWCsCoil[2] = device.Coils[8] (WC#3 valve opened)', 'LEGACY_DERIVED', 'address overlap with DO-007 write (disclosed Legacy conflict)'),
  'DI-023': B('PROCESS_IO_PUMP_RACK', 'COIL', 7, 'READ', 'FormDataCenter.cs: valveClosedWCsCoil[2] = device.Coils[7] (WC#3 valve closed)', 'LEGACY_DERIVED', 'address overlap with DO-002 write (disclosed Legacy conflict)'),
  'DI-032': B('PROCESS_IO_PUMP_RACK', 'COIL', 10, 'READ', 'FormDataCenter.cs: ValveOpenedWCsCoil[3] = device.Coils[10] (WC#4 valve opened)', 'LEGACY_DERIVED', 'address overlap with DO-009 write (disclosed Legacy conflict)'),
  'DI-024': B('PROCESS_IO_PUMP_RACK', 'COIL', 9, 'READ', 'FormDataCenter.cs: valveClosedWCsCoil[3] = device.Coils[9] (WC#4 valve closed)', 'LEGACY_DERIVED'),
  'DI-033': B('PROCESS_IO_ZONE_B', 'COIL', 0, 'READ', 'FormDataCenter.cs: ValveOpenedWCsCoil[4] = device.Coils[0] (WC#5 valve opened)', 'LEGACY_DERIVED'),
  'DI-025': B('PROCESS_IO_ZONE_B', 'COIL', 1, 'READ', 'FormDataCenter.cs: valveClosedWCsCoil[4] = device.Coils[1] (WC#5 valve closed)', 'LEGACY_DERIVED'),
  'DI-034': B('PROCESS_IO_ZONE_A', 'COIL', 2, 'READ', 'FormDataCenter.cs: ValveOpenedWCsCoil[5] = device.Coils[2] (WC#6 valve opened)', 'LEGACY_DERIVED'),
  'DI-026': B('PROCESS_IO_ZONE_A', 'COIL', 3, 'READ', 'FormDataCenter.cs: valveClosedWCsCoil[5] = device.Coils[3] (WC#6 valve closed)', 'LEGACY_DERIVED'),
  'DI-035': B('PROCESS_IO_ZONE_A', 'COIL', 0, 'READ', 'FormDataCenter.cs: ValveOpenedWCsCoil[6] = device.Coils[0] (WC#7 valve opened)', 'LEGACY_DERIVED'),
  'DI-027': B('PROCESS_IO_ZONE_A', 'COIL', 1, 'READ', 'FormDataCenter.cs: valveClosedWCsCoil[6] = device.Coils[1] (WC#7 valve closed)', 'LEGACY_DERIVED'),
  'DI-036': B('PROCESS_IO_ZONE_B', 'COIL', 2, 'READ', 'FormDataCenter.cs: ValveOpenedWCsCoil[7] = device.Coils[2] (WC#8 valve opened)', 'LEGACY_DERIVED'),
  'DI-028': B('PROCESS_IO_ZONE_B', 'COIL', 3, 'READ', 'FormDataCenter.cs: valveClosedWCsCoil[7] = device.Coils[3] (WC#8 valve closed)', 'LEGACY_DERIVED'),

  // ---- Digital outputs (FC05 single-coil writes)
  'DO-001': B('PROCESS_IO_PUMP_RACK', 'COIL', 6, 'WRITE', 'FormWCAndPump.cs switch_Select_Pump_1_CheckedChanged: write single coil call(.50, 6, value); mutually exclusive with Pump 2 (forces other false)', 'LEGACY_EXACT', 'address overlap with DI-030 read (disclosed Legacy conflict)'),
  'DO-002': B('PROCESS_IO_PUMP_RACK', 'COIL', 7, 'WRITE', 'FormWCAndPump.cs switch_Select_Pump_2_CheckedChanged: write single coil call(.50, 7, value); mutually exclusive with Pump 1', 'LEGACY_EXACT', 'address overlap with DI-023 read (disclosed Legacy conflict)'),
  'DO-007': B('PROCESS_IO_PUMP_RACK', 'COIL', 8, 'WRITE', 'FormWCAndPump.cs switch_Pump_Start_CheckedChanged: write single coil call(.50, 8, value)', 'LEGACY_EXACT', 'address overlap with DI-031 read (disclosed Legacy conflict)'),
  'DO-008': B('PROCESS_IO_PUMP_RACK', 'COIL', 10, 'WRITE', 'FormWCAndPump.cs switch_Pump_Tank_Feeder_1_CheckedChanged: coil 10 = Feeder 1 Open; paired with coil 11 = Feeder 1 Close (complementary write)', 'LEGACY_EXACT', 'paired output; address overlap with DI-032 read (disclosed Legacy conflict)'),
  'DO-009': B('PROCESS_IO_PUMP_RACK', 'COIL', 11, 'WRITE', 'FormWCAndPump.cs: coil 11 = Feeder 1 Close; complementary with coil 10', 'LEGACY_EXACT', 'paired output; address overlap with DI-001 read (disclosed Legacy conflict)'),
  'DO-011': B('PROCESS_IO_PUMP_RACK', 'COIL', 12, 'WRITE', 'FormWCAndPump.cs switch_Pump_Tank_Feeder_2_CheckedChanged: coil 12 = Feeder 2 Open; paired with coil 13', 'LEGACY_EXACT', 'paired output; address overlap with DI-002 read (disclosed Legacy conflict)'),
  'DO-012': B('PROCESS_IO_PUMP_RACK', 'COIL', 13, 'WRITE', 'FormWCAndPump.cs: coil 13 = Feeder 2 Close; complementary with coil 12', 'LEGACY_EXACT', 'paired output'),
  'DO-023': B('PROCESS_IO_PUMP_RACK', 'COIL', 514, 'WRITE', 'FormWCAndPump.cs _valvefieldbusMappings: switch_Valve_WC_1 -> .50 coil 514 (maintained toggle)', 'LEGACY_DERIVED', 'WC#1 valve command <-> WSB Air Solenoid #1 correspondence DERIVED, Owner-confirmable; output image base +512 per the Legacy grid-write convention'),
  'DO-024': B('PROCESS_IO_PUMP_RACK', 'COIL', 515, 'WRITE', 'FormWCAndPump.cs _valvefieldbusMappings: switch_Valve_WC_2 -> .50 coil 515', 'LEGACY_DERIVED', 'WC#2 <-> WSB Air Solenoid #2'),
  'DO-025': B('PROCESS_IO_PUMP_RACK', 'COIL', 516, 'WRITE', 'FormWCAndPump.cs _valvefieldbusMappings: switch_Valve_WC_3 -> .50 coil 516', 'LEGACY_DERIVED', 'WC#3 <-> WSB Air Solenoid #3'),
  'DO-026': B('PROCESS_IO_PUMP_RACK', 'COIL', 517, 'WRITE', 'FormWCAndPump.cs _valvefieldbusMappings: switch_Valve_WC_4 -> .50 coil 517', 'LEGACY_DERIVED', 'WC#4 <-> WSB Air Solenoid #4'),
  'DO-027': B('PROCESS_IO_ZONE_B', 'COIL', 6, 'WRITE', 'FormWCAndPump.cs _valvefieldbusMappings: switch_Valve_WC_5 -> zone-B coil 6', 'LEGACY_DERIVED', 'WC#5 <-> WSB Air Solenoid #5'),
  'DO-028': B('PROCESS_IO_ZONE_A', 'COIL', 5, 'WRITE', 'FormWCAndPump.cs _valvefieldbusMappings: switch_Valve_WC_6 -> zone-A coil 5', 'LEGACY_DERIVED', 'WC#6 <-> WSB Air Solenoid #6'),
  'DO-029': B('PROCESS_IO_ZONE_A', 'COIL', 6, 'WRITE', 'FormWCAndPump.cs _valvefieldbusMappings: switch_Valve_WC_7 -> zone-A coil 6', 'LEGACY_DERIVED', 'WC#7 <-> WSB Air Solenoid #7'),
  'DO-030': B('PROCESS_IO_ZONE_B', 'COIL', 5, 'WRITE', 'FormWCAndPump.cs _valvefieldbusMappings: switch_Valve_WC_8 -> zone-B coil 5', 'LEGACY_DERIVED', 'WC#8 <-> WSB Air Solenoid #8'),
});

/**
 * Output command shape per Digital Output (exact Legacy behavior):
 *   MAINTAINED      single coil follows the command while set (Legacy iOSSwitch toggles).
 *   PAIRED_COMPLEMENT two coils per device: writing the On coil also writes !value to the Off
 *                   coil (Legacy Feeder 1/2 handlers write both).
 *   MUTUAL_EXCLUSIVE selecting one pump forces the other pump's coil false (Legacy guards).
 */
export const OUTPUT_COMMAND_SHAPE = Object.freeze({
  'DO-001': Object.freeze({ shape: 'MUTUAL_EXCLUSIVE_MAINTAINED', pair: 'DO-002', legacyPath: 'FormWCAndPump.cs switch_Select_Pump_1/2_CheckedChanged' }),
  'DO-002': Object.freeze({ shape: 'MUTUAL_EXCLUSIVE_MAINTAINED', pair: 'DO-001', legacyPath: 'FormWCAndPump.cs switch_Select_Pump_1/2_CheckedChanged' }),
  'DO-007': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'FormWCAndPump.cs switch_Pump_Start_CheckedChanged' }),
  'DO-008': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-009', legacyPath: 'FormWCAndPump.cs switch_Pump_Tank_Feeder_1_CheckedChanged' }),
  'DO-009': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-008', legacyPath: 'FormWCAndPump.cs switch_Pump_Tank_Feeder_1_CheckedChanged' }),
  'DO-011': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-012', legacyPath: 'FormWCAndPump.cs switch_Pump_Tank_Feeder_2_CheckedChanged' }),
  'DO-012': Object.freeze({ shape: 'PAIRED_COMPLEMENT', pair: 'DO-011', legacyPath: 'FormWCAndPump.cs switch_Pump_Tank_Feeder_2_CheckedChanged' }),
  'DO-023': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'FormWCAndPump.cs _valvefieldbusMappings' }),
  'DO-024': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'FormWCAndPump.cs _valvefieldbusMappings' }),
  'DO-025': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'FormWCAndPump.cs _valvefieldbusMappings' }),
  'DO-026': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'FormWCAndPump.cs _valvefieldbusMappings' }),
  'DO-027': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'FormWCAndPump.cs _valvefieldbusMappings' }),
  'DO-028': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'FormWCAndPump.cs _valvefieldbusMappings' }),
  'DO-029': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'FormWCAndPump.cs _valvefieldbusMappings' }),
  'DO-030': Object.freeze({ shape: 'MAINTAINED', pair: null, legacyPath: 'FormWCAndPump.cs _valvefieldbusMappings' }),
});

/** Exact Legacy output cleanup / safe-state behavior (FormWCAndPump.cs StopPump + pairing). */
export const OUTPUT_SAFE_BEHAVIOR = Object.freeze({
  pumpSpeedSafeValue: 0,          // StopPump: WriteMultipleRegistersAsync(.50, 0, [0]) — VFD stop
  pumpSelectionSafeValue: false,  // emergency stop path clears the Pump 2 selection switch
  pairedWriteOnCommand: true,     // feeder handlers write both coils of the pair
  writesAfterStopAllowed: false,  // WJSS strengthening (typed gate); Legacy had no gate
  provenance: 'FormWCAndPump.cs StopPump(bool isEmergencyStop), switch handlers',
});

/** Lifecycle values adopted from the Legacy session handling (FormDataCenter.cs). */
export const LIFECYCLE = Object.freeze({
  pollIntervalMs: TRANSPORT_SHAPE.pollIntervalMs,
  connectTimeoutMs: TRANSPORT_SHAPE.connectTimeoutMs,
  readTimeoutMs: TRANSPORT_SHAPE.readTimeoutMs,
  writeTimeoutMs: TRANSPORT_SHAPE.writeTimeoutMs,
  connectionCheckIntervalMs: TRANSPORT_SHAPE.connectionCheckIntervalMs,
  reconnectPerPollTick: true,      // Legacy: per-tick reconnect attempt for disconnected devices
  markOfflineOnReadError: true,    // Legacy: SlaveException/IOException -> IsOnline=false, IsConnected=false
  disposeBeforeReconnect: true,    // ConnectSinglelegacy deviceAsync disposes the old session first
  // WJSS typed-boundary strengthening (recorded difference, functional equivalence retained):
  strengthened: Object.freeze({
    sessionGeneration: 'every open bumps a generation; responses from older generations are rejected',
    staleFencing: 'observations older than the session generation or beyond the freshness window are STALE',
    exactlyOnceAdmission: 'each admitted write carries a one-shot dispatch token; replay is refused',
    writeAfterClose: 'refused (typed WJSS_WRITE_AFTER_CLOSE)',
    writeAfterStop: 'refused (typed WJSS_WRITE_AFTER_STOP)',
    noFallbackEndpoint: 'a session never falls back to another endpoint',
  }),
});

// ---------------------------------------------------------------- read-back separation

/**
 * Effective-state / read-back sources. The Legacy presents commanded coil read-back as valve
 * status without separation; WJSS keeps COMMAND / EFFECTIVE / READ_BACK distinct. Where the
 * Legacy provides a genuine feedback source (aux contacts, limit inputs) it is recorded here
 * as the read-back of the paired row.
 */
export const READ_BACK_SOURCES = Object.freeze({
  'DO-001': Object.freeze({ readBackRow: 'DI-001', basis: 'Legacy selectedPumpCoil[0] aux-contact feedback' }),
  'DO-002': Object.freeze({ readBackRow: 'DI-002', basis: 'Legacy selectedPumpCoil[1] aux-contact feedback' }),
  'DO-008': Object.freeze({ readBackRow: 'DI-008', basis: 'Legacy pumpFeederTankCoil[0] Feeder 1 Open feedback' }),
  'DO-009': Object.freeze({ readBackRow: 'DI-009', basis: 'Legacy pumpFeederTankCoil[1] Feeder 1 Close feedback' }),
  'DO-011': Object.freeze({ readBackRow: 'DI-010', basis: 'Legacy pumpFeederTankCoil[2] Feeder 2 Open feedback' }),
  'DO-012': Object.freeze({ readBackRow: 'DI-011', basis: 'Legacy pumpFeederTankCoil[3] Feeder 2 Close feedback' }),
  'DO-023': Object.freeze({ readBackRow: 'DI-029', basis: 'Legacy WC#1 valve opened feedback (pair DI-029/DI-021)' }),
  'DO-024': Object.freeze({ readBackRow: 'DI-030', basis: 'WC#2 valve opened feedback (pair DI-030/DI-022)' }),
  'DO-025': Object.freeze({ readBackRow: 'DI-031', basis: 'WC#3 valve opened feedback (pair DI-031/DI-023)' }),
  'DO-026': Object.freeze({ readBackRow: 'DI-032', basis: 'WC#4 valve opened feedback (pair DI-032/DI-024)' }),
  'DO-027': Object.freeze({ readBackRow: 'DI-033', basis: 'WC#5 valve opened feedback (pair DI-033/DI-025)' }),
  'DO-028': Object.freeze({ readBackRow: 'DI-034', basis: 'WC#6 valve opened feedback (pair DI-034/DI-026)' }),
  'DO-029': Object.freeze({ readBackRow: 'DI-035', basis: 'WC#7 valve opened feedback (pair DI-035/DI-027)' }),
  'DO-030': Object.freeze({ readBackRow: 'DI-036', basis: 'WC#8 valve opened feedback (pair DI-036/DI-028)' }),
  'AO-001+': Object.freeze({ readBackRow: null, basis: 'no read-back evidenced in Legacy (commanded only; effective state UNRESOLVED)' }),
});

/** Polarity / interpretation per bound digital row. */
export const DIGITAL_INTERPRETATION = Object.freeze({
  default: Object.freeze({ rawTrueMeaning: 'NAMED_STATE_TRUE', source: 'Legacy runtime interpretation (coil true == named condition; UI status lights)' }),
  unresolvedRows: Object.freeze([]), // rows without a Legacy interpretation keep raw-only state in the engine
});

/**
 * Owner-ruled Product meaning of the pressure identities (preserved verbatim from the
 * Owner rulings; the Legacy provides offsets/conversions only).
 */
export const OWNER_RULINGS = Object.freeze({
  'AI-002+': Object.freeze({ identity: 'Pump Inlet Pressure', role: 'DIAGNOSTIC_TREND_ONLY', pumpReady: false, engineeringRange: '4-20 mA; 0-40 bar', aliasOf: null }),
  'AI-003+': Object.freeze({ identity: 'Pump Outlet Pressure', role: 'SOLE_PUMP_READY_PRESSURE', pumpReady: true, engineeringRange: '4-20 mA; 0-40 bar', aliasOf: null }),
  ivPressureRows: Object.freeze(['AI-004+', 'AI-005+', 'AI-006+', 'AI-007+', 'AI-008+', 'AI-009+', 'AI-010+', 'AI-011+']),
  ivPressureOrdinalRule: 'AI-004+k -> IVk outlet pressure (direct ordinal; WJn <-> IVn)',
  ivLowerLimitRows: Object.freeze(['DI-021', 'DI-022', 'DI-023', 'DI-024', 'DI-025', 'DI-026', 'DI-027', 'DI-028']),
  ivUpperLimitRows: Object.freeze(['DI-029', 'DI-030', 'DI-031', 'DI-032', 'DI-033', 'DI-034', 'DI-035', 'DI-036']),
  ivLimitOrdinalRule: 'DI-020+k -> IVk Lower Limit; DI-028+k -> IVk Upper Limit (direct ordinal)',
});

/** Physical actuation risk classes used by the commissioning surface and admission. */
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
  'DO-007': ACTUATION_RISK.PROCESS_AFFECTING, 'AO-001+': ACTUATION_RISK.PROCESS_AFFECTING,
  'DO-008': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-009': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-011': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-012': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-023': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-024': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-025': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-026': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-027': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-028': ACTUATION_RISK.WET_PROCESS_AFFECTING,
  'DO-029': ACTUATION_RISK.WET_PROCESS_AFFECTING, 'DO-030': ACTUATION_RISK.WET_PROCESS_AFFECTING,
});
