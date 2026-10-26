import test from 'node:test';
import assert from 'node:assert/strict';
import net from 'node:net';
import { PhysicalProcessIoTransport } from '../src/physicalTransport.mjs';
import { runProcessIoBench, REQUIRED_CONSENT_PHRASE } from '../src/benchRunner.mjs';
import { loadCatalogue } from '../src/catalogue.mjs';

test('physicalTransport: refuses operation without configured endpoint', async () => {
  const transport = new PhysicalProcessIoTransport({});
  await assert.rejects(
    () => transport.readCoil('PROCESS_IO_PUMP_RACK', 0),
    /No endpoint configured/
  );
  transport.close();
});

test('benchRunner: simulation mode executes without network endpoints', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, { mode: 'SIMULATION' });
  assert.equal(report.accepted, true);
  assert.equal(report.mode, 'SIMULATION');
  assert.equal(report.outcomeCode, 'TRIAL_ACCEPTED');
  assert.equal(report.summary.DI_READS_PERFORMED, 22);
  assert.equal(report.summary.AI_READS_PERFORMED, 14);
  assert.equal(report.summary.DO_CHANNELS_TESTED, 15);
  assert.equal(report.cleanupAudit.length, 16);
});

test('benchRunner: physical mode refuses without exact consent phrase', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_TEST',
    consentPhrase: 'WRONG_PHRASE',
    endpoints: { PROCESS_IO_PUMP_RACK: { targetAddress: '127.0.0.1', port: 502 } },
  });
  assert.equal(report.accepted, false);
  assert.equal(report.outcomeCode, 'CONSENT_REFUSED');
});

test('benchRunner: physical mode refuses without endpoints', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_TEST',
    consentPhrase: REQUIRED_CONSENT_PHRASE,
    endpoints: {},
  });
  assert.equal(report.accepted, false);
  assert.equal(report.outcomeCode, 'ENDPOINTS_UNCONFIGURED');
});
