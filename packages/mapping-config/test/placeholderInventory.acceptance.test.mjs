// Stage 0.4B-1 — reserved placeholder inventory: acceptance items P01–P20 (Owner ruling 2026-10-10).
// The 18 placeholder rows are DI-037, DO-031 and AI-020..AI-035. They are USED / RESERVED, UNBOUND, awaiting Owner
// identity, never FREE or SPARE, never auto-bound, and never given a numeric address. Synthetic workbook only.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { importWorkbook } from '../src/workbookImport.mjs';
import { OWNER_PLACEHOLDER_ROWS } from '../src/tagCatalogue.mjs';
import { buildWorkbook, authoritativeShapedRows } from './helpers/syntheticWorkbook.mjs';

const importRows = (rows, options) => importWorkbook(buildWorkbook({ rows }), options);
const inv = (r) => r.reservedInventory;
const ALLOWED_KEYS = [
  'workbookTag', 'moduleInstanceId', 'rackSlot', 'channel', 'direction', 'physicalStatus', 'signalIdentity',
  'bindingStatus', 'ownerInputStatus', 'autoBindingEligibility', 'availableAsSpare', 'addressStatus', 'mappingAuthorization',
];

test('P01 the inventory holds exactly 18 placeholder rows, and its summary total is 18', () => {
  const r = importRows(authoritativeShapedRows());
  assert.equal(OWNER_PLACEHOLDER_ROWS.length, 18);
  assert.equal(inv(r).rows.length, 18);
  assert.equal(inv(r).summary.total, 18);
});

test('P02 the inventory tags are exactly the Owner set, including AI-020, AI-035, DI-037 and DO-031', () => {
  const r = importRows(authoritativeShapedRows());
  const tags = inv(r).rows.map((row) => row.workbookTag).sort();
  assert.deepEqual(tags, [...OWNER_PLACEHOLDER_ROWS].sort());
  for (const must of ['AI-020', 'AI-035', 'DI-037', 'DO-031']) assert.ok(tags.includes(must), must);
});

test('P03 classifications.reservedUnresolved is 18: the earlier count of 17 is corrected', () => {
  const r = importRows(authoritativeShapedRows());
  assert.equal(r.classifications.reservedUnresolved, 18);
});

test('P04 DO-031 stays OUTPUT with mappingAuthorization NOT_AUTHORIZED_IN_READ_ONLY_STAGE', () => {
  const r = importRows(authoritativeShapedRows());
  const row = inv(r).rows.find((x) => x.workbookTag === 'DO-031');
  assert.equal(row.direction, 'OUTPUT');
  assert.equal(row.mappingAuthorization, 'NOT_AUTHORIZED_IN_READ_ONLY_STAGE');
});

test('P05 the other 17 placeholder rows are INPUT; the summary splits 17 INPUT and 1 OUTPUT', () => {
  const r = importRows(authoritativeShapedRows());
  const rows = inv(r).rows.filter((x) => x.workbookTag !== 'DO-031');
  assert.equal(rows.length, 17);
  assert.ok(rows.every((x) => x.direction === 'INPUT'));
  assert.deepEqual(inv(r).summary.byDirection, { INPUT: 17, OUTPUT: 1 });
});

test('P06 every row reads physicalStatus USED / RESERVED', () => {
  const r = importRows(authoritativeShapedRows());
  assert.ok(inv(r).rows.every((x) => x.physicalStatus === 'USED / RESERVED'));
});

test('P07 every row reads signalIdentity UNRESOLVED', () => {
  const r = importRows(authoritativeShapedRows());
  assert.ok(inv(r).rows.every((x) => x.signalIdentity === 'UNRESOLVED'));
});

test('P08 every row reads bindingStatus UNBOUND', () => {
  const r = importRows(authoritativeShapedRows());
  assert.ok(inv(r).rows.every((x) => x.bindingStatus === 'UNBOUND'));
});

test('P09 every row reads ownerInputStatus OWNER_INPUT_PENDING', () => {
  const r = importRows(authoritativeShapedRows());
  assert.ok(inv(r).rows.every((x) => x.ownerInputStatus === 'OWNER_INPUT_PENDING'));
});

test('P10 every row reads autoBindingEligibility PROHIBITED', () => {
  const r = importRows(authoritativeShapedRows());
  assert.ok(inv(r).rows.every((x) => x.autoBindingEligibility === 'PROHIBITED'));
});

test('P11 no placeholder is available as spare: availableAsSpare is false on every row', () => {
  const r = importRows(authoritativeShapedRows());
  assert.ok(inv(r).rows.every((x) => x.availableAsSpare === false));
});

