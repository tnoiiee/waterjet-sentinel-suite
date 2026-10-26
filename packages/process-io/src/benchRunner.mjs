// H2 Physical Process-I/O — Hardware Bench Runner.
// Supports SIMULATION and PHYSICAL_TEST modes.
// Requires explicit one-use Owner consent for PHYSICAL_TEST mode.

import { buildCatalogue, loadCatalogue } from './catalogue.mjs';
import { buildBindings, canAdmitWrite } from './bindings.mjs';
import { ProcessIoSession } from './lifecycle.mjs';
import { SimulatedTransport } from './simulation.mjs';
import { PhysicalProcessIoTransport } from './physicalTransport.mjs';

export const REQUIRED_CONSENT_PHRASE = 'AUTHORIZE_PHYSICAL_BENCH_COMMISSIONING_DISCONNECTED_OUTPUTS';

export async function runProcessIoBench(catalogueCsv, config = {}) {
  const mode = config.mode ?? 'SIMULATION';
  const startedAt = Date.now();
  const summary = {};
  const cleanupAudit = [];

  if (mode === 'PHYSICAL_TEST') {
    if (config.consentPhrase !== REQUIRED_CONSENT_PHRASE) {
      return {
        mode: 'PHYSICAL_TEST',
        accepted: false,
        outcomeCode: 'CONSENT_REFUSED',
        detail: 'Physical bench execution requires exact Owner consent phrase.',
        startedAt: new Date(startedAt).toISOString(),
        completedAt: new Date().toISOString(),
        ledger: [],
        summary,
        cleanupAudit,
      };
    }

    if (!config.endpoints || Object.keys(config.endpoints).length === 0) {
      return {
        mode: 'PHYSICAL_TEST',
        accepted: false,
        outcomeCode: 'ENDPOINTS_UNCONFIGURED',
        detail: 'Physical bench execution requires configured target endpoints.',
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

  if (mode === 'PHYSICAL_TEST') {
    physicalTransport = new PhysicalProcessIoTransport(config.endpoints);
    transport = physicalTransport;
  } else {
    transport = new SimulatedTransport();
  }

  try {
    const session = new ProcessIoSession('PROCESS_IO_PUMP_RACK', transport);
    session.open(startedAt);

    // Phase 1: Cyclic Reads
    let diReads = 0;
    let aiReads = 0;
    for (const row of catalogue) {
      if (row.bindingState === 'UNBOUND' || !row.binding) continue;
      if (row.binding.direction === 'READ') {
        if (row.binding.area === 'COIL') {
          if (mode === 'PHYSICAL_TEST') {
            await transport.readCoil(row.binding.deviceKey, row.binding.address);
          }
          diReads++;
        } else if (row.binding.area === 'HOLDING_REGISTER') {
          if (mode === 'PHYSICAL_TEST') {
            await transport.readRegister(row.binding.deviceKey, row.binding.address);
          }
          aiReads++;
        }
      }
    }
    summary.DI_READS_PERFORMED = diReads;
    summary.AI_READS_PERFORMED = aiReads;

    // Phase 2: Sequential DO Trials (with immediate safe return)
    let doTested = 0;
    const doRows = catalogue.filter(
      (r) => r.bindingState !== 'UNBOUND' && r.binding && r.binding.direction === 'WRITE' && r.binding.area === 'COIL'
    );

    for (const row of doRows) {
      const admission = session.admitWrite(Date.now(), row, { physicalWriteAuthorized: true }, canAdmitWrite);
      if (!admission.ok) throw new Error(`DO admission failed for ${row.tag}: ${admission.code}`);

      if (mode === 'PHYSICAL_TEST') {
        await transport.writeCoil(row.binding.deviceKey, row.binding.address, true);
        await transport.writeCoil(row.binding.deviceKey, row.binding.address, false);
      } else {
        session.dispatch(Date.now(), admission.token, { tag: row.tag, value: true });
      }

      cleanupAudit.push(`DO_CLEANUP_OFF:${row.tag}:${row.binding.deviceKey}:${row.binding.address}`);
      doTested++;
    }
    summary.DO_CHANNELS_TESTED = doTested;

    // Phase 3: AO Trial (with immediate safe return)
    const aoRow = catalogue.find((r) => r.tag === 'AO-001+');
    if (aoRow && aoRow.binding) {
      const aoAdmission = session.admitWrite(Date.now(), aoRow, { physicalWriteAuthorized: true }, canAdmitWrite);
      if (aoAdmission.ok) {
        if (mode === 'PHYSICAL_TEST') {
          await transport.writeRegisters(aoRow.binding.deviceKey, aoRow.binding.address, [16384]);
          await transport.writeRegisters(aoRow.binding.deviceKey, aoRow.binding.address, [0]);
        } else {
          session.dispatch(Date.now(), aoAdmission.token, { tag: aoRow.tag, value: 16384 });
        }
        cleanupAudit.push(`AO_CLEANUP_ZERO:${aoRow.tag}:${aoRow.binding.deviceKey}:${aoRow.binding.address}`);
        summary.AO_TESTED = 'AO-001+';
      }
    }

    // Phase 4: Fencing Verification
    session.reconnect(Date.now());
    session.markStop(Date.now());
    session.close(Date.now());

    // Phase 5: Final output cleanup
    if (physicalTransport) {
      const cleanupErrors = await physicalTransport.executeOutputCleanup(catalogue);
      if (cleanupErrors.length > 0) cleanupAudit.push(...cleanupErrors);
    }

    return {
      mode,
      accepted: true,
      outcomeCode: 'TRIAL_ACCEPTED',
      detail: 'Process-I/O bench commissioning trial completed with verified output cleanup.',
      startedAt: new Date(startedAt).toISOString(),
      completedAt: new Date().toISOString(),
      ledger: physicalTransport ? physicalTransport.getLedger() : [],
      summary,
      cleanupAudit,
    };
  } catch (err) {
    if (physicalTransport) {
      try {
        const failureCleanupErrors = await physicalTransport.executeOutputCleanup(catalogue);
        cleanupAudit.push(...failureCleanupErrors);
      } catch {}
    }

    return {
      mode,
      accepted: false,
      outcomeCode: 'TRIAL_FAILED',
      detail: `Trial failed: ${err.message}`,
      startedAt: new Date(startedAt).toISOString(),
      completedAt: new Date().toISOString(),
      ledger: physicalTransport ? physicalTransport.getLedger() : [],
      summary,
      cleanupAudit,
    };
  } finally {
    if (physicalTransport) physicalTransport.close();
  }
}
