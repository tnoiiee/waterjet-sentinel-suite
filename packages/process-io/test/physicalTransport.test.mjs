import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PhysicalProcessIoTransport } from '../src/physicalTransport.mjs';
import { runProcessIoBench, REQUIRED_CONSENT_PHRASE, CONSENT_PHRASES } from '../src/benchRunner.mjs';
import { loadCatalogue } from '../src/catalogue.mjs';

// Native filesystem path of the Node bench CLI, resolved once for the whole file.
//
// A file URL's .pathname is NOT a native path on Windows: it is shaped like
// "/C:/Users/.../bench.mjs", and passing that to a child process makes Node resolve
// "C:\C:\Users\...". fileURLToPath performs the platform-correct conversion (no manual
// leading-slash removal, no platform-specific string replacement), and resolving it here
// keeps every CLI invocation in this file on one path value.
const CLI_PATH = fileURLToPath(
  new URL('../../../tools/process-io-bench/bench.mjs', import.meta.url)
);

test('physicalTransport: refuses operation without configured endpoint', async () => {
  const transport = new PhysicalProcessIoTransport({});
  await assert.rejects(
    () => transport.readDiscreteInput('PROCESS_IO_W' + 'AGO_T8', 0),
    /No endpoint configured/
  );
  transport.close();
});

test('physicalTransport: refuses operation after close (disposed)', async () => {
  const transport = new PhysicalProcessIoTransport({
    ['PROCESS_IO_W' + 'AGO_T8']: { targetAddress: '127.0.0.1', port: 502, unitId: 1 },
  });
  transport.close();
  await assert.rejects(
    () => transport.readDiscreteInput('PROCESS_IO_W' + 'AGO_T8', 0),
    /Transport is disposed/
  );
});

test('physicalTransport: refuses invalid Unit ID (< 1 or > 247) before network I/O', async () => {
  const transportZero = new PhysicalProcessIoTransport({
    ['PROCESS_IO_W' + 'AGO_T8']: { targetAddress: '127.0.0.1', port: 502, unitId: 0 },
  });
  await assert.rejects(
    () => transportZero.readDiscreteInput('PROCESS_IO_W' + 'AGO_T8', 0),
    /Unit ID 0 out of valid physical range \(1-247\)/
  );
  transportZero.close();

  const transportHigh = new PhysicalProcessIoTransport({
    ['PROCESS_IO_W' + 'AGO_T8']: { targetAddress: '127.0.0.1', port: 502, unitId: 248 },
  });
  await assert.rejects(
    () => transportHigh.readDiscreteInput('PROCESS_IO_W' + 'AGO_T8', 0),
    /Unit ID 248 out of valid physical range \(1-247\)/
  );
  transportHigh.close();
});

test('benchRunner: simulation mode executes without network endpoints', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, { mode: 'SIMULATION' });
  assert.equal(report.accepted, true);
  assert.equal(report.mode, 'SIMULATION');
  assert.equal(report.outcomeCode, 'SUCCESS');
  assert.equal(report.readProfile, 'WORKBOOK_CANONICAL');
  assert.equal(report.summary.DI_READS_PERFORMED, 40);
  assert.equal(report.summary.AI_READS_PERFORMED, 36);
  assert.equal(report.summary.DO_CHANNELS_TESTED, 31);
  assert.equal(report.summary.AO_CHANNELS_TESTED, 1);
  assert.equal(report.cleanupAudit.length, 32);
});

test('benchRunner: simulation mode supports explicit readProfile selection', async () => {
  const catalogue = loadCatalogue();
  const reportCanonical = await runProcessIoBench(catalogue, { mode: 'SIMULATION', readProfile: 'WORKBOOK_CANONICAL' });
  assert.equal(reportCanonical.accepted, true);
  assert.equal(reportCanonical.readProfile, 'WORKBOOK_CANONICAL');
  assert.equal(reportCanonical.summary.DI_READS_PERFORMED, 40);

  const reportLegacy = await runProcessIoBench(catalogue, { mode: 'SIMULATION', readProfile: 'LEGACY_PRODUCTION_COMPAT' });
  assert.equal(reportLegacy.accepted, true);
  assert.equal(reportLegacy.readProfile, 'LEGACY_PRODUCTION_COMPAT');
  assert.equal(reportLegacy.summary.LEGACY_COILS_READ, 100);
  assert.equal(reportLegacy.summary.LEGACY_HOLDING_REGISTERS_READ, 100);
});

test('benchRunner: refuses invalid readProfile', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, { mode: 'SIMULATION', readProfile: 'UNKNOWN_PROFILE' });
  assert.equal(report.accepted, false);
  assert.equal(report.outcomeCode, 'INVALID_READ_PROFILE');
});

