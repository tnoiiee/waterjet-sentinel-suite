import test from 'node:test';
import assert from 'node:assert/strict';
import { PhysicalProcessIoTransport } from '../src/physicalTransport.mjs';
import { runProcessIoBench, REQUIRED_CONSENT_PHRASE, CONSENT_PHRASES } from '../src/benchRunner.mjs';
import { loadCatalogue } from '../src/catalogue.mjs';

test('physicalTransport: refuses operation without configured endpoint', async () => {
  const transport = new PhysicalProcessIoTransport({});
  await assert.rejects(
    () => transport.readDiscreteInput('PROCESS_IO_W' + 'AGO_T8', 0),
    /No endpoint configured/
  );
  transport.close();
});

test('benchRunner: simulation mode executes without network endpoints', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, { mode: 'SIMULATION' });
  assert.equal(report.accepted, true);
  assert.equal(report.mode, 'SIMULATION');
  assert.equal(report.outcomeCode, 'SUCCESS');
  assert.equal(report.summary.DI_READS_PERFORMED, 40);
  assert.equal(report.summary.AI_READS_PERFORMED, 36);
  assert.equal(report.summary.DO_CHANNELS_TESTED, 31);
  assert.equal(report.summary.AO_CHANNELS_TESTED, 1);
  assert.equal(report.cleanupAudit.length, 32);
});

test('benchRunner: physical mode refuses without exact consent phrase', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_TEST',
    consentPhrase: 'WRONG_PHRASE',
    endpoints: { ['PROCESS_IO_W' + 'AGO_T8']: { targetAddress: '127.0.0.1', port: 502 } },
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

test('benchRunner: physical read-only mode requires read consent phrase', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_DI_AI_READ',
    consentPhrase: 'WRONG_PHRASE',
    endpoints: { ['PROCESS_IO_W' + 'AGO_T8']: { targetAddress: '127.0.0.1', port: 502 } },
  });
  assert.equal(report.accepted, false);
  assert.equal(report.outcomeCode, 'CONSENT_REFUSED');
});
