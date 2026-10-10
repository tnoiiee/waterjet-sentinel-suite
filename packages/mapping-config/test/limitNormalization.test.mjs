// Stage 0.4B-1 — limit normalization and the Owner polarity rule.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { normalizeLimit } from '../src/limitNormalization.mjs';
import { POLARITY, OWNER_LIMIT_CONTACT_POLARITY } from '../src/constants.mjs';

const CLOSED = POLARITY.ACTIVE_WHEN_CLOSED;
const OPEN = POLARITY.ACTIVE_WHEN_OPEN;

test('the Owner polarity is ACTIVE_WHEN_CLOSED', () => {
  assert.equal(OWNER_LIMIT_CONTACT_POLARITY, CLOSED);
});

test('an open contact normalizes to LimitDetected = false when no inversion exists', () => {
  const r = normalizeLimit({ contactClosed: false, activePolarity: CLOSED });
  assert.equal(r.limitDetected, false);
  assert.equal(r.steps.rawInputInversion, 'NOT_CONFIGURED');
});

test('a closed contact normalizes to LimitDetected = true when no inversion exists', () => {
  const r = normalizeLimit({ contactClosed: true, activePolarity: CLOSED });
  assert.equal(r.limitDetected, true);
  assert.equal(r.steps.contactState, 'CLOSED');
});

test('ACTIVE_WHEN_OPEN reverses the contact state exactly once, and no inversion is assumed', () => {
  assert.equal(normalizeLimit({ contactClosed: true, activePolarity: OPEN }).limitDetected, false);
  assert.equal(normalizeLimit({ contactClosed: false, activePolarity: OPEN }).limitDetected, true);
});

test('a verified inversion is applied exactly once when it is configured', () => {
  const inverted = { verified: true, inverted: true };
  assert.equal(normalizeLimit({ contactClosed: true, activePolarity: CLOSED, inversion: inverted }).limitDetected, false);
  assert.equal(normalizeLimit({ contactClosed: false, activePolarity: CLOSED, inversion: inverted }).limitDetected, true);
  const r = normalizeLimit({ contactClosed: true, activePolarity: CLOSED, inversion: inverted });
  assert.equal(r.steps.rawInputInversion, 'APPLIED_ONCE');
});

test('a verified but non-inverting configuration leaves the contact value unchanged', () => {
  const r = normalizeLimit({ contactClosed: true, activePolarity: CLOSED, inversion: { verified: true, inverted: false } });
  assert.equal(r.limitDetected, true);
  assert.equal(r.steps.rawInputInversion, 'NOT_INVERTED');
});

test('an unverified inversion is refused, never applied', () => {
  assert.throws(() => normalizeLimit({ contactClosed: true, activePolarity: CLOSED, inversion: { verified: false, inverted: true } }), /not verified/);
  assert.throws(() => normalizeLimit({ contactClosed: true, activePolarity: CLOSED, inversion: { inverted: true } }), /not verified/);
});

test('ContactPolarity is never defaulted, and the contact state must be a boolean', () => {
  assert.throws(() => normalizeLimit({ contactClosed: true, activePolarity: null }), /ContactPolarity must be explicit/);
  assert.throws(() => normalizeLimit({ contactClosed: 1, activePolarity: CLOSED }), /boolean/);
});

test('a wire break is never claimed from a normally open contact alone', () => {
  for (const contactClosed of [true, false]) {
    for (const activePolarity of [CLOSED, OPEN]) {
      assert.equal(normalizeLimit({ contactClosed, activePolarity }).wireBreakDetectable, false);
    }
  }
});

test('the result is frozen', () => {
  const r = normalizeLimit({ contactClosed: true, activePolarity: CLOSED });
  assert.ok(Object.isFrozen(r));
  assert.ok(Object.isFrozen(r.steps));
});