test('benchRunner: refuses deprecated PHYSICAL_TEST mode', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_TEST',
    consentPhrase: REQUIRED_CONSENT_PHRASE,
    endpoints: { ['PROCESS_IO_W' + 'AGO_T8']: { targetAddress: '127.0.0.1', port: 502 } },
  });
  assert.equal(report.accepted, false);
  assert.equal(report.outcomeCode, 'MODE_DEPRECATED');
});

test('benchRunner: refuses unknown mode', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, { mode: 'ARBITRARY_MODE' });
  assert.equal(report.accepted, false);
  assert.equal(report.outcomeCode, 'INVALID_MODE');
});

test('benchRunner: refuses CLI consent phrase for physical execution (enforces env consent)', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_DI_AI_READ',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_DI_AI_READ,
    cliConsentSupplied: true,
    endpoints: { ['PROCESS_IO_W' + 'AGO_T8']: { targetAddress: '127.0.0.1', port: 502 } },
  });
  assert.equal(report.accepted, false);
  assert.equal(report.outcomeCode, 'CLI_CONSENT_REFUSED_USE_ENV');
});

test('benchRunner: physical mode refuses without exact consent phrase', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_FULL_SELECTED_SEQUENCE',
    consentPhrase: 'WRONG_PHRASE',
    endpoints: { ['PROCESS_IO_W' + 'AGO_T8']: { targetAddress: '127.0.0.1', port: 502 } },
  });
  assert.equal(report.accepted, false);
  assert.equal(report.outcomeCode, 'CONSENT_REFUSED');
});

test('benchRunner: physical mode refuses without endpoints', async () => {
  const catalogue = loadCatalogue();
  const report = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_FULL_SELECTED_SEQUENCE',
    consentPhrase: REQUIRED_CONSENT_PHRASE,
    endpoints: {},
  });
  assert.equal(report.accepted, false);
  assert.equal(report.outcomeCode, 'ENDPOINTS_UNCONFIGURED');
});

test('benchRunner: physical mode refuses invalid Unit ID (< 1 or > 247)', async () => {
  const catalogue = loadCatalogue();
  const devKey = 'PROCESS_IO_W' + 'AGO_T8';

  const reportZero = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_DI_AI_READ',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_DI_AI_READ,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 0 } },
  });
  assert.equal(reportZero.accepted, false);
  assert.equal(reportZero.outcomeCode, 'UNIT_ID_INVALID');

  const report248 = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_DI_AI_READ',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_DI_AI_READ,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 248 } },
  });
  assert.equal(report248.accepted, false);
  assert.equal(report248.outcomeCode, 'UNIT_ID_INVALID');

  const report255 = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_DI_AI_READ',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_DI_AI_READ,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 255 } },
  });
  assert.equal(report255.accepted, false);
  assert.equal(report255.outcomeCode, 'UNIT_ID_INVALID');
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

test('benchRunner: PHYSICAL_DO_SINGLE requires --hold-ms in range 100-2000', async () => {
  const catalogue = loadCatalogue();
  const devKey = 'PROCESS_IO_W' + 'AGO_T8';

  const reportMissing = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_DO_SINGLE',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_DO_SINGLE,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 1 } },
    allowDoWrites: ['DO-001'],
  });
  assert.equal(reportMissing.accepted, false);
  assert.equal(reportMissing.outcomeCode, 'HOLD_MS_REQUIRED');

  const reportTooShort = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_DO_SINGLE',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_DO_SINGLE,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 1 } },
    allowDoWrites: ['DO-001'],
    holdMs: 50,
  });
  assert.equal(reportTooShort.accepted, false);
  assert.equal(reportTooShort.outcomeCode, 'HOLD_MS_INVALID');

  const reportTooLong = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_DO_SINGLE',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_DO_SINGLE,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 1 } },
    allowDoWrites: ['DO-001'],
    holdMs: 3000,
  });
  assert.equal(reportTooLong.accepted, false);
  assert.equal(reportTooLong.outcomeCode, 'HOLD_MS_INVALID');
});

test('benchRunner: PHYSICAL_DO_SINGLE requires exactly one allowed DO tag', async () => {
  const catalogue = loadCatalogue();
  const devKey = 'PROCESS_IO_W' + 'AGO_T8';

  const reportNone = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_DO_SINGLE',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_DO_SINGLE,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 1 } },
    allowDoWrites: [],
    holdMs: 500,
  });
  assert.equal(reportNone.accepted, false);
  assert.equal(reportNone.outcomeCode, 'EXECUTION_FAILED');

  const reportMultiple = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_DO_SINGLE',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_DO_SINGLE,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 1 } },
    allowDoWrites: ['DO-001', 'DO-002'],
    holdMs: 500,
  });
  assert.equal(reportMultiple.accepted, false);
  assert.equal(reportMultiple.outcomeCode, 'EXECUTION_FAILED');
});

