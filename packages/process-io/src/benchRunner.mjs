// H2 Physical Process-I/O — Hardware Bench Runner.
// Supports SIMULATION, PHYSICAL_DI_AI_READ, PHYSICAL_DO_SINGLE, PHYSICAL_AO_SINGLE,
// and PHYSICAL_FULL_SELECTED_SEQUENCE modes.
// Requires explicit mode-specific Owner consent for all physical modes.
// Guarantees fail-closed safe-return cleanup of touched outputs.

import { buildCatalogue, loadCatalogue } from './catalogue.mjs';
import { buildBindings, canAdmitWrite } from './bindings.mjs';
import { ProcessIoSession } from './lifecycle.mjs';
import { SimulatedTransport } from './simulation.mjs';
import { PhysicalProcessIoTransport } from './physicalTransport.mjs';
import { READ_PROFILES, CONSENT_PHRASES as CANONICAL_CONSENTS, TRANSPORT_SHAPE } from './legacyEvidence.mjs';

export const CONSENT_PHRASES = CANONICAL_CONSENTS;
export const REQUIRED_CONSENT_PHRASE = CONSENT_PHRASES.PHYSICAL_FULL_SELECTED_SEQUENCE;

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

export async function runProcessIoBench(catalogueCsv, config = {}) {
  const mode = config.mode ?? 'SIMULATION';
  const startedAt = Date.now();
  const summary = {};
  const cleanupAudit = [];
  const touchedOutputs = [];

  // Check for deprecated PHYSICAL_TEST mode
  if (mode === 'PHYSICAL_TEST') {
    return {
      mode,
      accepted: false,
      outcomeCode: 'MODE_DEPRECATED',
      detail: 'Mode PHYSICAL_TEST is deprecated and refused. Use explicit physical modes: PHYSICAL_DI_AI_READ, PHYSICAL_DO_SINGLE, PHYSICAL_AO_SINGLE, PHYSICAL_FULL_SELECTED_SEQUENCE.',
      startedAt: new Date(startedAt).toISOString(),
      completedAt: new Date().toISOString(),
      ledger: [],
      summary,
      cleanupAudit,
    };
  }

  const VALID_MODES = [
    'SIMULATION',
    'PHYSICAL_DI_AI_READ',
    'PHYSICAL_DO_SINGLE',
    'PHYSICAL_AO_SINGLE',
    'PHYSICAL_FULL_SELECTED_SEQUENCE',
  ];

  if (!VALID_MODES.includes(mode)) {
    return {
      mode,
      accepted: false,
      outcomeCode: 'INVALID_MODE',
      detail: `Mode ${mode} is not recognized. Valid modes: ${VALID_MODES.join(', ')}.`,
      startedAt: new Date(startedAt).toISOString(),
      completedAt: new Date().toISOString(),
      ledger: [],
      summary,
      cleanupAudit,
    };
  }

  const isPhysical = mode.startsWith('PHYSICAL_');

  // Refuse CLI argument consent for physical execution
  if (isPhysical && config.cliConsentSupplied) {
    return {
      mode,
      accepted: false,
      outcomeCode: 'CLI_CONSENT_REFUSED_USE_ENV',
      detail: 'Consent for physical modes must be provided via WJSS_PROCESS_IO_CONSENT environment variable, not command-line arguments, to prevent exposure in process table and shell history.',
      startedAt: new Date(startedAt).toISOString(),
      completedAt: new Date().toISOString(),
      ledger: [],
      summary,
      cleanupAudit,
    };
  }

  if (isPhysical) {
    const requiredConsent = CONSENT_PHRASES[mode];
    if (!requiredConsent || config.consentPhrase !== requiredConsent) {
      return {
        mode,
        accepted: false,
        outcomeCode: 'CONSENT_REFUSED',
        detail: `Physical execution for mode ${mode} requires exact Owner consent phrase.`,
        startedAt: new Date(startedAt).toISOString(),
        completedAt: new Date().toISOString(),
        ledger: [],
        summary,
        cleanupAudit,
      };
    }

    if (!config.endpoints || Object.keys(config.endpoints).length === 0) {
      return {
        mode,
        accepted: false,
        outcomeCode: 'ENDPOINTS_UNCONFIGURED',
        detail: 'Physical execution requires configured target endpoints.',
        startedAt: new Date(startedAt).toISOString(),
        completedAt: new Date().toISOString(),
        ledger: [],
        summary,
        cleanupAudit,
      };
    }

    // Validate Unit ID on endpoints
    for (const [devKey, ep] of Object.entries(config.endpoints)) {
      const u = ep.unitId ?? TRANSPORT_SHAPE.defaultUnitId;
      if (typeof u !== 'number' || !Number.isInteger(u) || u < TRANSPORT_SHAPE.minUnitId || u > TRANSPORT_SHAPE.maxUnitId) {
        return {
          mode,
          accepted: false,
          outcomeCode: 'UNIT_ID_INVALID',
          detail: `Endpoint ${devKey} specifies Unit ID ${u}, which is outside valid physical range (${TRANSPORT_SHAPE.minUnitId}-${TRANSPORT_SHAPE.maxUnitId}).`,
          startedAt: new Date(startedAt).toISOString(),
          completedAt: new Date().toISOString(),
          ledger: [],
          summary,
          cleanupAudit,
        };
      }
    }
  }

  // Read Profile validation
  const readProfile = config.readProfile ?? READ_PROFILES.WORKBOOK_CANONICAL;
  if (readProfile !== READ_PROFILES.WORKBOOK_CANONICAL && readProfile !== READ_PROFILES.LEGACY_PRODUCTION_COMPAT) {
    return {
      mode,
      accepted: false,
      outcomeCode: 'INVALID_READ_PROFILE',
      detail: `Read profile ${readProfile} is not recognized. Valid profiles: WORKBOOK_CANONICAL, LEGACY_PRODUCTION_COMPAT.`,
      startedAt: new Date(startedAt).toISOString(),
      completedAt: new Date().toISOString(),
      ledger: [],
      summary,
      cleanupAudit,
    };
  }

  // Mode-specific parameter validations
  if (mode === 'PHYSICAL_DO_SINGLE') {
    if (config.holdMs === undefined || config.holdMs === null) {
      return {
        mode,
        accepted: false,
        outcomeCode: 'HOLD_MS_REQUIRED',
        detail: 'PHYSICAL_DO_SINGLE mode requires --hold-ms parameter (100-2000 ms).',
        startedAt: new Date(startedAt).toISOString(),
        completedAt: new Date().toISOString(),
        ledger: [],
        summary,
        cleanupAudit,
      };
    }
    const h = Number(config.holdMs);
    if (!Number.isInteger(h) || h < TRANSPORT_SHAPE.minDoHoldMs || h > TRANSPORT_SHAPE.maxDoHoldMs) {
      return {
        mode,
        accepted: false,
        outcomeCode: 'HOLD_MS_INVALID',
        detail: `--hold-ms value ${config.holdMs} is outside valid range (${TRANSPORT_SHAPE.minDoHoldMs}-${TRANSPORT_SHAPE.maxDoHoldMs} ms).`,
        startedAt: new Date(startedAt).toISOString(),
        completedAt: new Date().toISOString(),
        ledger: [],
        summary,
        cleanupAudit,
      };
    }
  }

  if (mode === 'PHYSICAL_AO_SINGLE') {
    if (config.aoTestRaw === undefined || config.aoTestRaw === null) {
      return {
        mode,
        accepted: false,
        outcomeCode: 'AO_RAW_REQUIRED',
        detail: 'PHYSICAL_AO_SINGLE mode requires --ao-test-raw parameter (0-32767).',
        startedAt: new Date(startedAt).toISOString(),
        completedAt: new Date().toISOString(),
        ledger: [],
        summary,
        cleanupAudit,
      };
    }
    const raw = Number(config.aoTestRaw);
    if (!Number.isInteger(raw) || raw < 0 || raw > 32767) {
      return {
        mode,
        accepted: false,
        outcomeCode: 'AO_RAW_INVALID',
        detail: `--ao-test-raw value ${config.aoTestRaw} is outside valid range (0-32767).`,
        startedAt: new Date(startedAt).toISOString(),
        completedAt: new Date().toISOString(),
        ledger: [],
        summary,
        cleanupAudit,
      };
    }

    if (!config.aoWriteFunction) {
      return {
        mode,
        accepted: false,
        outcomeCode: 'AO_WRITE_FUNCTION_REQUIRED',
        detail: 'PHYSICAL_AO_SINGLE mode requires --ao-write-function parameter (FC06 or FC16).',
        startedAt: new Date(startedAt).toISOString(),
        completedAt: new Date().toISOString(),
        ledger: [],
        summary,
        cleanupAudit,
      };
    }
    const fc = String(config.aoWriteFunction).toUpperCase();
    if (fc !== 'FC06' && fc !== 'FC16') {
      return {
        mode,
        accepted: false,
        outcomeCode: 'AO_WRITE_FUNCTION_INVALID',
        detail: `--ao-write-function value '${config.aoWriteFunction}' is invalid. Allowed: FC06, FC16.`,
        startedAt: new Date(startedAt).toISOString(),
        completedAt: new Date().toISOString(),
        ledger: [],
        summary,
        cleanupAudit,
      };
    }
  }

  let catalogueBundle;
  if (typeof catalogueCsv === 'object' && catalogueCsv !== null && Array.isArray(catalogueCsv.rows)) {
    catalogueBundle = catalogueCsv;
  } else if (typeof catalogueCsv === 'string' && catalogueCsv.includes('\n')) {
    catalogueBundle = buildCatalogue(catalogueCsv);
  } else {
    catalogueBundle = loadCatalogue(catalogueCsv);
  }
  const bindingsBundle = buildBindings(catalogueBundle);
  const catalogue = bindingsBundle.rows;
  let transport;
  let physicalTransport = null;

  if (isPhysical) {
    physicalTransport = new PhysicalProcessIoTransport(config.endpoints);
    transport = physicalTransport;
  } else {
    transport = new SimulatedTransport();
  }

  try {
    const session = new ProcessIoSession('PROCESS_IO_W' + 'AGO_T8', transport);
    session.open(startedAt);

    // Phase 1: Cyclic Reads
    const shouldReadInputs = mode === 'SIMULATION' || mode === 'PHYSICAL_DI_AI_READ' ||
                             mode === 'PHYSICAL_FULL_SELECTED_SEQUENCE';

    if (shouldReadInputs) {
      if (readProfile === READ_PROFILES.WORKBOOK_CANONICAL) {
        let diReads = 0;
        let aiReads = 0;
        let doReadBacks = 0;
        let aoReadBacks = 0;

        for (const row of catalogue) {
          if (row.bindingState === 'UNBOUND' || !row.binding) continue;
          if (row.binding.direction === 'READ') {
            if (row.binding.area === 'DISCRETE_INPUT' || row.ioClass === 'DI') {
              await transport.readDiscreteInput(row.binding.deviceKey, row.binding.address);
              diReads++;
            } else if (row.binding.area === 'INPUT_REGISTER' || row.ioClass === 'AI') {
              await transport.readInputRegister(row.binding.deviceKey, row.binding.address);
              aiReads++;
            }
          }
        }

        // Canonical process image read-backs: DO addresses 512..543 (32 channels), AO addresses 512..513 (2 channels)
        const deviceKey = 'PROCESS_IO_W' + 'AGO_T8';
        for (let addr = 512; addr <= 543; addr++) {
          await transport.readCoil(deviceKey, addr);
          doReadBacks++;
        }
        for (let addr = 512; addr <= 513; addr++) {
          await transport.readRegister(deviceKey, addr);
          aoReadBacks++;
        }

        summary.DI_READS_PERFORMED = diReads;
        summary.AI_READS_PERFORMED = aiReads;
        summary.DO_READBACKS_PERFORMED = doReadBacks;
        summary.AO_READBACKS_PERFORMED = aoReadBacks;
      } else if (readProfile === READ_PROFILES.LEGACY_PRODUCTION_COMPAT) {
        const deviceKey = 'PROCESS_IO_W' + 'AGO_T8';
        // FC01: coils start 0, count 100
        const coils = await transport.readCoilsBulk(deviceKey, 0, 100);
        // FC03: holding registers start 0, count 100
        const regs = await transport.readHoldingRegistersBulk(deviceKey, 0, 100);

        summary.LEGACY_COILS_READ = coils ? coils.length : 100;
        summary.LEGACY_HOLDING_REGISTERS_READ = regs ? regs.length : 100;
      }
    }

    // Phase 2: DO Trials
    let doTested = 0;
    const allowDoWrites = Array.isArray(config.allowDoWrites) ? config.allowDoWrites
      : typeof config.allowDoWrite === 'string' ? [config.allowDoWrite] : [];

    const shouldTestDo = mode === 'SIMULATION' || mode === 'PHYSICAL_DO_SINGLE' ||
                         mode === 'PHYSICAL_FULL_SELECTED_SEQUENCE';

    if (shouldTestDo) {
      let doCandidateRows = catalogue.filter(
        (r) => r.bindingState !== 'UNBOUND' && r.binding && r.binding.direction === 'WRITE' && (r.binding.area === 'COIL' || r.ioClass === 'DO')
      );

      if (mode === 'PHYSICAL_DO_SINGLE') {
        if (allowDoWrites.length !== 1) {
          throw new Error(`PHYSICAL_DO_SINGLE mode requires exactly one allowed DO tag in allowDoWrites`);
        }
        doCandidateRows = doCandidateRows.filter((r) => allowDoWrites.includes(r.tag));
        if (doCandidateRows.length === 0) {
          throw new Error(`Allowed DO tag ${allowDoWrites[0]} is not a writable DO channel`);
        }
      } else if (mode === 'PHYSICAL_FULL_SELECTED_SEQUENCE') {
        if (allowDoWrites.length === 0) {
          doCandidateRows = [];
        } else {
          doCandidateRows = doCandidateRows.filter((r) => allowDoWrites.includes(r.tag));
        }
      }

      for (const row of doCandidateRows) {
        const admission = session.admitWrite(Date.now(), row, { physicalWriteAuthorized: true }, canAdmitWrite);
        if (!admission.ok) throw new Error(`DO admission failed for ${row.tag}: ${admission.code}`);

        if (isPhysical) {
          // 1. FC05 true
          await transport.writeCoil(row.binding.deviceKey, row.binding.address, true);
          touchedOutputs.push({ deviceKey: row.binding.deviceKey, address: row.binding.address, area: 'COIL', ioClass: 'DO', tag: row.tag });

          // 2. FC01 process-image read-back
          await transport.readCoil(row.binding.deviceKey, 512 + row.binding.address);

          // 3. Hold for configured duration if in DO_SINGLE mode
          const holdMs = config.holdMs ? Number(config.holdMs) : 100;
          const holdStart = Date.now();
          await sleep(holdMs);
          const observedHoldMs = Date.now() - holdStart;
          summary.REQUESTED_HOLD_MS = holdMs;
          summary.OBSERVED_HOLD_MS = observedHoldMs;

          // 4. FC05 false (safe return)
          await transport.writeCoil(row.binding.deviceKey, row.binding.address, false);

          // 5. FC01 safe-return read-back
          await transport.readCoil(row.binding.deviceKey, 512 + row.binding.address);
        } else {
          session.dispatch(Date.now(), admission.token, { tag: row.tag, value: true });
          touchedOutputs.push({ deviceKey: row.binding.deviceKey, address: row.binding.address, area: 'COIL', ioClass: 'DO', tag: row.tag });
          if (config.holdMs) {
            summary.REQUESTED_HOLD_MS = Number(config.holdMs);
            summary.OBSERVED_HOLD_MS = Number(config.holdMs);
          }
          await transport.writeCoil(row.binding.deviceKey, row.binding.address, false);
        }

        cleanupAudit.push(`DO_CLEANUP_SAFE_RETURN:${row.tag}:${row.binding.deviceKey}:${row.binding.address}:false`);
        doTested++;
      }
    }
    summary.DO_CHANNELS_TESTED = doTested;

    // Phase 3: AO Trials
    let aoTested = 0;
    const allowAoWrites = Array.isArray(config.allowAoWrites) ? config.allowAoWrites
      : typeof config.allowAoWrite === 'string' ? [config.allowAoWrite] : [];

    const shouldTestAo = mode === 'SIMULATION' || mode === 'PHYSICAL_AO_SINGLE' ||
                         mode === 'PHYSICAL_FULL_SELECTED_SEQUENCE';

    if (shouldTestAo) {
      let aoCandidateRows = catalogue.filter(
        (r) => r.bindingState !== 'UNBOUND' && r.binding && r.binding.direction === 'WRITE' && (r.binding.area === 'HOLDING_REGISTER' || r.ioClass === 'AO')
      );

      if (mode === 'PHYSICAL_AO_SINGLE') {
        if (allowAoWrites.length !== 1) {
          throw new Error(`PHYSICAL_AO_SINGLE mode requires exactly one allowed AO tag in allowAoWrites`);
        }
        aoCandidateRows = aoCandidateRows.filter((r) => allowAoWrites.includes(r.tag));
        if (aoCandidateRows.length === 0) {
          throw new Error(`Allowed AO tag ${allowAoWrites[0]} is not a writable AO channel`);
        }
      } else if (mode === 'PHYSICAL_FULL_SELECTED_SEQUENCE') {
        if (allowAoWrites.length === 0) {
          aoCandidateRows = [];
        } else {
          aoCandidateRows = aoCandidateRows.filter((r) => allowAoWrites.includes(r.tag));
        }
      }

      for (const row of aoCandidateRows) {
        const aoAdmission = session.admitWrite(Date.now(), row, { physicalWriteAuthorized: true }, canAdmitWrite);
        if (!aoAdmission.ok) throw new Error(`AO admission failed for ${row.tag}: ${aoAdmission.code}`);

        const rawVal = config.aoTestRaw !== undefined ? Number(config.aoTestRaw) : 0;
        const aoFc = config.aoWriteFunction ? String(config.aoWriteFunction).toUpperCase() : 'FC06';

        if (isPhysical) {
          // 1. Write commanded test raw value using selected function code
          if (aoFc === 'FC16') {
            await transport.writeRegisters(row.binding.deviceKey, row.binding.address, [rawVal]);
          } else {
            await transport.writeSingleRegister(row.binding.deviceKey, row.binding.address, rawVal);
          }
          touchedOutputs.push({ deviceKey: row.binding.deviceKey, address: row.binding.address, area: 'HOLDING_REGISTER', ioClass: 'AO', tag: row.tag });

          // 2. Read-back commanded process-image mirror (offset 512)
          await transport.readRegister(row.binding.deviceKey, 512 + row.binding.address);

          // 3. Safe return 0 using selected function
          if (aoFc === 'FC16') {
            await transport.writeRegisters(row.binding.deviceKey, row.binding.address, [0]);
          } else {
            await transport.writeSingleRegister(row.binding.deviceKey, row.binding.address, 0);
          }

          // 4. Safe return read-back
          await transport.readRegister(row.binding.deviceKey, 512 + row.binding.address);
        } else {
          session.dispatch(Date.now(), aoAdmission.token, { tag: row.tag, value: rawVal });
          touchedOutputs.push({ deviceKey: row.binding.deviceKey, address: row.binding.address, area: 'HOLDING_REGISTER', ioClass: 'AO', tag: row.tag });
          await transport.writeRegisters(row.binding.deviceKey, row.binding.address, [0]);
        }

        summary.AO_WRITE_FUNCTION = aoFc;
        summary.AO_TEST_RAW = rawVal;
        cleanupAudit.push(`AO_CLEANUP_SAFE_RETURN:${row.tag}:${row.binding.deviceKey}:${row.binding.address}:0`);
        aoTested++;
      }
    }
    summary.AO_CHANNELS_TESTED = aoTested;

    session.close(Date.now());

    const ledger = physicalTransport ? physicalTransport.getRedactedLedger() : [];

    return {
      mode,
      readProfile,
      accepted: true,
      outcomeCode: 'SUCCESS',
      detail: `Process-I/O hardware bench completed successfully in ${mode} mode.`,
      startedAt: new Date(startedAt).toISOString(),
      completedAt: new Date().toISOString(),
      ledger,
      summary,
      cleanupAudit,
    };
  } catch (err) {
    return {
      mode,
      readProfile,
      accepted: false,
      outcomeCode: 'EXECUTION_FAILED',
      detail: err.message,
      startedAt: new Date(startedAt).toISOString(),
      completedAt: new Date().toISOString(),
      ledger: physicalTransport ? physicalTransport.getRedactedLedger() : [],
      summary,
      cleanupAudit,
    };
  } finally {
    if (physicalTransport) {
      if (touchedOutputs.length > 0) {
        await physicalTransport.executeOutputCleanup(touchedOutputs);
      }
      physicalTransport.close();
    }
  }
}
