// Stage 0.4B-2 — Verified Process-Image Evidence Foundation tests (Tests 1–13).
//
// 1. Profiles start incomplete without evidence.
// 2. Incomplete profiles produce ADDRESS_UNRESOLVED.
// 3. ProcessModulePosition is never accepted as ProcessImageOrder.
// 4. Conflicting evidence blocks derivation.
// 5. Missing status-byte information blocks affected analog mappings.
// 6. Missing head-station rule blocks all numeric addresses.
// 7. Unverified byte/word order blocks affected derivation.
// 8. A complete synthetic test profile derives deterministic non-overlapping addresses.
// 9. Synthetic profile cannot become authoritative.
// 10. Profile fingerprints change when relevant verified evidence changes.
// 11. Numeric manual address input remains refused.
// 12. Reorder invalidates or recalculates the derived manifest.
// 13. Authoritative Workbook remains unchanged.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

import {
  listProfiles, getProfile, profileCatalogueCanonical,
} from '../src/moduleProfiles.mjs';
import {
  deriveRackView, validateRack, moveModule,
} from '../src/rack.mjs';
import {
  deriveAddresses, isSyntheticRule,
} from '../src/addressDerivation.mjs';
import {
  createEvidenceRecord, createHeadStationEvidence, evaluateEvidenceCapability,
} from '../src/evidenceProfile.mjs';
import {
  validateMapping,
} from '../src/mappingValidation.mjs';
import {
  DraftSession, createConfiguration,
} from '../src/draftSession.mjs';
import {
  syntheticExampleConfiguration,
} from '../src/syntheticExample.mjs';
import {
  moduleProfileRevision, derivedAddressManifestFingerprint,
} from '../src/revisions.mjs';
import {
  EVIDENCE_STATE, REASON, ADDRESS_STATE, CHANNEL_TYPE,
} from '../src/constants.mjs';
import { fingerprintOf } from '../src/canonical.mjs';

const HERE = dirname(fileURLToPath(import.meta.url));
const REPO = join(HERE, '..', '..', '..');

const SYNTHETIC_TEST_RULE = Object.freeze({
  ruleId: 'SYNTHETIC-TEST-RULE-ARITHMETIC-ONLY',
  synthetic: true,
  moduleBits: Object.freeze({
    '750-471': Object.freeze({ kind: 'ANALOG' }),
    '750-554': Object.freeze({ kind: 'ANALOG' }),
    '750-430': Object.freeze({ kind: 'DIGITAL' }),
    '750-530': Object.freeze({ kind: 'DIGITAL' }),
  }),
  maxWordsByArea: Object.freeze({ INPUT: 64, OUTPUT: 64 }),
  headStationVerified: true,
  byteOrderVerified: true,
  wordOrderVerified: true,
  statusBytesVerified: true,
});

test('1. Profiles start incomplete without evidence', () => {
  const profiles = listProfiles();
  assert.equal(profiles.length, 8, '8 target hardware models in catalogue');
  for (const p of profiles) {
    assert.equal(p.profileStatus, 'INCOMPLETE', `profileStatus for ${p.modelNumber}`);
    assert.equal(p.addressRule, null, `addressRule for ${p.modelNumber}`);
    assert.ok(p.evidence, `evidence for ${p.modelNumber}`);
    assert.equal(p.evidence.profileCompleteness, 'INCOMPLETE', `evidence profileCompleteness for ${p.modelNumber}`);
    assert.notEqual(p.evidence.verificationState, EVIDENCE_STATE.VERIFIED_PRIMARY_SOURCE);
    assert.notEqual(p.evidence.verificationState, EVIDENCE_STATE.VERIFIED_WAGO_IO_CHECK_EXPORT);
    assert.equal(p.evidence.manufacturer, 'WAGO');
    assert.ok(p.missing.length > 0, `missing items for ${p.modelNumber}`);
  }
});