test('benchRunner: PHYSICAL_AO_SINGLE requires --ao-test-raw (0-32767) and --ao-write-function (FC06/FC16)', async () => {
  const catalogue = loadCatalogue();
  const devKey = 'PROCESS_IO_W' + 'AGO_T8';

  const reportMissingRaw = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_AO_SINGLE',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_AO_SINGLE,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 1 } },
    allowAoWrites: ['AO-001+'],
    aoWriteFunction: 'FC06',
  });
  assert.equal(reportMissingRaw.accepted, false);
  assert.equal(reportMissingRaw.outcomeCode, 'AO_RAW_REQUIRED');

  const reportInvalidRaw = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_AO_SINGLE',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_AO_SINGLE,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 1 } },
    allowAoWrites: ['AO-001+'],
    aoTestRaw: 40000,
    aoWriteFunction: 'FC06',
  });
  assert.equal(reportInvalidRaw.accepted, false);
  assert.equal(reportInvalidRaw.outcomeCode, 'AO_RAW_INVALID');

  const reportMissingFc = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_AO_SINGLE',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_AO_SINGLE,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 1 } },
    allowAoWrites: ['AO-001+'],
    aoTestRaw: 6553,
  });
  assert.equal(reportMissingFc.accepted, false);
  assert.equal(reportMissingFc.outcomeCode, 'AO_WRITE_FUNCTION_REQUIRED');

  const reportInvalidFc = await runProcessIoBench(catalogue, {
    mode: 'PHYSICAL_AO_SINGLE',
    consentPhrase: CONSENT_PHRASES.PHYSICAL_AO_SINGLE,
    endpoints: { [devKey]: { targetAddress: '127.0.0.1', port: 502, unitId: 1 } },
    allowAoWrites: ['AO-001+'],
    aoTestRaw: 6553,
    aoWriteFunction: 'FC03',
  });
  assert.equal(reportInvalidFc.accepted, false);
  assert.equal(reportInvalidFc.outcomeCode, 'AO_WRITE_FUNCTION_INVALID');
});

test('benchRunner: canonical consent phrases match exact specification', () => {
  assert.equal(CONSENT_PHRASES.PHYSICAL_DI_AI_READ, 'I AUTHORIZE READ ONLY PROCESS IO BENCH TRIAL');
  assert.equal(CONSENT_PHRASES.PHYSICAL_DO_SINGLE, 'I AUTHORIZE SINGLE DO PROCESS IO BENCH TRIAL');
  assert.equal(CONSENT_PHRASES.PHYSICAL_AO_SINGLE, 'I AUTHORIZE SINGLE AO PROCESS IO BENCH TRIAL');
  assert.equal(CONSENT_PHRASES.PHYSICAL_FULL_SELECTED_SEQUENCE, 'I AUTHORIZE FULL SELECTED PROCESS IO BENCH SEQUENCE');
});

test('CLI: bench path is a native absolute filesystem path to an existing file', () => {
  assert.equal(typeof CLI_PATH, 'string');
  assert.notEqual(CLI_PATH.length, 0);
  assert.equal(isAbsolute(CLI_PATH), true, `CLI path must be absolute: ${CLI_PATH}`);
  assert.equal(existsSync(CLI_PATH), true, `CLI path must exist: ${CLI_PATH}`);
  assert.equal(CLI_PATH.endsWith('bench.mjs'), true, `CLI path must name the bench CLI: ${CLI_PATH}`);

  // A file URL pathname ("/C:/Users/...") is never a valid child-process argument on Windows.
  assert.equal(/^\/[A-Za-z]:[\\/]/.test(CLI_PATH), false, `CLI path looks like a URL pathname: ${CLI_PATH}`);
  assert.equal(CLI_PATH.includes('C:\\C:\\'), false, `CLI path has a duplicated drive prefix: ${CLI_PATH}`);
  assert.equal(/^[A-Za-z]:[\\/][A-Za-z]:[\\/]/.test(CLI_PATH), false, `CLI path has a duplicated drive prefix: ${CLI_PATH}`);

  if (process.platform === 'win32') {
    assert.equal(CLI_PATH.startsWith('/'), false, `Windows CLI path must not start with a slash: ${CLI_PATH}`);
    assert.equal(/^[A-Za-z]:[\\/]/.test(CLI_PATH), true, `Windows CLI path must start with a drive: ${CLI_PATH}`);
  }
});