test('P12 every row reads addressStatus ADDRESS_UNRESOLVED', () => {
  const r = importRows(authoritativeShapedRows());
  assert.ok(inv(r).rows.every((x) => x.addressStatus === 'ADDRESS_UNRESOLVED'));
});

test('P13 INPUT placeholders read mappingAuthorization NOT_AUTHORIZED_UNTIL_OWNER_IDENTITY', () => {
  const r = importRows(authoritativeShapedRows());
  for (const x of inv(r).rows.filter((y) => y.direction === 'INPUT')) {
    assert.equal(x.mappingAuthorization, 'NOT_AUTHORIZED_UNTIL_OWNER_IDENTITY', x.workbookTag);
  }
});

test('P14 each row carries the ModuleInstanceId and channel of its own workbook row', () => {
  const r = importRows(authoritativeShapedRows());
  for (const x of inv(r).rows) {
    assert.ok(typeof x.moduleInstanceId === 'string' && x.moduleInstanceId.length > 0, x.workbookTag);
    assert.ok(Number.isInteger(x.channel) && x.channel >= 1, x.workbookTag);
    assert.ok(r.modules.some((m) => m.moduleInstanceId === x.moduleInstanceId), `${x.workbookTag} module exists`);
  }
  const ai20 = inv(r).rows.find((x) => x.workbookTag === 'AI-020');
  const dio31 = inv(r).rows.find((x) => x.workbookTag === 'DO-031');
  assert.notEqual(ai20.moduleInstanceId, dio31.moduleInstanceId, 'different physical modules');
});

test('P15 exclusive accounting: every workbook row is counted exactly once across the classifications', () => {
  const r = importRows(authoritativeShapedRows());
  const c = r.classifications;
  const total = c.inputUnambiguous + c.inputAmbiguous + c.outputNotAuthorised + c.reservedUnresolved
    + c.nonChannel + c.spare + c.boundByMappingRule;
  assert.equal(total, r.records.length);
});

test('P16 DO-031 is counted once: reservedUnresolved holds it, and outputNotAuthorised excludes it', () => {
  const r = importRows(authoritativeShapedRows());
  const outputRows = r.records.filter((rec) => rec.tag !== null && /^DO-/.test(rec.tag) && rec.channel !== null);
  assert.equal(r.classifications.outputNotAuthorised, outputRows.length - 1);
  assert.equal(r.classifications.reservedUnresolved, 18);
});

test('P17 the factual overlay: one placeholder is an output not authorised (DO-031), reported as an overlay', () => {
  const r = importRows(authoritativeShapedRows());
  assert.equal(inv(r).summary.overlays.outputNotAuthorised, 1);
});

test('P18 no placeholder is an enabled binding or a listed read-only tag: none is FREE, SPARE or auto-bound', () => {
  const r = importRows(authoritativeShapedRows());
  for (const tag of OWNER_PLACEHOLDER_ROWS) {
    assert.equal(r.bindings.some((b) => b.sourceWorkbookTag === tag && b.enabled !== false), false, `${tag} not enabled`);
    assert.equal(Object.prototype.hasOwnProperty.call(r.additionalTags, tag), false, `${tag} not a listed tag`);
  }
});

test('P19 a seed that binds a placeholder is refused (PLACEHOLDER_ROW_REFUSED) and the inventory is unchanged', () => {
  const plain = importRows(authoritativeShapedRows());
  const seeded = importRows(authoritativeShapedRows(), { bindingSeed: { IV1_UPPER_LIMIT: { source: 'AI-020' } } });
  assert.ok(seeded.issues.some((i) => i.code === 'PLACEHOLDER_ROW_REFUSED' && i.tagName === 'IV1_UPPER_LIMIT'));
  assert.equal(seeded.bindings.some((b) => b.sourceWorkbookTag === 'AI-020' && b.enabled !== false), false);
  assert.equal(JSON.stringify(inv(seeded)), JSON.stringify(inv(plain)));
});

test('P20 no numeric address, offset or register is exposed on any inventory row', () => {
  const r = importRows(authoritativeShapedRows());
  for (const x of inv(r).rows) {
    assert.deepEqual(Object.keys(x).sort(), [...ALLOWED_KEYS].sort(), x.workbookTag);
    for (const [key, value] of Object.entries(x)) {
      if (key === 'addressStatus') continue;
      assert.doesNotMatch(key, /offset|register|address|byte|word|bit/i, `${x.workbookTag}.${key}`);
      if (typeof value === 'string') assert.doesNotMatch(value, /^\d+(\.\d+)?$/, `${x.workbookTag}.${key} is not a bare number`);
    }
  }
});
