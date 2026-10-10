// Stage 0.4B-2 — SYNTHETIC TEST RULE evidence. TESTS ONLY.
//
// This set exists to exercise the derivation arithmetic. Its widths and orders are invented for the tests. They are
// NOT vendor evidence, NOT the real address map, and NOT a default. The set is marked synthetic, so it can never be
// authoritative, never address-verified and never activation-ready. It lives under test/ and is not part of the
// package surface.
import { createEvidenceSet } from '../../src/processImageEvidence.mjs';

export const SYNTHETIC_SOURCE_ID = 'SYNTHETIC-TEST-RULE';

const obs = (value) => [{ value, state: 'VERIFIED_PRIMARY_SOURCE', sourceId: SYNTHETIC_SOURCE_ID, note: 'SYNTHETIC TEST RULE' }];
const none = { input: 0, output: 0 };
const nonChannel = (modelNumber) => ({ modelNumber, manufacturer: null, evidenceNote: 'SYNTHETIC TEST RULE', cells: { processWidthBits: obs(none) } });
const channel = (modelNumber, direction, channels, channelBits) => ({
  modelNumber,
  manufacturer: null,
  evidenceNote: 'SYNTHETIC TEST RULE',
  cells: {
    processWidthBits: obs({ input: direction === 'input' ? channels * channelBits : 0, output: direction === 'output' ? channels * channelBits : 0 }),
    channelDataBits: obs(channelBits),
    fillerBits: obs(none),
    diagnosticBytes: obs(none),
    statusByte: obs({ behavior: 'NOT_PRESENT', inputBits: 0, outputBits: 0, placement: null }),
    byteOrder: obs('BIG_ENDIAN'),
    wordOrder: obs('HIGH_WORD_FIRST'),
  },
});

/** A fresh, mutable plain description of the synthetic set. Tests change it, then call createEvidenceSet. */
export function syntheticEvidenceInput({ maxWords = { input: 64, output: 64 } } = {}) {
  return {
    evidenceSetId: 'SYNTHETIC-TEST-RULE-ARITHMETIC-ONLY',
    label: 'SYNTHETIC TEST RULE (not vendor evidence)',
    synthetic: true,
    sources: [{
      sourceId: SYNTHETIC_SOURCE_ID,
      evidenceType: 'SYNTHETIC_TEST_RULE',
      documentTitle: 'SYNTHETIC TEST RULE: arithmetic only, not vendor evidence',
      documentSha256: null,
      documentRevision: null,
      effectiveDate: null,
      manufacturer: null,
    }],
    headStation: {
      modelNumber: '750-362',
      manufacturer: null,
      evidenceNote: 'SYNTHETIC TEST RULE',
      cells: {
        processImageGrouping: obs('ANALOG_THEN_DIGITAL'),
        moduleMappingOrder: obs('RACK_SLOT_ASCENDING'),
        wordWidthBits: obs(16),
        maxProcessImageWords: obs(maxWords),
      },
    },
    modules: [
      nonChannel('750-362'), nonChannel('750-601'), nonChannel('750-613'), nonChannel('750-600'),
      channel('750-471', 'input', 4, 16),
      channel('750-554', 'output', 2, 16),
      channel('750-430', 'input', 8, 1),
      channel('750-530', 'output', 8, 1),
    ],
  };
}

export const SYNTHETIC_TEST_EVIDENCE = createEvidenceSet(syntheticEvidenceInput());

/** The same synthetic set with one cell of one model removed (becomes NOT_PROVIDED). */
export function withoutCell(modelNumber, cell) {
  const input = syntheticEvidenceInput();
  const rec = modelNumber === 'HEAD' ? input.headStation : input.modules.find((m) => m.modelNumber === modelNumber);
  delete rec.cells[cell];
  return createEvidenceSet(input);
}