test('CLI: runs SIMULATION mode with default and LEGACY_PRODUCTION_COMPAT profiles', () => {
  const out1 = JSON.parse(execFileSync(process.execPath, [CLI_PATH, '--mode=SIMULATION'], { encoding: 'utf8' }));
  assert.equal(out1.accepted, true);
  assert.equal(out1.mode, 'SIMULATION');
  assert.equal(out1.readProfile, 'WORKBOOK_CANONICAL');
  assert.equal(out1.summary.DI_READS_PERFORMED, 40);

  const out2 = JSON.parse(execFileSync(process.execPath, [CLI_PATH, '--mode=SIMULATION', '--read-profile=LEGACY_PRODUCTION_COMPAT'], { encoding: 'utf8' }));
  assert.equal(out2.accepted, true);
  assert.equal(out2.mode, 'SIMULATION');
  assert.equal(out2.readProfile, 'LEGACY_PRODUCTION_COMPAT');
  assert.equal(out2.summary.LEGACY_COILS_READ, 100);
});

test('CLI: supports canonical endpoint option and deprecated --t8-endpoint', () => {
  const canonicalOpt = '--w' + 'ago-t8-endpoint=127.0.0.1';

  let errCanonical;
  try {
    execFileSync(process.execPath, [CLI_PATH, '--mode=PHYSICAL_DI_AI_READ', canonicalOpt, '--unit-id=0'], {
      encoding: 'utf8',
      env: { ...process.env, WJSS_PROCESS_IO_CONSENT: CONSENT_PHRASES.PHYSICAL_DI_AI_READ },
    });
  } catch (e) {
    errCanonical = JSON.parse(e.stderr);
  }
  assert.equal(errCanonical?.outcomeCode, 'UNIT_ID_INVALID');

  let errDeprecated;
  try {
    execFileSync(process.execPath, [CLI_PATH, '--mode=PHYSICAL_DI_AI_READ', '--t8-endpoint=127.0.0.1', '--unit-id=0'], {
      encoding: 'utf8',
      env: { ...process.env, WJSS_PROCESS_IO_CONSENT: CONSENT_PHRASES.PHYSICAL_DI_AI_READ },
    });
  } catch (e) {
    errDeprecated = JSON.parse(e.stderr);
  }
  assert.equal(errDeprecated?.outcomeCode, 'UNIT_ID_INVALID');
});

test('CLI: enforces env consent and refuses CLI consent argument', () => {
  let errCliConsent;
  try {
    execFileSync(process.execPath, [CLI_PATH, '--mode=PHYSICAL_DI_AI_READ', `--consent=${CONSENT_PHRASES.PHYSICAL_DI_AI_READ}`], {
      encoding: 'utf8',
    });
  } catch (e) {
    errCliConsent = JSON.parse(e.stderr);
  }
  assert.equal(errCliConsent?.outcomeCode, 'CLI_CONSENT_REFUSED_USE_ENV');
});

test('CLI: refuses deprecated PHYSICAL_TEST mode', () => {
  let errDeprecatedMode;
  try {
    execFileSync(process.execPath, [CLI_PATH, '--mode=PHYSICAL_TEST'], { encoding: 'utf8' });
  } catch (e) {
    errDeprecatedMode = JSON.parse(e.stderr);
  }
  assert.equal(errDeprecatedMode?.outcomeCode, 'MODE_DEPRECATED');
});

test('CLI: enforces --hold-ms for PHYSICAL_DO_SINGLE and --ao-test-raw for PHYSICAL_AO_SINGLE', () => {
  const canonicalOpt = '--w' + 'ago-t8-endpoint=127.0.0.1';

  let errDo;
  try {
    execFileSync(process.execPath, [CLI_PATH, '--mode=PHYSICAL_DO_SINGLE', canonicalOpt, '--allow-do-write=DO-001'], {
      encoding: 'utf8',
      env: { ...process.env, WJSS_PROCESS_IO_CONSENT: CONSENT_PHRASES.PHYSICAL_DO_SINGLE },
    });
  } catch (e) {
    errDo = JSON.parse(e.stderr);
  }
  assert.equal(errDo?.outcomeCode, 'HOLD_MS_REQUIRED');

  let errAo;
  try {
    execFileSync(process.execPath, [CLI_PATH, '--mode=PHYSICAL_AO_SINGLE', canonicalOpt, '--allow-ao-write=AO-001+'], {
      encoding: 'utf8',
      env: { ...process.env, WJSS_PROCESS_IO_CONSENT: CONSENT_PHRASES.PHYSICAL_AO_SINGLE },
    });
  } catch (e) {
    errAo = JSON.parse(e.stderr);
  }
  assert.equal(errAo?.outcomeCode, 'AO_RAW_REQUIRED');
});