test('2. Incomplete profiles produce ADDRESS_UNRESOLVED', () => {
  const c = syntheticExampleConfiguration();
  const rackView = deriveRackView(c.modules);
  const result = deriveAddresses(rackView, c.bindings, null);

  assert.equal(result.verified, false);
  const enabled = c.bindings.filter((b) => b.enabled !== false);
  assert.ok(enabled.length > 0);

  for (const e of result.entries.filter((x) => x.state !== 'NOT_ACTIVE')) {
    assert.equal(e.state, ADDRESS_STATE.UNRESOLVED);
    assert.equal(e.bitOffsetAbsolute, null);
    assert.equal(e.byteOffset, null);
    assert.equal(e.wordOffset, null);
    assert.equal(e.bitIndex, null);
    assert.equal(e.displayNotation, null);
    assert.ok(e.reasons.includes(REASON.HEAD_STATION_PROFILE_NOT_VERIFIED));
    assert.ok(e.reasons.includes(REASON.NO_VERIFIED_PROCESS_IMAGE_RULE));
    assert.ok(e.reasons.includes(REASON.MODULE_PROCESS_DATA_PROFILE_NOT_VERIFIED));
  }
});

test('3. ProcessModulePosition is never accepted as ProcessImageOrder', () => {
  const c = syntheticExampleConfiguration();
  const rackView = deriveRackView(c.modules);

  const ruleUsingPosition = {
    ...SYNTHETIC_TEST_RULE,
    processImageOrder: 'PROCESS_MODULE_POSITION',
  };
  const result = deriveAddresses(rackView, c.bindings, ruleUsingPosition);

  assert.equal(result.verified, false);
  for (const e of result.entries.filter((x) => x.state !== 'NOT_ACTIVE')) {
    assert.equal(e.state, ADDRESS_STATE.UNRESOLVED);
    assert.ok(e.reasons.includes(REASON.PROCESS_IMAGE_ORDER_NOT_VERIFIED));
  }
});

test('4. Conflicting evidence blocks derivation', () => {
  const c = syntheticExampleConfiguration();
  const rackView = deriveRackView(c.modules);

  const conflictingRule = {
    ...SYNTHETIC_TEST_RULE,
    conflictingEvidence: true,
  };
  const result = deriveAddresses(rackView, c.bindings, conflictingRule);

  assert.equal(result.verified, false);
  for (const e of result.entries.filter((x) => x.state !== 'NOT_ACTIVE')) {
    assert.equal(e.state, ADDRESS_STATE.UNRESOLVED);
    assert.ok(e.reasons.includes(REASON.CONFLICTING_PROCESS_IMAGE_EVIDENCE));
  }
});

test('5. Missing status-byte information blocks affected analog mappings', () => {
  const c = syntheticExampleConfiguration();
  const rackView = deriveRackView(c.modules);

  const noStatusBytesRule = {
    ...SYNTHETIC_TEST_RULE,
    statusBytesVerified: false,
  };
  const result = deriveAddresses(rackView, c.bindings, noStatusBytesRule);
  const byTag = new Map(result.entries.map((e) => [e.tagName, e]));

  const pumpOutlet = byTag.get('PUMP_OUTLET_PRESSURE');
  assert.equal(pumpOutlet.state, ADDRESS_STATE.UNRESOLVED);
  assert.ok(pumpOutlet.reasons.includes(REASON.MODULE_STATUS_BYTE_SETTING_NOT_VERIFIED));

  const digitalLimit = byTag.get('IV1_LOWER_LIMIT');
  assert.equal(digitalLimit.state, ADDRESS_STATE.DERIVED);
  assert.equal(digitalLimit.reasons.length, 0);
});

test('6. Missing head-station rule blocks all numeric addresses', () => {
  const c = syntheticExampleConfiguration();
  const rackView = deriveRackView(c.modules);

  const noHeadRule = {
    ...SYNTHETIC_TEST_RULE,
    headStationVerified: false,
  };
  const result = deriveAddresses(rackView, c.bindings, noHeadRule);

  assert.equal(result.verified, false);
  for (const e of result.entries.filter((x) => x.state !== 'NOT_ACTIVE')) {
    assert.equal(e.state, ADDRESS_STATE.UNRESOLVED);
    assert.ok(e.reasons.includes(REASON.HEAD_STATION_PROFILE_NOT_VERIFIED));
  }
});

test('7. Unverified byte/word order blocks affected derivation', () => {
  const c = syntheticExampleConfiguration();
  const rackView = deriveRackView(c.modules);

  const noByteOrderRule = {
    ...SYNTHETIC_TEST_RULE,
    byteOrderVerified: false,
  };
  const result1 = deriveAddresses(rackView, c.bindings, noByteOrderRule);
  for (const e of result1.entries.filter((x) => x.state !== 'NOT_ACTIVE')) {
    assert.equal(e.state, ADDRESS_STATE.UNRESOLVED);
    assert.ok(e.reasons.includes(REASON.BYTE_ORDER_NOT_VERIFIED));
  }

  const noWordOrderRule = {
    ...SYNTHETIC_TEST_RULE,
    wordOrderVerified: false,
  };
  const result2 = deriveAddresses(rackView, c.bindings, noWordOrderRule);
  for (const e of result2.entries.filter((x) => x.state !== 'NOT_ACTIVE')) {
    assert.equal(e.state, ADDRESS_STATE.UNRESOLVED);
    assert.ok(e.reasons.includes(REASON.WORD_ORDER_NOT_VERIFIED));
  }
});

