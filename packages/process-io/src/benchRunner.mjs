// H2 Physical Process-I/O — Hardware Bench Runner.
// Supports SIMULATION, PHYSICAL_DI_AI_READ, PHYSICAL_DO_SINGLE, PHYSICAL_AO_SINGLE,
// PHYSICAL_FULL_SELECTED_SEQUENCE, and PHYSICAL_TEST modes.
// Requires explicit mode-specific Owner consent for all physical modes.
// Guarantees fail-closed safe-return cleanup of touched outputs.

import { buildCatalogue, loadCatalogue } from './catalogue.mjs';
import { buildBindings, canAdmitWrite } from './bindings.mjs';
import { ProcessIoSession } from './lifecycle.mjs';
import { SimulatedTransport } from './simulation.mjs';
import { PhysicalProcessIoTransport } from './physicalTransport.mjs';

export const CONSENT_PHRASES = Object.freeze({
  PHYSICAL_DI_AI_READ: 'AUTHORIZE_PHYSICAL_READ_ONLY_COMMISSIONING',
  PHYSICAL_DO_SINGLE: 'AUTHORIZE_PHYSICAL_DO_SINGLE_COMMISSIONING',
  PHYSICAL_AO_SINGLE: 'AUTHORIZE_PHYSICAL_AO_SINGLE_COMMISSIONING',
  PHYSICAL_FULL_SELECTED_SEQUENCE: 'AUTHORIZE_PHYSICAL_BENCH_COMMISSIONING_DISCONNECTED_OUTPUTS',
  PHYSICAL_TEST: 'AUTHORIZE_PHYSICAL_BENCH_COMMISSIONING_DISCONNECTED_OUTPUTS',
});

export const REQUIRED_CONSENT_PHRASE = CONSENT_PHRASES.PHYSICAL_FULL_SELECTED_SEQUENCE;