test('8. A complete synthetic test profile derives deterministic non-overlapping addresses', () => {
  const c = syntheticExampleConfiguration();
  const rackView = deriveRackView(c.modules);

  const run1 = deriveAddresses(rackView, c.bindings, SYNTHETIC_TEST_RULE);
  const run2 = deriveAddresses(rackView, c.bindings, SYNTHETIC_TEST_RULE);

  assert.deepEqual(run1, run2, 'deterministic derivation across runs');

  const occupiedBits = new Set();
  for (const e of run1.entries.filter((x) => x.state === ADDRESS_STATE.DERIVED)) {
    assert.notEqual(e.bitOffsetAbsolute, null);
    assert.notEqual(e.wordOffset, null);
    const width = e.bitIndex === null ? 16 : 1;
    for (let i = 0; i < width; i += 1) {
      const bit = e.bitOffsetAbsolute + i;
      assert.ok(!occupiedBits.has(bit), `bit ${bit} collision for ${e.tagName}`);
      occupiedBits.add(bit);
    }
  }
});

test('9. Synthetic profile cannot become authoritative', () => {
  assert.equal(isSyntheticRule(SYNTHETIC_TEST_RULE), true);
  const result = deriveAddresses(deriveRackView(syntheticExampleConfiguration().modules), [], SYNTHETIC_TEST_RULE);
  assert.equal(result.synthetic, true);
  assert.equal(result.verified, false, 'synthetic rules are never verified production evidence');

  const defaultResult = deriveAddresses(deriveRackView(syntheticExampleConfiguration().modules), [], null);
  assert.equal(defaultResult.synthetic, false);
  assert.equal(defaultResult.verified, false);
});

test('10. Profile fingerprints change when relevant verified evidence changes', () => {
  const baseFp = moduleProfileRevision();
  assert.match(baseFp, /^[0-9a-f]{64}$/);

  // A different evidence payload produces a different fingerprint
  const modifiedCatalogue = listProfiles().map((p) => {
    if (p.modelNumber === '750-471') {
      return {
        ...p,
        evidence: {
          ...p.evidence,
          statusByteBehavior: 'CONFIGURED_EXPLICIT_OFF',
        },
      };
    }
    return p;
  });

  const modifiedFp = fingerprintOf({ kind: 'ModuleProfiles', profiles: modifiedCatalogue });
  assert.notEqual(baseFp, modifiedFp, 'fingerprint changed when evidence changed');
});

test('11. Numeric manual address input remains refused', () => {
  const c = syntheticExampleConfiguration();
  const withManual = c.bindings.map((b) => (b.tagName === 'PUMP_OUTLET_PRESSURE' ? { ...b, address: 40001 } : b));
  const issues = validateMapping(c.modules, withManual);
  assert.ok(issues.some((i) => i.code === 'MANUAL_ADDRESS_REFUSED'));
});

test('12. Reorder invalidates or recalculates the derived manifest', () => {
  const c = createConfiguration(syntheticExampleConfiguration());
  const session = new DraftSession(c);

  const initialRev = session.revisions().derivedAddressManifestFingerprint;

  // Move AI-MODULE-03
  const moveRes = session.moveModule('AI-MODULE-03', 5);
  assert.equal(moveRes.ok, true);

  const afterRev = session.revisions().derivedAddressManifestFingerprint;
  assert.notEqual(initialRev, afterRev, 'derived manifest fingerprint recalculates on reorder');
});

test('13. Authoritative Workbook remains unchanged', () => {
  const wbPath = join(REPO, 'T8_IO_Card_Mapping.xlsx');
  const bytes = readFileSync(wbPath);
  const hash = createHash('sha256').update(bytes).digest('hex').toUpperCase();
  assert.equal(hash, '4E0337E25C8377C01559F264653BAAB25BCFA23F4D3E071FDC8C80896F422E8E');
});