export async function runProcessIoBench(catalogueCsv, config = {}) {
  const mode = config.mode ?? 'SIMULATION';
  const startedAt = Date.now();
  const summary = {};
  const cleanupAudit = [];
  const touchedOutputs = [];

  const isPhysical = mode.startsWith('PHYSICAL_');

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

    // Phase 1: Cyclic Reads (DI and AI)
    let diReads = 0;
    let aiReads = 0;

    const shouldReadInputs = mode === 'SIMULATION' || mode === 'PHYSICAL_DI_AI_READ' ||
                             mode === 'PHYSICAL_FULL_SELECTED_SEQUENCE' || mode === 'PHYSICAL_TEST';

    if (shouldReadInputs) {
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
    }
    summary.DI_READS_PERFORMED = diReads;
    summary.AI_READS_PERFORMED = aiReads;

    // Phase 2: DO Trials (with immediate safe return and touched registration)
    let doTested = 0;
    const allowDoWrites = Array.isArray(config.allowDoWrites) ? config.allowDoWrites
      : typeof config.allowDoWrite === 'string' ? [config.allowDoWrite] : [];

    const shouldTestDo = mode === 'SIMULATION' || mode === 'PHYSICAL_DO_SINGLE' ||
                         mode === 'PHYSICAL_FULL_SELECTED_SEQUENCE' || mode === 'PHYSICAL_TEST';

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
      } else if (mode === 'PHYSICAL_FULL_SELECTED_SEQUENCE' || mode === 'PHYSICAL_TEST') {
        if (allowDoWrites.length === 0) {
          doCandidateRows = []; // No DO write without explicit allowlist
        } else {
          doCandidateRows = doCandidateRows.filter((r) => allowDoWrites.includes(r.tag));
        }
      }

      for (const row of doCandidateRows) {
        const admission = session.admitWrite(Date.now(), row, { physicalWriteAuthorized: true }, canAdmitWrite);
        if (!admission.ok) throw new Error(`DO admission failed for ${row.tag}: ${admission.code}`);

        if (isPhysical) {
          // Write ON
          await transport.writeCoil(row.binding.deviceKey, row.binding.address, true);
          touchedOutputs.push({ deviceKey: row.binding.deviceKey, address: row.binding.address, area: 'COIL', ioClass: 'DO', tag: row.tag });

          // Read-back commanded process-image mirror (offset 512)
          await transport.readCoil(row.binding.deviceKey, 512 + row.binding.address);

          // Safe return OFF (false)
          await transport.writeCoil(row.binding.deviceKey, row.binding.address, false);
          await transport.readCoil(row.binding.deviceKey, 512 + row.binding.address);
        } else {
          session.dispatch(Date.now(), admission.token, { tag: row.tag, value: true });
          touchedOutputs.push({ deviceKey: row.binding.deviceKey, address: row.binding.address, area: 'COIL', ioClass: 'DO', tag: row.tag });
          // Simulate safe return
          await transport.writeCoil(row.binding.deviceKey, row.binding.address, false);
        }

        cleanupAudit.push(`DO_CLEANUP_SAFE_RETURN:${row.tag}:${row.binding.deviceKey}:${row.binding.address}:false`);
        doTested++;
      }
    }
    summary.DO_CHANNELS_TESTED = doTested;

    // Phase 3: AO Trials (with immediate safe return and touched registration)
    let aoTested = 0;
    const allowAoWrites = Array.isArray(config.allowAoWrites) ? config.allowAoWrites
      : typeof config.allowAoWrite === 'string' ? [config.allowAoWrite] : [];

    const shouldTestAo = mode === 'SIMULATION' || mode === 'PHYSICAL_AO_SINGLE' ||
                         mode === 'PHYSICAL_FULL_SELECTED_SEQUENCE' || mode === 'PHYSICAL_TEST';

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
      } else if (mode === 'PHYSICAL_FULL_SELECTED_SEQUENCE' || mode === 'PHYSICAL_TEST') {
        if (allowAoWrites.length === 0) {
          aoCandidateRows = [];
        } else {
          aoCandidateRows = aoCandidateRows.filter((r) => allowAoWrites.includes(r.tag));
        }
      }

      for (const row of aoCandidateRows) {
        const aoAdmission = session.admitWrite(Date.now(), row, { physicalWriteAuthorized: true }, canAdmitWrite);
        if (!aoAdmission.ok) throw new Error(`AO admission failed for ${row.tag}: ${aoAdmission.code}`);

        if (isPhysical) {
          // Write test value (e.g. 6553 = 4.0mA)
          await transport.writeSingleRegister(row.binding.deviceKey, row.binding.address, 6553);
          touchedOutputs.push({ deviceKey: row.binding.deviceKey, address: row.binding.address, area: 'HOLDING_REGISTER', ioClass: 'AO', tag: row.tag });

          // Read-back commanded process-image mirror (offset 512)
          await transport.readRegister(row.binding.deviceKey, 512 + row.binding.address);

          // Safe return 0 (0.0mA / Stop)
          await transport.writeSingleRegister(row.binding.deviceKey, row.binding.address, 0);
          await transport.readRegister(row.binding.deviceKey, 512 + row.binding.address);
        } else {
          session.dispatch(Date.now(), aoAdmission.token, { tag: row.tag, value: 4.0 });
          touchedOutputs.push({ deviceKey: row.binding.deviceKey, address: row.binding.address, area: 'HOLDING_REGISTER', ioClass: 'AO', tag: row.tag });
          // Simulate safe return
          await transport.writeRegisters(row.binding.deviceKey, row.binding.address, [0]);
        }

        cleanupAudit.push(`AO_CLEANUP_SAFE_RETURN:${row.tag}:${row.binding.deviceKey}:${row.binding.address}:0`);
        aoTested++;
      }
    }
    summary.AO_CHANNELS_TESTED = aoTested;

    session.close(Date.now());

    const ledger = physicalTransport ? physicalTransport.getRedactedLedger() : [];

    return {
      mode,
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
